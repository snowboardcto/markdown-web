using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC4 / AC6 / AC8 / AC10 — the <c>NavigationController</c> history state machine, fully
/// <c>[Fact]</c>-testable with a FAKE fetch delegate + a recording render sink + a broken sink + a
/// fake <c>IUrlLauncher</c> (NO window, NO socket, NO process). Pins the story's
/// state-transition table:
///   • Navigate(success) → truncate forward tail, append, cursor=tip, render, CanGoBack/Forward;
///   • Navigate(failure/unsupported) → history UNCHANGED, onBroken only (no half-pushed entry);
///   • same-URL Navigate → RE-PUSHES a new entry (browser semantics), Reload does NOT push;
///   • Back/Forward → re-fetch+render; no-op at index 0 / at the tip;
///   • Reload → re-fetch Current in place (no push); no-op on empty history;
///   • Dispatch(External) → launcher.Open, no fetch/history; Dispatch(Anchor) → no fetch;
///   • Dispatch(Unsupported) → total no-op;
///   • Re-entrancy → last-wins via a generation token (a stale completion is dropped — single
///     render, no history corruption).
///
/// INTENDED API CONTRACT (Step-5 implementer must match; RED until it exists):
///
///   namespace TheMarkdownWeb.App;
///   public sealed class NavigationController
///   {
///       public NavigationController(
///           Func&lt;Uri, CancellationToken, Task&lt;FetchResult&gt;&gt; fetch, // default: MarkdownFetcher ∘ PageEndpointResolver
///           Action&lt;string, Uri&gt; renderSink,                        // (markdown, pageUrl) -> render
///           Action onBroken,                                          // show Broken
///           IUrlLauncher launcher);                                   // external dispatch
///       public Uri? Current { get; }
///       public bool CanGoBack { get; }
///       public bool CanGoForward { get; }
///       public Task NavigateToAsync(Uri pageUrl, CancellationToken ct = default);
///       public Task GoBackAsync(CancellationToken ct = default);
///       public Task GoForwardAsync(CancellationToken ct = default);
///       public Task ReloadAsync(CancellationToken ct = default);
///       public Task DispatchAsync(LinkTarget target, CancellationToken ct = default);
///   }
///
/// All [Fact] — pure state machine with injected fakes; no window, no network.
/// </summary>
public class NavigationControllerTests
{
    private static readonly Uri A = new("https://themarkdownweb.com/a.md");
    private static readonly Uri B = new("https://themarkdownweb.com/b.md");
    private static readonly Uri C = new("https://themarkdownweb.com/c.md");
    private static readonly Uri D = new("https://themarkdownweb.com/d.md");

    // ---- Test doubles -------------------------------------------------------------------------

    /// <summary>
    /// Fake fetch delegate: records every requested page Uri, returns a canned result per Uri
    /// (default Success), and optionally GATES the first call so re-entrancy can be exercised.
    /// </summary>
    private sealed class FakeFetcher
    {
        private readonly Dictionary<Uri, FetchResult> _canned = new();
        private FetchResult _default = FetchResult.Success("# default");

        public List<Uri> Requested { get; } = new();
        public TaskCompletionSource<bool>? Gate { get; private set; }
        public Uri? GateOn { get; set; }

        public FakeFetcher Returns(Uri url, FetchResult result) { _canned[url] = result; return this; }
        public FakeFetcher ReturnsByDefault(FetchResult result) { _default = result; return this; }

        public TaskCompletionSource<bool> GateFirstCallTo(Uri url)
        {
            GateOn = url;
            Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            return Gate;
        }

        public async Task<FetchResult> FetchAsync(Uri url, CancellationToken ct)
        {
            Requested.Add(url);
            if (Gate is not null && GateOn is not null && url == GateOn)
            {
                var g = Gate;
                Gate = null; // gate only the first matching call
                await g.Task.ConfigureAwait(false);
            }
            return _canned.TryGetValue(url, out FetchResult r) ? r : _default;
        }
    }

    /// <summary>Render sink that records every (markdown, pageUrl) rendered.</summary>
    private sealed class RecordingSink
    {
        public List<(string Markdown, Uri Url)> Rendered { get; } = new();
        public int BrokenCount { get; private set; }

        public void Render(string markdown, Uri url) => Rendered.Add((markdown, url));
        public void Broken() => BrokenCount++;

        public Uri? LastUrl => Rendered.Count == 0 ? null : Rendered[^1].Url;
    }

    /// <summary>Fake launcher: records the launched Uri, spawns no process.</summary>
    private sealed class FakeLauncher : IUrlLauncher
    {
        public List<Uri> Opened { get; } = new();
        public void Open(Uri url) => Opened.Add(url);
    }

    private static (NavigationController controller, FakeFetcher fetch, RecordingSink sink, FakeLauncher launcher)
        Build(FakeFetcher? fetcher = null)
    {
        var f = fetcher ?? new FakeFetcher();
        var sink = new RecordingSink();
        var launcher = new FakeLauncher();
        var controller = new NavigationController(f.FetchAsync, sink.Render, sink.Broken, launcher);
        return (controller, f, sink, launcher);
    }

