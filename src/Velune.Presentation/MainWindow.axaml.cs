using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using Velune.Presentation.ViewModels;

namespace Velune.Presentation.Views;

public partial class MainWindow : Window
{
    private double _trackpadNavigationAccumulator;

    public MainWindow()
    {
        InitializeComponent();
    }

    [ActivatorUtilitiesConstructor]
    public MainWindow(MainWindowViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;
    }

    private async void OnDocumentPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (!viewModel.HasOpenDocument)
        {
            _trackpadNavigationAccumulator = 0;
            return;
        }

        if (viewModel.ShouldUseTrackpadForPan)
        {
            _trackpadNavigationAccumulator = 0;
            return;
        }

        if (Math.Abs(e.Delta.Y) <= double.Epsilon)
        {
            return;
        }

        _trackpadNavigationAccumulator += e.Delta.Y;

        if (_trackpadNavigationAccumulator >= 1.0)
        {
            _trackpadNavigationAccumulator = 0;
            e.Handled = true;
            await viewModel.NavigateToPreviousPageFromTrackpadAsync();
            return;
        }

        if (_trackpadNavigationAccumulator <= -1.0)
        {
            _trackpadNavigationAccumulator = 0;
            e.Handled = true;
            await viewModel.NavigateToNextPageFromTrackpadAsync();
        }
    }

    private async void OnDocumentViewerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        await viewModel.UpdateDocumentViewportAsync(
            e.NewSize.Width,
            e.NewSize.Height);
    }
}
