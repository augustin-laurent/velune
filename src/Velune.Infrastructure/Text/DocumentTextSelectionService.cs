using System.Text;
using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Text;

/// <summary>
/// Resolves text selections from anchor/active point pairs within a page's text content.
/// </summary>
public sealed class DocumentTextSelectionService : IDocumentTextSelectionService
{
    /// <inheritdoc />
    public Result<DocumentTextSelectionResult> ResolveByRange(
        DocumentTextIndex index,
        PageIndex pageIndex,
        int startCharacterIndex,
        int endCharacterIndex)
    {
        ArgumentNullException.ThrowIfNull(index);

        PageTextContent? pageContent = index.Pages
            .FirstOrDefault(page => page.PageIndex == pageIndex);
        if (pageContent is null)
        {
            return ResultFactory.Failure<DocumentTextSelectionResult>(
                AppError.Validation(
                    "document.text.selection.page.missing",
                    "The selected page has no searchable text content."));
        }

        startCharacterIndex = Math.Max(0, startCharacterIndex);
        endCharacterIndex = Math.Min(pageContent.Text.Length - 1, endCharacterIndex);
        if (startCharacterIndex > endCharacterIndex)
        {
            return ResultFactory.Success(CreateEmptySelection(pageContent));
        }

        if (pageContent.SourceKind is TextSourceKind.EmbeddedPdfText &&
            pageContent.CharacterRegionsByIndex.Count > 0)
        {
            List<SelectableCharacter> selectableCharacters = ResolveSelectableCharacters(pageContent);
            NormalizedTextRegion[] selectedRegions = selectableCharacters
                .Where(c => c.TextIndex >= startCharacterIndex && c.TextIndex <= endCharacterIndex)
                .OrderBy(c => c.TextIndex)
                .Select(c => c.Region)
                .ToArray();

            if (selectedRegions.Length == 0)
            {
                return ResultFactory.Success(CreateEmptySelection(pageContent));
            }

            string? selectedText = NormalizeSelectedText(
                pageContent.Text[startCharacterIndex..Math.Min(pageContent.Text.Length, endCharacterIndex + 1)]);
            List<NormalizedTextRegion> regions = MergeNearbyRegions(
                selectedRegions, pageContent.SourceWidth, pageContent.SourceHeight);

            return ResultFactory.Success(
                new DocumentTextSelectionResult(
                    pageContent.PageIndex, selectedText, regions,
                    pageContent.SourceKind, startCharacterIndex, endCharacterIndex));
        }

        // Run-based: select runs that overlap the character range
        List<SelectableRun> selectableRuns = ResolveSelectableRuns(pageContent);
        SelectableRun[] overlapping = selectableRuns
            .Where(r => r.Run.StartIndex + r.Run.Length > startCharacterIndex &&
                        r.Run.StartIndex <= endCharacterIndex)
            .OrderBy(r => r.OrderedIndex)
            .ToArray();

        if (overlapping.Length == 0)
        {
            return ResultFactory.Success(CreateEmptySelection(pageContent));
        }

        List<NormalizedTextRegion> runRegions = MergeNearbyRegions(
            overlapping.SelectMany(r => r.Run.Regions), pageContent.SourceWidth, pageContent.SourceHeight);
        string? runText = NormalizeSelectedText(
            pageContent.Text[startCharacterIndex..Math.Min(pageContent.Text.Length, endCharacterIndex + 1)]);

        return ResultFactory.Success(
            new DocumentTextSelectionResult(
                pageContent.PageIndex, runText, runRegions,
                pageContent.SourceKind, startCharacterIndex, endCharacterIndex));
    }

    /// <inheritdoc />
    public Result<DocumentTextSelectionResult> Resolve(DocumentTextSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        PageTextContent? pageContent = request.Index.Pages
            .FirstOrDefault(page => page.PageIndex == request.PageIndex);
        if (pageContent is null)
        {
            return ResultFactory.Failure<DocumentTextSelectionResult>(
                AppError.Validation(
                    "document.text.selection.page.missing",
                    "The selected page has no searchable text content."));
        }

        if (pageContent.SourceKind is TextSourceKind.EmbeddedPdfText &&
            pageContent.CharacterRegionsByIndex.Count > 0)
        {
            return SelectCharacterBasedText(pageContent, request);
        }

        return SelectRunBasedText(pageContent, request);
    }

