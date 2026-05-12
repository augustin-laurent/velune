using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.UseCases;
using Velune.Domain.ValueObjects;
using Velune.Windows.Services;
using Velune.Windows.ViewModels.UndoSystem;

namespace Velune.Windows.ViewModels;

/// <summary>
/// View model for the page organizer window, supporting reorder, rotate, delete, and duplicate operations with undo/redo.
/// </summary>
public sealed partial class PageOrganizerViewModel : ObservableObject
{
    private const double OrganizerThumbnailZoom = 0.25;
    private const int OrganizerThumbnailWidth = 280;
    private const int OrganizerThumbnailHeight = 360;

    private readonly IDocumentSessionStore _documentSessionStore;
    private readonly IRenderOrchestrator _renderOrchestrator;
    private readonly ReorderPdfPagesUseCase _reorderPdfPagesUseCase;
    private readonly DeletePdfPagesUseCase _deletePdfPagesUseCase;
    private readonly ExtractPdfPagesUseCase _extractPdfPagesUseCase;
    private readonly MergePdfDocumentsUseCase _mergePdfDocumentsUseCase;
    private readonly IWindowsFileDialogService _fileDialogService;
    private readonly IWindowsTextCatalog _textCatalog;
    private readonly UndoRedoManager _globalUndoManager;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly Stack<IPageOperation> _undoStack = new();
    private readonly Stack<IPageOperation> _redoStack = new();

    private DocumentId _sessionId;
    private string _filePath = string.Empty;
    private int _originalPageCount;

