using Velune.Application.DTOs;

namespace Velune.Application.Abstractions;

/// <summary>Manages the list of recently opened files.</summary>
public interface IRecentFilesService
{
    /// <summary>Gets all recent file entries ordered by most recent first.</summary>
    /// <returns>The list of recent file items.</returns>
    IReadOnlyList<RecentFileItem> GetAll();

    /// <summary>Adds or promotes a file to the top of the recent files list.</summary>
    /// <param name="item">The recent file entry to add.</param>
    void Add(RecentFileItem item);

    /// <summary>Removes all recent file entries.</summary>
    void Clear();
}
