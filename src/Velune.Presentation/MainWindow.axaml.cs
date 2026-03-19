using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Velune.Presentation.ViewModels;

namespace Velune.Presentation.Views;

public partial class MainWindow : Window
{
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
}
