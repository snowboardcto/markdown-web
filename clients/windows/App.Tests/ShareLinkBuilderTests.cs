using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;
using AppMainWindow = global::TheMarkdownWeb.App.MainWindow;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 5.1 AC5 — <c>ShareLinkBuilder.ToShareUrl</c> and the native "Copy link" toolbar button.
///
/// RED until Task 4 (ShareLinkBuilder.cs), Task 5 (MainWindow toolbar button + IClipboard seam),
/// and Task 6 (this test file) are implemented.
///
/// Test scope mirrors AC5 / Task 6:
///   (A) Pure <c>[Fact]</c>s over <c>ShareLinkBuilder.ToShareUrl</c> — the full AC5 edge-case floor:
///       app-host URL variants (plain, trailing-slash, with query, with fragment, root /, www. vs apex,
///       percent-encoded/unicode path) → canonical share URL; non-app-host returned unchanged; null/
///       relative input is total (no throw).
///   (B) AC2 round-trip parity — for every app-host shape: <c>ToFetchEndpoint(ToShareUrl(current))</c>
///       maps to the SAME <c>/api/negotiate/&lt;slug&gt;</c> endpoint the original page fetched from.
///       Asserted against the REAL builder + resolver (anti-tautology discipline from 2.2).
///   (C) <c>[Fact]</c> over the copy action with an in-memory <c>IClipboard</c> fake — loaded page →
///       fake clipboard receives the canonical share URL; no-page-loaded → no-op, no throw.
///   (D) <c>[StaFact]</c> (construct-not-Show; mirrors <see cref="PersonalitySelectorWindowTests"/> /
///       <see cref="AddressBarWindowTests"/>) — the <c>ShareLinkButton</c> exists in the toolbar,
///       has a non-empty <c>AutomationProperties.Name</c> ("Copy link"), is Focusable + a tab stop,
///       and its TabIndex sits after <c>AddressInput</c> and the established controls (before or
///       equal to <c>ContentScroll</c>).
///
/// INTENDED API CONTRACT (implementer must match; RED until it exists):
///
///   namespace TheMarkdownWeb.App;
///   public static class ShareLinkBuilder
///   {
///       /// Returns the canonical shareable .md URL for an app-host page Uri, or the input unchanged
///       /// for non-app-host; null/relative input → null/empty, never throws.
///       public static string? ToShareUrl(Uri? current);
///   }
///
///   public interface IClipboard
///   {
///       void SetText(string text);
///   }
///
/// All [Fact] are pure (no window, no network, no real clipboard). [StaFact] constructs but never
/// Shows the window. CollectionBehavior(DisableTestParallelization = true) is already in AssemblyInfo.cs.
/// </summary>
public class ShareLinkBuilderTests
{
    // ── (A) ShareLinkBuilder.ToShareUrl — AC5 edge-case floor ─────────────────

    [Theory] // Happy-path app-host URLs → canonical share URL (scheme+host+path, no query/fragment).
    [InlineData("https://themarkdownweb.com/gear-guide", "https://themarkdownweb.com/gear-guide")]
    [InlineData("https://themarkdownweb.com/sub/page", "https://themarkdownweb.com/sub/page")]
    [InlineData("https://themarkdownweb.com/", "https://themarkdownweb.com/")]
    public void ToShareUrl_AppHostUrl_ReturnsCanonicalShareUrl(string input, string expected)
    {
        string? result = ShareLinkBuilder.ToShareUrl(new Uri(input));

        Assert.Equal(expected, result);
    }

