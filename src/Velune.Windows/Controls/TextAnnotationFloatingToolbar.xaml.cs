using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Velune.Windows.ViewModels;

namespace Velune.Windows.Controls;

public sealed partial class TextAnnotationFloatingToolbar : UserControl
{
    private WindowsMainViewModel _viewModel = null!;

    public TextAnnotationFloatingToolbar()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
    }

    public WindowsMainViewModel ViewModel
    {
        get => _viewModel;
        set
        {
            _viewModel = value;
            Bindings.Update();
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ViewModel.ActiveDocumentTab?.SetTextAnnotationToolbarSize(
            e.NewSize.Width,
            e.NewSize.Height,
            ViewModel.SelectedAnnotationId);
    }

    private void OnTextColorClicked(object sender, RoutedEventArgs e)
    {
        WindowsAnnotationColorItem? color = sender is FrameworkElement { Tag: WindowsAnnotationColorItem tagColor }
            ? tagColor
            : sender is FrameworkElement { DataContext: WindowsAnnotationColorItem contextColor }
                ? contextColor
                : null;
        if (color is not null)
        {
            ViewModel.SelectAnnotationColorCommand.Execute(color);
        }
    }

}
