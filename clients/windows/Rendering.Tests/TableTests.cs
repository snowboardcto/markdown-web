using System.Linq;
using System.Windows;
using System.Windows.Documents;
using Markdig;
using Markdig.Syntax;
using Xunit;

namespace TheMarkdownWeb.Rendering.Tests;

/// <summary>
/// AC9 — GFM pipe tables map to a System.Windows.Documents.Table with one TableColumn per
/// column, a header TableRow (bold/marked cells), and one body TableRow per data row.
/// </summary>
public class TableTests
{
    private const string TwoColTable =
        "| H1 | H2 |\n| --- | --- |\n| a1 | a2 |\n| b1 | b2 |\n";

    [StaFact]
    public void Table_HasCorrectColumnAndRowCounts_WithBoldHeader()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render(TwoColTable);

        Table table = Assert.IsType<Table>(document.Blocks.Single());

        Assert.Equal(2, table.Columns.Count);

        var rows = FlowDocumentTestHelpers.AllRows(table);
        Assert.Equal(3, rows.Count); // 1 header + 2 body

        // The header row's cells are bold/marked as a header.
        TableRow header = rows[0];
        Assert.All(header.Cells.Cast<TableCell>(), cell => Assert.True(
            CellIsBold(cell), "Header cell must be bold/marked as a header."));

        // A known body cell's text round-trips.
        TableCell bodyCell = rows[1].Cells[0];
        Assert.Equal("a1", FlowDocumentTestHelpers.BlockText(bodyCell.Blocks.First()).Trim());
    }

    [StaFact]
    public void Table_HeaderOnly_NoBodyRows_DoesNotThrow(/* D8 */)
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("| H1 | H2 |\n| --- | --- |\n");

        Table table = Assert.IsType<Table>(document.Blocks.Single());
        Assert.Equal(2, table.Columns.Count);
        Assert.True(FlowDocumentTestHelpers.AllRows(table).Count >= 1);
    }

    [StaFact]
    public void Table_RaggedBodyRow_DoesNotThrow(/* D8 */)
    {
        var renderer = new FlowDocumentRenderer();

        // Body row with fewer cells than the header columns (ragged).
        FlowDocument document = renderer.Render("| H1 | H2 |\n| --- | --- |\n| only |\n");

        Table table = Assert.IsType<Table>(document.Blocks.Single());
        Assert.NotNull(table);
    }

    [Fact]
    public void Markdig_ParsesPipeTable_ToTableAst_ProvesExtensionIsOn()
    {
        // Pure Markdig-AST-shape assertion (no WPF type) — proves UsePipeTables is enabled.
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        MarkdownDocument doc = Markdown.Parse(TwoColTable, pipeline);

        var astTable = doc.Descendants().OfType<Markdig.Extensions.Tables.Table>().Single();
        // 3 rows in the AST: header + 2 body.
        Assert.Equal(3, astTable.Count);
    }

    private static bool CellIsBold(TableCell cell)
    {
        // Bold may be marked on the cell itself or on its inner runs.
        if (cell.FontWeight == FontWeights.Bold)
        {
            return true;
        }

        return cell.Blocks.OfType<Paragraph>()
            .SelectMany(FlowDocumentTestHelpers.CollectRuns)
            .Any(r => r.FontWeight == FontWeights.Bold);
    }
}
