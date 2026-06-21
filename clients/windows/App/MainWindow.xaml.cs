using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TheMarkdownWeb.Agent;
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

    // The reader's local agent (BYO-key). 4.1 selects the constant Basic persona (pass-through); the 4.2
    // chip will replace the () => Persona.Basic selector. Composed once for the window's lifetime.
    private readonly ISecretStore _secretStore = new DpapiSecretStore();
    private readonly PersonalizationGateway _gateway;

    private readonly ShellViewModel _viewModel;
    private readonly AddressBarViewModel _addressBar;
    private readonly NavigationController _controller;
    private readonly ContentHostController _contentHost;

    public MainWindow()
    {
        InitializeComponent();

        // Compose the agent: AnthropicLlmClient (reader's key, TLS) -> PersonalityEngine -> gateway.
        var llmClient = new AnthropicLlmClient(SharedHttpClient, _secretStore);
        var engine = new PersonalityEngine(llmClient, _secretStore);
        _gateway = new PersonalizationGateway(engine, () => Persona.Basic);

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

    /// <summary>
    /// Fetches a page URL via the negotiate-endpoint mapping (AC9), then runs the fetched markdown through
    /// the personalization gateway (Story 4.1 AC2) so the agent's output rides on the returned
    /// <see cref="FetchResult.Markdown"/> into the controller's existing guarded render sink. This keeps
    /// <see cref="NavigationController"/>'s last-wins re-entrancy intact: the gateway call is awaited inside
    /// the same navigation generation and the controller re-checks its generation token after the awaited
    /// fetch (NavigationController.cs line 153), so a superseded navigation's resolved markdown is dropped.
    /// Never throws — the gateway is total, and a fetch failure flows through unchanged.
    /// </summary>
    private async Task<FetchResult> FetchEndpointAsync(Uri pageUrl, CancellationToken ct)
    {
        Uri endpoint = PageEndpointResolver.ToFetchEndpoint(pageUrl);
        FetchResult fetched = await _fetcher.FetchAsync(endpoint.ToString(), ct).ConfigureAwait(true);

        if (!fetched.IsSuccess)
        {
            return fetched;
        }

        string resolved = await _gateway
            .ResolveMarkdownAsync(fetched.Markdown ?? string.Empty, pageUrl, ct)
            .ConfigureAwait(true);

        return FetchResult.Success(resolved);
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
