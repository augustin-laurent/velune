# Velune — Sequence Diagrams

## 1. Open Document Flow

The primary user flow: user opens a file, system loads it, renders first page.

```mermaid
sequenceDiagram
    participant User
    participant VM as MainWindowViewModel
    participant OpenUC as OpenDocumentUseCase
    participant Opener as DispatchingDocumentOpener
    participant Pdfium as PdfiumDocumentOpener
    participant Store as IDocumentSessionStore
    participant RenderOrch as RenderOrchestrator
    participant RenderSvc as PdfiumRenderService
    participant Cache as RenderMemoryCache

    User->>VM: Drop file / Pick file
    VM->>OpenUC: ExecuteAsync(OpenDocumentRequest)
    
    OpenUC->>Opener: OpenAsync(filePath)
    
    alt PDF file
        Opener->>Pdfium: Open(filePath)
        Pdfium->>Pdfium: FPDF_LoadDocument(path)
        Pdfium-->>Opener: PdfiumDocumentSession
    else Image file
        Opener->>Opener: SkiaImageDocumentOpener.Open(filePath)
        Opener-->>Opener: ImageDocumentSession
    end
    
    Opener-->>OpenUC: IDocumentSession

    OpenUC->>Store: Add(session, makeActive: true)
    OpenUC->>RenderOrch: CancelDocumentJobsAsync(previousDocId)
    OpenUC-->>VM: Result<IDocumentSession> (Success)

    VM->>VM: Create DocumentTabViewModel
    VM->>VM: UpdateDocumentViewportAsync()
    
    VM->>RenderOrch: Submit(RenderRequest[page=0, priority=Viewer])
    
    RenderOrch->>Cache: TryGet(docId, request)
    Cache-->>RenderOrch: miss

    RenderOrch->>RenderOrch: Enqueue to _viewerQueue
    
    Note over RenderOrch: Background worker picks up job

    RenderOrch->>RenderSvc: RenderPageAsync(session, page0, zoom, rotation)
    RenderSvc->>RenderSvc: FPDF_RenderPageBitmap (native)
    RenderSvc-->>RenderOrch: RenderedPage (pixel data)
    
    RenderOrch->>Cache: Store(docId, request, page)
    RenderOrch-->>VM: RenderResult (via TaskCompletionSource)
    
    VM->>VM: Convert to platform bitmap
    VM-->>User: Page displayed
```

---

## 2. Page Navigation Flow

User navigates to a different page via arrow keys, scroll, or thumbnail click.

```mermaid
sequenceDiagram
    participant User
    participant VM as MainWindowViewModel
    participant ChangeUC as ChangePageUseCase
    participant Store as IDocumentSessionStore
    participant VPStore as IPageViewportStore
    participant RenderOrch as RenderOrchestrator
    participant Cache as RenderMemoryCache
    participant RenderSvc as IRenderService

    User->>VM: Next page (arrow key / scroll / thumbnail)
    VM->>ChangeUC: Execute(ChangePageRequest{pageIndex})
    
    ChangeUC->>Store: Current (get active session)
    ChangeUC->>ChangeUC: Validate pageIndex in range
    
    alt Invalid page
        ChangeUC-->>VM: Result.Failure(NotFound)
        VM-->>User: No change
    else Valid page
        ChangeUC->>Store: UpdateViewport(newViewport)
        ChangeUC-->>VM: Result<ViewportState> (Success)
    end

    VM->>VPStore: SetActivePage(pageIndex)
    VM->>RenderOrch: Submit(RenderRequest[newPage, Viewer])

    RenderOrch->>Cache: TryGet(docId, request)
    
    alt Cache hit
        Cache-->>RenderOrch: RenderedPage
        RenderOrch-->>VM: RenderResult (immediate)
    else Cache miss
        RenderOrch->>RenderOrch: Enqueue job
        RenderOrch->>RenderSvc: RenderPageAsync(...)
        RenderSvc-->>RenderOrch: RenderedPage
        RenderOrch->>Cache: Store(...)
        RenderOrch-->>VM: RenderResult
    end

    VM->>VM: Update bitmap display
    VM-->>User: New page shown
```

---

## 3. Render Orchestrator Internal Flow

Shows priority queue processing with viewer vs thumbnail priority.

