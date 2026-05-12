using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Velune.Application.Configuration;
using Velune.Application.DTOs;
using Velune.Infrastructure.State;

namespace Velune.Tests.Unit.Infrastructure.State;

public sealed class JsonRecentFilesServiceTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "velune-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Add_ShouldPersistRecentFilesBetweenInstances()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        string filePath = Path.Combine(_temporaryDirectory, "recent-files.json");
        JsonRecentFilesService service = CreateService(filePath, limit: 5);

        service.Add(new RecentFileItem("a.pdf", "/tmp/a.pdf", "Pdf"));
        service.Add(new RecentFileItem("b.png", "/tmp/b.png", "Image"));

        JsonRecentFilesService reloadedService = CreateService(filePath, limit: 5);

        Assert.Equal(2, reloadedService.GetAll().Count);
        Assert.Equal("b.png", reloadedService.GetAll()[0].FileName);
        Assert.Equal("a.pdf", reloadedService.GetAll()[1].FileName);
    }

    [Fact]
    public void Add_ShouldDeduplicateAndRespectLimit_WhenPersisting()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        string filePath = Path.Combine(_temporaryDirectory, "recent-files.json");
        JsonRecentFilesService service = CreateService(filePath, limit: 2);

        service.Add(new RecentFileItem("a.pdf", "/tmp/a.pdf", "Pdf"));
        service.Add(new RecentFileItem("b.pdf", "/tmp/b.pdf", "Pdf"));
        service.Add(new RecentFileItem("a.pdf", "/tmp/a.pdf", "Pdf"));
        service.Add(new RecentFileItem("c.pdf", "/tmp/c.pdf", "Pdf"));

        IReadOnlyList<RecentFileItem> reloadedItems = CreateService(filePath, limit: 2).GetAll();

        Assert.Equal(2, reloadedItems.Count);
        Assert.Equal("c.pdf", reloadedItems[0].FileName);
        Assert.Equal("a.pdf", reloadedItems[1].FileName);
    }

    [Fact]
    public void Clear_ShouldPersistEmptyRecentFiles()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        string filePath = Path.Combine(_temporaryDirectory, "recent-files.json");
        JsonRecentFilesService service = CreateService(filePath, limit: 5);

        service.Add(new RecentFileItem("a.pdf", "/tmp/a.pdf", "Pdf"));
        service.Clear();

        Assert.Empty(CreateService(filePath, limit: 5).GetAll());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup for temporary recent-file stores.
        }
    }

    private static JsonRecentFilesService CreateService(string filePath, int limit)
    {
        return new JsonRecentFilesService(
            NullLogger<JsonRecentFilesService>.Instance,
            Options.Create(new AppOptions
            {
                Name = "Velune.Tests",
                RecentFilesLimit = limit,
                RecentFilesPath = filePath
            }));
    }
}
