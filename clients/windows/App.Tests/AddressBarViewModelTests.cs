using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC3 (decline + open-in-browser) + AC6 (state machine) — drives <c>AddressBarViewModel</c> with
/// a fake <c>IUrlLauncher</c> (records the launched Uri, no Process.Start) and a real
/// <c>MarkdownFetcher</c> built on a stub <see cref="HttpMessageHandler"/> (no socket). NO window.
///
/// INTENDED API CONTRACT (Step-5 implementer must match; RED until it exists):
///
///   namespace TheMarkdownWeb.App;
///   public enum AddressBarState { Idle, Loading, Loaded, NotMarkdown, Broken }
///   public interface IUrlLauncher { void Open(Uri url); }
///   public sealed class AddressBarViewModel : System.ComponentModel.INotifyPropertyChanged
///   {
///       public AddressBarViewModel(MarkdownFetcher fetcher, IUrlLauncher launcher);
///       public string AddressText { get; set; }
///       public AddressBarState State { get; }
///       public string? LastFetchedMarkdown { get; }
///       public string? DeclinedUrl { get; }
///       public Task SubmitAsync(CancellationToken ct = default);
///       public void OpenDeclinedInBrowser();
///   }
/// </summary>
public class AddressBarViewModelTests
{
    // ---------------------------------------------------------------- AC6: Idle on construction.
    [Fact]
    public void NewViewModel_State_DefaultsToIdle()
    {
        AddressBarViewModel vm = NewViewModel(out _, out _);

        Assert.Equal(AddressBarState.Idle, vm.State);
    }

    // ---------------------------------------------------------------- AC6: success path.
    [Fact]
    public async Task SubmitAsync_ValidMarkdownUrl_Success_GoesToLoaded_AndHoldsMarkdown()
    {
        AddressBarViewModel vm = NewViewModel(
            out _,
            out _,
            handler: ConstantHandler(Ok("# Hello")));
        vm.AddressText = "https://h/x.md";

        await vm.SubmitAsync();

        Assert.Equal(AddressBarState.Loaded, vm.State);
        Assert.Equal("# Hello", vm.LastFetchedMarkdown);
    }

    // ---------------------------------------------------------------- AC6: failure taxonomy -> Broken.
    public static IEnumerable<object[]> FailureCases()
    {
        yield return new object[] { "404", ConstantHandler(Status(HttpStatusCode.NotFound)) };
        yield return new object[] { "500", ConstantHandler(Status(HttpStatusCode.InternalServerError)) };
        yield return new object[] { "text/html 200", ConstantHandler(OkWith("<html>", "text/html")) };
        yield return new object[] { "empty body", ConstantHandler(Ok(string.Empty)) };
        yield return new object[] { "HttpRequestException", ThrowingHandler() };
    }

    [Theory]
    [MemberData(nameof(FailureCases))]
    public async Task SubmitAsync_ValidMarkdownUrl_Failure_GoesToBroken_WithoutCrashing(
        string label, HttpMessageHandler handler)
    {
        AddressBarViewModel vm = NewViewModel(out _, out _, handler: handler);
        vm.AddressText = "https://h/x.md";

        var ex = await Record.ExceptionAsync(() => vm.SubmitAsync());

        Assert.Null(ex); // AC6: no unhandled exception escapes SubmitAsync ($"case: {label}").
        Assert.Equal(AddressBarState.Broken, vm.State);
        _ = label;
    }

    // ---------------------------------------------------------------- AC3: non-.md decline, no fetch.
    [Fact]
    public async Task SubmitAsync_NonMarkdownHttpUrl_Declines_NoFetch_SetsDeclinedUrl()
    {
        var countingHandler = new CountingHandler(Ok("# Hello"));
        AddressBarViewModel vm = NewViewModel(out _, out _, handler: countingHandler);
        vm.AddressText = "https://example.com/about";

        await vm.SubmitAsync();

        Assert.Equal(AddressBarState.NotMarkdown, vm.State);
        Assert.Equal(0, countingHandler.SendCount); // AC3: ZERO HTTP requests for a declined input.
        Assert.Equal("https://example.com/about", vm.DeclinedUrl);
    }

    [Fact] // AC3 — OpenDeclinedInBrowser launches EXACTLY the declined absolute http(s) Uri.
    public async Task OpenDeclinedInBrowser_Launches_TheDeclinedAbsoluteHttpUrl()
    {
        AddressBarViewModel vm = NewViewModel(out _, out FakeLauncher launcher);
        vm.AddressText = "https://example.com/about";
        await vm.SubmitAsync();

        vm.OpenDeclinedInBrowser();

        Assert.Equal(1, launcher.OpenCount);
        Assert.NotNull(launcher.LastOpened);
        Assert.Equal("https://example.com/about", launcher.LastOpened!.ToString().TrimEnd('/'));
    }

    [Theory] // AC3 — non-URL / non-http scheme: DeclinedUrl == null and OpenDeclinedInBrowser no-ops.
    [InlineData("not a url")]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:x@y.z")]
    [InlineData("file:///c:/x.md")]
    public async Task SubmitAsync_NonHttpDecline_LeavesDeclinedUrlNull_AndOpenIsNoOp(string input)
    {
        AddressBarViewModel vm = NewViewModel(out _, out FakeLauncher launcher);
        vm.AddressText = input;

        await vm.SubmitAsync();
        var ex = Record.Exception(() => vm.OpenDeclinedInBrowser());

        Assert.Equal(AddressBarState.NotMarkdown, vm.State);
        Assert.Null(vm.DeclinedUrl);
        Assert.Null(ex); // no crash
        Assert.Equal(0, launcher.OpenCount); // no launch offered for a non-http(s) input.
    }

