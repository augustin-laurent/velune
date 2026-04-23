using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Velune.Presentation.Platform;

namespace Velune.Presentation.Localization;

internal sealed class NativeMenuLocalizationBinding
{
    private readonly AvaloniaObject _owner;
    private readonly NativeMenu _menu;
    private readonly ILocalizationService _localizationService;
    private readonly Action<NativeMenu, ILocalizationService> _localizeMenu;
    private bool _isDisposed;

    internal NativeMenuLocalizationBinding(
        AvaloniaObject owner,
        NativeMenu menu,
        ILocalizationService localizationService,
        Action<NativeMenu, ILocalizationService> localizeMenu)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _menu = menu ?? throw new ArgumentNullException(nameof(menu));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _localizeMenu = localizeMenu ?? throw new ArgumentNullException(nameof(localizeMenu));

        _menu.NeedsUpdate += OnMenuNeedsUpdate;
        _localizationService.LanguageChanged += OnLanguageChanged;
        Refresh();
    }

    internal void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        _localizeMenu(_menu, _localizationService);
    }

    internal void Detach()
    {
        if (_isDisposed)
        {
            return;
        }

        _menu.NeedsUpdate -= OnMenuNeedsUpdate;
        _localizationService.LanguageChanged -= OnLanguageChanged;
        _isDisposed = true;
    }

    private void OnMenuNeedsUpdate(object? sender, EventArgs e)
    {
        ExecuteOnUiThread(Refresh);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        ExecuteOnUiThread(() =>
        {
            Refresh();

            if (!PresentationPlatform.IsMacOS)
            {
                NativeMenuRefreshHelper.Reapply(_owner, _menu);
            }
        });
    }

    private static void ExecuteOnUiThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.InvokeAsync(action).GetAwaiter().GetResult();
    }
}
