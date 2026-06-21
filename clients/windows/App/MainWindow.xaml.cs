using System.Windows;

namespace TheMarkdownWeb.App;

/// <summary>
/// Interaction logic for MainWindow.xaml — the shell window. Hosts the browser-like toolbar
/// (Back / Forward / Reload) wired to an inert <see cref="ShellViewModel"/>. Real navigation lands
/// in Story 3-2 / 3-5; at this story the handlers only record the last action.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => _viewModel.OnBack();

    private void ForwardButton_Click(object sender, RoutedEventArgs e) => _viewModel.OnForward();

    private void ReloadButton_Click(object sender, RoutedEventArgs e) => _viewModel.OnReload();
}
