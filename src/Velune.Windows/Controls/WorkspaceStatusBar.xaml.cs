using Microsoft.UI.Xaml.Controls;
using Velune.Windows.ViewModels;

namespace Velune.Windows.Controls;

public sealed partial class WorkspaceStatusBar : UserControl
{
    public WorkspaceStatusBar()
    {
        InitializeComponent();
    }

    public WindowsMainViewModel? ViewModel
    {
        get => DataContext as WindowsMainViewModel;
        set => DataContext = value;
    }
}
