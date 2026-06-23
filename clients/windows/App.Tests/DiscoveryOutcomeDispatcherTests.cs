using System;
using System.Collections.Generic;
using Xunit;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 6.4 AC5 — pure <c>[Fact]</c> tests for <see cref="DiscoveryOutcomeDispatcher"/>.
/// No window, no network, no socket. Feeds each <see cref="DiscoveryResult"/> case and asserts
/// the correct sink was invoked via recording delegates.
/// </summary>
public class DiscoveryOutcomeDispatcherTests
{
    private static readonly Uri AnyUri = new("https://example.com/page");

    // ── PageMarkdown → onPageMarkdown ─────────────────────────────────────────────────────────────

    [Fact]
    public void Dispatch_PageMarkdown_InvokesOnPageMarkdown_WithCorrectArgs()
    {
        string? gotMarkdown = null;
        Uri? gotUrl = null;
        bool llmsInvoked = false;
        bool noMarkdownInvoked = false;
        bool blockedInvoked = false;

        var result = new DiscoveryResult.PageMarkdown("# Hello", AnyUri);

        DiscoveryOutcomeDispatcher.Dispatch(
            result,
            onPageMarkdown: (md, url) => { gotMarkdown = md; gotUrl = url; },
            onLlmsIndex: _ => { llmsInvoked = true; },
            onNoMarkdown: _ => { noMarkdownInvoked = true; },
            onBlocked: (_, __) => { blockedInvoked = true; });

        Assert.Equal("# Hello", gotMarkdown);
        Assert.Equal(AnyUri, gotUrl);
        Assert.False(llmsInvoked, "onLlmsIndex must NOT fire for PageMarkdown.");
        Assert.False(noMarkdownInvoked, "onNoMarkdown must NOT fire for PageMarkdown.");
        Assert.False(blockedInvoked, "onBlocked must NOT fire for PageMarkdown.");
    }

    // ── LlmsIndex → onLlmsIndex ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Dispatch_LlmsIndex_InvokesOnLlmsIndex_WithCorrectArg()
    {
        DiscoveryResult.LlmsIndex? gotIndex = null;
        bool pageMarkdownInvoked = false;
        bool noMarkdownInvoked = false;
        bool blockedInvoked = false;

        var links = new List<Uri> { new Uri("https://example.com/docs.md") };
        var result = new DiscoveryResult.LlmsIndex("# Site Index\n\n[Docs](https://example.com/docs.md)", links, AnyUri);

        DiscoveryOutcomeDispatcher.Dispatch(
            result,
            onPageMarkdown: (_, __) => { pageMarkdownInvoked = true; },
            onLlmsIndex: idx => { gotIndex = idx; },
            onNoMarkdown: _ => { noMarkdownInvoked = true; },
            onBlocked: (_, __) => { blockedInvoked = true; });

        Assert.NotNull(gotIndex);
        Assert.Same(result, gotIndex);
        Assert.False(pageMarkdownInvoked, "onPageMarkdown must NOT fire for LlmsIndex.");
        Assert.False(noMarkdownInvoked, "onNoMarkdown must NOT fire for LlmsIndex.");
        Assert.False(blockedInvoked, "onBlocked must NOT fire for LlmsIndex.");
    }

    // ── NoMarkdown → onNoMarkdown ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Dispatch_NoMarkdown_InvokesOnNoMarkdown_WithCorrectUrl()
    {
        Uri? gotUrl = null;
        bool pageMarkdownInvoked = false;
        bool llmsInvoked = false;
        bool blockedInvoked = false;

        var result = new DiscoveryResult.NoMarkdown(AnyUri);

        DiscoveryOutcomeDispatcher.Dispatch(
            result,
            onPageMarkdown: (_, __) => { pageMarkdownInvoked = true; },
            onLlmsIndex: _ => { llmsInvoked = true; },
            onNoMarkdown: url => { gotUrl = url; },
            onBlocked: (_, __) => { blockedInvoked = true; });

        Assert.Equal(AnyUri, gotUrl);
        Assert.False(pageMarkdownInvoked);
        Assert.False(llmsInvoked);
        Assert.False(blockedInvoked);
    }

    // ── Blocked → onBlocked ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Dispatch_Blocked_InvokesOnBlocked_WithCorrectUrlAndStatusCode()
    {
        Uri? gotUrl = null;
        int? gotCode = -999;
        bool pageMarkdownInvoked = false;
        bool llmsInvoked = false;
        bool noMarkdownInvoked = false;

        var result = new DiscoveryResult.Blocked(AnyUri, 403);

        DiscoveryOutcomeDispatcher.Dispatch(
            result,
            onPageMarkdown: (_, __) => { pageMarkdownInvoked = true; },
            onLlmsIndex: _ => { llmsInvoked = true; },
            onNoMarkdown: _ => { noMarkdownInvoked = true; },
            onBlocked: (url, code) => { gotUrl = url; gotCode = code; });

        Assert.Equal(AnyUri, gotUrl);
        Assert.Equal(403, gotCode);
        Assert.False(pageMarkdownInvoked);
        Assert.False(llmsInvoked);
        Assert.False(noMarkdownInvoked);
    }

