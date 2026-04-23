using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Velune.Application.Configuration;
using Velune.Presentation.Localization;
using Velune.Presentation.Platform;

namespace Velune.Tests.Unit.Presentation.Localization;

public sealed class NativeMenuLocalizationBindingTests
{
    [Fact]
    public void Constructor_ShouldLocalizeMenuImmediately()
    {
        var previousDetector = PresentationPlatform.IsMacOSDetector;
        PresentationPlatform.IsMacOSDetector = static () => false;

        try
        {
            var owner = new Decorator();
            var menu = new NativeMenu();
            NativeMenu.SetMenu(owner, menu);
            var localizationService = new TriggerableLocalizationService();
            var localizeCalls = 0;

            var binding = new NativeMenuLocalizationBinding(
                owner,
                menu,
                localizationService,
                (_, _) => localizeCalls++);

            Assert.Equal(1, localizeCalls);
            binding.Detach();
        }
        finally
        {
            PresentationPlatform.IsMacOSDetector = previousDetector;
        }
    }

    [Fact]
    public void LanguageChanged_ShouldRelocalizeAndReapplyMenu()
    {
        var previousDetector = PresentationPlatform.IsMacOSDetector;
        PresentationPlatform.IsMacOSDetector = static () => false;

        try
        {
            var owner = new Decorator();
            var menu = new NativeMenu();
            NativeMenu.SetMenu(owner, menu);
            var localizationService = new TriggerableLocalizationService();
            var localizeCalls = 0;
            var binding = new NativeMenuLocalizationBinding(
                owner,
                menu,
                localizationService,
                (_, _) => localizeCalls++);

            localizeCalls = 0;
            var menuPropertyChanges = 0;
            owner.PropertyChanged += (_, args) =>
            {
                if (args.Property == NativeMenu.MenuProperty)
                {
                    menuPropertyChanges++;
                }
            };

            localizationService.RaiseLanguageChanged();

            Assert.Equal(1, localizeCalls);
            Assert.True(menuPropertyChanges >= 2);
            Assert.Same(menu, NativeMenu.GetMenu(owner));
            binding.Detach();
        }
        finally
        {
            PresentationPlatform.IsMacOSDetector = previousDetector;
        }
    }

    [Fact]
    public void NeedsUpdate_ShouldRelocalizeMenuBeforeDisplay()
    {
        var previousDetector = PresentationPlatform.IsMacOSDetector;
        PresentationPlatform.IsMacOSDetector = static () => false;

        try
        {
            var owner = new Decorator();
            var menu = new NativeMenu();
            NativeMenu.SetMenu(owner, menu);
            var localizationService = new TriggerableLocalizationService();
            var localizeCalls = 0;
            var binding = new NativeMenuLocalizationBinding(
                owner,
                menu,
                localizationService,
                (_, _) => localizeCalls++);

            localizeCalls = 0;
            var raiseNeedsUpdate = typeof(NativeMenu).GetMethod(
                "Avalonia.Controls.INativeMenuExporterEventsImplBridge.RaiseNeedsUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(raiseNeedsUpdate);

            raiseNeedsUpdate!.Invoke(menu, Array.Empty<object>());

            Assert.Equal(1, localizeCalls);
            binding.Detach();
        }
        finally
        {
            PresentationPlatform.IsMacOSDetector = previousDetector;
        }
    }

    [Fact]
    public void LanguageChanged_ShouldNotReapplyMenuOnMacOs()
    {
        var previousDetector = PresentationPlatform.IsMacOSDetector;
        PresentationPlatform.IsMacOSDetector = static () => true;

        try
        {
            var owner = new Decorator();
            var menu = new NativeMenu();
            NativeMenu.SetMenu(owner, menu);
            var localizationService = new TriggerableLocalizationService();
            var localizeCalls = 0;
            var binding = new NativeMenuLocalizationBinding(
                owner,
                menu,
                localizationService,
                (_, _) => localizeCalls++);

            localizeCalls = 0;
            var menuPropertyChanges = 0;
            owner.PropertyChanged += (_, args) =>
            {
                if (args.Property == NativeMenu.MenuProperty)
                {
                    menuPropertyChanges++;
                }
            };

            localizationService.RaiseLanguageChanged();

            Assert.Equal(1, localizeCalls);
            Assert.Equal(0, menuPropertyChanges);
            Assert.Same(menu, NativeMenu.GetMenu(owner));
            binding.Detach();
        }
        finally
        {
            PresentationPlatform.IsMacOSDetector = previousDetector;
        }
    }

    private sealed class TriggerableLocalizationService : ILocalizationService
    {
        public string CurrentLanguageCode => "en";

        public AppLanguagePreference CurrentLanguagePreference => AppLanguagePreference.English;

        public int Version => 1;

        public event EventHandler? LanguageChanged;

        public string GetString(string key)
        {
            return key;
        }

        public string GetString(string key, params object?[] arguments)
        {
            return string.Format(GetString(key), arguments);
        }

        public bool HasKey(string key)
        {
            return true;
        }

        public void RaiseLanguageChanged()
        {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
