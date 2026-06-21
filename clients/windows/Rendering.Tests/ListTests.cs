using System.Linq;
using System.Windows;
using System.Windows.Documents;
using Xunit;

namespace TheMarkdownWeb.Rendering.Tests;

/// <summary>
/// AC6 — unordered lists map to a List with MarkerStyle Disc + one ListItem per item;
/// ordered lists map to a List with MarkerStyle Decimal + correct item count; nested lists
/// nest as a List inside a ListItem's blocks.
/// </summary>
public class ListTests
{
    [StaFact]
    public void UnorderedList_IsDiscMarked_WithOneItemPerLine()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("- a\n- b\n");

        List list = Assert.IsType<List>(document.Blocks.Single());
        Assert.Equal(TextMarkerStyle.Disc, list.MarkerStyle);
        Assert.Equal(2, list.ListItems.Count);
    }

    [StaFact]
    public void OrderedList_IsDecimalMarked_WithCorrectItemCount()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("1. x\n2. y\n3. z\n");

        List list = Assert.IsType<List>(document.Blocks.Single());
        Assert.Equal(TextMarkerStyle.Decimal, list.MarkerStyle);
        Assert.Equal(3, list.ListItems.Count);
    }

    [StaFact]
    public void NestedList_IsReachableInsideFirstItemsBlocks()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("- a\n  - a1\n");

        List outer = Assert.IsType<List>(document.Blocks.Single());
        ListItem first = outer.ListItems.First();

        List inner = first.Blocks.OfType<List>().Single();
        Assert.Equal(TextMarkerStyle.Disc, inner.MarkerStyle);
        Assert.Single(inner.ListItems);
    }

    [StaFact]
    public void ListItem_HoldsItemInlines()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("- hello\n");

        List list = Assert.IsType<List>(document.Blocks.Single());
        ListItem item = list.ListItems.Single();
        Assert.Contains("hello", FlowDocumentTestHelpers.ItemText(item));
    }
}
