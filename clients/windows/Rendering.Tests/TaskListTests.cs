using System.Linq;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;

namespace TheMarkdownWeb.Rendering.Tests;

/// <summary>
/// AC7 — GFM task lists. Each task item carries a read-only checkbox reflecting its checked
/// state. The marker contract accepts EITHER representation: an inline read-only CheckBox
/// (IsChecked + IsEnabled==false) OR a glyph (☑ checked / ☐ unchecked). The helpers below
/// assert whichever the impl chose by checking both forms.
/// </summary>
public class TaskListTests
{
    [StaFact]
    public void TaskList_ItemsCarryCheckedAndUncheckedMarkers()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("- [ ] todo\n- [x] done\n");

        List list = Assert.IsType<List>(document.Blocks.Single());
        Assert.Equal(2, list.ListItems.Count);

        ListItem unchecked_ = list.ListItems.ElementAt(0);
        ListItem checked_ = list.ListItems.ElementAt(1);

        Assert.True(
            FlowDocumentTestHelpers.ItemHasUncheckedMarker(unchecked_),
            "First task item must carry an unchecked marker (CheckBox.IsChecked==false or ☐).");
        Assert.True(
            FlowDocumentTestHelpers.ItemHasCheckedMarker(checked_),
            "Second task item must carry a checked marker (CheckBox.IsChecked==true or ☑).");
    }

    [StaFact]
    public void TaskList_AnyCheckBox_IsReadOnly()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("- [ ] todo\n- [x] done\n");

        List list = Assert.IsType<List>(document.Blocks.Single());

        foreach (ListItem item in list.ListItems)
        {
            CheckBox? checkBox = FlowDocumentTestHelpers.FindCheckBox(item);
            if (checkBox is not null)
            {
                // If the impl uses a real CheckBox, it must be read-only at this story.
                Assert.False(checkBox.IsEnabled, "Task-list CheckBox must be read-only (IsEnabled==false).");
            }
        }
    }
}