    /// <summary>
    /// Initializes the page organizer view model with required services.
    /// </summary>
    public PageOrganizerViewModel(
        IDocumentSessionStore documentSessionStore,
        IRenderOrchestrator renderOrchestrator,
        ReorderPdfPagesUseCase reorderPdfPagesUseCase,
        DeletePdfPagesUseCase deletePdfPagesUseCase,
        ExtractPdfPagesUseCase extractPdfPagesUseCase,
        MergePdfDocumentsUseCase mergePdfDocumentsUseCase,
        IWindowsFileDialogService fileDialogService,
        UndoRedoManager globalUndoManager,
        IWindowsTextCatalog textCatalog)
    {
        ArgumentNullException.ThrowIfNull(documentSessionStore);
        ArgumentNullException.ThrowIfNull(renderOrchestrator);
        ArgumentNullException.ThrowIfNull(reorderPdfPagesUseCase);
        ArgumentNullException.ThrowIfNull(deletePdfPagesUseCase);
        ArgumentNullException.ThrowIfNull(extractPdfPagesUseCase);
        ArgumentNullException.ThrowIfNull(mergePdfDocumentsUseCase);
        ArgumentNullException.ThrowIfNull(fileDialogService);
        ArgumentNullException.ThrowIfNull(globalUndoManager);
        ArgumentNullException.ThrowIfNull(textCatalog);

        _documentSessionStore = documentSessionStore;
        _renderOrchestrator = renderOrchestrator;
        _reorderPdfPagesUseCase = reorderPdfPagesUseCase;
        _deletePdfPagesUseCase = deletePdfPagesUseCase;
        _extractPdfPagesUseCase = extractPdfPagesUseCase;
        _mergePdfDocumentsUseCase = mergePdfDocumentsUseCase;
        _fileDialogService = fileDialogService;
        _globalUndoManager = globalUndoManager;
        _textCatalog = textCatalog;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    /// <summary>
    /// Gets the collection of page items displayed in the organizer.
    /// </summary>
    public ObservableCollection<PageOrganizerItemViewModel> Pages
    {
        get;
    } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial int SelectedCount
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool CanUndo
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool CanRedo
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool IsBusy
    {
        get; set;
    }

    public string StatusText => _textCatalog.Format("organizer.status", Pages.Count, SelectedCount);

    public bool HasChanges => _undoStack.Count > 0;

    public string WindowTitle => _textCatalog.GetString("organizer.title");
    public string RotateLeftLabel => _textCatalog.GetString("organizer.rotate_left");
    public string RotateRightLabel => _textCatalog.GetString("organizer.rotate_right");
    public string DeleteLabel => _textCatalog.GetString("organizer.delete");
    public string DuplicateLabel => _textCatalog.GetString("organizer.duplicate");
    public string ReverseLabel => _textCatalog.GetString("organizer.reverse");
    public string UndoLabel => _textCatalog.GetString("organizer.undo");
    public string RedoLabel => _textCatalog.GetString("organizer.redo");
    public string SelectAllLabel => _textCatalog.GetString("organizer.select_all");
    public string SelectOddLabel => _textCatalog.GetString("organizer.select_odd");
    public string SelectEvenLabel => _textCatalog.GetString("organizer.select_even");
    public string SelectInvertLabel => _textCatalog.GetString("organizer.select_invert");
    public string SelectNoneLabel => _textCatalog.GetString("organizer.select_none");
    public string SelectionLabel => _textCatalog.GetString("organizer.selection");
    public string CancelLabel => _textCatalog.GetString("organizer.cancel");
    public string ApplyLabel => _textCatalog.GetString("organizer.apply");

    /// <summary>
    /// Initializes the organizer with a document session and populates the page grid.
    /// </summary>
    /// <param name="sessionId">The document session identifier.</param>
    /// <param name="filePath">The file path of the document.</param>
    /// <param name="pageCount">The total number of pages.</param>
    /// <param name="existingThumbnails">Pre-rendered thumbnails to reuse.</param>
    public void Initialize(
        DocumentId sessionId,
        string filePath,
        int pageCount,
        IReadOnlyList<WindowsPageThumbnailViewModel>? existingThumbnails = null)
    {
        _sessionId = sessionId;
        _filePath = filePath;
        _originalPageCount = pageCount;

        Pages.Clear();
        _undoStack.Clear();
        _redoStack.Clear();
        UpdateUndoRedoState();

        for (int i = 1; i <= pageCount; i++)
        {
            var item = new PageOrganizerItemViewModel(i);
            if (existingThumbnails is not null && i - 1 < existingThumbnails.Count)
            {
                WindowsPageThumbnailViewModel existing = existingThumbnails[i - 1];
                item.Rotation = existing.Rotation;
                if (existing.Image is not null && existing.Rotation is Rotation.Deg0)
                {
                    item.Thumbnail = existing.Image;
                }
            }

            Pages.Add(item);
        }

        SelectedCount = 0;
        _ = RenderAllThumbnailsAsync();
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (PageOrganizerItemViewModel page in Pages)
        {
            page.IsSelected = true;
        }

        SelectedCount = Pages.Count;
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (PageOrganizerItemViewModel page in Pages)
        {
            page.IsSelected = false;
        }

        SelectedCount = 0;
    }

    [RelayCommand]
    private void SelectOddPages()
    {
        for (int i = 0; i < Pages.Count; i++)
        {
            Pages[i].IsSelected = (i + 1) % 2 != 0;
        }

        RefreshSelectedCount();
    }

    [RelayCommand]
    private void SelectEvenPages()
    {
        for (int i = 0; i < Pages.Count; i++)
        {
            Pages[i].IsSelected = (i + 1) % 2 == 0;
        }

        RefreshSelectedCount();
    }

    [RelayCommand]
    private void InvertSelection()
    {
        foreach (PageOrganizerItemViewModel page in Pages)
        {
            page.IsSelected = !page.IsSelected;
        }

        RefreshSelectedCount();
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        var selected = Pages.Where(p => p.IsSelected).ToList();
        if (selected.Count == 0 || selected.Count == Pages.Count)
        {
            return;
        }

        var operation = new DeleteOperation(selected, Pages);
        ExecuteOperation(operation);
    }

    [RelayCommand]
    private void RotateLeftSelected()
    {
        var selected = Pages.Where(p => p.IsSelected).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var operation = new RotateOperation(selected, rotateRight: false);
        ExecuteOperation(operation);
    }

    [RelayCommand]
    private void RotateRightSelected()
    {
        var selected = Pages.Where(p => p.IsSelected).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var operation = new RotateOperation(selected, rotateRight: true);
        ExecuteOperation(operation);
    }

    [RelayCommand]
    private void DuplicateSelected()
    {
        var selected = Pages.Where(p => p.IsSelected).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var operation = new DuplicateOperation(selected, Pages);
        ExecuteOperation(operation);
    }

    [RelayCommand]
    private void ReverseSelected()
    {
        var selected = Pages.Where(p => p.IsSelected).ToList();
        if (selected.Count < 2)
        {
            return;
        }

        var operation = new ReverseOperation(selected, Pages);
        ExecuteOperation(operation);
    }

    [RelayCommand]
    private void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        IPageOperation operation = _undoStack.Pop();
        operation.Undo(Pages);
        _redoStack.Push(operation);
        UpdateUndoRedoState();
        RefreshPageNumbers();
        RefreshSelectedCount();
    }

