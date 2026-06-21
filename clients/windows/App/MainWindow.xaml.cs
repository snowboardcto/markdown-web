using System.Net.Http;
using System.Windows;
using System.Windows.Input;

namespace TheMarkdownWeb.App;

/// <summary>
/// Interaction logic for MainWindow.xaml — the shell window. Hosts the browser-like toolbar
/// (Back / Forward / Reload) wired to an inert <see cref="ShellViewModel"/>, plus the Story 3-2 address
/// bar (lock + host/path input + <c>.md only</c> tag) backed by an <see cref="AddressBarViewModel"/>.
/// Submitting a <c>.md</c> URL fetches the raw markdown; rendering it is Story 3-3 (the fetched string is
/// held on the VM, not displayed).
/// </summary>
public partial class MainWindow : Window
{
    // A single shared HttpClient for the lifetime of the window (App owns networking).
    private static readonly HttpClient SharedHttpClient = new();

    private readonly ShellViewModel _viewModel = new();
    private readonly AddressBarViewModel _addressBar =
        new(new MarkdownFetcher(SharedHttpClient), new SystemBrowserLauncher());

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        // The address bar binds to its own VM (AddressText / State), distinct from the shell VM.
        AddressBar.DataContext = _addressBar;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => _viewModel.OnBack();

    private void ForwardButton_Click(object sender, RoutedEventArgs e) => _viewModel.OnForward();

    private void ReloadButton_Click(object sender, RoutedEventArgs e) => _viewModel.OnReload();

    private async void AddressInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            // SubmitAsync never throws (AC6); fetched markdown is held on the VM, not rendered here (3-3).
            await _addressBar.SubmitAsync();
        }
    }
}