    [Fact] // Trailing slash: /gear-guide/ → same canonical as /gear-guide (no trailing slash drift).
    public void ToShareUrl_TrailingSlash_CanonicalizesSameAsNoSlash()
    {
        // Both forms must produce the SAME canonical (no slug divergence).
        string? withSlash = ShareLinkBuilder.ToShareUrl(new Uri("https://themarkdownweb.com/gear-guide/"));
        string? withoutSlash = ShareLinkBuilder.ToShareUrl(new Uri("https://themarkdownweb.com/gear-guide"));

        // The canonical form must be non-null.
        Assert.NotNull(withSlash);
        Assert.NotNull(withoutSlash);

        // Both forms map to the same /api/negotiate/<slug> when fed through PageEndpointResolver.
        // (AC2 round-trip — if the slugs agree, the share URL and the fetch endpoint agree.)
        // The implementation may strip the trailing slash or preserve it — what matters is round-trip
        // parity. Assert via the fetch endpoint (the true parity measure).
        Uri fetchWithSlash = PageEndpointResolver.ToFetchEndpoint(new Uri(withSlash!));
        Uri fetchWithoutSlash = PageEndpointResolver.ToFetchEndpoint(new Uri(withoutSlash!));

        Assert.Equal(fetchWithoutSlash.ToString(), fetchWithSlash.ToString());
    }

    [Fact] // Query string: dropped from the share URL (consistent with PageEndpointResolver).
    public void ToShareUrl_DropQueryString()
    {
        string? result = ShareLinkBuilder.ToShareUrl(new Uri("https://themarkdownweb.com/gear-guide?v=1&ref=home"));

        Assert.NotNull(result);
        Assert.DoesNotContain("?", result!);
        Assert.DoesNotContain("v=1", result!);
    }

    [Fact] // Fragment: dropped from the share URL (consistent with PageEndpointResolver).
    public void ToShareUrl_DropFragment()
    {
        string? result = ShareLinkBuilder.ToShareUrl(new Uri("https://themarkdownweb.com/gear-guide#section-2"));

        Assert.NotNull(result);
        Assert.DoesNotContain("#", result!);
        Assert.DoesNotContain("section-2", result!);
    }

    [Fact] // Query AND fragment: both dropped.
    public void ToShareUrl_DropQueryAndFragment()
    {
        string? result = ShareLinkBuilder.ToShareUrl(new Uri("https://themarkdownweb.com/x?v=1#h"));

        Assert.NotNull(result);
        Assert.DoesNotContain("?", result!);
        Assert.DoesNotContain("#", result!);
    }

    [Fact] // Root / index URL: produces a well-formed canonical (not empty, not bare host).
    public void ToShareUrl_RootUrl_ProducesWellFormedCanonical()
    {
        string? result = ShareLinkBuilder.ToShareUrl(new Uri("https://themarkdownweb.com/"));

        Assert.NotNull(result);
        Assert.NotEqual(string.Empty, result);
        // Must start with the canonical origin.
        Assert.StartsWith("https://themarkdownweb.com", result!);
    }

    [Fact] // www. variant: host is preserved (not forced to apex; both variants are valid app hosts).
    public void ToShareUrl_WwwVariant_HostPreserved()
    {
        string? result = ShareLinkBuilder.ToShareUrl(new Uri("https://www.themarkdownweb.com/gear-guide"));

        Assert.NotNull(result);
        // The www. host must be PRESERVED (not silently rewritten to the apex host).
        Assert.Contains("www.themarkdownweb.com", result!);
    }

    [Fact] // www. variant round-trips through IsAppHost (the share URL is recognized as an app host).
    public void ToShareUrl_WwwVariant_RoundTripsThroughIsAppHost()
    {
        string? shareUrl = ShareLinkBuilder.ToShareUrl(new Uri("https://www.themarkdownweb.com/gear-guide"));
        Assert.NotNull(shareUrl);

        // The produced share URL must be recognized as an app-host URL by PageEndpointResolver.
        bool isApp = PageEndpointResolver.IsAppHost(new Uri(shareUrl!));
        Assert.True(isApp, "The share URL produced for www. variant must still be recognized as app host.");
    }

    [Fact] // Percent-encoded path: decoded ONCE (mirrors PageEndpointResolver's single decode).
    public void ToShareUrl_PercentEncodedPath_DecodedOnce()
    {
        // %20 in the path: the share URL should NOT double-encode it.
        string? result = ShareLinkBuilder.ToShareUrl(new Uri("https://themarkdownweb.com/My%20Notes/page"));

        Assert.NotNull(result);
        // Must not double-encode (%%2020 or %2520).
        Assert.DoesNotContain("%25", result!);
    }

