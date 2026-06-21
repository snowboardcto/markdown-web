using System;
using Xunit;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC2 — <c>LinkClassifier.Classify(href, basePageUrl)</c> deterministically + totally classifies a
/// rendered <c>Hyperlink</c>'s href (resolved against the current page base) into one of four
/// <c>LinkKind</c>s:
///   • <b>InternalMarkdown</b> — a relative-or-absolute link whose RESOLVED absolute URL is an
///     <c>http(s)</c> <c>.md</c> page on the SAME host as the base (navigate in place, AC4);
///   • <b>Anchor</b> — a pure fragment <c>#heading</c> (scroll, AC5; no fetch);
///   • <b>External</b> — an absolute <c>http(s)</c> link that is NOT an internal <c>.md</c> page
///     (open in the system browser, AC6);
///   • <b>Unsupported</b> — <c>mailto:</c>/<c>tel:</c>/<c>javascript:</c>/<c>data:</c>/empty/garbage
///     (no-op, never a crash).
/// Pure (no I/O), total (never throws for any string), case-insensitive on scheme + <c>.md</c>.
/// The resolved payload is carried on <c>LinkTarget</c>: the resolved page Url for InternalMarkdown,
/// the fragment for Anchor, the absolute Url for External.
///
/// INTENDED API CONTRACT (Step-5 implementer must match; RED until it exists):
///
///   namespace TheMarkdownWeb.App;
///   public enum LinkKind { InternalMarkdown, Anchor, External, Unsupported }
///   public readonly record struct LinkTarget(LinkKind Kind, Uri? Url, string? Fragment)
///   {
///       public static LinkTarget Internal(Uri pageUrl);
///       public static LinkTarget AnchorTo(string fragment);
///       public static LinkTarget ExternalTo(Uri url);
///       public static LinkTarget Unsupported { get; }
///   }
///   public static class LinkClassifier
///   {
///       public static LinkTarget Classify(string? href, Uri? basePageUrl);
///   }
///
/// All [Fact] — pure, no window, no network.
/// </summary>
public class LinkClassifierTests
{
    private static readonly Uri Base = new("https://themarkdownweb.com/guides/gear.md");

    // ---- InternalMarkdown rows (story link-classification edge matrix) -----------------------

    [Theory]
    [InlineData("./powder.md", "https://themarkdownweb.com/guides/powder.md")] // relative .md, same host
    [InlineData("../index.md", "https://themarkdownweb.com/index.md")]         // .. -> parent dir
    [InlineData("notes.md", "https://themarkdownweb.com/guides/notes.md")]     // bare relative
    [InlineData("/x.md", "https://themarkdownweb.com/x.md")]                   // absolute-path .md (host root)
    [InlineData("powder.MD", "https://themarkdownweb.com/guides/powder.MD")]   // .md CASE-INSENSITIVE
    [InlineData("https://themarkdownweb.com/sub/page.md", "https://themarkdownweb.com/sub/page.md")] // absolute same-host .md
    public void Classify_InternalMarkdown_ResolvesAbsolutePageUrl(string href, string expectedUrl)
    {
        LinkTarget target = LinkClassifier.Classify(href, Base);

        Assert.Equal(LinkKind.InternalMarkdown, target.Kind);
        Assert.NotNull(target.Url);
        Assert.Equal(expectedUrl, target.Url!.ToString());
    }

    [Fact] // x.md#install: a cross-page link WITH a fragment classifies by the PATH -> InternalMarkdown.
    public void Classify_CrossPageLinkWithFragment_IsInternalMarkdown_FragmentRetained()
    {
        LinkTarget target = LinkClassifier.Classify("x.md#install", Base);

        Assert.Equal(LinkKind.InternalMarkdown, target.Kind);
        Assert.NotNull(target.Url);
        Assert.Equal("https://themarkdownweb.com/guides/x.md", target.Url!.GetLeftPart(UriPartial.Path));
        // The fragment travels on the resolved Url so the host can best-effort scroll after load.
        Assert.Equal("install", target.Url.Fragment.TrimStart('#'));
    }