    private static Result<DocumentTextSelectionResult> SelectCharacterBasedText(
        PageTextContent pageContent,
        DocumentTextSelectionRequest request)
    {
        List<SelectableCharacter> selectableCharacters = ResolveSelectableCharacters(pageContent);
        if (selectableCharacters.Count == 0)
        {
            return ResultFactory.Success(CreateEmptySelection(pageContent));
        }

        SelectableCharacter? anchorCharacter = FindNearestCharacter(selectableCharacters, request.AnchorPoint);
        SelectableCharacter? activeCharacter = FindNearestCharacter(selectableCharacters, request.ActivePoint) ?? anchorCharacter;
        if (anchorCharacter is null || activeCharacter is null)
        {
            return ResultFactory.Success(CreateEmptySelection(pageContent));
        }

        int startIndex = Math.Min(anchorCharacter.TextIndex, activeCharacter.TextIndex);
        int endIndex = Math.Max(anchorCharacter.TextIndex, activeCharacter.TextIndex);
        NormalizedTextRegion[] selectedRegions = selectableCharacters
            .Where(character => character.TextIndex >= startIndex && character.TextIndex <= endIndex)
            .OrderBy(character => character.TextIndex)
            .Select(character => character.Region)
            .ToArray();

        if (selectedRegions.Length == 0)
        {
            return ResultFactory.Success(CreateEmptySelection(pageContent));
        }

        string? selectedText = endIndex >= startIndex && startIndex < pageContent.Text.Length
            ? NormalizeSelectedText(pageContent.Text[startIndex..Math.Min(pageContent.Text.Length, endIndex + 1)])
            : null;
        List<NormalizedTextRegion> regions = MergeNearbyRegions(
            selectedRegions,
            pageContent.SourceWidth,
            pageContent.SourceHeight);

        return ResultFactory.Success(
            new DocumentTextSelectionResult(
                pageContent.PageIndex,
                selectedText,
                regions,
                pageContent.SourceKind,
                startIndex,
                endIndex));
    }

