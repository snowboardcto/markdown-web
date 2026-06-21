using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls; // TextMarkerStyle
using System.Windows.Documents;
using System.Windows.Media;
using Xunit;

namespace TheMarkdownWeb.Rendering.Tests;

/// <summary>
/// Story 3.6 — the faithful basic (GitHub-light) default theme, asserted ADDITIVELY on the
/// produced <see cref="FlowDocument"/>. Each [StaFact] reads the EXACT styled value off the
/// rendered logical tree (no pixels, no Measure/Arrange) AND re-confirms the relevant 3.3/3.4
/// marker so the theme provably stays additive:
///   • the COLOR tokens are asserted EXACT (full-opacity ARGB) against the DESIGN.md / github.css
///     :root tokens — border #d1d9e0, muted #59636e, code-bg #f6f8fa, link #0969da;
///   • the SPACING/indent tokens are asserted as positive (> 0 / > body) per the story's contract
///     (the exact magnitudes are the implementer's latitude — only the inequalities are pinned).
///
/// RED-phase until Step 5 adds <see cref="RenderTheme"/>, <c>FlowDocumentRenderOptions.Theme</c>,
/// and the Basic-theme styling in <see cref="FlowDocumentRenderer"/>. These tests reference the new
/// <see cref="RenderTheme"/> enum, so the Rendering.Tests assembly will not compile until the theme
/// API exists — expected red, identical to prior stories.
///
/// STA: every test constructs a FlowDocument and reads Brush/Margin/LineHeight/Background/
/// BorderThickness/Padding (DispatcherObject properties) → [StaFact]. Xunit.StaFact +
/// [assembly: CollectionBehavior(DisableTestParallelization = true)] are already present.
/// </summary>
public class BasicThemeTests
{
    // ---- DESIGN.md / github.css :root tokens → exact full-opacity WPF colors ------------------
    private static readonly Color Border = Color.FromRgb(0xD1, 0xD9, 0xE0); // colors.border  #d1d9e0
    private static readonly Color Muted = Color.FromRgb(0x59, 0x63, 0x6E);  // colors.muted   #59636e
    private static readonly Color CodeBg = Color.FromRgb(0xF6, 0xF8, 0xFA); // colors.code-bg #f6f8fa
    private static readonly Color Link = Color.FromRgb(0x09, 0x69, 0xDA);   // colors.link    #0969da

    // ---- AC2/AC3 — the no-personality default IS the faithful basic render --------------------

    [Fact] // AC2/AC3 — the default Theme is Basic with no personality (plain CLR enum, no WPF type).
    public void RenderOptions_DefaultTheme_IsBasic()
    {
        var options = new FlowDocumentRenderOptions();

        Assert.Equal(RenderTheme.Basic, options.Theme);
    }

    [Fact] // AC3 — Basic is the only member 3.6 ships (the Epic-4 override seam starts single-valued).
    public void RenderTheme_Basic_IsTheOnlyMember()
    {
        RenderTheme[] members = (RenderTheme[])System.Enum.GetValues(typeof(RenderTheme));

        Assert.Equal(new[] { RenderTheme.Basic }, members);
    }

    [Fact] // AC3 — explicitly selecting Basic is equivalent to the default (the seam's default path).
    public void ExplicitBasic_EqualsDefault_Theme()
    {
        var defaulted = new FlowDocumentRenderOptions();
        var explicitBasic = new FlowDocumentRenderOptions { Theme = RenderTheme.Basic };

        Assert.Equal(defaulted.Theme, explicitBasic.Theme);
        Assert.Equal(RenderTheme.Basic, explicitBasic.Theme);
    }

    [Fact] // AC2 — the existing font defaults are UNCHANGED (RenderOptions_DefaultsAreConsolasAndSegoeUi stays green).
    public void RenderOptions_FontDefaults_AreUnchanged()
    {
        var options = new FlowDocumentRenderOptions();

        Assert.Equal("Consolas", options.MonospaceFontFamily);
        Assert.Equal("Segoe UI", options.BodyFontFamily);
        Assert.True(options.SyntaxHighlighting);
    }

    [StaFact] // AC2 — the DEFAULT renderer (the exact ctor ContentHostController uses) renders Basic — no opt-in.
    public void DefaultRenderer_RendersFaithfulBasicTheme_NoOptIn()
    {
        // new FlowDocumentRenderer() == new FlowDocumentRenderer(new FlowDocumentRenderOptions())
        // and the default options carry Theme==Basic, so the default render IS the faithful basic render.
        var defaultRenderer = new FlowDocumentRenderer();

        FlowDocument document = defaultRenderer.Render("# H\n\npara");

        // A heading carries the Basic h1 bottom hairline (proof the theme applied with no opt-in).
        Paragraph h1 = document.Blocks.OfType<Paragraph>().First(p => (p.Tag as string) == "h1");
        Assert.True(h1.BorderThickness.Bottom > 0, "Default (Basic) render must give h1 a bottom hairline.");
        AssertExactColor(Border, h1.BorderBrush);

        // A body paragraph carries the Basic block spacing (proof the body style applied by default).
        Paragraph body = document.Blocks.OfType<Paragraph>().First(p => p.Tag is null);
        Assert.True(body.Margin.Bottom > 0, "Default (Basic) render must give body paragraphs block spacing.");
    }