    [Fact] // Non-app-host URL: returned UNCHANGED byte-for-byte (never silently rewritten).
    public void ToShareUrl_NonAppHost_ReturnedUnchanged()
    {
        var original = new Uri("https://example.com/some/path");
        string? result = ShareLinkBuilder.ToShareUrl(original);

        Assert.Equal(original.ToString(), result);
    }

    [Fact] // Another non-app-host variant: not rewritten.
    public void ToShareUrl_NonAppHost_Variant_ReturnedUnchanged()
    {
        var original = new Uri("https://evil-themarkdownweb.com/x");
        string? result = ShareLinkBuilder.ToShareUrl(original);

        Assert.Equal(original.ToString(), result);
    }

    [Fact] // null input: total — returns null without throwing.
    public void ToShareUrl_NullInput_ReturnsNullWithoutThrowing()
    {
        // Must not throw ArgumentNullException or any other exception.
        string? result = ShareLinkBuilder.ToShareUrl(null);

        // Returns null (or empty) — either is acceptable per AC5 "total, never throws".
        // The primary assertion is no exception.
        Assert.True(result is null || result.Length == 0,
            "null input must return null or empty string, not throw.");
    }

    [Fact] // Relative Uri: total — does not throw InvalidOperationException from .Host/.Scheme.
    public void ToShareUrl_RelativeUri_TotalNeverThrows()
    {
        var relative = new Uri("/some/path", UriKind.Relative);

        // Must not throw.
        string? result = ShareLinkBuilder.ToShareUrl(relative);

        // Result for a relative URI: returned unchanged or null — either is acceptable.
        // Primary assertion: no exception.
        Assert.True(result is null || result == relative.ToString() || result.Length == 0,
            "relative Uri must be handled totally (no InvalidOperationException from .Host/.Scheme).");
    }

    // ── (B) AC2 Round-trip parity: ToFetchEndpoint(ToShareUrl(current)) == original endpoint ─
    // For every app-host URL shape, the share URL fed back through PageEndpointResolver.ToFetchEndpoint
    // must map to the SAME /api/negotiate/<slug> as the original .md page URL fetched from.
    // Asserted against REAL ShareLinkBuilder + PageEndpointResolver (anti-tautology, 2.2).

    [Theory]
    [InlineData(
        "https://themarkdownweb.com/gear-guide.md",
        "https://themarkdownweb.com/gear-guide",
        "https://themarkdownweb.com/api/negotiate/gear-guide")]
    [InlineData(
        "https://themarkdownweb.com/sub/page.md",
        "https://themarkdownweb.com/sub/page",
        "https://themarkdownweb.com/api/negotiate/sub/page")]
    [InlineData(
        "https://themarkdownweb.com/x.md",
        "https://themarkdownweb.com/x",
        "https://themarkdownweb.com/api/negotiate/x")]
    public void ToShareUrl_RoundTrip_ToFetchEndpoint_MatchesOriginalEndpoint(
        string originalMdPageUrl,
        string shareablePageUrl,
        string expectedEndpoint)
    {
        // The share URL is the page URL WITHOUT the .md extension (the browsable form).
        // AC5: ToShareUrl of the browsable page URL → same endpoint as the .md URL.
        string? shareUrl = ShareLinkBuilder.ToShareUrl(new Uri(shareablePageUrl));
        Assert.NotNull(shareUrl);

        Uri fetchFromShare = PageEndpointResolver.ToFetchEndpoint(new Uri(shareUrl!));
        Uri fetchFromOriginal = PageEndpointResolver.ToFetchEndpoint(new Uri(originalMdPageUrl));

        Assert.Equal(fetchFromOriginal.ToString(), fetchFromShare.ToString());

        // Also assert the endpoint is the expected /api/negotiate/<slug>.
        Assert.Equal(expectedEndpoint, fetchFromOriginal.ToString());
    }