    private static Result<DocumentTextSelectionResult> SelectRunBasedText(
        PageTextContent pageContent,
        DocumentTextSelectionRequest request)
    {
        List<SelectableRun> selectableRuns = ResolveSelectableRuns(pageContent);
        if (selectableRuns.Count == 0)
        {
            return ResultFactory.Success(CreateEmptySelection(pageContent));
        }

        SelectableRun? anchorRun = FindNearestRun(selectableRuns, request.AnchorPoint);
        SelectableRun? activeRun = FindNearestRun(selectableRuns, request.ActivePoint);
        if (anchorRun is null || activeRun is null)
        {
            return ResultFactory.Success(CreateEmptySelection(pageContent));
        }

        int startIndex = Math.Min(anchorRun.OrderedIndex, activeRun.OrderedIndex);
        int endIndex = Math.Max(anchorRun.OrderedIndex, activeRun.OrderedIndex);
        SelectableRun[] selectedRuns = selectableRuns
            .Where(run => run.OrderedIndex >= startIndex && run.OrderedIndex <= endIndex)
            .OrderBy(run => run.OrderedIndex)
            .ToArray();

        if (selectedRuns.Length == 0)
        {
            return ResultFactory.Success(CreateEmptySelection(pageContent));
        }

        List<NormalizedTextRegion> regions = MergeNearbyRegions(
            selectedRuns.SelectMany(run => run.Run.Regions),
            pageContent.SourceWidth,
            pageContent.SourceHeight);

        int selectionStart = selectedRuns[0].Run.StartIndex;
        int selectionEnd = Math.Min(
            pageContent.Text.Length,
            selectedRuns[^1].Run.StartIndex + selectedRuns[^1].Run.Length);

        string? selectedText = selectionEnd > selectionStart
            ? NormalizeSelectedText(pageContent.Text[selectionStart..selectionEnd])
            : null;

        return ResultFactory.Success(
            new DocumentTextSelectionResult(
                pageContent.PageIndex,
                selectedText,
                regions,
                pageContent.SourceKind,
                selectionStart,
                selectionEnd - 1));
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
        SourceBox current = boxes[0];

        for (int index = 1; index < boxes.Count; index++)
        {
            SourceBox next = boxes[index];
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
        double verticalOverlap = Math.Min(current.Bottom, next.Bottom) - Math.Max(current.Top, next.Top);
        double minimumHeight = Math.Min(current.Height, next.Height);
        double maximumHeight = Math.Max(current.Height, next.Height);
        double centerDistance = Math.Abs(current.CenterY - next.CenterY);

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
        double boxHeight = Math.Max(0, box.Bottom - box.Top);
        double horizontalPadding = Math.Max(1.5, boxHeight * 0.12);
        double verticalPadding = Math.Max(1.5, boxHeight * 0.18);

        double left = Math.Max(0, box.Left - horizontalPadding);
        double top = Math.Max(0, box.Top - verticalPadding);
        double right = Math.Min(sourceWidth, box.Right + horizontalPadding);
        double bottom = Math.Min(sourceHeight, box.Bottom + verticalPadding);

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
        bool previousWasWhitespace = false;

        foreach (char character in text.Trim())
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
        TextRun[] orderedRuns = pageContent.Runs
            .OrderBy(run => run.StartIndex)
            .ToArray();

        var selectableRuns = new List<SelectableRun>(orderedRuns.Length);

        for (int index = 0; index < orderedRuns.Length; index++)
        {
            if (!TryGetRunBounds(pageContent, orderedRuns[index], out double left, out double top, out double right, out double bottom))
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
        double nearestDistance = double.MaxValue;

        foreach (SelectableRun run in selectableRuns)
        {
            double horizontalDistance = point.X < run.Left
                ? run.Left - point.X
                : point.X > run.Right
                    ? point.X - run.Right
                    : 0;
            double verticalDistance = point.Y < run.Top
                ? run.Top - point.Y
                : point.Y > run.Bottom
                    ? point.Y - run.Bottom
                    : 0;
            double distance = (horizontalDistance * horizontalDistance) + (verticalDistance * verticalDistance);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestRun = run;
            }
        }

        return nearestRun;
    }

    private static List<SelectableCharacter> ResolveSelectableCharacters(PageTextContent pageContent)
    {
        var selectableCharacters = new List<SelectableCharacter>(pageContent.CharacterRegionsByIndex.Count);

        foreach ((int textIndex, NormalizedTextRegion region) in pageContent.CharacterRegionsByIndex.OrderBy(entry => entry.Key))
        {
            if (textIndex < 0 || textIndex >= pageContent.Text.Length)
            {
                continue;
            }

            double left = region.X * pageContent.SourceWidth;
            double top = region.Y * pageContent.SourceHeight;
            double right = (region.X + region.Width) * pageContent.SourceWidth;
            double bottom = (region.Y + region.Height) * pageContent.SourceHeight;
            if (right <= left || bottom <= top)
            {
                continue;
            }

            selectableCharacters.Add(new SelectableCharacter(textIndex, region, left, top, right, bottom));
        }

        return selectableCharacters;
    }

    private static SelectableCharacter? FindNearestCharacter(
        IReadOnlyList<SelectableCharacter> selectableCharacters,
        DocumentTextSelectionPoint point)
    {
        if (selectableCharacters.Count == 0)
        {
            return null;
        }

        SelectableCharacter? nearestCharacter = null;
        double nearestDistance = double.MaxValue;

        foreach (SelectableCharacter character in selectableCharacters)
        {
            double horizontalDistance = point.X < character.Left
                ? character.Left - point.X
                : point.X > character.Right
                    ? point.X - character.Right
                    : 0;
            double verticalDistance = point.Y < character.Top
                ? character.Top - point.Y
                : point.Y > character.Bottom
                    ? point.Y - character.Bottom
                    : 0;
            double distance = (horizontalDistance * horizontalDistance) + (verticalDistance * verticalDistance);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestCharacter = character;
            }
        }

        return nearestCharacter;
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

    private sealed record SelectableCharacter(
        int TextIndex,
        NormalizedTextRegion Region,
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
