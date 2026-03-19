namespace Velune.Presentation.ViewModels;

public sealed class MainWindowViewModel
{
    public string Title => "Velune";
    public string ApplicationTitle => "Velune";
    public string SidebarTitle => "Pages";
    public string EmptyStateTitle => "Open a document";
    public string EmptyStateDescription => "Open a PDF or an image to start viewing it.";
    public string StatusText => "Ready";
}
