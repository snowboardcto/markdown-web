using System;
using System.Threading.Tasks;
using Xunit;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 6.1 — <c>[Fact]</c> tests for <see cref="HomeNavigator"/> and the launch/Home controller
/// seam. No window, no network — mirrors the <see cref="NavigationControllerTests"/> injected-fetch
/// pattern exactly.
/// </summary>
public class HomeNavigatorTests
{
    // ── AC1: home URL ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HomeUrl_IsAbsolute()
    {
        Assert.True(HomeNavigator.HomeUrl.IsAbsoluteUri);
    }

    [Fact]
    public void HomeUrl_IsHttps()
    {
        Assert.Equal("https", HomeNavigator.HomeUrl.Scheme, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void HomeUrl_HostIsTheMarkdownWebCom()
    {
        Assert.Equal("themarkdownweb.com", HomeNavigator.HomeUrl.Host, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void HomeUrl_IsRecognizedAsAppHost()
    {
        // AC1: PageEndpointResolver.IsAppHost must return true for the home URL so the existing
        // /api/negotiate/<slug> mapping serves it without a second canonical-host literal.
        Assert.True(
            PageEndpointResolver.IsAppHost(HomeNavigator.HomeUrl),
            "HomeNavigator.HomeUrl must be recognized as the app host by PageEndpointResolver.IsAppHost.");
    }

    // ── AC2: launch-drives-controller ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task NavigateHomeAsync_FetchesHomeUrl_AndRendersContent()
    {
        // Arrange: injected fetch delegate that records calls (the NavigationControllerTests pattern).
        Uri? capturedFetchUrl = null;
        string? capturedMarkdown = null;

        var controller = new NavigationController(
            fetch: (uri, ct) =>
            {
                capturedFetchUrl = uri;
                return Task.FromResult(FetchResult.Success("# Home"));
            },
            renderSink: (markdown, pageUrl) =>
            {
                capturedMarkdown = markdown;
            },
            onBroken: () => { },
            launcher: new FakeUrlLauncher());

        // Act: call the home-navigate seam (used by both the launch hook and HomeButton_Click).
        await HomeNavigator.NavigateHomeAsync(controller);

        // Assert: the fetch was called with the home URL, the render sink received content, and
        // Current is the home URL.
        Assert.NotNull(capturedFetchUrl);
        Assert.Equal(HomeNavigator.HomeUrl, capturedFetchUrl);
        Assert.Equal("# Home", capturedMarkdown);
        Assert.Equal(HomeNavigator.HomeUrl, controller.Current);
    }

    [Fact]
    public async Task NavigateHomeAsync_IsAPushNotABack_CanGoBackAfterNavigatingAway()
    {
        // Arrange: start on another page, then navigate home — Home must push, so CanGoBack = true.
        var other = new Uri("https://themarkdownweb.com/other");
        var controller = new NavigationController(
            fetch: (_, __) => Task.FromResult(FetchResult.Success("# Page")),
            renderSink: (_, __) => { },
            onBroken: () => { },
            launcher: new FakeUrlLauncher());

        await controller.NavigateToAsync(other);
        Assert.Equal(other, controller.Current);

        // Act: navigate home.
        await HomeNavigator.NavigateHomeAsync(controller);

        // Assert: current is home AND we can go back (home was a push).
        Assert.Equal(HomeNavigator.HomeUrl, controller.Current);
        Assert.True(controller.CanGoBack,
            "Home navigation must push into history so the reader can go Back.");
    }

    [Fact]
    public async Task NavigateHomeAsync_TotalOnFailure_DoesNotThrow()
    {
        // Arrange: fetch delegate throws — the controller must handle it, not propagate.
        var controller = new NavigationController(
            fetch: (_, __) => Task.FromResult(FetchResult.Failure("network down")),
            renderSink: (_, __) => { },
            onBroken: () => { },
            launcher: new FakeUrlLauncher());

        // Act + Assert: no exception.
        var ex = await Record.ExceptionAsync(() => HomeNavigator.NavigateHomeAsync(controller));
        Assert.Null(ex);
    }

    [Fact]
    public async Task NavigateHomeAsync_NullController_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            HomeNavigator.NavigateHomeAsync(null!));
    }

    // ── Minimal fake helpers ────────────────────────────────────────────────────────────────────────

    private sealed class FakeUrlLauncher : IUrlLauncher
    {
        public void Open(Uri url) { }
    }
}
