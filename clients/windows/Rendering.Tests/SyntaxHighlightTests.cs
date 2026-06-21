using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;
using System.Windows.Media;
using Xunit;

namespace TheMarkdownWeb.Rendering.Tests;

/// <summary>
/// Story 3.4 — code syntax highlighting. A fenced code block whose info-string names a language
/// ColorCode recognizes is tokenized and emitted as a sequence of mono <see cref="Run"/>s whose
/// <see cref="Run.Foreground"/> varies by token (the github-light palette). The 3.3 outer
/// contract is preserved: still one top-level block, still <c>Tag</c> == language, still mono
/// <c>FontFamily</c> on every run, still verbatim text (incl. newlines). Unknown/missing language
/// or <c>SyntaxHighlighting = false</c> degrades to the 3.3 single-color mono fallback, never an
/// error.
///
/// Every test that calls <c>Render(...)</c> or reads a <c>Run.Foreground</c> walks WPF
/// DispatcherObjects, so it runs on an STA thread via [StaFact]. (DisableTestParallelization is
/// already set assembly-wide from 3.3.)
/// </summary>
public class SyntaxHighlightTests
{
    // The exact github-light palette (AC2) — all full-opacity (alpha 0xFF).
    private static readonly Color Keyword = Color.FromArgb(0xFF, 0xCF, 0x22, 0x2E); // #cf222e
    private static readonly Color StringColor = Color.FromArgb(0xFF, 0x0A, 0x30, 0x69); // #0a3069
    private static readonly Color Comment = Color.FromArgb(0xFF, 0x59, 0x63, 0x6E); // #59636e
    private static readonly Color Number = Color.FromArgb(0xFF, 0x05, 0x50, 0xAE); // #0550ae

    // ---- AC1 — the colorizer plugs into MapCodeBlock; the block contract is preserved ----------

    [StaFact]
    public void KnownLanguage_PreservesBlockContract_TagMonoVerbatim()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("```csharp\nvar x = 1;\n// note\n```");

        // One top-level code block.
        Block block = Assert.Single(document.Blocks);

        // Language preserved in Tag.
        Assert.Equal("csharp", BlockTag(block));

        // Mono font on every run.
        IReadOnlyList<Run> runs = CollectBlockRuns(block);
        Assert.NotEmpty(runs);
        Assert.All(runs, r => Assert.Equal("Consolas", r.FontFamily.Source));

