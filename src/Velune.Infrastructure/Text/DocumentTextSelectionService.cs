using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Documents;
using Velune.Infrastructure.Pdf;
using System.Text;

namespace Velune.Infrastructure.Text;

public sealed class DocumentTextSelectionService : IDocumentTextSelectionService
{
    private const double PdfHitToleranceFactor = 0.015;
    private const double MinimumPdfHitTolerance = 4.0;

    public Result<DocumentTextSelectionResult> Resolve(DocumentTextSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pageContent = request.Index.Pages
            .FirstOrDefault(page => page.PageIndex == request.PageIndex);
        if (pageContent is null)
        {
            return ResultFactory.Failure<DocumentTextSelectionResult>(
                AppError.Validation(
                    "document.text.selection.page.missing",
                    "The selected page has no searchable text content."));
        }

        if (pageContent.SourceKind is TextSourceKind.EmbeddedPdfText &&
            request.Session is PdfiumDocumentSession pdfSession)
        {
            return SelectPdfText(pdfSession, pageContent, request);
        }

        return SelectRunBasedText(pageContent, request);
    }

    private static Result<DocumentTextSelectionResult> SelectRunBasedText(
        PageTextContent pageContent,
        DocumentTextSelectionRequest request)
    {
        var selectableRuns = ResolveSelectableRuns(pageContent);
        if (selectableRuns.Count == 0)
        {
            return ResultFactory.Success(CreateEmptySelection(pageContent));
        }

        var anchorRun = FindNearestRun(selectableRuns, request.AnchorPoint);
        var activeRun = FindNearestRun(selectableRuns, request.ActivePoint);
        if (anchorRun is null || activeRun is null)
        {
            return ResultFactory.Success(CreateEmptySelection(pageContent));
        }

        var startIndex = Math.Min(anchorRun.OrderedIndex, activeRun.OrderedIndex);
        var endIndex = Math.Max(anchorRun.OrderedIndex, activeRun.OrderedIndex);
        var selectedRuns = selectableRuns
            .Where(run => run.OrderedIndex >= startIndex && run.OrderedIndex <= endIndex)
            .OrderBy(run => run.OrderedIndex)
            .ToArray();

        if (selectedRuns.Length == 0)
        {
            return ResultFactory.Success(CreateEmptySelection(pageContent));
        }

        var regions = MergeNearbyRegions(
            selectedRuns.SelectMany(run => run.Run.Regions),
            pageContent.SourceWidth,
            pageContent.SourceHeight);

        var selectionStart = selectedRuns[0].Run.StartIndex;
        var selectionEnd = Math.Min(
            pageContent.Text.Length,
            selectedRuns[^1].Run.StartIndex + selectedRuns[^1].Run.Length);

        var selectedText = selectionEnd > selectionStart
            ? NormalizeSelectedText(pageContent.Text[selectionStart..selectionEnd])
            : null;

        return ResultFactory.Success(
            new DocumentTextSelectionResult(
                pageContent.PageIndex,
                selectedText,
                regions,
                pageContent.SourceKind));
    }

    private static Result<DocumentTextSelectionResult> SelectPdfText(
        PdfiumDocumentSession session,
        PageTextContent pageContent,
        DocumentTextSelectionRequest request)
    {
        var pageHandle = PdfiumNative.FPDF_LoadPage(session.Resource.Handle, request.PageIndex.Value);
        if (pageHandle == nint.Zero)
        {
            return ResultFactory.Failure<DocumentTextSelectionResult>(
                AppError.Infrastructure(
                    "document.text.selection.pdf.page_load_failed",
                    "The PDF page could not be loaded for text selection."));
        }

        try
        {
            var textPageHandle = PdfiumNative.FPDFText_LoadPage(pageHandle);
            if (textPageHandle == nint.Zero)
            {
                return ResultFactory.Failure<DocumentTextSelectionResult>(
                    AppError.Infrastructure(
                        "document.text.selection.pdf.text_page_load_failed",
                        "The PDF text layer could not be loaded for selection."));
            }

            try
            {
                var anchorIndex = FindNearestPdfCharIndex(textPageHandle, pageContent, request.AnchorPoint);
                if (anchorIndex < 0)
                {
                    return ResultFactory.Success(CreateEmptySelection(pageContent));
                }

                var activeIndex = FindNearestPdfCharIndex(textPageHandle, pageContent, request.ActivePoint);
                if (activeIndex < 0)
                {
                    activeIndex = anchorIndex;
                }

                var startIndex = Math.Min(anchorIndex, activeIndex);
                var count = Math.Abs(activeIndex - anchorIndex) + 1;
                var selectedText = NormalizeSelectedText(ExtractPdfText(textPageHandle, startIndex, count));
                var regions = ExtractPdfSelectionRegions(pageContent, startIndex, count);

                return ResultFactory.Success(
                    new DocumentTextSelectionResult(
                        pageContent.PageIndex,
                        selectedText,
                        regions,
                        pageContent.SourceKind));
            }
            finally
            {
                PdfiumNative.FPDFText_ClosePage(textPageHandle);
            }
        }
        finally
        {
            PdfiumNative.FPDF_ClosePage(pageHandle);
        }
    }