    // ---- AC4 — Navigate success: push + render + history ------------------------------------

    [Fact]
    public async Task NavigateToAsync_Success_SetsCurrent_Renders_AndEnablesBack()
    {
        var (controller, fetch, sink, _) = Build(new FakeFetcher().Returns(A, FetchResult.Success("# A")));

        await controller.NavigateToAsync(A);

        Assert.Equal(A, controller.Current);
        Assert.Contains(A, fetch.Requested);                  // the controller fetched the navigated page
        Assert.Single(sink.Rendered);
        Assert.Equal("# A", sink.Rendered[0].Markdown);
        Assert.Equal(A, sink.Rendered[0].Url);
        Assert.False(controller.CanGoBack);                   // one entry, cursor at 0
        Assert.False(controller.CanGoForward);
    }

    [Fact]
    public async Task NavigateToAsync_ThreePages_BuildsHistory()
    {
        var (controller, _, _, _) = Build();

        await controller.NavigateToAsync(A);
        await controller.NavigateToAsync(B);
        await controller.NavigateToAsync(C);

        Assert.Equal(C, controller.Current);
        Assert.True(controller.CanGoBack);
        Assert.False(controller.CanGoForward);
    }

    // ---- AC10 — Back / Forward ----------------------------------------------------------------

    [Fact]
    public async Task GoBack_ThenForward_MovesThroughHistory_AndReFetches()
    {
        var (controller, fetch, sink, _) = Build();
        await controller.NavigateToAsync(A);
        await controller.NavigateToAsync(B);
        await controller.NavigateToAsync(C);

        await controller.GoBackAsync();
        Assert.Equal(B, controller.Current);
        Assert.True(controller.CanGoForward);
        Assert.Equal(B, sink.LastUrl);                        // re-rendered B

        await controller.GoForwardAsync();
        Assert.Equal(C, controller.Current);
        Assert.False(controller.CanGoForward);
        Assert.Contains(C, fetch.Requested);                  // re-fetched on forward
    }

    [Fact]
    public async Task GoBack_AtIndexZero_IsNoOp()
    {
        var (controller, fetch, sink, _) = Build();
        await controller.NavigateToAsync(A);
        int fetchesBefore = fetch.Requested.Count;
        int rendersBefore = sink.Rendered.Count;

        await controller.GoBackAsync(); // cursor==0 -> no-op

        Assert.Equal(A, controller.Current);
        Assert.Equal(fetchesBefore, fetch.Requested.Count);   // no fetch
        Assert.Equal(rendersBefore, sink.Rendered.Count);     // no render
    }

    [Fact]
    public async Task GoForward_AtTip_IsNoOp()
    {
        var (controller, fetch, _, _) = Build();
        await controller.NavigateToAsync(A);
        int fetchesBefore = fetch.Requested.Count;

        await controller.GoForwardAsync(); // at tip -> no-op

        Assert.Equal(A, controller.Current);
        Assert.Equal(fetchesBefore, fetch.Requested.Count);
    }

    // ---- AC10 — forward-tail truncation + same-URL re-push -----------------------------------

    [Fact]
    public async Task NavigateFromMidHistory_TruncatesForwardTail()
    {
        var (controller, _, _, _) = Build();
        await controller.NavigateToAsync(A);
        await controller.NavigateToAsync(B);
        await controller.NavigateToAsync(C);
        await controller.GoBackAsync();                       // Current == B, C is ahead

        await controller.NavigateToAsync(D);                  // truncates C, appends D

        Assert.Equal(D, controller.Current);
        Assert.False(controller.CanGoForward);                // C was truncated
        Assert.True(controller.CanGoBack);
    }

    [Fact]
    public async Task SameUrlNavigate_RePushesNewEntry()
    {
        var (controller, _, _, _) = Build();
        await controller.NavigateToAsync(A);
        await controller.NavigateToAsync(B);                  // Current == B, cursor==1

        await controller.NavigateToAsync(B);                  // same URL -> re-push (browser semantics)

        Assert.Equal(B, controller.Current);
        Assert.True(controller.CanGoBack);                    // a Back now lands on the earlier B
        Assert.False(controller.CanGoForward);                // re-push truncates any forward tail
        // Two Backs return to A (proving B was genuinely pushed twice, not collapsed).
        await controller.GoBackAsync();
        Assert.Equal(B, controller.Current);
        await controller.GoBackAsync();
        Assert.Equal(A, controller.Current);
    }

    // ---- AC10 — Reload ------------------------------------------------------------------------

    [Fact]
    public async Task Reload_ReFetchesCurrent_WithoutGrowingHistory()
    {
        var (controller, fetch, sink, _) = Build();
        await controller.NavigateToAsync(A);
        await controller.NavigateToAsync(B);
        int requestsBefore = fetch.Requested.Count;

        await controller.ReloadAsync();

        Assert.Equal(B, controller.Current);                  // unchanged
        Assert.Equal(requestsBefore + 1, fetch.Requested.Count); // re-fetched once
        Assert.Equal(B, fetch.Requested[^1]);
        Assert.Equal(B, sink.LastUrl);                        // re-rendered B
        Assert.True(controller.CanGoBack);                    // history not grown: still [A, B]
        Assert.False(controller.CanGoForward);
    }

