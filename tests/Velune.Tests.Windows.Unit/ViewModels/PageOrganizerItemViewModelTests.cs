using Velune.Windows.ViewModels;

namespace Velune.Tests.Windows.Unit.ViewModels;

public sealed class PageOrganizerItemViewModelTests
{
    [Fact]
    public void AutomationId_FollowsCurrentPageNumber()
    {
        var item = new PageOrganizerItemViewModel(3);

        item.PageNumber = 7;

        Assert.Equal("PageOrganizerPageItem_7", item.AutomationId);
    }

    [Fact]
    public void AutomationName_FollowsCurrentPageNumber()
    {
        var item = new PageOrganizerItemViewModel(3, automationNameFormat: "Page {0}");

        item.PageNumber = 7;

        Assert.Equal("Page 7", item.AutomationName);
    }
}
