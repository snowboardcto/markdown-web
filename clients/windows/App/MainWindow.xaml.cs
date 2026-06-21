using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TheMarkdownWeb.Rendering;

namespace TheMarkdownWeb.App;

/// <summary>
/// Interaction logic for MainWindow.xaml — the shell window. Story 3.5 wires the real navigation:
/// the fetcher → <see cref="NavigationController"/> (history + endpoint mapping) → a
/// <see cref="ContentHostController"/> that hosts the rendered <see cref="FlowDocument"/> in
/// <c>ContentScroll</c>. The toolbar Back/Forward/Reload and a submitted address all drive the
/// controller; hosted hyperlinks classify + dispatch through the same controller. <c>Rendering</c>
/// stays pure — all I/O (fetch, image load, browser launch) lives here in <c>App</c>.
/// </summary>
public partial class MainWindow : Window
{
    // A single shared HttpClient for the lifetime of the window (App owns networking).
    private static readonly HttpClient SharedHttpClient = new();

    private readonly MarkdownFetcher _fetcher = new(SharedHttpClient);
    private readonly IUrlLauncher _launcher = new SystemBrowserLauncher();

    private readonly ShellViewModel _viewModel;
    private readonly AddressBarViewModel _addressBar;
    private readonly NavigationController _controller;
    private readonly ContentHostController _contentHost;

    public MainWindow()
    {
        InitializeComponent();

        _addressBar = new AddressBarViewModel(_fetcher, _launcher);

        // The content host renders into the named read-only scroll viewer and attaches the single
        // host-level hyperlink handler that dispatches classified clicks through the controller.
        _contentHost = new ContentHostController(
            ContentScroll,
            new FlowDocumentRenderer(),
            new SystemImageLoader(),
            DispatchLinkAsync);

        // The controller fetches the /api/negotiate/<slug> endpoint (AC9) and renders into the host.
        _controller = new NavigationController(
            FetchEndpointAsync,
            (markdown, pageUrl) => _contentHost.ShowMarkdown(markdown, pageUrl),
            () => _contentHost.ShowBroken(),
            _launcher);

        _viewModel = new ShellViewModel(_controller);
        DataContext = _viewModel;
        AddressBar.DataContext = _addressBar;
    }

    /// <summary>Fetches a page URL via the negotiate-endpoint mapping (AC9). Never throws.</summary>
    private Task<FetchResult> FetchEndpointAsync(Uri pageUrl, CancellationToken ct)
    {
        Uri endpoint = PageEndpointResolver.ToFetchEndpoint(pageUrl);
        return _fetcher.FetchAsync(endpoint.ToString(), ct);
    }

    /// <summary>Dispatches a classified hosted-link click; an Anchor scrolls in place, others go to the controller.</summary>
    private Task DispatchLinkAsync(LinkTarget target)
    {
        if (target.Kind == LinkKind.Anchor && target.Fragment is not null)
        {
            _contentHost.ScrollToAnchor(target.Fragment);
            return Task.CompletedTask;
        }

        return _controller.DispatchAsync(target);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => _viewModel.OnBack();

    private void ForwardButton_Click(object sender, RoutedEventArgs e) => _viewModel.OnForward();

    private void ReloadButton_Click(object sender, RoutedEventArgs e) => _viewModel.OnReload();

    private async void AddressInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;

        string input = _addressBar.AddressText;
        if (AddressBarValidation.IsLoadableMarkdownUrl(input) &&
            Uri.TryCreate(input.Trim(), UriKind.Absolute, out Uri? pageUrl) && pageUrl is not null)
        {
            // A loadable .md URL navigates through the controller (push + endpoint-fetch + render).
            await _controller.NavigateToAsync(pageUrl);
        }
        else
        {
            // Non-.md input: keep the 3.2 decline UX (NotMarkdown + offer system browser), no fetch.
            await _addressBar.SubmitAsync();
        }
    }
}
