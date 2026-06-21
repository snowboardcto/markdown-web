using System;
using Xunit;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC3 — <c>PageUrlResolver.ResolveAgainst</c> resolves a relative href/src against the current
/// page's absolute base URL using standard <c>new Uri(base, rel)</c> semantics: <c>./</c> and
/// <c>..</c> resolve against the base's DIRECTORY, a leading <c>/</c> against the host root, an
/// already-absolute ref is returned unchanged, and an unresolvable/garbage ref returns <c>null</c>
/// (NEVER throws). This single resolver is shared by link navigation (AC4) and image resolution (AC7).
///
/// INTENDED API CONTRACT (Step-5 implementer must match; RED until it exists):
///
///   namespace TheMarkdownWeb.App;
///   public static class PageUrlResolver
///   {
///       // new Uri(base, rel) semantics; null (never throws) for unresolvable/garbage.
///       public static Uri? ResolveAgainst(Uri basePageUrl, string relativeRef);
///   }
///
/// All [Fact] — pure, no window, no network.
/// </summary>
public class PageUrlResolverTests
{
    private static readonly Uri Base = new("https://themarkdownweb.com/guides/gear.md");

    [Theory] // The story's AC3 resolution table.
    [InlineData("./powder.md", "https://themarkdownweb.com/guides/powder.md")] // sibling via ./
    [InlineData("powder.md", "https://themarkdownweb.com/guides/powder.md")]   // bare relative
    [InlineData("../index.md", "https://themarkdownweb.com/index.md")]         // .. -> parent dir
    [InlineData("media/pic.png", "https://themarkdownweb.com/guides/media/pic.png")] // nested relative
    [InlineData("../img/a.png", "https://themarkdownweb.com/img/a.png")]       // .. + nested
    [InlineData("/x.md", "https://themarkdownweb.com/x.md")]                   // leading / -> host root
    [InlineData("/logo.png", "https://themarkdownweb.com/logo.png")]           // root-absolute image
    [InlineData("https://other.com/a.md", "https://other.com/a.md")]           // already-absolute -> as-is
    public void ResolveAgainst_ProducesExpectedAbsoluteUrl(string relativeRef, string expected)
    {
        Uri? resolved = PageUrlResolver.ResolveAgainst(Base, relativeRef);

        Assert.NotNull(resolved);
        Assert.Equal(expected, resolved!.ToString());
    }

    [Fact] // A query on a relative ref is preserved by base resolution.
    public void ResolveAgainst_PreservesQuery()
    {
        Uri? resolved = PageUrlResolver.ResolveAgainst(Base, "x.md?v=1");

        Assert.NotNull(resolved);
        Assert.Equal("https://themarkdownweb.com/guides/x.md?v=1", resolved!.ToString());
    }

    [Fact] // A fragment on a relative ref is preserved by base resolution.
    public void ResolveAgainst_PreservesFragment()
    {
        Uri? resolved = PageUrlResolver.ResolveAgainst(Base, "x.md#install");

        Assert.NotNull(resolved);
        Assert.Equal("install", resolved!.Fragment.TrimStart('#'));
    }

    [Theory] // Totality: an unresolvable/garbage ref returns null, never throws.
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("::://garbage")]
    [InlineData("ht tp://x")]
    public void ResolveAgainst_ReturnsNull_OnUnresolvableRef_NeverThrows(string garbage)
    {
        Uri? resolved = PageUrlResolver.ResolveAgainst(Base, garbage);

        Assert.Null(resolved); // total — null, not an exception
    }
}
