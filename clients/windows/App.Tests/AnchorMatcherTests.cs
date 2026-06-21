using Xunit;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC5 (pure half) — the fragment→heading matcher locates the target heading for an <c>#anchor</c>
/// click by github-style slugging the heading text and comparing it to the fragment. (The actual
/// scroll-into-view is exercised in the [StaFact] anchor test; this pins the PURE matching logic.)
/// A fragment with no matching heading yields no match (the host then no-ops — no scroll, no fetch,
/// no crash). Pure + total — never throws.
///
/// The heading-anchor slug is the SAME per-segment github-slug used by <c>SlugDeriver</c> (lowercase,
/// delete punctuation/symbols, spaces→'-', no collapse, no trim), so anchors agree with GitHub's
/// rendered heading ids.
///
/// INTENDED API CONTRACT (Step-5 implementer must match; RED until it exists):
///
///   namespace TheMarkdownWeb.App;
///   public static class AnchorMatcher
///   {
///       // github-style heading-anchor slug of the visible heading text. Total, never throws.
///       public static string SlugHeading(string headingText);
///       // true iff the heading text's anchor slug equals the (sans-'#') fragment. Total.
///       public static bool Matches(string headingText, string fragment);
///   }
///
/// All [Fact] — pure, no window.
/// </summary>
public class AnchorMatcherTests
{
    [Theory] // Heading text -> github-style anchor slug.
    [InlineData("Install", "install")]
    [InlineData("Getting Started", "getting-started")]
    [InlineData("C# & .NET", "c--net")]          // '#','&','.' deleted, 2 spaces -> '--'
    [InlineData("FAQ?", "faq")]                  // '?' deleted
    [InlineData("foo_bar", "foo_bar")]           // '_' preserved
    public void SlugHeading_MatchesGithubAnchorSlug(string headingText, string expected)
    {
        Assert.Equal(expected, AnchorMatcher.SlugHeading(headingText));
    }

    [Fact] // A fragment matches a heading whose slug equals it (case-insensitive on the heading text).
    public void Matches_True_WhenHeadingSlugEqualsFragment()
    {
        Assert.True(AnchorMatcher.Matches("Install", "install"));
        Assert.True(AnchorMatcher.Matches("Getting Started", "getting-started"));
    }

    [Fact] // A fragment with no matching heading -> no match (host no-ops; no scroll).
    public void Matches_False_WhenNoHeadingMatchesFragment()
    {
        Assert.False(AnchorMatcher.Matches("Install", "nonexistent"));
    }

    [Theory] // Totality: degenerate input never throws.
    [InlineData("", "")]
    [InlineData("   ", "x")]
    [InlineData("Install", "")]
    public void Matches_NeverThrows_OnDegenerateInput(string headingText, string fragment)
    {
        bool _ = AnchorMatcher.Matches(headingText, fragment);
        // No exception is the assertion (total).
        Assert.True(true);
    }
}
