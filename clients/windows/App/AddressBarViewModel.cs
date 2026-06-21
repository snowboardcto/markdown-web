using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace TheMarkdownWeb.App;

/// <summary>
/// The observable state of the address bar / load flow (Story 3-2 AC6).
/// </summary>
public enum AddressBarState
{
    /// <summary>No load attempted yet (fresh VM).</summary>
    Idle,

    /// <summary>A fetch is in flight.</summary>
    Loading,

    /// <summary>Markdown was fetched and is held.</summary>
    Loaded,

    /// <summary>The input was declined because it is not a loadable <c>.md</c> URL.</summary>
    NotMarkdown,

    /// <summary>The load could not complete (page not found / not a markdown page). Never a crash.</summary>
    Broken,
}

/// <summary>
/// View-model behind the address bar (Story 3-2 AC2/AC3/AC4/AC6): validate the typed URL, then either
/// decline a non-<c>.md</c> input (offering an open-in-system-browser action) or fetch the raw markdown.
/// Exposes an observable <see cref="State"/> machine. Networking + URL-launch live in <c>App</c>; this VM
/// holds the fetched markdown as a raw string (NOT rendered — that is Story 3-3).
/// </summary>
public sealed class AddressBarViewModel : INotifyPropertyChanged
{
    private readonly MarkdownFetcher _fetcher;
    private readonly IUrlLauncher _launcher;

    private string _addressText = string.Empty;
    private AddressBarState _state = AddressBarState.Idle;
    private string? _lastFetchedMarkdown;
    private string? _declinedUrl;

    // Re-entrancy guard: each submit takes the next generation; only the latest submit may write a
    // terminal state, so a stale in-flight completion is ignored (last submit wins, no double-write).
    private int _submitGeneration;

    /// <summary>
    /// Constructs the VM over the injectable <see cref="MarkdownFetcher"/> and <see cref="IUrlLauncher"/>
    /// so tests can pass a stub fetcher and a fake launcher.
    /// </summary>
    public AddressBarViewModel(MarkdownFetcher fetcher, IUrlLauncher launcher)
    {
        _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The typed URL shown/edited in the address bar.</summary>
    public string AddressText
    {
        get => _addressText;
        set
        {
            if (_addressText != value)
            {
                _addressText = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>The observable load state (Story 3-2 AC6).</summary>
    public AddressBarState State
    {
        get => _state;
        private set
        {
            if (_state != value)
            {
                _state = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>The raw markdown most recently fetched (held, not rendered at this story).</summary>
    public string? LastFetchedMarkdown
    {
        get => _lastFetchedMarkdown;
        private set
        {
            if (_lastFetchedMarkdown != value)
            {
                _lastFetchedMarkdown = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The non-<c>.md</c> URL offered for open-in-system-browser; non-<c>null</c> only when the declined
    /// input parses as an absolute http(s) Uri (Story 3-2 AC3).
    /// </summary>
    public string? DeclinedUrl
    {
        get => _declinedUrl;
        private set
        {
            if (_declinedUrl != value)
            {
                _declinedUrl = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Validates <see cref="AddressText"/>; a non-<c>.md</c> input is declined (<see cref="State"/> =
    /// <see cref="AddressBarState.NotMarkdown"/>, no fetch); a loadable <c>.md</c> URL is fetched
    /// (<see cref="AddressBarState.Loading"/> then <see cref="AddressBarState.Loaded"/> or
    /// <see cref="AddressBarState.Broken"/>). Never throws. Re-entrancy: the last submit wins.
    /// </summary>
    public async Task SubmitAsync(CancellationToken ct = default)
    {
        string input = AddressText;

        // Claim this submit's generation BEFORE any await, so re-entrancy is ordered deterministically.
        int generation = Interlocked.Increment(ref _submitGeneration);

        if (!AddressBarValidation.IsLoadableMarkdownUrl(input))
        {
            // Decline. Offer an open-in-browser action ONLY for an absolute http(s) Uri.
            DeclinedUrl = TryGetLaunchableHttpUrl(input, out string canonical) ? canonical : null;
            State = AddressBarState.NotMarkdown;
            return;
        }

        // Loadable — clear any prior decline and enter Loading SYNCHRONOUSLY before the first await.
        DeclinedUrl = null;
        State = AddressBarState.Loading;

        FetchResult result;
        try
        {
            // No ConfigureAwait(false): resume on the captured UI SynchronizationContext so the
            // post-await PropertyChanged (State/LastFetchedMarkdown) fires on the UI thread for
            // bindings (Story 3.3 surfaces State visually). MarkdownFetcher keeps ConfigureAwait(false).
            result = await _fetcher.FetchAsync(input, ct);
        }
        catch (Exception ex)
        {
            // FetchAsync is contracted not to throw; this is a belt-and-suspenders guard (AC6 no-crash).
            result = FetchResult.Failure(ex.Message);
        }

        // Ignore a stale completion: a newer submit has superseded this one (last submit wins).
        if (generation != Volatile.Read(ref _submitGeneration))
        {
            return;
        }

        if (result.IsSuccess)
        {
            LastFetchedMarkdown = result.Markdown;
            State = AddressBarState.Loaded;
        }
        else
        {
            State = AddressBarState.Broken;
        }
    }

    /// <summary>
    /// Opens <see cref="DeclinedUrl"/> in the system browser via the injected launcher; no-op (no crash)
    /// when there is no launchable declined URL (Story 3-2 AC3).
    /// </summary>
    public void OpenDeclinedInBrowser()
    {
        string? declined = DeclinedUrl;
        if (string.IsNullOrEmpty(declined))
        {
            return;
        }

        if (TryGetLaunchableHttpUrl(declined, out _) &&
            Uri.TryCreate(declined, UriKind.Absolute, out Uri? uri) && uri is not null)
        {
            _launcher.Open(uri);
        }
    }

    /// <summary>
    /// True iff <paramref name="input"/> parses as an absolute http(s) Uri. Outputs the original input as
    /// the canonical declined-URL string (the exact text the reader typed, so the offer matches it).
    /// </summary>
    private static bool TryGetLaunchableHttpUrl(string? input, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string trimmed = input.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri) || uri is null)
        {
            return false;
        }

        bool isHttp =
            string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        if (!isHttp)
        {
            return false;
        }

        canonical = trimmed;
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