    [Fact]
    public async Task Reload_OnEmptyHistory_IsNoOp()
    {
        var (controller, fetch, sink, _) = Build();

        await controller.ReloadAsync(); // nothing loaded -> no-op

        Assert.Null(controller.Current);
        Assert.Empty(fetch.Requested);
        Assert.Empty(sink.Rendered);
    }

    // ---- AC8 — failed navigation: Broken, history untouched ----------------------------------

    [Fact]
    public async Task NavigateToAsync_Failure_RoutesToBroken_AndLeavesHistoryUnchanged()
    {
        var fetcher = new FakeFetcher().Returns(B, FetchResult.Failure("HTTP 404"));
        var (controller, _, sink, _) = Build(fetcher);
        await controller.NavigateToAsync(A);                  // a real, rendered page
        int rendersBefore = sink.Rendered.Count;

        await controller.NavigateToAsync(B);                  // fails

        Assert.Equal(A, controller.Current);                  // NOT corrupted — Current stays A
        Assert.False(controller.CanGoForward);
        Assert.Equal(1, sink.BrokenCount);                    // Broken signalled
        Assert.Equal(rendersBefore, sink.Rendered.Count);     // no new render
    }

    [Fact]
    public async Task FailedNavigate_DoesNotThrow_AndBackStillReturnsToRealPage()
    {
        var fetcher = new FakeFetcher().Returns(B, FetchResult.Failure("network error"));
        var (controller, _, sink, _) = Build(fetcher);
        await controller.NavigateToAsync(A);

        await controller.NavigateToAsync(B); // fails — no half-pushed broken entry
        await controller.GoBackAsync();      // index 0 -> no-op, Current is still the real A

        Assert.Equal(A, controller.Current);
    }

    // ---- AC6 — Dispatch(External) -> launcher, no fetch --------------------------------------

    [Fact]
    public async Task DispatchAsync_External_OpensLauncher_NoFetch_NoHistory()
    {
        var (controller, fetch, sink, launcher) = Build();
        var ext = new Uri("https://example.com/x");

        await controller.DispatchAsync(LinkTarget.ExternalTo(ext));

        Assert.Single(launcher.Opened);
        Assert.Equal(ext, launcher.Opened[0]);
        Assert.Empty(fetch.Requested);                        // NOT fetched in-client
        Assert.Empty(sink.Rendered);
        Assert.Null(controller.Current);                      // history untouched
    }

    [Fact]
    public async Task DispatchAsync_InternalMarkdown_RoutesThroughNavigate()
    {
        var (controller, fetch, sink, _) = Build();

        await controller.DispatchAsync(LinkTarget.Internal(A));

        Assert.Equal(A, controller.Current);
        Assert.Contains(A, fetch.Requested);
        Assert.Single(sink.Rendered);
    }

    // ---- AC8 — Dispatch(Unsupported) is a total no-op ----------------------------------------

    [Fact]
    public async Task DispatchAsync_Unsupported_IsNoOp()
    {
        var (controller, fetch, sink, launcher) = Build();

        await controller.DispatchAsync(LinkTarget.Unsupported);

        Assert.Empty(fetch.Requested);
        Assert.Empty(sink.Rendered);
        Assert.Empty(launcher.Opened);
        Assert.Null(controller.Current);
    }

    [Fact]
    public async Task DispatchAsync_Anchor_DoesNotFetchOrLaunch()
    {
        var (controller, fetch, _, launcher) = Build();

        await controller.DispatchAsync(LinkTarget.AnchorTo("install"));

        Assert.Empty(fetch.Requested);   // anchor scroll never re-fetches
        Assert.Empty(launcher.Opened);
    }

    // ---- AC10 — re-entrancy / last-wins (generation token) -----------------------------------

    [Fact]
    public async Task RapidNavigation_LastWins_SingleRender_NoHistoryCorruption()
    {
        // Nav(A) blocks on a gate; Nav(B) runs to completion; then A is released LATE.
        var fetcher = new FakeFetcher()
            .Returns(A, FetchResult.Success("# A"))
            .Returns(B, FetchResult.Success("# B"));
        TaskCompletionSource<bool> gateA = fetcher.GateFirstCallTo(A);
        var (controller, _, sink, _) = Build(fetcher);

        Task navA = controller.NavigateToAsync(A); // awaits the gate inside the fetch
        await controller.NavigateToAsync(B);       // supersedes A; completes and renders B

        gateA.SetResult(true);                     // release the stale A fetch LATE
        await navA;                                // its completion must be DROPPED

        Assert.Equal(B, controller.Current);       // last submit wins
        Assert.Equal(B, sink.LastUrl);             // the sink saw B last (A's late render dropped)
        Assert.DoesNotContain(sink.Rendered, r => r.Url == A); // A never rendered (stale dropped)
        Assert.False(controller.CanGoForward);     // single tip — no corruption
    }
}
