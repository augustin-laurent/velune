using Microsoft.UI.Xaml.Controls;
using Velune.Windows.ViewModels;

namespace Velune.Windows.Controls;

public sealed partial class SettingsPanelControl : UserControl
{
    public SettingsPanelControl()
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
}
