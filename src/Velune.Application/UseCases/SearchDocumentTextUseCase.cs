using System.Text;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Documents;

namespace Velune.Application.UseCases;

public sealed class SearchDocumentTextUseCase
{
    private const int ExcerptContextLength = 32;

    public Result<IReadOnlyList<SearchHit>> Execute(SearchDocumentTextRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Index.HasSearchableText)
        {
            return ResultFactory.Success<IReadOnlyList<SearchHit>>([]);
        }

        if (string.IsNullOrWhiteSpace(request.Query.Text))
        {
            return ResultFactory.Failure<IReadOnlyList<SearchHit>>(
                AppError.Validation(
                    "document.search.query.empty",
                    "Search text cannot be empty."));
        }

        var hits = new List<SearchHit>();
        var query = request.Query.Text;

        foreach (var page in request.Index.Pages)
        {
            if (string.IsNullOrWhiteSpace(page.Text))
            {
                continue;
            }

            var searchStart = 0;
            while (searchStart < page.Text.Length)
            {
                var matchIndex = page.Text.IndexOf(query, searchStart, StringComparison.OrdinalIgnoreCase);
                if (matchIndex < 0)
                {
                    break;
                }

                hits.Add(new SearchHit(
                    page.PageIndex,
                    matchIndex,
                    query.Length,
                    BuildExcerpt(page.Text, matchIndex, query.Length),
                    CollectRegions(page, matchIndex, query.Length),
                    page.SourceKind));

                searchStart = matchIndex + Math.Max(1, query.Length);
            }
        }

        return ResultFactory.Success<IReadOnlyList<SearchHit>>(hits);
    }

    private static List<NormalizedTextRegion> CollectRegions(PageTextContent page, int matchStart, int matchLength)
    {
        var matchEnd = matchStart + matchLength;
        var regions = page.Runs
            .Where(run => run.StartIndex < matchEnd && run.StartIndex + run.Length > matchStart)
            .SelectMany(run => run.Regions)
            .ToList();

        return regions.Count == 0 ? [] : regions;
    }

    private static string BuildExcerpt(string text, int matchStart, int matchLength)
    {
        var start = Math.Max(0, matchStart - ExcerptContextLength);
        var end = Math.Min(text.Length, matchStart + matchLength + ExcerptContextLength);
        var excerpt = text[start..end].ReplaceLineEndings(" ").Trim();

        var builder = new StringBuilder();
        if (start > 0)
        {
            builder.Append('…');
        }

        builder.Append(excerpt);

        if (end < text.Length)
        {
            builder.Append('…');
        }

        return builder.ToString();
    }
}
