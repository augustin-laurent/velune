using Microsoft.Extensions.Options;
using Velune.Application.Configuration;
using Velune.Application.DTOs;

namespace Velune.Application.Abstractions;

public sealed class InMemoryRecentFilesService : IRecentFilesService
{
    private readonly int _limit;
    private readonly List<RecentFileItem> _items = [];

    public InMemoryRecentFilesService(IOptions<AppOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _limit = Math.Max(1, options.Value.RecentFilesLimit);
    }

    public IReadOnlyList<RecentFileItem> GetAll() => _items;

    public void Add(RecentFileItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        _items.RemoveAll(existing =>
            string.Equals(existing.FilePath, item.FilePath, StringComparison.OrdinalIgnoreCase));

        _items.Insert(0, item);

        if (_items.Count > _limit)
        {
            _items.RemoveRange(_limit, _items.Count - _limit);
        }
    }

    public void Clear()
    {
        _items.Clear();
    }
}