```mermaid
sequenceDiagram
    participant Caller as ViewModel
    participant Orch as RenderOrchestrator
    participant ViewerQ as ViewerQueue (high priority)
    participant ThumbQ as ThumbnailQueue (low priority)
    participant Worker as Background Worker
    participant Cache as RenderMemoryCache
    participant DiskCache as ThumbnailDiskCache
    participant Render as IRenderService

    Caller->>Orch: Submit(RenderRequest{priority=Viewer})
    Orch->>Orch: Lock(_gate)
    Orch->>ViewerQ: Enqueue(jobId)
    Orch->>Orch: _signal.Set()
    Orch-->>Caller: RenderJobHandle{Task}

    Caller->>Orch: Submit(RenderRequest{priority=Thumbnail})
    Orch->>ThumbQ: Enqueue(jobId)

    Note over Worker: Worker loop wakes on signal

    Worker->>Orch: Lock(_gate)
    
    alt ViewerQueue not empty (higher priority)
        Worker->>ViewerQ: Dequeue
    else Only thumbnails
        Worker->>ThumbQ: Dequeue
    end

    Worker->>Cache: TryGet(docId, request)
    
    alt Already cached
        Worker-->>Caller: Complete TCS with cached result
    else Not cached
        Worker->>Render: RenderPageAsync(session, page, zoom, rotation)
        Render-->>Worker: RenderedPage
        Worker->>Cache: Store(docId, request, page)
        
        alt Thumbnail job
            Worker->>DiskCache: StoreAsync(docId, request, page)
        end
        
        Worker-->>Caller: Complete TCS with result
    end

    Note over Worker: Loop back, check queues again
```

---

## 4. PDF Page Structure Operations (Delete/Reorder/Rotate/Extract/Merge)

Uses qpdf external process for structural PDF modifications.

```mermaid
sequenceDiagram
    participant User
    participant VM as ViewModel
    participant UC as DeletePdfPagesUseCase
    participant Qpdf as QpdfDocumentStructureService
    participant Process as qpdf.exe
    participant Pdfium as PdfiumDocumentOpener
    participant Store as IDocumentSessionStore

    User->>VM: Delete selected pages
    VM->>UC: ExecuteAsync(DeletePdfPagesRequest)
    UC->>Qpdf: DeletePagesAsync(filePath, pageIndices)

    Qpdf->>Qpdf: Create temp output path
    Qpdf->>Process: Start qpdf --pages ... -- input output
    
    Note over Process: qpdf processes PDF structure<br/>No shell execution (ArgumentList.Add)

    Process-->>Qpdf: Exit code 0
    
    Qpdf->>Qpdf: Replace original with output
    Qpdf-->>UC: Result<string> (output path)
    UC-->>VM: Result<string> (Success)

    VM->>VM: Close current session
    VM->>Pdfium: Reopen modified file
    Pdfium-->>VM: New IDocumentSession
    VM->>Store: Add(newSession, makeActive: true)
    VM-->>User: Updated document displayed
```

---

## 5. OCR Text Extraction Flow

Background text analysis via Tesseract OCR engine.

```mermaid
sequenceDiagram
    participant User
    participant VM as MainWindowViewModel
    participant TextOrch as DocumentTextAnalysisOrchestrator
    participant TextSvc as DocumentTextService
    participant OCR as TesseractOcrEngine
    participant Process as tesseract.exe
    participant DiskCache as DocumentTextDiskCache

    User->>VM: Enable text selection / Search
    VM->>TextOrch: Submit(DocumentTextAnalysisRequest)
    TextOrch-->>VM: DocumentTextJobHandle

    Note over TextOrch: Background worker processes

    loop For each page
        TextOrch->>DiskCache: TryLoad(docId, pageIndex)
        
        alt Cached
            DiskCache-->>TextOrch: PageTextContent
        else Not cached
            TextOrch->>TextSvc: LoadAsync(session, languages)
            TextSvc->>TextSvc: Try embedded PDF text first
            
            alt Has embedded text
                TextSvc-->>TextOrch: DocumentTextLoadResult
            else No embedded text — run OCR
                TextSvc->>OCR: RecognizePageAsync(OcrPageRequest)
                OCR->>OCR: Render page to temp image
                OCR->>Process: Start tesseract image output --oem 3 -l eng tsv
                Process-->>OCR: TSV output with word positions
                OCR->>OCR: Parse TSV → OcrPageContent
                OCR-->>TextSvc: Result<OcrPageContent>
                TextSvc-->>TextOrch: DocumentTextLoadResult
            end
            
            TextOrch->>DiskCache: Store(docId, pageIndex, content)
        end
    end

    TextOrch-->>VM: Completed (all pages indexed)
    VM-->>User: Text selection / search now available
```