    private static int FindNearestPdfCharIndex(
        nint textPageHandle,
        PageTextContent pageContent,
        DocumentTextSelectionPoint point)
    {
        var xTolerance = Math.Max(MinimumPdfHitTolerance, pageContent.SourceWidth * PdfHitToleranceFactor);
        var yTolerance = Math.Max(MinimumPdfHitTolerance, pageContent.SourceHeight * PdfHitToleranceFactor);
        var pdfX = Math.Clamp(point.X, 0, pageContent.SourceWidth);
        var pdfY = Math.Clamp(pageContent.SourceHeight - point.Y, 0, pageContent.SourceHeight);

        return PdfiumNative.FPDFText_GetCharIndexAtPos(
            textPageHandle,
            pdfX,
            pdfY,
            xTolerance,
            yTolerance);
    }

    private static string? ExtractPdfText(nint textPageHandle, int startIndex, int count)
    {
        if (count <= 0)
        {
            return null;
        }

        var buffer = new ushort[count + 1];
        var written = PdfiumNative.FPDFText_GetText(textPageHandle, startIndex, count, buffer);
        if (written <= 1)
        {
            return null;
        }

        return new string(buffer[..(written - 1)].Select(value => (char)value).ToArray()).Trim();
    }

    private static List<NormalizedTextRegion> ExtractPdfSelectionRegions(
        PageTextContent pageContent,
        int startIndex,
        int count)
    {
        if (count <= 0)
        {
            return [];
        }

        if (pageContent.CharacterRegionsByIndex.Count == 0)
        {
            return [];
        }

        var selectedRegions = new List<NormalizedTextRegion>(count);
        for (var characterIndex = startIndex; characterIndex < startIndex + count; characterIndex++)
        {
            if (!pageContent.CharacterRegionsByIndex.TryGetValue(characterIndex, out var region))
            {
                continue;
            }

            selectedRegions.Add(region);
        }

        return MergeNearbyRegions(selectedRegions, pageContent.SourceWidth, pageContent.SourceHeight);
    }

    private static List<NormalizedTextRegion> MergeNearbyRegions(
        IEnumerable<NormalizedTextRegion> regions,
        double sourceWidth,
        double sourceHeight)
    {
        var boxes = regions
            .Select(region => new SourceBox(
                region.X * sourceWidth,
                region.Y * sourceHeight,
                (region.X + region.Width) * sourceWidth,
                (region.Y + region.Height) * sourceHeight))
            .OrderBy(box => box.CenterY)
            .ThenBy(box => box.Left)
            .ToList();

        if (boxes.Count == 0)
        {
            return [];
        }

        var merged = new List<SourceBox>(boxes.Count);
        var current = boxes[0];

        for (var index = 1; index < boxes.Count; index++)
        {
            var next = boxes[index];
            if (ShouldMergeIntoSameLine(current, next))
            {
                current = new SourceBox(
                    Math.Min(current.Left, next.Left),
                    Math.Min(current.Top, next.Top),
                    Math.Max(current.Right, next.Right),
                    Math.Max(current.Bottom, next.Bottom));
                continue;
            }

            merged.Add(current);
            current = next;
        }

        merged.Add(current);

        return
        [
            .. merged.Select(box => ExpandSelectionBox(box, sourceWidth, sourceHeight))
        ];
    }

    private static bool ShouldMergeIntoSameLine(SourceBox current, SourceBox next)
    {
        var verticalOverlap = Math.Min(current.Bottom, next.Bottom) - Math.Max(current.Top, next.Top);
        var minimumHeight = Math.Min(current.Height, next.Height);
        var maximumHeight = Math.Max(current.Height, next.Height);
        var centerDistance = Math.Abs(current.CenterY - next.CenterY);

        if (verticalOverlap >= minimumHeight * 0.2)
        {
            return true;
        }

        return centerDistance <= maximumHeight * 0.85;
    }

