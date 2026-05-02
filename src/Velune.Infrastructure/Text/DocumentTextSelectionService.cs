using System.Text;
using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Documents;

namespace Velune.Infrastructure.Text;

public sealed class DocumentTextSelectionService : IDocumentTextSelectionService
{
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
        var selectableCharacters = ResolveSelectableCharacters(pageContent);
        if (selectableCharacters.Count == 0)
        {
            return ResultFactory.Success(CreateEmptySelection(pageContent));
        }

        var anchorCharacter = FindNearestCharacter(selectableCharacters, request.AnchorPoint);
        var activeCharacter = FindNearestCharacter(selectableCharacters, request.ActivePoint) ?? anchorCharacter;
        if (anchorCharacter is null || activeCharacter is null)
        {
            return ResultFactory.Success(CreateEmptySelection(pageContent));
        }

        var startIndex = Math.Min(anchorCharacter.TextIndex, activeCharacter.TextIndex);
        var endIndex = Math.Max(anchorCharacter.TextIndex, activeCharacter.TextIndex);
        var selectedRegions = selectableCharacters
            .Where(character => character.TextIndex >= startIndex && character.TextIndex <= endIndex)
            .OrderBy(character => character.TextIndex)
            .Select(character => character.Region)
            .ToArray();

        if (selectedRegions.Length == 0)
        {
            return ResultFactory.Success(CreateEmptySelection(pageContent));
        }

        var selectedText = endIndex >= startIndex && startIndex < pageContent.Text.Length
            ? NormalizeSelectedText(pageContent.Text[startIndex..Math.Min(pageContent.Text.Length, endIndex + 1)])
            : null;
        var regions = MergeNearbyRegions(
            selectedRegions,
            pageContent.SourceWidth,
            pageContent.SourceHeight);

        return ResultFactory.Success(
            new DocumentTextSelectionResult(
                pageContent.PageIndex,
                selectedText,
                regions,
                pageContent.SourceKind));
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

    private static List<SelectableCharacter> ResolveSelectableCharacters(PageTextContent pageContent)
    {
        var selectableCharacters = new List<SelectableCharacter>(pageContent.CharacterRegionsByIndex.Count);

        foreach (var (textIndex, region) in pageContent.CharacterRegionsByIndex.OrderBy(entry => entry.Key))
        {
            if (textIndex < 0 || textIndex >= pageContent.Text.Length)
            {
                continue;
            }

            var left = region.X * pageContent.SourceWidth;
            var top = region.Y * pageContent.SourceHeight;
            var right = (region.X + region.Width) * pageContent.SourceWidth;
            var bottom = (region.Y + region.Height) * pageContent.SourceHeight;
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
        var nearestDistance = double.MaxValue;

        foreach (var character in selectableCharacters)
        {
            var horizontalDistance = point.X < character.Left
                ? character.Left - point.X
                : point.X > character.Right
                    ? point.X - character.Right
                    : 0;
            var verticalDistance = point.Y < character.Top
                ? character.Top - point.Y
                : point.Y > character.Bottom
                    ? point.Y - character.Bottom
                    : 0;
            var distance = (horizontalDistance * horizontalDistance) + (verticalDistance * verticalDistance);

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
