using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Velune.Presentation.Views;

public static class AboutWindowFactory
{
    private static readonly Uri AppIconUri = new("avares://Velune.Presentation/Assets/Brand/velune-app-icon.png");

    public static Window Create()
    {
        var version = typeof(AboutWindowFactory).Assembly.GetName().Version?.ToString(3) ?? "Development";

        var closeButton = new Button
        {
            Content = "Close",
            MinWidth = 88,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var aboutWindow = new Window
        {
            Title = "About Velune",
            Width = 420,
            Height = 320,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        closeButton.Click += (_, _) => aboutWindow.Close();
        using (var iconStream = AssetLoader.Open(AppIconUri))
        {
            aboutWindow.Icon = new WindowIcon(iconStream);
        }

        Bitmap logoBitmap;
        using (var imageStream = AssetLoader.Open(AppIconUri))
        {
            logoBitmap = new Bitmap(imageStream);
        }

        aboutWindow.Content = new Border
        {
            Padding = new Thickness(24),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new Border
                    {
                        Width = 80,
                        Height = 80,
                        CornerRadius = new CornerRadius(24),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Background = new LinearGradientBrush
                        {
                            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                            GradientStops = new GradientStops
                            {
                                new GradientStop(Color.Parse("#FFF6EEFB"), 0),
                                new GradientStop(Color.Parse("#FFEAF4FF"), 1)
                            }
                        },
                        Child = new Image
                        {
                            Source = logoBitmap,
                            Width = 60,
                            Height = 60,
                            Stretch = Stretch.Uniform,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    },
                    new TextBlock
                    {
                        Text = "Velune",
                        FontSize = 24,
                        FontWeight = FontWeight.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = $"Version {version}",
                        Opacity = 0.75,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "Native PDF and image viewing, page management, search, OCR, and printing in a focused desktop workspace.",
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center
                    },
                    closeButton
                }
            }
        };

        return aboutWindow;
    }
}
