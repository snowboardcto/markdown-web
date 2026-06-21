using Xunit;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC9 — <c>SlugDeriver.PathToSlug</c> is a BYTE-IDENTICAL C# port of the server
/// <c>api/negotiate/slug.mjs</c> <c>pathToSlug</c> (github-slugger parity). The slug derived
/// client-side MUST equal the server's manifest key for the same vault path, or a live <c>.md</c>
/// click resolves to <c>/api/negotiate/&lt;wrong-slug&gt;</c> → 404 → Broken.
///
/// The server algorithm (verified against <c>slug.mjs</c> + github-slugger <c>regex.js</c>) is, per
/// <c>/</c>-separated segment, EXACTLY:
///   <c>segment.toLowerCase().replace(&lt;github-slugger Unicode regex&gt;, '').replace(/ /g, '-')</c>
/// i.e. (a) invariant-lowercase, (b) DELETE (not replace) every char matched by the github-slugger
/// Unicode class (punctuation/symbols/control: <c>.</c> <c>,</c> <c>!</c> <c>#</c> <c>&amp;</c> <c>%</c> …),
/// (c) replace ONLY the literal space U+0020 with <c>-</c>. NO hyphen-run collapse, NO trim.
/// Then: drop a trailing <c>.md</c> (case-insensitive) BEFORE slugging, split on <c>/</c>, slug each
/// segment, re-join <c>/</c>, strip a trailing <c>/index</c>, map a bare <c>index</c> → <c>""</c>.
///
/// INTENDED API CONTRACT (Step-5 implementer must match; RED until it exists):
///
///   namespace TheMarkdownWeb.App;
///   public static class SlugDeriver
///   {
///       // Total, never throws. Input = a decoded relative POSIX path (no leading '/').
///       public static string PathToSlug(string relPosixPath);
///   }
///
/// All [Fact] — pure, no window, no network.
/// </summary>
public class SlugDeriverTests
{
    // ---- The parity golden table (story §"SlugDeriver parity table") -------------------------
    // Each row's expected slug is the EXACT server output; the manifest-key rows are also a live
    // cross-check (asserted again in ManifestKeys_AreReproduced below).

    [Theory]
    [InlineData("gear-guide.md", "gear-guide")]          // drop .md, already a slug
    [InlineData("README.md", "readme")]                  // invariant-lowercase
    [InlineData("My Notes.md", "my-notes")]              // space->'-', lowercase
    [InlineData("My Notes Dir/page.md", "my-notes-dir/page")] // per-segment, multi-segment
    [InlineData("sub/page.md", "sub/page")]              // nested
    [InlineData("sub/index.md", "sub")]                  // trailing /index collapse
    [InlineData("index.md", "")]                         // bare index -> "" (vault root)
    [InlineData("x.md", "x")]                            // single char
    [InlineData("Gear Guide.md", "gear-guide")]          // space + case
    [InlineData("a.b.c.md", "abc")]                      // dots DELETED, not '-' (drop only trailing .md)
    [InlineData("100%.md", "100")]                       // '%' deleted, no trailing '-'
    [InlineData("C# & .NET.md", "c--net")]               // '#','&','.' deleted, 2 spaces -> '--'
    [InlineData("--x--.md", "--x--")]                    // NO hyphen-collapse, NO trim
    [InlineData("foo_bar.md", "foo_bar")]                // '_' preserved (not in the regex class)
    [InlineData("Hello, World!.md", "hello-world")]      // ',' '!' deleted, space -> '-'
    public void PathToSlug_MatchesServerPathToSlug(string input, string expected)
    {
        Assert.Equal(expected, SlugDeriver.PathToSlug(input));
    }

    [Fact] // .md drop is case-insensitive (mirrors /\.md$/i): trailing .MD / .Md also dropped.
    public void PathToSlug_DropsMdExtension_CaseInsensitively()
    {
        Assert.Equal("readme", SlugDeriver.PathToSlug("README.MD"));
        Assert.Equal("readme", SlugDeriver.PathToSlug("README.Md"));
    }

    [Fact] // Only a TRAILING .md is dropped; an interior ".md" inside a name is slugged (dot deleted).
    public void PathToSlug_OnlyTrailingMdExtensionDropped()
    {
        // "a.md.b.md": drop the trailing .md -> "a.md.b" -> dots deleted -> "amdb".
        Assert.Equal("amdb", SlugDeriver.PathToSlug("a.md.b.md"));
    }

    [Fact] // Bare "index" (no extension or as the whole path after .md drop) collapses to "".
    public void PathToSlug_BareIndex_CollapsesToEmpty()
    {
        Assert.Equal("", SlugDeriver.PathToSlug("index.md"));
        Assert.Equal("", SlugDeriver.PathToSlug("index"));
    }

    [Fact] // Trailing "/index" collapses to the parent route (server .replace(/\/index$/, '')).
    public void PathToSlug_TrailingIndex_CollapsesToParent()
    {
        Assert.Equal("sub", SlugDeriver.PathToSlug("sub/index.md"));
        Assert.Equal("a/b", SlugDeriver.PathToSlug("a/b/index.md"));
    }

    [Theory] // Totality: degenerate input never throws (returns SOME string, possibly empty).
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    [InlineData(".md")]
    [InlineData("!!!.md")]
    public void PathToSlug_NeverThrows_OnDegenerateInput(string input)
    {
        string result = SlugDeriver.PathToSlug(input);
        Assert.NotNull(result); // total — a string (never an exception)
    }

    /// <summary>
    /// Live cross-check: the exact (key -> source vault path) pairs from
    /// <c>api/negotiate/manifest.json</c>. Re-deriving each source path with the C# port MUST
    /// reproduce the manifest key the server built — proving client and server agree on the slug
    /// for every SHIPPED page (no drift → no live-page 404).
    /// </summary>
    [Theory]
    [InlineData("My Notes Dir/page.md", "my-notes-dir/page")]
    [InlineData("My Notes.md", "my-notes")]
    [InlineData("README.md", "readme")]
    [InlineData("empty.md", "empty")]
    [InlineData("gear-guide.md", "gear-guide")]
    [InlineData("h1-only.md", "h1-only")]
    [InlineData("no-h1.md", "no-h1")]
    [InlineData("sub/index.md", "sub")]
    [InlineData("sub/page.md", "sub/page")]
    [InlineData("sub/page2.md", "sub/page2")]
    [InlineData("sub/sibling.md", "sub/sibling")]
    [InlineData("x.md", "x")]
    public void ManifestKeys_AreReproduced(string vaultPath, string manifestKey)
    {
        Assert.Equal(manifestKey, SlugDeriver.PathToSlug(vaultPath));
    }
}
