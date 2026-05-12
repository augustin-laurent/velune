using Velune.Windows.Services;

namespace Velune.Tests.Windows.Unit.Services;

public sealed class WindowsWindowCoordinatorTests
{
    [Fact]
    public async Task RunAfterWorkspaceLoadedAsync_DoesNotDispatchOpenUntilWorkspaceIsLoaded()
    {
        var workspaceLoaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int dispatchCount = 0;
        int openCount = 0;

        Task runTask = WindowsWindowCoordinator.RunAfterWorkspaceLoadedAsync(
            workspaceLoaded.Task,
            operation =>
            {
                dispatchCount++;
                return operation();
            },
            () =>
            {
                openCount++;
                return Task.CompletedTask;
            });

        await Task.Delay(50);

        Assert.False(runTask.IsCompleted);
        Assert.Equal(0, dispatchCount);
        Assert.Equal(0, openCount);

        workspaceLoaded.SetResult();
        await runTask;

        Assert.Equal(1, dispatchCount);
        Assert.Equal(1, openCount);
    }
}
