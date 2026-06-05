using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Velune.Windows.ViewModels;

namespace Velune.Windows.Controls;

public sealed partial class DocumentTabsBar : UserControl
{
    public DocumentTabsBar()
    {
        InitializeComponent();
    }

    public WindowsMainViewModel? ViewModel
    {
        get => DataContext as WindowsMainViewModel;
        set => DataContext = value;
    }

    private async void OnTabClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null ||
            sender is not FrameworkElement { DataContext: WindowsDocumentTabViewModel tab } ||
            !ViewModel.ActivateTabCommand.CanExecute(tab))
        {
            return;
        }

        await ViewModel.ActivateTabCommand.ExecuteAsync(tab);
    }

    private void OnTabPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsDocumentTabViewModel tab })
        {
            tab.RefreshTabChrome(true);
        }
    }

    private void OnTabPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsDocumentTabViewModel tab })
        {
            tab.RefreshTabChrome(false);
        }
    }

    private async void OnCloseTabClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null ||
            sender is not FrameworkElement { DataContext: WindowsDocumentTabViewModel tab } ||
            !ViewModel.CloseTabCommand.CanExecute(tab))
        {
            return;
        }

        await ViewModel.CloseTabCommand.ExecuteAsync(tab);
    }
}
