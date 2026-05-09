using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Velune.Windows.ViewModels;

namespace Velune.Windows.Services;

/// <summary>
/// Manages window lifecycle transitions between the welcome screen and workspace.
/// </summary>
public sealed class WindowsWindowCoordinator
{
    private readonly IServiceProvider _services;
    private readonly DispatcherQueue _dispatcherQueue;
    private WelcomeWindow? _welcomeWindow;
    private MainWindow? _workspaceWindow;
    private bool _isTransitioning;

    /// <summary>
    /// Initializes the coordinator with the DI container for resolving windows.
    /// </summary>
    /// <param name="services">The application service provider.</param>
    public WindowsWindowCoordinator(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("The Windows UI dispatcher is not available.");
    }

    /// <summary>
    /// Shows or activates the welcome window.
    /// </summary>
    /// <returns>The welcome window instance.</returns>
    public WelcomeWindow ShowWelcome()
    {
        if (_welcomeWindow is null)
        {
            _welcomeWindow = _services.GetRequiredService<WelcomeWindow>();
        }

        _welcomeWindow.Activate();
        return _welcomeWindow;
    }

    /// <summary>
    /// Shows or activates the main workspace window.
    /// </summary>
    /// <returns>The workspace window instance.</returns>
    public MainWindow ShowWorkspace()
    {
        if (_workspaceWindow is null)
        {
            _workspaceWindow = _services.GetRequiredService<MainWindow>();
        }

        _workspaceWindow.Activate();
        return _workspaceWindow;
    }

    /// <summary>
    /// Opens the workspace window and loads the specified files into document tabs.
    /// </summary>
    /// <param name="paths">File paths to open.</param>
    public async Task OpenWorkspaceWithFilesAsync(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var pathsToOpen = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();

        if (pathsToOpen.Length == 0)
        {
            return;
        }

        await RunOnCoordinatorDispatcherAsync(() => OpenWorkspaceWithFilesOnDispatcherAsync(pathsToOpen));
    }

    private async Task OpenWorkspaceWithFilesOnDispatcherAsync(string[] pathsToOpen)
    {
        if (_isTransitioning)
        {
            return;
        }

        _isTransitioning = true;
        try
        {
            var workspace = ShowWorkspace();
            var viewModel = _services.GetRequiredService<WindowsMainViewModel>();
            await RunAfterWorkspaceLoadedAsync(
                workspace.WaitUntilLoadedAsync(),
                RunOnCoordinatorDispatcherAsync,
                async () =>
                {
                    _welcomeWindow?.Close();
                    await viewModel.HandleHomeFilesDroppedAsync(pathsToOpen);
                });
        }
        finally
        {
            await RunOnCoordinatorDispatcherAsync(() =>
            {
                _isTransitioning = false;
                return Task.CompletedTask;
            });
        }
    }

    /// <summary>
    /// Closes the workspace window and returns to the welcome screen.
    /// </summary>
    /// <param name="window">The workspace window to close.</param>
    public void ReturnToWelcome(MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (!_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(() => ReturnToWelcome(window));
            return;
        }

        if (_isTransitioning)
        {
            return;
        }

        _isTransitioning = true;
        try
        {
            ShowWelcome();
            window.Close();
            var viewModel = _services.GetRequiredService<WindowsMainViewModel>();
            viewModel.ResetForWelcome();
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    /// <summary>
    /// Notifies the coordinator that the welcome window has been closed.
    /// </summary>
    /// <param name="window">The closed welcome window.</param>
    public void NotifyWelcomeClosed(WelcomeWindow window)
    {
        if (ReferenceEquals(_welcomeWindow, window))
        {
            _welcomeWindow = null;
        }
    }

    /// <summary>
    /// Notifies the coordinator that the workspace window has been closed.
    /// </summary>
    /// <param name="window">The closed workspace window.</param>
    public void NotifyWorkspaceClosed(MainWindow window)
    {
        if (ReferenceEquals(_workspaceWindow, window))
        {
            _workspaceWindow = null;
        }
    }

    private Task RunOnCoordinatorDispatcherAsync(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_dispatcherQueue.HasThreadAccess)
        {
            return operation();
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await operation();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }))
        {
            completion.SetException(new InvalidOperationException("The Windows UI dispatcher is not available."));
        }

        return completion.Task;
    }

    internal static async Task RunAfterWorkspaceLoadedAsync(
        Task workspaceLoaded,
        Func<Func<Task>, Task> dispatchAsync,
        Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(workspaceLoaded);
        ArgumentNullException.ThrowIfNull(dispatchAsync);
        ArgumentNullException.ThrowIfNull(operation);

        await workspaceLoaded;
        await dispatchAsync(operation);
    }
}
