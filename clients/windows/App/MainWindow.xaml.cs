using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

    // The reader's local agent (BYO-key). The 4.2 selector drives the gateway: the persona Func is now
    // () => _selection.Current (replacing the 4.1 constant () => Persona.Basic). Composed once for the
    // window's lifetime.
    private readonly ISecretStore _secretStore = new DpapiSecretStore();
    private readonly PersonalizationGateway _gateway;

    private readonly ShellViewModel _viewModel;
    private readonly AddressBarViewModel _addressBar;
    private readonly NavigationController _controller;
    private readonly ContentHostController _contentHost;

    // Story 4.2: the personality selection state + the re-render-in-place coordinator (held RAW markdown,
    // no re-fetch, last-wins). A selection change re-runs the held markdown through the gateway.
    private readonly PersonalitySelectionViewModel _selection = new();
    private readonly PersonalityRerenderCoordinator _rerender;

    // Story 4.4: the target-language selection state (sourced into the gateway's ReaderContext for the
    // Translate persona), the SAPI speech seam for the Audio persona, and the testable audio read-aloud
    // route. _heldRaw mirrors the coordinator's held RAW markdown so the audio route can speak the current
    // page without a re-fetch (and without touching the render-sink — the visible page stays as-is).
    private readonly LanguageSelectionViewModel _languageSelection = new();
    private readonly ISpeechSynthesizer _speech = new SapiSpeechSynthesizer();
    private readonly AudioReadAloudController _audio;
    private string? _heldRaw;

    // The Translate persona's language picker offers these common languages plus a free-text fallback
    // (IsEditable). The chosen string is passed VERBATIM through to the engine's target-language directive.
    private static readonly string[] CommonLanguages =
    {
        "Spanish", "French", "German", "Italian", "Portuguese",
        "Japanese", "Chinese", "Korean", "Arabic", "Hindi",
    };

    // Suppresses the ComboBox.SelectionChanged that fires while wiring the initial selection in the ctor
    // (we must not trigger a re-render before any page has loaded).
    private bool _suppressSelectionChanged;

    public MainWindow()
    {
        InitializeComponent();

        // Compose the agent: AnthropicLlmClient (reader's key, TLS) -> PersonalityEngine -> gateway. The
        // 4.2 selector drives the persona: () => _selection.Current (the gateway reads it per resolve).
        var llmClient = new AnthropicLlmClient(SharedHttpClient, _secretStore);
        var engine = new PersonalityEngine(llmClient, _secretStore);
        // Story 4.4: the gateway also sources the reader's chosen target language (Q-Lang-Source) — the
        // engine appends it as a directive ONLY for the Translate persona; every other persona ignores it.
        _gateway = new PersonalizationGateway(
            engine, () => _selection.Current, () => _languageSelection.Current);

        // Story 4.4: the Audio persona's read-aloud route over the SAPI speech seam (offline, no key).
        _audio = new AudioReadAloudController(_speech);

        _addressBar = new AddressBarViewModel(_fetcher, _launcher);

        // The content host renders into the named read-only scroll viewer and attaches the single
        // host-level hyperlink handler that dispatches classified clicks through the controller.
        _contentHost = new ContentHostController(
            ContentScroll,
            new FlowDocumentRenderer(),
            new SystemImageLoader(),
            DispatchLinkAsync);

        // The re-render-in-place coordinator shares the SAME render-sink the controller uses, so a
        // personality switch re-renders the HELD raw markdown into the SAME host (no re-fetch).
        _rerender = new PersonalityRerenderCoordinator(
            _gateway,
            (markdown, pageUrl) => _contentHost.ShowMarkdown(markdown, pageUrl));

        // The controller fetches the /api/negotiate/<slug> endpoint (AC9) and renders into the host.
        _controller = new NavigationController(
            FetchEndpointAsync,
            (markdown, pageUrl) => _contentHost.ShowMarkdown(markdown, pageUrl),
            () => _contentHost.ShowBroken(),
            _launcher);

        _viewModel = new ShellViewModel(_controller);
        DataContext = _viewModel;
        AddressBar.DataContext = _addressBar;

        // Wire the personality selector to the selection state (default = Basic; first run is the
        // faithful basic render). ItemsSource = the seed registry; SelectedItem = the current selection.
        _suppressSelectionChanged = true;
        PersonalitySelector.ItemsSource = _selection.Options;
        PersonalitySelector.SelectedItem = _selection.Current;
        // Story 4.4: seed the language picker with common languages (free-text fallback via IsEditable).
        // It stays collapsed until the Translate persona is selected.
        LanguagePicker.ItemsSource = CommonLanguages;
        _suppressSelectionChanged = false;
    }

    /// <summary>
    /// The toolbar selector changed: drive the selection state, then re-render the current page IN PLACE
    /// using the held RAW markdown (Story 4.2 AC2/AC3) — NO re-fetch. On <c>NeedsKey</c> surface the
    /// key-entry dialog and re-render on save; on Cancel/blank keep the chosen persona + the held
    /// original rendered (Q-Revert). Total — never throws into the UI.
    /// </summary>
    private async void PersonalitySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChanged)
        {
            return;
        }

        if (PersonalitySelector.SelectedItem is not Persona picked)
        {
            return;
        }

        _selection.Select(picked);

        // Story 4.4: the language picker is only relevant for the Translate persona — show it for Translate,
        // collapse it otherwise so it does not clutter the toolbar for the other personas.
        LanguagePicker.Visibility = string.Equals(picked.Id, "translate", StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Story 4.4 (Q-Audio-Trigger): the Audio persona routes to the SPEECH path BEFORE the gateway/
        // coordinator — speak the held RAW markdown in reading order and leave the visible document EXACTLY
        // as-is (the route makes ZERO render-sink/gateway/LLM calls; audio is offline + key-free). Return
        // before the re-render so the page is never blanked/replaced.
        if (string.Equals(picked.Id, "audio", StringComparison.Ordinal))
        {
            await _audio.ReadAsync(_heldRaw).ConfigureAwait(true);
            return;
        }

        await _rerender.RerenderAsync().ConfigureAwait(true);

        if (_rerender.LastOutcome == PersonalizationOutcome.NeedsKey)
        {
            PromptForApiKeyAndRerender();
        }
    }

    /// <summary>
    /// The target-language picker selection changed (Story 4.4 AC2): set the language-selection state and
    /// re-render the current page IN PLACE via the coordinator (held RAW, NO re-fetch). Total — never throws.
    /// </summary>
    private async void LanguagePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChanged)
        {
            return;
        }

        await ApplyLanguageAndRerenderAsync(LanguagePicker.SelectedItem as string ?? LanguagePicker.Text)
            .ConfigureAwait(true);
    }

    /// <summary>Committing a free-text language with Enter re-renders in place (Story 4.4 AC2).</summary>
    private async void LanguagePicker_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await ApplyLanguageAndRerenderAsync(LanguagePicker.Text).ConfigureAwait(true);
    }

    private async Task ApplyLanguageAndRerenderAsync(string? language)
    {
        _languageSelection.Select(language);
        await _rerender.RerenderAsync().ConfigureAwait(true);

        if (_rerender.LastOutcome == PersonalizationOutcome.NeedsKey)
        {
            PromptForApiKeyAndRerender();
        }
    }

    /// <summary>
    /// Surfaces the BYO-key entry dialog (AC4). On a successful save the page re-renders with the new key;
    /// on Cancel/blank the chosen persona is KEPT and the held original stays rendered (Q-Revert).
    /// </summary>
    private void PromptForApiKeyAndRerender()
    {
        var dialog = new ApiKeyEntryDialog(new ApiKeyPromptViewModel(_secretStore)) { Owner = this };
        bool? result = dialog.ShowDialog();
        if (result == true && dialog.Saved)
        {
            _ = _rerender.RerenderAsync();
        }
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

        // Story 4.2: hold the RAW (pre-transform) markdown + url so a later personality switch can
        // re-personalize FROM SOURCE without re-fetching (and Basic->Cozy->Basic stays byte-identical).
        // SetCurrentPage also bumps the coordinator's generation, invalidating any in-flight re-render
        // (a fresh navigation supersedes a pending switch).
        string raw = fetched.Markdown ?? string.Empty;
        _rerender.SetCurrentPage(raw, pageUrl);

        // Story 4.4: mirror the held RAW markdown for the audio read-aloud route, and stop any in-progress
        // speech on navigation so a previous page's read-aloud does not bleed into the new page.
        _heldRaw = raw;
        _audio.Stop();

        string resolved = await _gateway
            .ResolveMarkdownAsync(raw, pageUrl, ct)
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
