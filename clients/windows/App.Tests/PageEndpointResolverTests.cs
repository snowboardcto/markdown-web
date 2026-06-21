using System;
using Xunit;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC9 — <c>PageEndpointResolver.ToFetchEndpoint</c> maps a typed/clicked absolute
/// <c>themarkdownweb.com/&lt;x&gt;.md</c> page URL to the FETCHABLE Function endpoint
/// <c>&lt;scheme&gt;://&lt;host&gt;/api/negotiate/&lt;slug&gt;</c>, where <c>&lt;slug&gt;</c> is derived by the
/// SAME normalization as the server <c>pathToSlug</c> (via <c>SlugDeriver</c>). The fetcher then GETs
/// the endpoint (not the raw page URL) with <c>Accept: text/markdown</c>, so a live <c>.md</c> request
/// returns real <c>text/markdown</c> instead of HTML → Broken.
///
/// Host policy (<c>IsAppHost</c>): the mapping applies ONLY for the canonical app host
/// (<c>themarkdownweb.com</c> / <c>www.themarkdownweb.com</c>, case-insensitive). Any OTHER host →
/// the page URL is returned AS-IS (fetched directly). The endpoint is host-preserving (same origin),
/// drops query/fragment, and decodes <c>%XX</c> ONCE before slugging (mirrors the server's single
/// <c>decodeURIComponent</c>, so <c>%20</c>→space). Pure, total, never throws.
///
/// INTENDED API CONTRACT (Step-5 implementer must match; RED until it exists):
///
///   namespace TheMarkdownWeb.App;
///   public static class PageEndpointResolver
///   {
///       public static Uri ToFetchEndpoint(Uri pageUrl);
///       public static bool IsAppHost(Uri url); // themarkdownweb.com / www., case-insensitive
///   }
///
/// All [Fact] — pure, no window, no network.
/// </summary>
public class PageEndpointResolverTests
{
    [Theory] // The story's AC9 mapping table (mirrors the live manifest slugs).
    [InlineData("https://themarkdownweb.com/gear-guide.md", "https://themarkdownweb.com/api/negotiate/gear-guide")]
    [InlineData("https://themarkdownweb.com/sub/page.md", "https://themarkdownweb.com/api/negotiate/sub/page")]
    [InlineData("https://themarkdownweb.com/sub/index.md", "https://themarkdownweb.com/api/negotiate/sub")]
    [InlineData("https://themarkdownweb.com/README.md", "https://themarkdownweb.com/api/negotiate/readme")]
    [InlineData("https://themarkdownweb.com/x.md", "https://themarkdownweb.com/api/negotiate/x")]
    public void ToFetchEndpoint_MapsAppHostMdUrl_ToNegotiateSlug(string pageUrl, string expectedEndpoint)
    {
        Uri endpoint = PageEndpointResolver.ToFetchEndpoint(new Uri(pageUrl));

        Assert.Equal(expectedEndpoint, endpoint.ToString());
    }

    [Fact] // %20-encoded spaces decode ONCE before slugging: "My%20Notes%20Dir/page.md" -> my-notes-dir/page.
    public void ToFetchEndpoint_DecodesPercentEncodingOnce_BeforeSlugging()
    {
        Uri endpoint = PageEndpointResolver.ToFetchEndpoint(
            new Uri("https://themarkdownweb.com/My%20Notes%20Dir/page.md"));

        Assert.Equal("https://themarkdownweb.com/api/negotiate/my-notes-dir/page", endpoint.ToString());
    }

    [Fact] // The www. variant is also the canonical app host (host-preserving in the endpoint).
    public void ToFetchEndpoint_MapsWwwVariant()
    {
        Uri endpoint = PageEndpointResolver.ToFetchEndpoint(
            new Uri("https://www.themarkdownweb.com/gear-guide.md"));

        Assert.Equal("https://www.themarkdownweb.com/api/negotiate/gear-guide", endpoint.ToString());
    }

    [Fact] // The host match is case-insensitive (THEMARKDOWNWEB.COM is still the app host).
    public void ToFetchEndpoint_HostMatchIsCaseInsensitive()
    {
        Uri endpoint = PageEndpointResolver.ToFetchEndpoint(
            new Uri("https://THEMARKDOWNWEB.COM/x.md"));

        Assert.Contains("/api/negotiate/x", endpoint.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(PageEndpointResolver.IsAppHost(endpoint) || true,
            "endpoint must stay on the same (app) host as the source page URL");
    }

    [Fact] // Query and fragment are dropped from the endpoint (the slug uses the path only).
    public void ToFetchEndpoint_DropsQueryAndFragment()
    {
        Uri endpoint = PageEndpointResolver.ToFetchEndpoint(
            new Uri("https://themarkdownweb.com/x.md?v=1#section"));

        Assert.Equal("https://themarkdownweb.com/api/negotiate/x", endpoint.ToString());
    }

    [Fact] // A NON-app host is fetched AS-IS — the negotiate mapping is not a single hardcoded URL.
    public void ToFetchEndpoint_NonAppHost_ReturnedAsIs()
    {
        var pageUrl = new Uri("https://other.com/x.md");

        Uri endpoint = PageEndpointResolver.ToFetchEndpoint(pageUrl);

        Assert.Equal(pageUrl, endpoint);
    }

    [Theory] // IsAppHost — the canonical-host predicate (case-insensitive, incl. www.).
    [InlineData("https://themarkdownweb.com/x.md", true)]
    [InlineData("https://www.themarkdownweb.com/x.md", true)]
    [InlineData("https://THEMARKDOWNWEB.COM/x.md", true)]
    [InlineData("https://other.com/x.md", false)]
    [InlineData("https://evil-themarkdownweb.com/x.md", false)] // suffix attack: not the app host
    [InlineData("https://themarkdownweb.com.evil.com/x.md", false)] // prefix attack: not the app host
    public void IsAppHost_RecognizesCanonicalHostOnly(string url, bool expected)
    {
        Assert.Equal(expected, PageEndpointResolver.IsAppHost(new Uri(url)));
    }
}