    [Fact] // x.md?v=1: query preserved on the resolved Url (endpoint mapper later uses path only).
    public void Classify_CrossPageLinkWithQuery_IsInternalMarkdown_QueryPreserved()
    {
        LinkTarget target = LinkClassifier.Classify("x.md?v=1", Base);

        Assert.Equal(LinkKind.InternalMarkdown, target.Kind);
        Assert.Equal("https://themarkdownweb.com/guides/x.md?v=1", target.Url!.ToString());
    }

    // ---- Anchor rows --------------------------------------------------------------------------

    [Fact] // Pure fragment -> Anchor, scroll, no fetch; the fragment (sans '#') is carried.
    public void Classify_PureFragment_IsAnchor()
    {
        LinkTarget target = LinkClassifier.Classify("#install", Base);

        Assert.Equal(LinkKind.Anchor, target.Kind);
        Assert.Equal("install", target.Fragment);
    }

    [Fact] // Whitespace is trimmed before classify; "  #top  " -> Anchor("top").
    public void Classify_TrimsWhitespace_BeforeClassifying()
    {
        LinkTarget target = LinkClassifier.Classify("  #top  ", Base);

        Assert.Equal(LinkKind.Anchor, target.Kind);
        Assert.Equal("top", target.Fragment);
    }

    // ---- External rows ------------------------------------------------------------------------

    [Theory]
    [InlineData("https://other.com/a.md")]            // .md but DIFFERENT host -> external
    [InlineData("https://themarkdownweb.com/about")]  // same host, NO .md -> external
    [InlineData("http://example.com/x")]              // absolute http(s), non-.md
    public void Classify_External_CarriesAbsoluteUri(string href)
    {
        LinkTarget target = LinkClassifier.Classify(href, Base);

        Assert.Equal(LinkKind.External, target.Kind);
        Assert.NotNull(target.Url);
        Assert.Equal(href, target.Url!.ToString());
    }

    [Fact] // Protocol-relative //host/x.md adopts the base scheme; app host + .md -> InternalMarkdown.
    public void Classify_ProtocolRelative_AppHost_IsInternalMarkdown()
    {
        LinkTarget target = LinkClassifier.Classify("//themarkdownweb.com/x.md", Base);

        Assert.Equal(LinkKind.InternalMarkdown, target.Kind);
        Assert.Equal("https://themarkdownweb.com/x.md", target.Url!.ToString());
    }

    [Fact] // Protocol-relative //host/x.md to a DIFFERENT host -> External.
    public void Classify_ProtocolRelative_OtherHost_IsExternal()
    {
        LinkTarget target = LinkClassifier.Classify("//other.com/x.md", Base);

        Assert.Equal(LinkKind.External, target.Kind);
    }

    // ---- Unsupported rows (total; never throws, never executes) -------------------------------

    [Theory]
    [InlineData("mailto:a@b.com")]
    [InlineData("tel:+15550123")]
    [InlineData("javascript:alert(1)")] // hostile scheme — NEVER executed, just Unsupported
    [InlineData("data:text/html,x")]
    [InlineData(":://garbage")]
    [InlineData("ht tp://x")]
    public void Classify_Unsupported_ForNonHttpOrGarbage(string href)
    {
        LinkTarget target = LinkClassifier.Classify(href, Base);

        Assert.Equal(LinkKind.Unsupported, target.Kind);
    }

    [Theory] // Empty / whitespace / null -> Unsupported, never throws (totality).
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Classify_Unsupported_ForEmptyOrNull_NeverThrows(string? href)
    {
        LinkTarget target = LinkClassifier.Classify(href, Base);

        Assert.Equal(LinkKind.Unsupported, target.Kind);
    }

    [Fact] // Totality with a null base: a relative ref cannot resolve -> Unsupported (no throw).
    public void Classify_RelativeRef_WithNullBase_IsUnsupported_NeverThrows()
    {
        LinkTarget target = LinkClassifier.Classify("./powder.md", null);

        // Cannot resolve a relative ref without a base; total -> Unsupported, never an exception.
        Assert.Equal(LinkKind.Unsupported, target.Kind);
    }

    [Fact] // Deterministic: the same inputs always yield the same classification.
    public void Classify_IsDeterministic()
    {
        LinkTarget a = LinkClassifier.Classify("./powder.md", Base);
        LinkTarget b = LinkClassifier.Classify("./powder.md", Base);

        Assert.Equal(a, b);
    }
}