    [Fact]
    public void Dispatch_Blocked_NullStatusCode_PassesNullToSink()
    {
        int? gotCode = -999; // sentinel — must be overwritten by null
        var result = new DiscoveryResult.Blocked(AnyUri, null);

        DiscoveryOutcomeDispatcher.Dispatch(
            result,
            onPageMarkdown: (_, __) => { },
            onLlmsIndex: _ => { },
            onNoMarkdown: _ => { },
            onBlocked: (_, code) => { gotCode = code; });

        Assert.Null(gotCode);
    }

    // ── Invalid → onInvalid (optional sink) ───────────────────────────────────────────────────────

    [Fact]
    public void Dispatch_Invalid_InvokesOnInvalid_WhenProvided()
    {
        bool invalidInvoked = false;
        bool pageMarkdownInvoked = false;
        bool noMarkdownInvoked = false;

        var result = new DiscoveryResult.Invalid("no scheme");

        DiscoveryOutcomeDispatcher.Dispatch(
            result,
            onPageMarkdown: (_, __) => { pageMarkdownInvoked = true; },
            onLlmsIndex: _ => { },
            onNoMarkdown: _ => { noMarkdownInvoked = true; },
            onBlocked: (_, __) => { },
            onInvalid: () => { invalidInvoked = true; });

        Assert.True(invalidInvoked, "onInvalid must fire when the result is Invalid and the sink is provided.");
        Assert.False(pageMarkdownInvoked);
        Assert.False(noMarkdownInvoked);
    }

    [Fact]
    public void Dispatch_Invalid_WithNoInvalidSink_DoesNotThrow()
    {
        // onInvalid is optional — omitting it must be safe (no NullReferenceException).
        var result = new DiscoveryResult.Invalid("no scheme");

        var ex = Record.Exception(() =>
        {
            DiscoveryOutcomeDispatcher.Dispatch(
                result,
                onPageMarkdown: (_, __) => { },
                onLlmsIndex: _ => { },
                onNoMarkdown: _ => { },
                onBlocked: (_, __) => { }
                /* onInvalid omitted */);
        });

        Assert.Null(ex);
    }

    // ── Null result: defensive — routes to onNoMarkdown ───────────────────────────────────────────

    [Fact]
    public void Dispatch_NullResult_DoesNotThrow_AndInvokesNoMarkdown()
    {
        bool noMarkdownInvoked = false;

        var ex = Record.Exception(() =>
        {
            DiscoveryOutcomeDispatcher.Dispatch(
                null!,
                onPageMarkdown: (_, __) => { },
                onLlmsIndex: _ => { },
                onNoMarkdown: _ => { noMarkdownInvoked = true; },
                onBlocked: (_, __) => { });
        });

        Assert.Null(ex);
        Assert.True(noMarkdownInvoked, "A null result must fall back to the onNoMarkdown sink.");
    }

    // ── Sinks are all optional (null-safe invocation) ────────────────────────────────────────────

    [Fact]
    public void Dispatch_NullSinks_DoNotThrow()
    {
        // The dispatcher uses ?. invocation — null sinks must be silently skipped.
        var ex = Record.Exception(() =>
        {
            DiscoveryOutcomeDispatcher.Dispatch(
                new DiscoveryResult.PageMarkdown("# x", AnyUri),
                onPageMarkdown: null!,
                onLlmsIndex: null!,
                onNoMarkdown: null!,
                onBlocked: null!,
                onInvalid: null);
        });

        Assert.Null(ex);
    }

    // ── Throwing sink: does NOT propagate ─────────────────────────────────────────────────────────

    [Fact]
    public void Dispatch_ThrowingSink_DoesNotPropagateException()
    {
        // The dispatcher is contracted Total — a crashing sink must never reach the UI pump.
        var result = new DiscoveryResult.NoMarkdown(AnyUri);

        var ex = Record.Exception(() =>
        {
            DiscoveryOutcomeDispatcher.Dispatch(
                result,
                onPageMarkdown: (_, __) => { },
                onLlmsIndex: _ => { },
                onNoMarkdown: _ => throw new InvalidOperationException("sink crash"),
                onBlocked: (_, __) => { });
        });

        Assert.Null(ex);
    }

    // ── Total: never throws for any input ────────────────────────────────────────────────────────

    [Fact]
    public void Dispatch_NeverThrows_ForAnyInput()
    {
        var ex = Record.Exception(() =>
        {
            DiscoveryOutcomeDispatcher.Dispatch(null!, null!, null!, null!, null!, null);
            DiscoveryOutcomeDispatcher.Dispatch(
                new DiscoveryResult.Invalid("bad"),
                (_, __) => { }, _ => { }, _ => { }, (_, __) => { });
            DiscoveryOutcomeDispatcher.Dispatch(
                new DiscoveryResult.LlmsIndex("# x", new List<Uri>(), AnyUri),
                (_, __) => { }, _ => { }, _ => { }, (_, __) => { });
        });

        Assert.Null(ex);
    }
}
