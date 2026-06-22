using System;
using System.Threading;
using System.Threading.Tasks;
using TheMarkdownWeb.Agent;

namespace TheMarkdownWeb.App;

/// <summary>
/// The re-render-in-place path (Story 4.2 AC2 — the load-bearing AC — + AC5). Holds the current page's
/// RAW fetched markdown + <see cref="Uri"/>; a personality switch re-runs that HELD RAW markdown through
/// the <see cref="PersonalizationGateway"/> with the now-current persona and pushes the result to the
/// render-sink — with ZERO network GETs. No-re-fetch is true BY CONSTRUCTION: the only dependencies are
/// the gateway and the render-sink — there is NO fetcher/HttpClient/endpoint, so a switch CANNOT fetch.
/// Holding the RAW (pre-transform) markdown means Basic → Cozy → Basic returns the byte-identical
/// original (never a transform-of-a-transform).
///
/// Last-wins re-entrancy uses an OWN monotonic generation (INDEPENDENT of
/// <see cref="NavigationController"/> — Q-Token): <see cref="RerenderAsync"/> captures the generation
/// before the awaited resolve and drops its result if a newer action superseded it.
/// <see cref="SetCurrentPage"/> (called on every navigation render) ALSO bumps the generation, so a
/// fresh navigation invalidates any in-flight switch re-render (nav supersedes a pending re-render).
/// Total — never throws.
/// </summary>
public sealed class PersonalityRerenderCoordinator
{
    private readonly PersonalizationGateway _gateway;
    private readonly Action<string, Uri> _renderSink;

    private string? _heldRaw;
    private Uri? _heldUrl;
    private int _generation;

    public PersonalityRerenderCoordinator(PersonalizationGateway gateway, Action<string, Uri> renderSink)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _renderSink = renderSink ?? throw new ArgumentNullException(nameof(renderSink));
    }

    /// <summary>The outcome of the last completed re-render — passed through from the gateway (AC5).</summary>
    public PersonalizationOutcome LastOutcome { get; private set; } = PersonalizationOutcome.PassThrough;

    /// <summary>The last non-blocking, key-free notice — passed through from the gateway (AC5).</summary>
    public string? LastNotice { get; private set; }

    /// <summary>
    /// Stores the current page's RAW (pre-transform) markdown + <see cref="Uri"/> — called on every
    /// successful navigation render. ALSO bumps the generation so any in-flight re-render is invalidated
    /// (a fresh navigation supersedes a pending switch — the nav×switch precedence rule).
    /// </summary>
    public void SetCurrentPage(string rawMarkdown, Uri pageUrl)
    {
        _heldRaw = rawMarkdown ?? string.Empty;
        _heldUrl = pageUrl;
        _generation++;
    }

    /// <summary>
    /// Re-runs the HELD RAW markdown through the gateway with the now-current persona and pushes the
    /// result to the render-sink. No-op (no sink call, no throw) if nothing has been held yet. Last-wins:
    /// a superseded re-render's result is dropped (no sink call). NO re-fetch — there is no fetcher.
    /// </summary>
    public async Task RerenderAsync(CancellationToken ct = default)
    {
        string? raw = _heldRaw;
        Uri? url = _heldUrl;
        if (raw is null || url is null)
        {
            return; // nothing held yet — a safe no-op (no fetch, no crash).
        }

        int myGen = ++_generation;

        string resolved = await _gateway.ResolveMarkdownAsync(raw, url, ct).ConfigureAwait(true);

        // Last-wins: a newer switch or a navigation (SetCurrentPage) bumped the generation — drop this
        // stale result without rendering (it would otherwise overwrite the newer page/persona).
        if (myGen != _generation)
        {
            return;
        }

        LastOutcome = _gateway.LastOutcome;
        LastNotice = _gateway.LastNotice;
        _renderSink(resolved, url);
    }
}