    // ---------------------------------------------------------------- AC6: re-entrancy, last wins.
    [Fact]
    public async Task SubmitAsync_ReEntrant_WhileLoading_LastSubmitWins_SingleTerminalState()
    {
        // First submit blocks on a gate; the second submit (different body) supersedes it.
        var gated = new GatedHandler();
        AddressBarViewModel vm = NewViewModel(out _, out _, handler: gated);

        vm.AddressText = "https://h/first.md";
        Task first = vm.SubmitAsync();

        // VM should be Loading while the first fetch is in flight.
        Assert.Equal(AddressBarState.Loading, vm.State);

        vm.AddressText = "https://h/second.md";
        Task second = vm.SubmitAsync();

        // Release both responses; the stale (first) completion must NOT clobber the latest terminal state.
        gated.ReleaseAll(Ok("# Second"));

        await Task.WhenAll(first, second);

        // A single, consistent terminal state — the last submit wins, no double-write/crash.
        Assert.Equal(AddressBarState.Loaded, vm.State);
        Assert.Equal("# Second", vm.LastFetchedMarkdown);
    }

    // ---------------------------------------------------------------- AC1/AC6: INotifyPropertyChanged.
    [Fact]
    public async Task SubmitAsync_RaisesPropertyChanged_ForState()
    {
        AddressBarViewModel vm = NewViewModel(out _, out _, handler: ConstantHandler(Ok("# Hello")));
        var changed = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.AddressText = "https://h/x.md";
        await vm.SubmitAsync();

        Assert.Contains(nameof(AddressBarViewModel.State), changed);
    }

    [Fact]
    public void SettingAddressText_RaisesPropertyChanged_ForAddressText()
    {
        AddressBarViewModel vm = NewViewModel(out _, out _);
        var changed = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.AddressText = "https://h/x.md";

        Assert.Contains(nameof(AddressBarViewModel.AddressText), changed);
    }

    // ============================================================ helpers / fakes ============

    private static AddressBarViewModel NewViewModel(
        out MarkdownFetcher fetcher,
        out FakeLauncher launcher,
        HttpMessageHandler? handler = null)
    {
        fetcher = new MarkdownFetcher(new HttpClient(handler ?? ConstantHandler(Ok("# Hello"))));
        launcher = new FakeLauncher();
        return new AddressBarViewModel(fetcher, launcher);
    }

    private static HttpResponseMessage Ok(string body) => OkWith(body, "text/markdown");

    private static HttpResponseMessage OkWith(string body, string mediaType)
    {
        var content = new StringContent(body, System.Text.Encoding.UTF8, mediaType);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static HttpResponseMessage Status(HttpStatusCode status)
        => new HttpResponseMessage(status)
        {
            Content = new StringContent("x", System.Text.Encoding.UTF8, "text/markdown"),
        };

    private static HttpMessageHandler ConstantHandler(HttpResponseMessage response)
        => new FuncHandler((_, __) => Clone(response));

    private static HttpMessageHandler ThrowingHandler()
        => new FuncHandler((_, __) => throw new HttpRequestException("boom"));

    // StringContent can only be consumed once; re-create a fresh response per send.
    private static HttpResponseMessage Clone(HttpResponseMessage template)
    {
        string media = template.Content.Headers.ContentType?.MediaType ?? "text/markdown";
        string body = template.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        return new HttpResponseMessage(template.StatusCode)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, media),
        };
    }

    private sealed class FakeLauncher : IUrlLauncher
    {
        public int OpenCount { get; private set; }
        public Uri? LastOpened { get; private set; }

        public void Open(Uri url)
        {
            OpenCount++;
            LastOpened = url;
        }
    }

    private sealed class FuncHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _responder;

        public FuncHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
            => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request, cancellationToken));
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _template;
        private int _sendCount;

        public CountingHandler(HttpResponseMessage template) => _template = template;

        public int SendCount => _sendCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _sendCount);
            string media = _template.Content.Headers.ContentType?.MediaType ?? "text/markdown";
            string body = _template.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return Task.FromResult(new HttpResponseMessage(_template.StatusCode)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, media),
            });
        }
    }

    /// <summary>
    /// Holds every in-flight send until <see cref="ReleaseAll"/> is called, so a re-entrant submit
    /// can be started while the first fetch is provably still pending.
    /// </summary>
    private sealed class GatedHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<HttpResponseMessage> _gate =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ReleaseAll(HttpResponseMessage response)
        {
            string media = response.Content.Headers.ContentType?.MediaType ?? "text/markdown";
            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            _gate.TrySetResult(new HttpResponseMessage(response.StatusCode)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, media),
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(() => _gate.TrySetCanceled(cancellationToken)))
            {
                HttpResponseMessage shared = await _gate.Task.ConfigureAwait(false);
                // Each caller needs its own consumable content instance.
                string media = shared.Content.Headers.ContentType?.MediaType ?? "text/markdown";
                string body = await shared.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return new HttpResponseMessage(shared.StatusCode)
                {
                    Content = new StringContent(body, System.Text.Encoding.UTF8, media),
                };
            }
        }
    }
}
