using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Velune.Windows.Services;

/// <summary>
/// Tracks the currently active WinUI window and provides its handle and dispatcher queue.
/// </summary>
public sealed class WindowsWindowContext
{
    private Window? _lastKnownWindow;

    /// <summary>
    /// Gets the currently active window.
    /// </summary>
    public Window? ActiveWindow
    {
        get;
        private set;
    }

    /// <summary>
    /// Sets the specified window as the currently active window.
    /// </summary>
    /// <param name="window">The window to mark as active.</param>
    public void SetActiveWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        ActiveWindow = window;
        _lastKnownWindow = window;
    }

    /// <summary>
    /// Clears the active window reference if it matches the specified window.
    /// </summary>
    /// <param name="window">The window being deactivated or closed.</param>
    public void ClearActiveWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (ReferenceEquals(ActiveWindow, window))
        {
            ActiveWindow = null;
        }
    }

    /// <summary>
    /// Gets the dispatcher queue from the active or last known window.
    /// </summary>
    /// <returns>The dispatcher queue, or null if no window is available.</returns>
    public DispatcherQueue? GetDispatcherQueue()
    {
        return (ActiveWindow ?? _lastKnownWindow)?.DispatcherQueue;
    }

    /// <summary>
    /// Gets the native window handle (HWND) for the active window.
    /// </summary>
    /// <returns>The native window handle.</returns>
    public nint GetWindowHandle()
    {
        Window? window = ActiveWindow ?? _lastKnownWindow;
        if (window is null)
        {
            throw new InvalidOperationException("The active window is not available yet.");
        }

        return WindowNative.GetWindowHandle(window);
    }
}