    [Fact] // Trailing-slash round-trip: /gear-guide/ produces the SAME endpoint as /gear-guide.md.
    public void ToShareUrl_TrailingSlash_RoundTrip_SameEndpointAsNoSlash()
    {
        Uri withSlash = new Uri("https://themarkdownweb.com/gear-guide/");
        Uri withoutSlash = new Uri("https://themarkdownweb.com/gear-guide");
        Uri originalMd = new Uri("https://themarkdownweb.com/gear-guide.md");

        string? shareWithSlash = ShareLinkBuilder.ToShareUrl(withSlash);
        string? shareWithoutSlash = ShareLinkBuilder.ToShareUrl(withoutSlash);

        Assert.NotNull(shareWithSlash);
        Assert.NotNull(shareWithoutSlash);

        Uri fetchFromSlash = PageEndpointResolver.ToFetchEndpoint(new Uri(shareWithSlash!));
        Uri fetchFromNoSlash = PageEndpointResolver.ToFetchEndpoint(new Uri(shareWithoutSlash!));
        Uri fetchFromMd = PageEndpointResolver.ToFetchEndpoint(originalMd);

        // All three forms must resolve to the SAME /api/negotiate/<slug>.
        Assert.Equal(fetchFromMd.ToString(), fetchFromSlash.ToString());
        Assert.Equal(fetchFromMd.ToString(), fetchFromNoSlash.ToString());
    }

    [Fact] // Query+fragment round-trip: dropping them must not change the endpoint.
    public void ToShareUrl_QueryAndFragment_RoundTrip_SameEndpointAsCleanUrl()
    {
        Uri dirtyUrl = new Uri("https://themarkdownweb.com/gear-guide?v=1#section");
        Uri cleanUrl = new Uri("https://themarkdownweb.com/gear-guide");

        string? shareFromDirty = ShareLinkBuilder.ToShareUrl(dirtyUrl);
        string? shareFromClean = ShareLinkBuilder.ToShareUrl(cleanUrl);

        Assert.NotNull(shareFromDirty);
        Assert.NotNull(shareFromClean);

        Uri fetchFromDirty = PageEndpointResolver.ToFetchEndpoint(new Uri(shareFromDirty!));
        Uri fetchFromClean = PageEndpointResolver.ToFetchEndpoint(new Uri(shareFromClean!));

        Assert.Equal(fetchFromClean.ToString(), fetchFromDirty.ToString());
    }

    // ── (B-extra) AC2 round-trip parity: %20, %2F, and unicode path shapes ──────
    // These assert the "decoded once" contract: ToShareUrl must call Uri.UnescapeDataString
    // so that %2F becomes "/" and round-trips through ToFetchEndpoint identically to the
    // non-encoded form. Without the decode, %2F would diverge (share ≠ fetch endpoint).

    [Fact] // %20 (space) in path: round-trip must produce the same endpoint as the decoded form.
    public void ToShareUrl_PercentEncodedSpace_RoundTrip_MatchesDecodedForm()
    {
        // %20 in a URL path is a space; the decoded slug should match "My Notes/page".
        var encodedUri = new Uri("https://themarkdownweb.com/My%20Notes/page");
        var decodedUri = new Uri("https://themarkdownweb.com/My Notes/page", UriKind.Absolute);

        string? shareEncoded = ShareLinkBuilder.ToShareUrl(encodedUri);
        Assert.NotNull(shareEncoded);

        // Both must produce the same /api/negotiate/<slug> endpoint.
        Uri fetchFromEncoded = PageEndpointResolver.ToFetchEndpoint(new Uri(shareEncoded!));
        Uri fetchFromDecoded = PageEndpointResolver.ToFetchEndpoint(decodedUri);
        Assert.Equal(fetchFromDecoded.ToString(), fetchFromEncoded.ToString());
    }