---

## 6. Annotation Save Flow (PDF)

User draws annotations, then saves — annotations rendered into PDF via Skia.

```mermaid
sequenceDiagram
    participant User
    participant VM as ViewModel
    participant MarkupSvc as SkiaPdfMarkupService
    participant SigStore as ISignatureAssetStore
    participant Pdfium as PdfiumNative
    participant Skia as SkiaSharp

    User->>VM: Draw annotations (freehand, text, signature, highlight)
    VM->>VM: Store DocumentAnnotation[] in tab state

    User->>VM: Save (Ctrl+S)
    VM->>MarkupSvc: ApplyAnnotationsAsync(ApplyPdfAnnotationsRequest)

    Note over MarkupSvc: Request contains:<br/>- source PDF path<br/>- output path<br/>- DocumentAnnotation[]<br/>- page dimensions

    MarkupSvc->>Pdfium: FPDF_LoadDocument(sourcePath)
    
    loop For each page with annotations
        MarkupSvc->>Pdfium: FPDF_LoadPage(doc, pageIndex)
        MarkupSvc->>Skia: Create SKBitmap(pageWidth, pageHeight)
        MarkupSvc->>Skia: Create SKCanvas(bitmap)
        
        loop For each annotation on page
            alt Freehand / Highlight
                MarkupSvc->>Skia: canvas.DrawPath(points, paint)
            else Text
                MarkupSvc->>Skia: canvas.DrawText(text, position, paint)
            else Signature
                MarkupSvc->>SigStore: GetAssetPath(assetId)
                SigStore-->>MarkupSvc: imagePath
                MarkupSvc->>Skia: canvas.DrawImage(sigImage, bounds)
            end
        end

        MarkupSvc->>Pdfium: FPDFPage_InsertObject (image object)
        MarkupSvc->>Pdfium: FPDFPage_GenerateContent
    end

    MarkupSvc->>Pdfium: FPDF_SaveAsCopy (via PdfiumFileWriter callback)
    MarkupSvc-->>VM: Result<string> (output path)
    
    VM->>VM: Reopen saved document
    VM-->>User: Saved document with flattened annotations
```

---

## 7. Print Document Flow

```mermaid
sequenceDiagram
    participant User
    participant VM as ViewModel
    participant PrintUC as PrintDocumentUseCase
    participant PrintSvc as IPrintService
    participant RenderSvc as IRenderService
    participant OS as Windows Print Spooler

    User->>VM: Print (Ctrl+P)
    VM->>PrintSvc: GetPrintersAsync()
    PrintSvc-->>VM: List<PrintDestinationInfo>
    VM-->>User: Show print dialog

    User->>VM: Select printer, pages, orientation → Confirm
    VM->>PrintUC: ExecuteAsync(PrintDocumentRequest)

    PrintUC->>PrintUC: Resolve page range (All / Range / Current)
    
    loop For each page in range
        PrintUC->>RenderSvc: RenderPageAsync(session, page, printZoom, rotation)
        RenderSvc-->>PrintUC: RenderedPage
        PrintUC->>PrintSvc: SendPageToSpooler(page, destination)
        PrintSvc->>OS: Submit rendered bitmap
    end

    PrintUC-->>VM: Result (Success)
    VM-->>User: Print job submitted
```

---

## 8. Application Startup Flow

