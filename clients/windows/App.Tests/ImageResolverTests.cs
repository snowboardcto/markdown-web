using System;
using Xunit;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC7 (resolve half) — <c>ImageResolver.Resolve(recordedSource, basePageUrl)</c> maps each recorded
/// <c>Image.Tag</c> source string (3.3) + the current page's base URL → an absolute <c>Uri?</c>,
/// reusing <c>PageUrlResolver</c>. Returns <c>null</c> (never throws) for an unresolvable/garbage
/// source. The App-side loader (an injected <c>IImageLoader</c>, exercised in the [StaFact] tests)
/// then loads the resolved Uri; a <c>null</c> resolution or a failed load leaves the <c>Image</c>
/// empty with its alt preserved — never a crash.
///
/// INTENDED API CONTRACT (Step-5 implementer must match; RED until it exists):
///
///   namespace TheMarkdownWeb.App;
///   public static class ImageResolver
///   {
///       public static Uri? Resolve(string? recordedSource, Uri? basePageUrl); // reuse PageUrlResolver; total.
///   }
///
/// All [Fact] — pure, no window, no network, no decode.
/// </summary>
public class ImageResolverTests
{
    private static readonly Uri Base = new("https://themarkdownweb.com/guides/x.md");

    [Theory] // The story's image edge matrix (resolution column).
    [InlineData("media/pic.png", "https://themarkdownweb.com/guides/media/pic.png")] // relative
    [InlineData("../img/a.png", "https://themarkdownweb.com/img/a.png")]             // .. -> parent
    [InlineData("/logo.png", "https://themarkdownweb.com/logo.png")]                 // root-absolute
    [InlineData("https://cdn.x/p.png", "https://cdn.x/p.png")]                       // already absolute
    public void Resolve_RelativeOrAbsolute_ProducesAbsoluteUri(string source, string expected)
    {
        Uri? resolved = ImageResolver.Resolve(source, Base);

        Assert.NotNull(resolved);
        Assert.Equal(expected, resolved!.ToString());
    }

    [Fact] // A data: URI is already absolute and is returned as the absolute Uri (loader may decode it).
    public void Resolve_DataUri_IsReturnedAsAbsolute()
    {
        const string dataUri = "data:image/png;base64,iVBORw0KGgo=";

        Uri? resolved = ImageResolver.Resolve(dataUri, Base);

        Assert.NotNull(resolved);
        Assert.Equal(Uri.UriSchemeData, resolved!.Scheme);
    }

    [Fact] // Protocol-relative //cdn/p.png adopts the base scheme.
    public void Resolve_ProtocolRelative_AdoptsBaseScheme()
    {
        Uri? resolved = ImageResolver.Resolve("//cdn.x/p.png", Base);

        Assert.NotNull(resolved);
        Assert.Equal("https://cdn.x/p.png", resolved!.ToString());
    }

    [Theory] // Totality: missing/garbage source -> null (unresolvable), never throws.
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("::garbage")]
    public void Resolve_ReturnsNull_OnUnresolvableSource_NeverThrows(string? source)
    {
        Uri? resolved = ImageResolver.Resolve(source, Base);

        Assert.Null(resolved);
    }

    [Fact] // Totality: a relative source with a null base cannot resolve -> null, never throws.
    public void Resolve_RelativeSource_NullBase_ReturnsNull()
    {
        Uri? resolved = ImageResolver.Resolve("media/pic.png", null);

        Assert.Null(resolved);
    }
}