    [RelayCommand]
    private void Redo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        IPageOperation operation = _redoStack.Pop();
        operation.Execute(Pages);
        _undoStack.Push(operation);
        UpdateUndoRedoState();
        RefreshPageNumbers();
        RefreshSelectedCount();
    }

    /// <summary>
    /// Moves the specified pages to the given insertion index.
    /// </summary>
    /// <param name="pagesToMove">The pages to move.</param>
    /// <param name="insertIndex">The target insertion index.</param>
    public void MovePages(IReadOnlyList<PageOrganizerItemViewModel> pagesToMove, int insertIndex)
    {
        ArgumentNullException.ThrowIfNull(pagesToMove);

        if (pagesToMove.Count == 0)
        {
            return;
        }

        var operation = new MoveOperation(pagesToMove, insertIndex, Pages);
        ExecuteOperation(operation);
    }

    /// <summary>
    /// Toggles selection on a page item, supporting Ctrl and Shift multi-select.
    /// </summary>
    /// <param name="item">The page item clicked.</param>
    /// <param name="isCtrlHeld">Whether Ctrl is held for toggle behavior.</param>
    /// <param name="isShiftHeld">Whether Shift is held for range selection.</param>
    public void ToggleSelection(PageOrganizerItemViewModel item, bool isCtrlHeld, bool isShiftHeld)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (isShiftHeld && Pages.Count > 0)
        {
            PageOrganizerItemViewModel? lastSelected = Pages.LastOrDefault(p => p.IsSelected && p != item);
            if (lastSelected is not null)
            {
                int startIdx = Pages.IndexOf(lastSelected);
                int endIdx = Pages.IndexOf(item);
                int from = Math.Min(startIdx, endIdx);
                int to = Math.Max(startIdx, endIdx);
                for (int i = from; i <= to; i++)
                {
                    Pages[i].IsSelected = true;
                }
            }
            else
            {
                item.IsSelected = true;
            }
        }
        else if (isCtrlHeld)
        {
            item.IsSelected = !item.IsSelected;
        }
        else
        {
            foreach (PageOrganizerItemViewModel page in Pages)
            {
                page.IsSelected = page == item;
            }
        }

        RefreshSelectedCount();
    }

    /// <summary>
    /// Gets the final page order as a list of original page numbers.
    /// </summary>
    /// <returns>The ordered original page numbers.</returns>
    public IReadOnlyList<int> GetFinalPageOrder()
    {
        return Pages.Select(p => p.OriginalPageNumber).ToList();
    }

    /// <summary>
    /// Gets the original page numbers that have been deleted.
    /// </summary>
    /// <returns>Deleted original page numbers.</returns>
    public IReadOnlyList<int> GetDeletedOriginalPages()
    {
        var currentOriginals = Pages.Select(p => p.OriginalPageNumber).ToHashSet();
        return Enumerable.Range(1, _originalPageCount)
            .Where(p => !currentOriginals.Contains(p))
            .ToList();
    }

    /// <summary>
    /// Gets all non-zero rotations applied to pages.
    /// </summary>
    /// <returns>Tuples of original page number and rotation.</returns>
    public IReadOnlyList<(int OriginalPage, Rotation Rotation)> GetRotations()
    {
        return Pages
            .Where(p => p.Rotation != Rotation.Deg0)
            .Select(p => (p.OriginalPageNumber, p.Rotation))
            .ToList();
    }

    private void ExecuteOperation(IPageOperation operation)
    {
        operation.Execute(Pages);
        _undoStack.Push(operation);
        _redoStack.Clear();
        UpdateUndoRedoState();
        RefreshPageNumbers();
        RefreshSelectedCount();

        _globalUndoManager.Push(new PageOperationAction(
            "Page operation",
            () =>
            {
                operation.Execute(Pages);
                _undoStack.Push(operation);
                _redoStack.Clear();
                UpdateUndoRedoState();
                RefreshPageNumbers();
                RefreshSelectedCount();
            },
            () =>
            {
                operation.Undo(Pages);
                _redoStack.Push(operation);
                UpdateUndoRedoState();
                RefreshPageNumbers();
                RefreshSelectedCount();
            }));
    }

    private void UpdateUndoRedoState()
    {
        CanUndo = _undoStack.Count > 0;
        CanRedo = _redoStack.Count > 0;
        OnPropertyChanged(nameof(HasChanges));
    }

    private void RefreshPageNumbers()
    {
        for (int i = 0; i < Pages.Count; i++)
        {
            Pages[i].PageNumber = i + 1;
        }

        OnPropertyChanged(nameof(StatusText));
    }

    /// <summary>
    /// Recalculates and updates the selected page count.
    /// </summary>
    private void RefreshSelectedCount()
    {
        SelectedCount = Pages.Count(p => p.IsSelected);
    }

    private async Task RenderAllThumbnailsAsync()
    {
        foreach (PageOrganizerItemViewModel page in Pages.AsEnumerable().Where(page => page.Thumbnail is null))
        {
            page.IsLoading = true;

            var pageIndex = new PageIndex(page.OriginalPageNumber - 1);
            DocumentId? previousActiveSessionId = _documentSessionStore.ActiveSessionId;

            if (!_documentSessionStore.TryActivate(_sessionId))
            {
                page.IsLoading = false;
                continue;
            }

            RenderJobHandle handle = _renderOrchestrator.Submit(new RenderRequest(
                JobKey: $"organizer-thumbnail:{_sessionId.Value}:{pageIndex.Value}",
                PageIndex: pageIndex,
                ZoomFactor: OrganizerThumbnailZoom,
                Rotation: Rotation.Deg0,
                RequestedWidth: OrganizerThumbnailWidth,
                RequestedHeight: OrganizerThumbnailHeight,
                Priority: RenderPriority.Thumbnail));

            RestoreActiveSession(previousActiveSessionId);

            RenderResult result = await handle.Completion;
            if (result.IsSuccess && result.Page is not null)
            {
                await RunOnUiThreadAsync(() =>
                {
                    page.Thumbnail = WindowsBitmapFactory.Create(result.Page);
                    page.IsLoading = false;
                });
            }
            else
            {
                await RunOnUiThreadAsync(() => page.IsLoading = false);
            }
        }
    }

    private void RestoreActiveSession(DocumentId? documentId)
    {
        if (documentId is { } activeSessionId &&
            _documentSessionStore.Sessions.Any(s => s.Id == activeSessionId))
        {
            _documentSessionStore.TryActivate(activeSessionId);
        }
    }

    private Task RunOnUiThreadAsync(Action action)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    #region Operations

    private interface IPageOperation
    {
        void Execute(ObservableCollection<PageOrganizerItemViewModel> pages);
        void Undo(ObservableCollection<PageOrganizerItemViewModel> pages);
    }

    private sealed class DeleteOperation : IPageOperation
    {
        private readonly List<(int Index, PageOrganizerItemViewModel Item)> _removedItems;

        public DeleteOperation(
            IReadOnlyList<PageOrganizerItemViewModel> selectedItems,
            ObservableCollection<PageOrganizerItemViewModel> pages)
        {
            _removedItems = selectedItems
                .Select(item => (pages.IndexOf(item), item))
                .OrderBy(pair => pair.Item1)
                .ToList();
        }

        public void Execute(ObservableCollection<PageOrganizerItemViewModel> pages)
        {
            foreach ((int _, PageOrganizerItemViewModel? item) in _removedItems.AsEnumerable().Reverse())
            {
                pages.Remove(item);
            }
        }

        public void Undo(ObservableCollection<PageOrganizerItemViewModel> pages)
        {
            foreach ((int index, PageOrganizerItemViewModel? item) in _removedItems)
            {
                int insertAt = Math.Min(index, pages.Count);
                pages.Insert(insertAt, item);
            }
        }
    }

    private sealed class RotateOperation : IPageOperation
    {
        private readonly List<PageOrganizerItemViewModel> _items;
        private readonly bool _rotateRight;

        public RotateOperation(IReadOnlyList<PageOrganizerItemViewModel> items, bool rotateRight)
        {
            _items = items.ToList();
            _rotateRight = rotateRight;
        }

        public void Execute(ObservableCollection<PageOrganizerItemViewModel> pages)
        {
            foreach (PageOrganizerItemViewModel item in _items)
            {
                item.Rotation = _rotateRight ? RotateRight(item.Rotation) : RotateLeft(item.Rotation);
            }
        }

        public void Undo(ObservableCollection<PageOrganizerItemViewModel> pages)
        {
            foreach (PageOrganizerItemViewModel item in _items)
            {
                item.Rotation = _rotateRight ? RotateLeft(item.Rotation) : RotateRight(item.Rotation);
            }
        }

        private static Rotation RotateRight(Rotation current) => current switch
        {
            Rotation.Deg0 => Rotation.Deg90,
            Rotation.Deg90 => Rotation.Deg180,
            Rotation.Deg180 => Rotation.Deg270,
            Rotation.Deg270 => Rotation.Deg0,
            _ => Rotation.Deg0
        };

        private static Rotation RotateLeft(Rotation current) => current switch
        {
            Rotation.Deg0 => Rotation.Deg270,
            Rotation.Deg90 => Rotation.Deg0,
            Rotation.Deg180 => Rotation.Deg90,
            Rotation.Deg270 => Rotation.Deg180,
            _ => Rotation.Deg0
        };
    }

    private sealed class DuplicateOperation : IPageOperation
    {
        private readonly List<(int InsertIndex, PageOrganizerItemViewModel Clone)> _insertedItems = [];

        public DuplicateOperation(
            IReadOnlyList<PageOrganizerItemViewModel> selectedItems,
            ObservableCollection<PageOrganizerItemViewModel> pages)
        {
            int offset = 0;
            foreach (PageOrganizerItemViewModel? item in selectedItems.OrderBy(pages.IndexOf))
            {
                int sourceIndex = pages.IndexOf(item);
                var clone = new PageOrganizerItemViewModel(item.OriginalPageNumber, item.Rotation)
                {
                    Thumbnail = item.Thumbnail
                };
                _insertedItems.Add((sourceIndex + 1 + offset, clone));
                offset++;
            }
        }

        public void Execute(ObservableCollection<PageOrganizerItemViewModel> pages)
        {
            foreach ((int index, PageOrganizerItemViewModel? clone) in _insertedItems)
            {
                int insertAt = Math.Min(index, pages.Count);
                pages.Insert(insertAt, clone);
            }
        }

        public void Undo(ObservableCollection<PageOrganizerItemViewModel> pages)
        {
            foreach ((int _, PageOrganizerItemViewModel? clone) in _insertedItems.AsEnumerable().Reverse())
            {
                pages.Remove(clone);
            }
        }
    }

    private sealed class MoveOperation : IPageOperation
    {
        private readonly List<(int OriginalIndex, PageOrganizerItemViewModel Item)> _movedItems;
        private readonly int _insertIndex;

        public MoveOperation(
            IReadOnlyList<PageOrganizerItemViewModel> items,
            int insertIndex,
            ObservableCollection<PageOrganizerItemViewModel> pages)
        {
            _movedItems = items
                .Select(item => (OriginalIndex: pages.IndexOf(item), Item: item))
                .OrderBy(pair => pair.OriginalIndex)
                .ToList();
            _insertIndex = insertIndex;
        }

        public void Execute(ObservableCollection<PageOrganizerItemViewModel> pages)
        {
            var items = _movedItems.Select(m => m.Item).ToList();
            foreach (PageOrganizerItemViewModel? item in items)
            {
                pages.Remove(item);
            }

            int adjustedIndex = Math.Min(_insertIndex, pages.Count);
            for (int i = 0; i < items.Count; i++)
            {
                pages.Insert(adjustedIndex + i, items[i]);
            }
        }

        public void Undo(ObservableCollection<PageOrganizerItemViewModel> pages)
        {
            var items = _movedItems.Select(m => m.Item).ToList();
            foreach (PageOrganizerItemViewModel? item in items)
            {
                pages.Remove(item);
            }

            foreach ((int originalIndex, PageOrganizerItemViewModel? item) in _movedItems)
            {
                int insertAt = Math.Min(originalIndex, pages.Count);
                pages.Insert(insertAt, item);
            }
        }
    }

    private sealed class ReverseOperation : IPageOperation
    {
        private readonly List<int> _indices;

        public ReverseOperation(
            IReadOnlyList<PageOrganizerItemViewModel> selectedItems,
            ObservableCollection<PageOrganizerItemViewModel> pages)
        {
            _indices = selectedItems
                .Select(item => pages.IndexOf(item))
                .OrderBy(i => i)
                .ToList();
        }

        public void Execute(ObservableCollection<PageOrganizerItemViewModel> pages)
        {
            var items = _indices.Select(i => pages[i]).ToList();
            items.Reverse();
            for (int i = 0; i < _indices.Count; i++)
            {
                pages[_indices[i]] = items[i];
            }
        }

        public void Undo(ObservableCollection<PageOrganizerItemViewModel> pages)
        {
            Execute(pages);
        }
    }

    #endregion
}
