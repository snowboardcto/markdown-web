using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TheMarkdownWeb.App;

/// <summary>
/// The navigation history state machine (Story 3.5 AC4/AC6/AC8/AC10). Drives typed-address and
/// clicked-link navigation through ONE push-and-render path, so the toolbar Back/Forward/Reload and a
/// clicked internal link share the same behavior. Fully unit-testable with an injected fetch delegate
/// + render sink + broken sink + <see cref="IUrlLauncher"/> (no window, no socket, no process).
///
/// History is a <c>List&lt;Uri&gt;</c> + a cursor index. A new <see cref="NavigateToAsync"/> from
/// mid-history truncates the forward tail then appends (browser semantics; a same-URL navigate
/// re-pushes). Back/Forward/Reload at the ends are total no-ops. A failed/Unsupported navigate routes
/// to <c>onBroken</c> and leaves history untouched (no half-pushed broken entry).
///
/// Re-entrancy is LAST-WINS via a monotonic generation token: when an awaited fetch resumes it checks
/// its generation against the current one; a STALE completion is dropped (no render, no history
/// mutation), and the superseded fetch's <see cref="CancellationToken"/> is cancelled. Single-threaded
/// on the UI/STA thread. Never throws.
/// </summary>
public sealed class NavigationController
{
    private readonly Func<Uri, CancellationToken, Task<FetchResult>> _fetch;
    private readonly Action<string, Uri> _renderSink;
    private readonly Action _onBroken;
    private readonly IUrlLauncher _launcher;

    private readonly List<Uri> _entries = new();
    private int _cursor = -1;

    // Monotonic generation token guarding every async op (last-wins re-entrancy).
    private int _generation;
    private CancellationTokenSource? _inFlight;

    public NavigationController(
        Func<Uri, CancellationToken, Task<FetchResult>> fetch,
        Action<string, Uri> renderSink,
        Action onBroken,
        IUrlLauncher launcher)
    {
        _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
        _renderSink = renderSink ?? throw new ArgumentNullException(nameof(renderSink));
        _onBroken = onBroken ?? throw new ArgumentNullException(nameof(onBroken));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    /// <summary>The currently-displayed page, or <c>null</c> if nothing has loaded.</summary>
    public Uri? Current => _cursor >= 0 && _cursor < _entries.Count ? _entries[_cursor] : null;

    /// <summary><c>true</c> iff a Back move is possible (cursor &gt; 0).</summary>
    public bool CanGoBack => _cursor > 0;

    /// <summary><c>true</c> iff a Forward move is possible (cursor &lt; tip).</summary>
    public bool CanGoForward => _cursor >= 0 && _cursor < _entries.Count - 1;

    /// <summary>
    /// Push + fetch + render the page in place. On success truncates any forward tail, appends the
    /// page (same-URL re-pushes), and moves the cursor to the tip. On failure/cancellation leaves
    /// history untouched and signals Broken. Total — never throws.
    /// </summary>
    public Task NavigateToAsync(Uri pageUrl, CancellationToken ct = default)
        => RunAsync(pageUrl, NavKind.Navigate, ct);

    /// <summary>Move to the previous entry and re-fetch it. No-op at index 0.</summary>
    public Task GoBackAsync(CancellationToken ct = default)
    {
        if (!CanGoBack)
        {
            return Task.CompletedTask;
        }
        return RunAsync(_entries[_cursor - 1], NavKind.Back, ct);
    }

    /// <summary>Move to the next entry and re-fetch it. No-op at the tip.</summary>
    public Task GoForwardAsync(CancellationToken ct = default)
    {
        if (!CanGoForward)
        {
            return Task.CompletedTask;
        }
        return RunAsync(_entries[_cursor + 1], NavKind.Forward, ct);
    }

    /// <summary>Re-fetch the current entry in place (no push). No-op on empty history.</summary>
    public Task ReloadAsync(CancellationToken ct = default)
    {
        Uri? current = Current;
        if (current is null)
        {
            return Task.CompletedTask;
        }
        return RunAsync(current, NavKind.Reload, ct);
    }

    /// <summary>
    /// Routes a classified <see cref="LinkTarget"/> by kind: InternalMarkdown → navigate;
    /// External → launcher; Anchor → no fetch (the host scrolls); Unsupported → total no-op.
    /// </summary>
    public Task DispatchAsync(LinkTarget target, CancellationToken ct = default)
    {
        switch (target.Kind)
        {
            case LinkKind.InternalMarkdown when target.Url is not null:
                return NavigateToAsync(target.Url, ct);

            case LinkKind.External when target.Url is not null:
                _launcher.Open(target.Url);
                return Task.CompletedTask;

            case LinkKind.Anchor:
            case LinkKind.InternalMarkdown:
            case LinkKind.External:
            case LinkKind.Unsupported:
            default:
                // Anchor scroll is handled by the content host; Unsupported is a no-op.
                return Task.CompletedTask;
        }
    }

    private enum NavKind { Navigate, Back, Forward, Reload }

    private async Task RunAsync(Uri target, NavKind kind, CancellationToken ct)
    {
        // Supersede any in-flight navigation and claim this generation (last-wins).
        int myGen = ++_generation;
        _inFlight?.Cancel();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _inFlight = cts;

        // For Back/Forward, the destination cursor is determined now (the entry already exists).
        int targetCursor = kind switch
        {
            NavKind.Back => _cursor - 1,
            NavKind.Forward => _cursor + 1,
            _ => _cursor, // Reload/Navigate don't move via a precomputed cursor
        };

        FetchResult result;
        try
        {
            result = await _fetch(target, cts.Token).ConfigureAwait(true);
        }
        catch
        {
            // The fetch delegate is contractually total, but guard defensively — a throw is Broken.
            result = FetchResult.Failure("fetch failed");
        }

        // Drop a stale completion: a newer navigation has superseded this one.
        if (myGen != _generation)
        {
            return;
        }

        // This is the winning navigation — release the in-flight handle.
        _inFlight = null;

        if (result.IsSuccess)
        {
            switch (kind)
            {
                case NavKind.Navigate:
                    // Truncate the forward tail, append, move cursor to the new tip.
                    if (_cursor < _entries.Count - 1)
                    {
                        _entries.RemoveRange(_cursor + 1, _entries.Count - _cursor - 1);
                    }
                    _entries.Add(target);
                    _cursor = _entries.Count - 1;
                    break;

                case NavKind.Back:
                case NavKind.Forward:
                    _cursor = targetCursor;
                    break;

                case NavKind.Reload:
                    // cursor/entries unchanged
                    break;
            }

            _renderSink(result.Markdown ?? string.Empty, target);
        }
        else
        {
            // Failure: Back/Forward have legitimately moved to a real (but now-failing) entry — commit
            // the cursor move and show Broken for it. Navigate/Reload leave history untouched.
            if (kind is NavKind.Back or NavKind.Forward)
            {
                _cursor = targetCursor;
            }

            _onBroken();
        }
    }
}
