using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Velune.Windows.ViewModels;
using Windows.System;

namespace Velune.Windows.Controls;

public sealed partial class SearchPanelControl : UserControl
{
    public SearchPanelControl()
    {
        InitializeComponent();
    }

    public WindowsMainViewModel? ViewModel
    {
        get => DataContext as WindowsMainViewModel;
        set
        {
            DataContext = value;
            Bindings?.Update();
        }
    }

    private async void OnSearchBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel is null ||
            e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        if (sender is TextBox textBox &&
            ViewModel.ActiveDocumentTab is { } tab)
        {
            tab.SearchQuery = textBox.Text;
        }

        try
        {
            if (ViewModel.SearchTextCommand.CanExecute(null))
            {
                await ViewModel.SearchTextCommand.ExecuteAsync(null);
            }
        }
        catch (Exception exception)
        {
            ViewModel.StatusText = exception.Message;
        }
    }

    private async void OnSearchResultSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        try
        {
            if (sender is ListView { SelectedItem: WindowsSearchResultItemViewModel result } &&
                ViewModel.OpenSearchResultCommand.CanExecute(result))
            {
                await ViewModel.OpenSearchResultCommand.ExecuteAsync(result);
            }
        }
        catch (Exception exception)
        {
            ViewModel.StatusText = exception.Message;
        }
    }
}