    private static NormalizedTextRegion ExpandSelectionBox(
        SourceBox box,
        double sourceWidth,
        double sourceHeight)
    {
        var boxHeight = Math.Max(0, box.Bottom - box.Top);
        var horizontalPadding = Math.Max(1.5, boxHeight * 0.12);
        var verticalPadding = Math.Max(1.5, boxHeight * 0.18);

        var left = Math.Max(0, box.Left - horizontalPadding);
        var top = Math.Max(0, box.Top - verticalPadding);
        var right = Math.Min(sourceWidth, box.Right + horizontalPadding);
        var bottom = Math.Min(sourceHeight, box.Bottom + verticalPadding);

        return new NormalizedTextRegion(
            left / sourceWidth,
            top / sourceHeight,
            Math.Max(0.0001, (right - left) / sourceWidth),
            Math.Max(0.0001, (bottom - top) / sourceHeight));
    }

    private static string? NormalizeSelectedText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var buffer = new StringBuilder(text.Length);
        var previousWasWhitespace = false;

        foreach (var character in text.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                if (buffer.Length == 0 || previousWasWhitespace)
                {
                    continue;
                }

                buffer.Append(' ');
                previousWasWhitespace = true;
                continue;
            }

            buffer.Append(character);
            previousWasWhitespace = false;
        }

        return buffer.Length == 0
            ? null
            : buffer.ToString();
    }

    private static List<SelectableRun> ResolveSelectableRuns(PageTextContent pageContent)
    {
        var orderedRuns = pageContent.Runs
            .OrderBy(run => run.StartIndex)
            .ToArray();

        var selectableRuns = new List<SelectableRun>(orderedRuns.Length);

        for (var index = 0; index < orderedRuns.Length; index++)
        {
            if (!TryGetRunBounds(pageContent, orderedRuns[index], out var left, out var top, out var right, out var bottom))
            {
                continue;
            }

            selectableRuns.Add(new SelectableRun(index, orderedRuns[index], left, top, right, bottom));
        }

        return selectableRuns;
    }

    private static bool TryGetRunBounds(
        PageTextContent pageContent,
        TextRun run,
        out double left,
        out double top,
        out double right,
        out double bottom)
    {
        left = 0;
        top = 0;
        right = 0;
        bottom = 0;

        if (run.Regions.Count == 0)
        {
            return false;
        }

        left = run.Regions.Min(region => region.X * pageContent.SourceWidth);
        top = run.Regions.Min(region => region.Y * pageContent.SourceHeight);
        right = run.Regions.Max(region => (region.X + region.Width) * pageContent.SourceWidth);
        bottom = run.Regions.Max(region => (region.Y + region.Height) * pageContent.SourceHeight);

        return right > left && bottom > top;
    }

    private static SelectableRun? FindNearestRun(
        IReadOnlyList<SelectableRun> selectableRuns,
        DocumentTextSelectionPoint point)
    {
        if (selectableRuns.Count == 0)
        {
            return null;
        }

        SelectableRun? nearestRun = null;
        var nearestDistance = double.MaxValue;

        foreach (var run in selectableRuns)
        {
            var horizontalDistance = point.X < run.Left
                ? run.Left - point.X
                : point.X > run.Right
                    ? point.X - run.Right
                    : 0;
            var verticalDistance = point.Y < run.Top
                ? run.Top - point.Y
                : point.Y > run.Bottom
                    ? point.Y - run.Bottom
                    : 0;
            var distance = (horizontalDistance * horizontalDistance) + (verticalDistance * verticalDistance);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestRun = run;
            }
        }

        return nearestRun;
    }

    private static DocumentTextSelectionResult CreateEmptySelection(PageTextContent pageContent)
    {
        return new DocumentTextSelectionResult(
            pageContent.PageIndex,
            null,
            [],
            pageContent.SourceKind);
    }

    private sealed record SelectableRun(
        int OrderedIndex,
        TextRun Run,
        double Left,
        double Top,
        double Right,
        double Bottom);

    private sealed record SourceBox(
        double Left,
        double Top,
        double Right,
        double Bottom)
    {
        public double Height => Bottom - Top;

        public double CenterY => (Top + Bottom) * 0.5;
    }
}
