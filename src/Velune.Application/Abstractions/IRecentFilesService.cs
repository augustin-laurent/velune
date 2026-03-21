using Velune.Application.DTOs;

namespace Velune.Application.Abstractions;

public interface IRecentFilesService
{
    IReadOnlyList<RecentFileItem> GetAll();

    void Add(RecentFileItem item);

    void Clear();
}