    // ---- AC1 — body line-height + block spacing -----------------------------------------------

    [StaFact] // AC1 — body paragraphs carry a positive bottom margin (block rhythm) + a line-height > body size.
    public void BodyParagraph_HasBlockSpacing_AndLineHeightAboveBodyFontSize()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("para one\n\npara two");

        var bodyParagraphs = document.Blocks.OfType<Paragraph>().Where(p => p.Tag is null).ToList();
        Assert.NotEmpty(bodyParagraphs);

        foreach (Paragraph body in bodyParagraphs)
        {
            // spacing.block (1em) -> a positive bottom margin between blocks.
            Assert.True(body.Margin.Bottom > 0,
                "Body paragraphs must carry a positive bottom margin (spacing.block) for vertical rhythm.");

            // typography.line (1.6) -> a LineHeight strictly above the 14px body size (~22.4).
            // LineHeight defaults to NaN (auto); a themed line-height is a real number > body size.
            Assert.False(double.IsNaN(body.LineHeight), "Body paragraphs must set an explicit LineHeight (not auto).");
            Assert.True(body.LineHeight > document.FontSize,
                $"Body LineHeight ({body.LineHeight}) must exceed the body font size ({document.FontSize}).");
        }

        // Marker preserved: body paragraphs are still Tag==null (ParagraphTests' 3.3 marker holds).
        Assert.All(bodyParagraphs, p => Assert.Null(p.Tag));
    }

    // ---- AC1 — heading spacing + h1/h2 bottom hairline ----------------------------------------

    [StaFact] // AC1 — h1/h2 carry a bottom hairline (#d1d9e0) + spacing, while Tag/Bold/FontSize stay 3.3.
    public void Headings_HaveBottomHairline_AndKeepMarkers()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("# H1\n\n## H2\n\n### H3");

        var headings = document.Blocks.OfType<Paragraph>().ToList();
        Paragraph h1 = headings.Single(p => (p.Tag as string) == "h1");
        Paragraph h2 = headings.Single(p => (p.Tag as string) == "h2");
        Paragraph h3 = headings.Single(p => (p.Tag as string) == "h3");

        // h1 + h2 carry the GitHub bottom hairline: BorderThickness.Bottom > 0 + BorderBrush == border.
        Assert.True(h1.BorderThickness.Bottom > 0, "h1 must carry a bottom hairline (BorderThickness.Bottom > 0).");
        Assert.True(h2.BorderThickness.Bottom > 0, "h2 must carry a bottom hairline (BorderThickness.Bottom > 0).");
        AssertExactColor(Border, h1.BorderBrush);
        AssertExactColor(Border, h2.BorderBrush);

        // Heading spacing: a positive top or bottom margin (spacing.section) on the headings.
        Assert.True(h1.Margin.Top > 0 || h1.Margin.Bottom > 0, "Headings must carry GitHub-style spacing.");

        // 3.3 markers UNCHANGED (this test doubles as a regression guard under the theme):
        Assert.Equal(FontWeights.Bold, h1.FontWeight);
        Assert.Equal(FontWeights.Bold, h2.FontWeight);
        Assert.Equal(FontWeights.Bold, h3.FontWeight);
        Assert.Equal(30.0, h1.FontSize);
        Assert.Equal(24.0, h2.FontSize);
        Assert.Equal(20.0, h3.FontSize);
        Assert.True(h1.FontSize > h2.FontSize && h2.FontSize > h3.FontSize, "Heading sizes stay strictly monotonic.");
        Assert.True(h3.FontSize > document.FontSize, "Every heading stays larger than the body font size.");
    }

    // ---- AC1 — hr hairline recolor ------------------------------------------------------------

    [StaFact] // AC1 — the hr paragraph (Tag=="hr") is recolored to the #d1d9e0 hairline (was #EAECEF).
    public void ThematicBreak_IsRecoloredToGithubHairline_AndKeepsTag()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("---");

        Paragraph hr = document.Blocks.OfType<Paragraph>().Single(p => (p.Tag as string) == "hr");

        // 3.3 markers UNCHANGED: Tag=="hr" + a bottom rule.
        Assert.Equal("hr", hr.Tag as string);
        Assert.True(hr.BorderThickness.Bottom > 0, "hr must keep a bottom rule (BorderThickness.Bottom > 0).");

        // 3.6 refines ONLY the color to the github hairline #d1d9e0.
        AssertExactColor(Border, hr.BorderBrush);
    }

    // ---- AC1 — blockquote muted text + left rule ----------------------------------------------

    [StaFact] // AC1 — blockquote keeps its left rule + non-null brush, ADDs muted (#59636e) quoted text.
    public void Blockquote_HasMutedText_AndKeepsLeftRule()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("> quoted");

        Section section = Assert.IsType<Section>(document.Blocks.Single());

        // 3.3 markers UNCHANGED: left rule + a non-null border brush.
        Assert.True(section.BorderThickness.Left > 0, "Blockquote must keep its left rule (BorderThickness.Left > 0).");
        Assert.NotNull(section.BorderBrush);

        // 3.6 ADDs the muted (#59636e) foreground on the quoted text.
        Paragraph inner = section.Blocks.OfType<Paragraph>().Single();
        Brush? mutedForeground = MutedForeground(section, inner);
        Assert.True(mutedForeground is not null,
            "Blockquote must apply a muted foreground (#59636e) on the quoted text — on the Section, the inner Paragraph, or its runs.");
        AssertExactColor(Muted, mutedForeground);

        // Text round-trips (BlockquoteTests' marker holds).
        Assert.Equal("quoted", FlowDocumentTestHelpers.ParagraphText(inner));
    }

    // ---- AC1 — table cell borders + header shade + cell padding -------------------------------

    [StaFact] // AC1 — table cells carry a #d1d9e0 hairline + padding; the header is shaded #f6f8fa + stays bold.
    public void Table_HasCellBorders_HeaderShade_AndPadding_KeepsStructure()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render(
            "| H1 | H2 |\n| --- | --- |\n| a1 | a2 |\n| b1 | b2 |\n");

        var table = Assert.IsType<System.Windows.Documents.Table>(document.Blocks.Single());

        // 3.3 structure UNCHANGED: 2 columns, 3 rows (1 header + 2 body).
        Assert.Equal(2, table.Columns.Count);
        IReadOnlyList<TableRow> rows = FlowDocumentTestHelpers.AllRows(table);
        Assert.Equal(3, rows.Count);

        TableRow headerRow = rows[0];
        TableCell headerCell = headerRow.Cells[0];
        TableCell bodyCell = rows[1].Cells[0];

        // Header shade #f6f8fa, applied on the header row and/or its cells.
        Brush? headerShade = HeaderShade(headerRow, headerCell);
        Assert.True(headerShade is not null, "A header row/cell must carry the #f6f8fa header shade.");
        AssertExactColor(CodeBg, headerShade);

        // Cell hairline border #d1d9e0 + a positive thickness on a body cell.
        Assert.NotNull(bodyCell.BorderBrush);
        Assert.True(MaxSide(bodyCell.BorderThickness) > 0, "Table cells must carry a hairline border (thickness > 0).");
        AssertExactColor(Border, bodyCell.BorderBrush);

        // Cell padding > 0 on a body cell.
        Assert.True(MaxSide(bodyCell.Padding) > 0, "Table cells must carry positive padding.");

        // Header stays bold (TableTests' marker holds, on the cell or its runs).
        Assert.True(CellIsBold(headerCell), "The header cell must stay bold.");
    }

    // ---- AC1 — link color ---------------------------------------------------------------------

    [StaFact] // AC1 — a hyperlink's foreground is the github link color #0969da, NavigateUri UNCHANGED.
    public void Hyperlink_HasGithubLinkColor_AndKeepsNavigateUri()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("[t](https://x.example/a.md)");

        Paragraph paragraph = document.Blocks.OfType<Paragraph>().First();
        Hyperlink? link = FlowDocumentTestHelpers.FirstHyperlink(paragraph);
        Assert.True(link is not null, "rendered document must contain a Hyperlink.");

        AssertExactColor(Link, link!.Foreground);

        // 3.5 navigation behavior UNCHANGED: the renderer still records the href inertly.
        Assert.NotNull(link.NavigateUri);
        Assert.Equal("https://x.example/a.md", link.NavigateUri!.OriginalString);
    }

    // ---- AC1 — list indentation ---------------------------------------------------------------

    [StaFact] // AC1 — lists carry a positive left indent; MarkerStyle UNCHANGED.
    public void List_HasPositiveIndent_AndKeepsMarkerStyle()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("- a\n- b");

        var list = Assert.IsType<List>(document.Blocks.Single());

        // GitHub-style indent: a positive MarkerOffset and/or a positive left Padding/Margin.
        bool hasPositiveIndent =
            list.MarkerOffset > 0
            || list.Padding.Left > 0
            || list.Margin.Left > 0;
        Assert.True(hasPositiveIndent,
            $"List must carry a positive left indent (MarkerOffset={list.MarkerOffset}, Padding.Left={list.Padding.Left}, Margin.Left={list.Margin.Left}).");

        // 3.3 marker UNCHANGED: an unordered list stays a Disc bullet list.
        Assert.Equal(TextMarkerStyle.Disc, list.MarkerStyle);
        Assert.Equal(2, list.ListItems.Count);
    }

    // ---- AC1 / G1 — code-block container background (the 3.4 deferral lands) -------------------

    [StaFact] // AC1 (G1) — code block gets a CONTAINER background #f6f8fa + padding, with 3.4 foregrounds intact.
    public void CodeBlock_HasContainerBackgroundAndPadding_AndKeeps3Point4Markers()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("```csharp\nvar x = 1;\n// c\n```");

        // 3.4: still ONE top-level code block (no shape change).
        Block block = Assert.Single(document.Blocks);
        Paragraph code = Assert.IsType<Paragraph>(block);

        // G1: the #f6f8fa fill is a BLOCK (Paragraph.Background) — NOT a per-run background.
        AssertExactColor(CodeBg, code.Background);

        // Padding around the code at the container level.
        Assert.True(MaxSide(code.Padding) > 0, "The code block container must carry positive padding.");

        // 3.4 markers SURVIVE the background — this is the guardrail proof:
        //   • Tag is the language.
        Assert.Equal("csharp", code.Tag as string);
        //   • mono FontFamily on every run.
        IReadOnlyList<Run> runs = FlowDocumentTestHelpers.CollectRuns(code);
        Assert.NotEmpty(runs);
        Assert.All(runs, r => Assert.Equal("Consolas", r.FontFamily.Source));
        //   • verbatim text incl. the newline.
        string text = FlowDocumentTestHelpers.BlockText(block);
        Assert.Contains("var x = 1;", text);
        Assert.Contains('\n', text);
        //   • G1: DistinctForegrounds (which read run.Foreground ONLY) still >= 2 — the
        //     container Background did NOT recolor any run, so highlighting is untouched.
        IReadOnlyList<Brush> foregrounds = FlowDocumentTestHelpers.DistinctForegrounds(block);
        Assert.True(foregrounds.Count >= 2,
            $"Syntax-highlight foregrounds must survive the container background (>=2 distinct), found {foregrounds.Count}.");
    }

    // ---- shared assertion helpers -------------------------------------------------------------

    /// <summary>
    /// Asserts <paramref name="brush"/> is a full-opacity <see cref="SolidColorBrush"/> of exactly
    /// <paramref name="expected"/> (the DESIGN.md token). Full opacity == Color.FromArgb(0xFF, r,g,b).
    /// </summary>
    private static void AssertExactColor(Color expected, Brush? brush)
    {
        var solid = Assert.IsType<SolidColorBrush>(brush);
        Color full = Color.FromArgb(0xFF, expected.R, expected.G, expected.B);
        Assert.Equal(full, solid.Color);
        Assert.Equal((byte)0xFF, solid.Color.A); // explicitly full-opacity.
    }

    private static double MaxSide(Thickness thickness)
        => System.Math.Max(
            System.Math.Max(thickness.Left, thickness.Top),
            System.Math.Max(thickness.Right, thickness.Bottom));

    /// <summary>The muted blockquote foreground, accepting it on the Section, the inner Paragraph, or any run.</summary>
    private static Brush? MutedForeground(Section section, Paragraph inner)
    {
        if (section.Foreground is not null)
        {
            return section.Foreground;
        }

        if (inner.Foreground is not null)
        {
            return inner.Foreground;
        }

        return FlowDocumentTestHelpers.CollectRuns(inner)
            .Select(r => r.Foreground)
            .FirstOrDefault(b => b is not null);
    }

    /// <summary>The header shade, accepting it on the header row or any header cell background.</summary>
    private static Brush? HeaderShade(TableRow headerRow, TableCell headerCell)
    {
        if (headerRow.Background is not null)
        {
            return headerRow.Background;
        }

        return headerRow.Cells.Cast<TableCell>()
            .Select(c => c.Background)
            .FirstOrDefault(b => b is not null)
            ?? headerCell.Background;
    }

    private static bool CellIsBold(TableCell cell)
    {
        if (cell.FontWeight == FontWeights.Bold)
        {
            return true;
        }

        return cell.Blocks.OfType<Paragraph>()
            .SelectMany(FlowDocumentTestHelpers.CollectRuns)
            .Any(r => r.FontWeight == FontWeights.Bold);
    }
}