    [Fact] // %2F (encoded slash) in path: ToShareUrl decodes once so %2F -> "/" before slugging.
    public void ToShareUrl_PercentEncodedSlash_RoundTrip_DecodedOnceBeforeSlug()
    {
        // %2F in a path component is an encoded "/" (a sub-path separator).
        // After decode-once, "sub%2Fpage" → "sub/page" → slug "sub/page" → endpoint /api/negotiate/sub/page.
        var uri = new Uri("https://themarkdownweb.com/sub%2Fpage");
        string? shareUrl = ShareLinkBuilder.ToShareUrl(uri);
        Assert.NotNull(shareUrl);

        // The share URL, when fed through ToFetchEndpoint, must produce the same endpoint
        // as the explicitly decoded URL (proving the single-decode contract holds).
        var decodedUri = new Uri("https://themarkdownweb.com/sub/page");
        Uri fetchFromShare = PageEndpointResolver.ToFetchEndpoint(new Uri(shareUrl!));
        Uri fetchFromDecoded = PageEndpointResolver.ToFetchEndpoint(decodedUri);
        Assert.Equal(fetchFromDecoded.ToString(), fetchFromShare.ToString());
    }

    [Fact] // Unicode path: non-ASCII slug chars round-trip through SlugDeriver correctly.
    public void ToShareUrl_UnicodePath_RoundTrip_MatchesNormalized()
    {
        // Unicode in a URI path is percent-encoded. After decode-once, it's a unicode string.
        // SlugDeriver normalizes it. This asserts the round-trip doesn't throw and produces a valid endpoint.
        var uri = new Uri("https://themarkdownweb.com/caf%C3%A9"); // "café"
        string? shareUrl = ShareLinkBuilder.ToShareUrl(uri);
        Assert.NotNull(shareUrl);

        // The share URL must round-trip without throwing.
        Uri fetchFromShare = PageEndpointResolver.ToFetchEndpoint(new Uri(shareUrl!));
        Assert.NotNull(fetchFromShare);
        Assert.Contains("/api/negotiate/", fetchFromShare.ToString());
    }

    // ── (C) Copy action via the REAL extracted method (MainWindow.ExecuteCopyLink) ──
    // These test the actual wiring — not a re-implementation of the handler inline.
    // MainWindow.ExecuteCopyLink(IClipboard, Uri?) is the testable extract (Review fix #5).

    [Fact] // Loaded page → fake clipboard receives the canonical share URL.
    public void CopyLinkAction_WithLoadedPage_WritesShareUrlToFakeClipboard()
    {
        // Arrange: an in-memory IClipboard fake (mirrors the IUrlLauncher fake pattern).
        var fakeClipboard = new FakeClipboard();
        var currentPage = new Uri("https://themarkdownweb.com/gear-guide");

        // Act: invoke the REAL extracted handler logic via the public static method.
        MainWindow.ExecuteCopyLink(fakeClipboard, currentPage);

        // Assert: the fake clipboard received the canonical share URL.
        Assert.Equal("https://themarkdownweb.com/gear-guide", fakeClipboard.LastSetText);
    }

    [Fact] // No page loaded (current == null): copy action is a no-op; clipboard untouched; no throw.
    public void CopyLinkAction_NoPageLoaded_NullCurrent_IsNoOpNoThrow()
    {
        var fakeClipboard = new FakeClipboard();

        // Act: invoke the REAL extracted handler with null current (no page loaded).
        MainWindow.ExecuteCopyLink(fakeClipboard, null);

        // Clipboard must be untouched (no SetText called).
        Assert.Null(fakeClipboard.LastSetText);
    }

    [Fact] // Non-app-host page: copy action passes the URL unchanged (no silent rewrite).
    public void CopyLinkAction_NonAppHostPage_ClipboardReceivesOriginalUrl()
    {
        var fakeClipboard = new FakeClipboard();
        var externalPage = new Uri("https://example.com/some/page");

        // Act: invoke the REAL extracted handler.
        MainWindow.ExecuteCopyLink(fakeClipboard, externalPage);

        // The clipboard should contain the original external URL (unchanged).
        Assert.Equal("https://example.com/some/page", fakeClipboard.LastSetText);
    }

    // ── (D) [StaFact] toolbar button — construct-not-Show; mirrors PersonalitySelectorWindowTests ─

    [StaFact] // AC5 — ShareLinkButton exists in the toolbar with correct name and type.
    public void ShareLinkButton_Exists_InToolbar()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();

