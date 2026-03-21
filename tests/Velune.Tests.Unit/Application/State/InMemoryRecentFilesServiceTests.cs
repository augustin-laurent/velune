using Microsoft.Extensions.Options;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.DTOs;

namespace Velune.Tests.Unit.Application.State;

public sealed class InMemoryRecentFilesServiceTests
{
    [Fact]
    public void Add_ShouldInsertNewestItemFirst()
    {
        var service = CreateService(limit: 5);

        service.Add(new RecentFileItem("a.pdf", "/tmp/a.pdf", "Pdf"));
        service.Add(new RecentFileItem("b.pdf", "/tmp/b.pdf", "Pdf"));

        Assert.Equal(2, service.GetAll().Count);
        Assert.Equal("b.pdf", service.GetAll()[0].FileName);
        Assert.Equal("a.pdf", service.GetAll()[1].FileName);
    }

    [Fact]
    public void Add_ShouldRemoveDuplicateAndMoveItemToTop()
    {
        var service = CreateService(limit: 5);

        service.Add(new RecentFileItem("a.pdf", "/tmp/a.pdf", "Pdf"));
        service.Add(new RecentFileItem("b.pdf", "/tmp/b.pdf", "Pdf"));
        service.Add(new RecentFileItem("a.pdf", "/tmp/a.pdf", "Pdf"));

        Assert.Equal(2, service.GetAll().Count);
        Assert.Equal("a.pdf", service.GetAll()[0].FileName);
    }

    [Fact]
    public void Add_ShouldRespectConfiguredLimit()
    {
        var service = CreateService(limit: 2);

        service.Add(new RecentFileItem("a.pdf", "/tmp/a.pdf", "Pdf"));
        service.Add(new RecentFileItem("b.pdf", "/tmp/b.pdf", "Pdf"));
        service.Add(new RecentFileItem("c.pdf", "/tmp/c.pdf", "Pdf"));

        Assert.Equal(2, service.GetAll().Count);
        Assert.Equal("c.pdf", service.GetAll()[0].FileName);
        Assert.Equal("b.pdf", service.GetAll()[1].FileName);
    }

    private static IRecentFilesService CreateService(int limit)
    {
        var options = Options.Create(new AppOptions
        {
            RecentFilesLimit = limit
        });

        return new InMemoryRecentFilesService(options);
    }
}