```mermaid
sequenceDiagram
    participant OS as Windows
    participant Program as Program.cs
    participant App as App.xaml.cs
    participant DI as ServiceCollection
    participant Pdfium as PdfiumInitializer
    participant Prefs as IUserPreferencesService
    participant Window as MainWindow

    OS->>Program: Launch
    Program->>Program: ComWrappersSupport.InitializeComWrappers()
    Program->>App: Application.Start()

    App->>DI: ConfigureServices()
    
    Note over DI: Register all layers:<br/>AddApplication()<br/>AddInfrastructure()<br/>AddPresentation()

    DI->>DI: Singleton: PdfiumInitializer, PdfiumExecutionGate
    DI->>DI: Singleton: IUserPreferencesService, IRecentFilesService
    DI->>DI: Singleton: IDocumentSessionStore, IPageViewportStore
    DI->>DI: Singleton: IRenderOrchestrator, IRenderMemoryCache
    DI->>DI: Transient: Use Cases, IRenderService, IDocumentOpener

    App->>Pdfium: EnsureLoaded()
    Pdfium->>Pdfium: FPDF_InitLibraryWithConfig()

    App->>Prefs: LoadAsync()
    Prefs-->>App: UserPreferences (theme, locale, etc.)

    App->>Window: new MainWindow(viewModel)
    Window->>Window: ExtendsContentIntoTitleBar = true
    Window->>Window: SetTitleBar()
    Window-->>OS: Window displayed

    Note over Window: If launched with file argument:<br/>auto-open that file
```

---

## 9. Search Document Text Flow

```mermaid
sequenceDiagram
    participant User
    participant VM as MainWindowViewModel
    participant SearchUC as SearchDocumentTextUseCase
    participant TextIndex as DocumentTextIndex
    participant Tab as DocumentTabViewModel

    User->>VM: Ctrl+F → Type query
    VM->>VM: Debounce input (300ms)
    
    VM->>SearchUC: Execute(SearchDocumentTextRequest)
    
    SearchUC->>SearchUC: Get DocumentTextIndex for document
    
    alt Text not loaded yet
        SearchUC-->>VM: Result.Failure("Text not ready")
        VM->>VM: Trigger background text loading
        VM-->>User: "Loading text..."
    else Text available
        SearchUC->>TextIndex: Search(query, options)
        
        Note over TextIndex: Full-text search across all pages<br/>Case-insensitive matching<br/>Returns word positions + page locations
        
        TextIndex-->>SearchUC: List<SearchHit>
        SearchUC-->>VM: Result<List<SearchHit>>
    end

    VM->>Tab: ApplySearchResults(hits)
    Tab->>Tab: Create highlight rectangles per page
    VM-->>User: Highlights shown, result count displayed

    User->>VM: F3 (Next result)
    VM->>VM: Advance currentHitIndex
    VM->>VM: Navigate to hit's page if different
    VM-->>User: Scroll to next highlight
```

---

## 10. Document Close and Cleanup Flow

```mermaid
sequenceDiagram
    participant User
    participant VM as ViewModel
    participant CloseUC as CloseDocumentUseCase
    participant Store as IDocumentSessionStore
    participant RenderOrch as IRenderOrchestrator
    participant TextOrch as IDocumentTextAnalysisOrchestrator
    participant Session as PdfiumDocumentSession
    participant Resource as PdfiumDocumentResource
    participant Gate as PdfiumExecutionGate

    User->>VM: Close tab (X button / Ctrl+W)

    alt Document has unsaved annotations
        VM-->>User: "Save changes?" dialog
        User->>VM: Don't save / Save / Cancel
    end

    VM->>CloseUC: ExecuteAsync(CloseDocumentRequest)
    
    CloseUC->>RenderOrch: CancelDocumentJobsAsync(docId)
    Note over RenderOrch: Cancel all pending render jobs<br/>for this document
    RenderOrch->>RenderOrch: Mark jobs as obsolete, cancel CTS
    RenderOrch-->>CloseUC: Done

    CloseUC->>TextOrch: Cancel(textJobId)
    Note over TextOrch: Cancel any running OCR for this doc

    CloseUC->>Store: Remove(docId)
    Store->>Store: Remove from _sessions list
    
    alt Session is IReleasableDocumentSession
        Store->>Session: ReleaseResources()
        Session->>Resource: Dispose()
        Resource->>Gate: AcquireAsync() [wait for render to finish]
        Gate-->>Resource: Semaphore acquired
        Resource->>Resource: FPDF_CloseDocument(handle)
        Resource->>Gate: Release semaphore
    end

    CloseUC-->>VM: Result (Success)
    
    VM->>VM: Remove tab from DocumentTabs
    VM->>VM: Activate next tab (or show welcome)
    VM-->>User: Tab closed
```