        var shareButton = window.FindName("ShareLinkButton") as Button;
        Assert.True(shareButton is not null,
            "MainWindow must host a Button named 'ShareLinkButton' in the toolbar (Task 5 not yet implemented?).");
    }

    [StaFact] // AC5 — the button has a non-empty AutomationProperties.Name ("Copy link").
    public void ShareLinkButton_HasNonEmptyAutomationName()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();
        var shareButton = window.FindName("ShareLinkButton") as Button;
        Assert.True(shareButton is not null, "ShareLinkButton must exist.");

        string automationName = AutomationProperties.GetName(shareButton!);
        Assert.False(string.IsNullOrWhiteSpace(automationName),
            "ShareLinkButton needs a stable AutomationProperties.Name (e.g. \"Copy link\") — not a bare glyph.");
        Assert.Equal("Copy link", automationName);
    }

    [StaFact] // AC5 — the button is keyboard-reachable (Focusable + tab stop).
    public void ShareLinkButton_IsKeyboardReachable()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();
        var shareButton = window.FindName("ShareLinkButton") as Button;
        Assert.True(shareButton is not null, "ShareLinkButton must exist.");

        Assert.True(shareButton!.Focusable, "ShareLinkButton must be Focusable for keyboard reachability.");
        Assert.True(KeyboardNavigation.GetIsTabStop(shareButton),
            "ShareLinkButton must be a tab stop (KeyboardNavigation.IsTabStop != false).");
    }

    [StaFact] // AC5 — TabIndex sits after AddressInput and before or equal to ContentScroll.
    public void ShareLinkButton_TabIndex_IsAfterAddressInput_AndBeforeOrEqualToContentScroll()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();

        var shareButton = window.FindName("ShareLinkButton") as Button;
        Assert.True(shareButton is not null, "ShareLinkButton must exist.");

        var addressInput = window.FindName("AddressInput") as System.Windows.Controls.TextBox;
        Assert.True(addressInput is not null, "AddressInput must exist.");

        var contentScroll = window.FindName("ContentScroll") as System.Windows.Controls.FlowDocumentScrollViewer;
        Assert.True(contentScroll is not null, "ContentScroll must exist.");

        Assert.True(shareButton!.TabIndex > addressInput!.TabIndex,
            $"ShareLinkButton.TabIndex ({shareButton.TabIndex}) must follow AddressInput ({addressInput.TabIndex}).");
        Assert.True(contentScroll!.TabIndex >= shareButton.TabIndex,
            $"ContentScroll.TabIndex ({contentScroll.TabIndex}) must be >= ShareLinkButton ({shareButton.TabIndex}).");
    }

    [StaFact] // Regression: nav StackPanel still contains exactly Back/Forward/Reload (new button in a separate column).
    public void Toolbar_NavStackPanel_Unchanged_WithShareButton()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();
        Button? back = ShellTestHelpers.FindButton(window, ShellTestHelpers.BackButtonName);

        // Assert BackButton is non-null first — if it were renamed, the count assertion below
        // would silently pass on an empty list, making it a tautology. This guard surfaces renames.
        Assert.True(back is not null,
            $"BackButton named '{ShellTestHelpers.BackButtonName}' must exist in the toolbar. " +
            "If it was renamed, update ShellTestHelpers.BackButtonName to match.");

        // The Q-Placement pattern puts the Share button in a NEW column, NOT in the nav StackPanel.
        // The nav stack must still have exactly 3 buttons (Back/Forward/Reload).
        var ordered = ShellTestHelpers.ButtonsInToolbarOrder(back!);
        Assert.Equal(3, ordered.Count);
    }
}

/// <summary>
/// In-memory <see cref="IClipboard"/> test double for CI — no real OS clipboard.
/// Mirrors the IUrlLauncher fake pattern established in the existing App.Tests.
/// </summary>
internal sealed class FakeClipboard : IClipboard
{
    /// <summary>The last text written via <see cref="SetText"/>, or null if never called.</summary>
    public string? LastSetText { get; private set; }

    /// <inheritdoc />
    public void SetText(string text)
    {
        LastSetText = text;
    }
}