        // Verbatim text — the concatenated run/linebreak text reproduces the source (incl. the newline).
        string text = FlowDocumentTestHelpers.BlockText(block);
        Assert.Contains("var x = 1;", text);
        Assert.Contains("// note", text);
        Assert.Contains('\n', text);
    }

    [StaFact]
    public void Default_SyntaxHighlighting_Option_IsTrue()
    {
        var options = new FlowDocumentRenderOptions();
        Assert.True(options.SyntaxHighlighting,
            "SyntaxHighlighting must default to ON so the bedrock Render path highlights known languages.");
    }

    // ---- AC2 — exact github-light token palette ------------------------------------------------

    [StaFact]
    public void CSharp_Keyword_String_Comment_Number_CarryExactPaletteColors()
    {
        var renderer = new FlowDocumentRenderer();

        // Tokens chosen so each is unambiguous: a line comment, a string literal, an int literal,
        // and the `int` keyword.
        FlowDocument document = renderer.Render("```csharp\n// c\nint n = 1; string s = \"x\";\n```");
        Block block = Assert.Single(document.Blocks);
        IReadOnlyList<Run> runs = CollectBlockRuns(block);

        // The comment text "// c" carries the comment brush.
        AssertRunColor(runs, "// c", Comment);

        // The keyword "int" carries the keyword brush.
        AssertRunColor(runs, "int", Keyword);

        // The number literal "1" carries the number brush.
        AssertRunColor(runs, "1", Number);

        // The string literal carries the string brush. ColorCode may split the quotes/escape into
        // their own runs, so assert that the run carrying the string content "x" uses the string
        // color (rather than pinning the exact run boundaries around the quotes).
        AssertSomeRunColored(runs, "x", StringColor);
    }

    // ---- AC3 — a known-language fence carries >=2 distinct foregrounds (>1 language) ------------

    [StaFact]
    public void CSharp_KnownLanguage_HasAtLeastTwoDistinctForegrounds()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("```csharp\nvar x = \"hi\"; // c\n```");
        Block block = Assert.Single(document.Blocks);

        var foregrounds = FlowDocumentTestHelpers.DistinctForegrounds(block);
        Assert.True(foregrounds.Count >= 2,
            $"A highlighted C# fence must carry >=2 distinct foregrounds, found {foregrounds.Count}.");

        Assert.Equal("csharp", BlockTag(block));
        Assert.All(CollectBlockRuns(block), r => Assert.Equal("Consolas", r.FontFamily.Source));
        Assert.Contains("var x = \"hi\";", FlowDocumentTestHelpers.BlockText(block));
    }

    [StaFact]
    public void JavaScript_KnownLanguage_HasAtLeastTwoDistinctForegrounds()
    {
        var renderer = new FlowDocumentRenderer();

        // `js` is a GFM alias the ResolveLanguage map must resolve to ColorCode's JavaScript grammar.
        FlowDocument document = renderer.Render("```js\nconst n = 1; // c\n```");
        Block block = Assert.Single(document.Blocks);

        var foregrounds = FlowDocumentTestHelpers.DistinctForegrounds(block);
        Assert.True(foregrounds.Count >= 2,
            $"A highlighted JS fence must carry >=2 distinct foregrounds, found {foregrounds.Count}.");

        Assert.Equal("js", BlockTag(block));
        Assert.All(CollectBlockRuns(block), r => Assert.Equal("Consolas", r.FontFamily.Source));
        Assert.Contains("const n = 1;", FlowDocumentTestHelpers.BlockText(block));
    }

    // ---- AC4 — unknown / missing language → single-color mono fallback, no throw ----------------

    [StaFact]
    public void UnknownLanguage_FallsBackToSingleColorMono_NoThrow()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("```nosuchlang\nplain text\n```");
        Block block = Assert.Single(document.Blocks);

        // Unknown id is not resolvable to an ILanguage → 3.3 single-color fallback.
        var foregrounds = FlowDocumentTestHelpers.DistinctForegrounds(block);
        Assert.True(foregrounds.Count <= 1,
            $"An unknown-language fence must use a single foreground (plain mono), found {foregrounds.Count}.");

        Assert.Equal("nosuchlang", BlockTag(block));
        Assert.All(CollectBlockRuns(block), r => Assert.Equal("Consolas", r.FontFamily.Source));
        Assert.Contains("plain text", FlowDocumentTestHelpers.BlockText(block));
    }

    [StaFact]
    public void BareFence_NoLanguage_FallsBackToSingleColorMono_NoThrow()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("```\nplain\n```");
        Block block = Assert.Single(document.Blocks);

        var foregrounds = FlowDocumentTestHelpers.DistinctForegrounds(block);
        Assert.True(foregrounds.Count <= 1,
            $"A bare (no-language) fence must use a single foreground (plain mono), found {foregrounds.Count}.");

        string? tag = BlockTag(block);
        Assert.True(string.IsNullOrEmpty(tag), "A no-language fence should have a null/empty language Tag.");
        Assert.All(CollectBlockRuns(block), r => Assert.Equal("Consolas", r.FontFamily.Source));
        Assert.Contains("plain", FlowDocumentTestHelpers.BlockText(block));
    }

    // ---- AC1 (option flag) — SyntaxHighlighting = false disables coloring -----------------------

    [StaFact]
    public void HighlightingDisabled_KnownLanguage_FallsBackToSingleColorMono()
    {
        var renderer = new FlowDocumentRenderer(
            new FlowDocumentRenderOptions { SyntaxHighlighting = false });

        FlowDocument document = renderer.Render("```csharp\nvar x = 1;\nif (x) {}\n```");
        Block block = Assert.Single(document.Blocks);

        var foregrounds = FlowDocumentTestHelpers.DistinctForegrounds(block);
        Assert.True(foregrounds.Count <= 1,
            $"With SyntaxHighlighting=false a known-language fence must be single-color, found {foregrounds.Count}.");

        // Contract still preserved when highlighting is off.
        Assert.Equal("csharp", BlockTag(block));
        Assert.All(CollectBlockRuns(block), r => Assert.Equal("Consolas", r.FontFamily.Source));
        Assert.Contains("var x = 1;", FlowDocumentTestHelpers.BlockText(block));
    }

    // ---- AC5 — totality: highlighting never throws on any code content -------------------------

    [Theory]
    [InlineData("```csharp\n{ \"a\": 1 }\n```")]                 // row 5: JSON body in a C# fence
    [InlineData("```json\n{ \"a\":\n```")]                       // row 6: unterminated JSON
    [InlineData("```python\nx = \" and a  control char\n```")] // row 7: stray quote + control char
    [InlineData("```csharp\n```")]                               // row 8: empty fence body
    [InlineData("```csharp\n   \n\t\n```")]                      // row 9: whitespace-only body
    [InlineData("```csharp\nvar s = \"héllo 🚀\";\n```")] // row 11: non-ASCII + emoji surrogate pair
    [InlineData("````\na `b` c\n````")]                         // row 12: nested backticks, no language
    public void HostileInputs_DoNotThrow_AndProduceACodeBlock(string markdown)
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render(markdown);

        // A block is produced (colored or plain-mono fallback) and Render never throws.
        Block block = Assert.Single(document.Blocks);
        Assert.NotNull(block);
    }

    [StaFact]
    public void VeryLongSingleLine_DoesNotThrow()
    {
        var renderer = new FlowDocumentRenderer();
        string longLine = new string('a', 50_000);

        FlowDocument document = renderer.Render($"```csharp\nvar s = \"{longLine}\";\n```");

        Block block = Assert.Single(document.Blocks);
        Assert.Contains(longLine, FlowDocumentTestHelpers.BlockText(block));
    }

    // ---- AC2/AC3 — known aliases + casing resolve and highlight ---------------------------------

    [Theory]
    [InlineData("cs")]      // alias → csharp
    [InlineData("C#")]      // alias (case-insensitive) → csharp
    [InlineData("CSharp")]  // canonical id, mixed case → csharp
    [InlineData("js")]      // alias → javascript
    public void KnownAliases_AreHighlighted_AtLeastTwoForegrounds(string language)
    {
        var renderer = new FlowDocumentRenderer();

        // `var x = 1;` has a keyword + a number → at least the keyword brush + default = >=2.
        // (`const n = 1;` for the js case is equally keyword+number.)
        string body = language.Equals("js", System.StringComparison.OrdinalIgnoreCase)
            ? "const n = 1;"
            : "var x = 1;";
        FlowDocument document = renderer.Render($"```{language}\n{body}\n```");
        Block block = Assert.Single(document.Blocks);

        var foregrounds = FlowDocumentTestHelpers.DistinctForegrounds(block);
        Assert.True(foregrounds.Count >= 2,
            $"Alias '{language}' should resolve to a known language and highlight (>=2 foregrounds), found {foregrounds.Count}.");

        Assert.Equal(language, BlockTag(block));
        Assert.All(CollectBlockRuns(block), r => Assert.Equal("Consolas", r.FontFamily.Source));
    }

    [StaFact]
    public void InfoStringWithExtraArgs_TakesFirstWordAsLanguage_AndHighlights()
    {
        var renderer = new FlowDocumentRenderer();

        // Only the first word ("csharp") is the language; the rest is ignored.
        FlowDocument document = renderer.Render("```csharp hl_lines=\"1\"\nvar x = 1;\n```");
        Block block = Assert.Single(document.Blocks);

        Assert.Equal("csharp", BlockTag(block));
        var foregrounds = FlowDocumentTestHelpers.DistinctForegrounds(block);
        Assert.True(foregrounds.Count >= 2,
            $"A 'csharp <extra args>' fence should highlight on the first word, found {foregrounds.Count}.");
    }

    // ---- helpers -------------------------------------------------------------------------------

    /// <summary>Asserts the run whose Text == <paramref name="text"/> has a SolidColorBrush of the given color.</summary>
    private static void AssertRunColor(IReadOnlyList<Run> runs, string text, Color expected)
    {
        Run? run = runs.FirstOrDefault(r => r.Text == text);
        Assert.True(run is not null, $"Expected a run with exact text \"{text}\" in the highlighted block.");
        var brush = Assert.IsType<SolidColorBrush>(run!.Foreground);
        Assert.Equal(expected, brush.Color);
    }

    /// <summary>
    /// Asserts SOME run whose Text contains <paramref name="substring"/> carries the given color
    /// (tolerant of how the tokenizer splits a multi-part token into adjacent runs).
    /// </summary>
    private static void AssertSomeRunColored(IReadOnlyList<Run> runs, string substring, Color expected)
    {
        bool found = runs.Any(r =>
            r.Text.Contains(substring)
            && r.Foreground is SolidColorBrush brush
            && brush.Color == expected);
        Assert.True(found,
            $"Expected at least one run containing \"{substring}\" to carry color {expected}.");
    }

    private static string? BlockTag(Block block) => block switch
    {
        Paragraph p => p.Tag as string,
        Section s => s.Tag as string,
        _ => null,
    };

    private static IReadOnlyList<Run> CollectBlockRuns(Block block)
    {
        return block switch
        {
            Paragraph p => FlowDocumentTestHelpers.CollectRuns(p),
            Section s => s.Blocks.OfType<Paragraph>()
                .SelectMany(FlowDocumentTestHelpers.CollectRuns).ToList(),
            _ => new List<Run>(),
        };
    }
}
