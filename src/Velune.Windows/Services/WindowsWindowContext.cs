using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Velune.Windows.Services;

public sealed class WindowsWindowContext
{
    private Window? _lastKnownWindow;

    public Window? MainWindow
    {
        get => ActiveWindow;
        set => ActiveWindow = value;
    }

    public Window? ActiveWindow
    {
        get;
        private set;
    }

    public void SetActiveWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        ActiveWindow = window;
        _lastKnownWindow = window;
    }

    public void ClearActiveWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (ReferenceEquals(ActiveWindow, window))
        {
            ActiveWindow = null;
        }
    }

    public DispatcherQueue? GetDispatcherQueue()
    {
        return (ActiveWindow ?? _lastKnownWindow)?.DispatcherQueue;
    }

    public nint GetWindowHandle()
    {
        var window = ActiveWindow ?? _lastKnownWindow;
        if (window is null)
        {
            throw new InvalidOperationException("The active window is not available yet.");
        }

        return WindowNative.GetWindowHandle(window);
    }
}
