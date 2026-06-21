using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Xunit;
using TheMarkdownWeb.Rendering;
using AppMainWindow = global::TheMarkdownWeb.App.MainWindow;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC4 (UX-DR9) — the Epic-3 EXIT a11y lock, consolidated in ONE place. Constructs the real
/// <see cref="AppMainWindow"/> (construct-not-Show; headless runner), hosts a small rendered page
/// into the window's own <c>ContentScroll</c> via a <see cref="ContentHostController"/>, and asserts
/// the FULL shell accessibility bar:
///   • toolbar Back/Forward/Reload: exact unique AutomationProperties.Name + Focusable + tab stop +
///     Back(0) -> Forward(1) -> Reload(2) order;
///   • address bar lock/input/.md-only tag: non-empty names; AddressInput Focusable + tab stop with
///     TabIndex (3) strictly AFTER Reload (2);
///   • content host ContentScroll: keyboard-reachable (Focusable + tab stop), keeping the
///     Back(0)/Forward(1)/Reload(2)/AddressInput(3) sequence undisturbed (ContentScroll optionally 4);
///   • content readability: after ShowMarkdown of a non-empty page, ContentScroll.Document is a
///     non-null FlowDocument with >= 1 block whose concatenated text is non-empty (UIA-text-readable).
///
/// RED-phase until Step 5: this assembly references <see cref="RenderTheme"/>-themed rendering and
/// the story's narrow additive ContentScroll Focusable/tab-stop XAML edit. The toolbar + address-bar
/// assertions mirror (and consolidate) the existing ToolbarAccessibilityTests + AddressBarWindowTests,
/// which stay intact and green. Reuses <see cref="ShellTestHelpers"/>; no helper changes were needed.
///
/// [StaFact] throughout — constructs a Window + FlowDocument (DispatcherObjects). No .Show(), no
/// Dispatcher pump, no socket, no real Process.Start, no pixels. DisableTestParallelization is
/// already declared in App.Tests/AssemblyInfo.cs — not re-added here.
/// </summary>
public class ShellAccessibilityExitTests
{
    private const string AddressInputName = "AddressInput";
    private const string LockIndicatorName = "LockIndicator";
    private const string MdOnlyTagName = "MdOnlyTag";
    private const string ContentScrollName = "ContentScroll";

    // A no-op image loader (returns null -> empty Image, never throws) so hosting needs no I/O.
    private sealed class NullImageLoader : IImageLoader
    {
        public ImageSource? Load(Uri absolute) => null;
    }

    [StaFact] // AC4 — toolbar + address bar + content host labeled/reachable + content UIA-readable.
    public void EpicThreeExit_ShellA11yBar_IsLockedAndContentIsReadable()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();

        // ---- Toolbar (3.1 lock): exact unique names + keyboard reachability ----------------
        Button back = RequireButton(window, ShellTestHelpers.BackButtonName);
        Button forward = RequireButton(window, ShellTestHelpers.ForwardButtonName);
        Button reload = RequireButton(window, ShellTestHelpers.ReloadButtonName);

        Assert.Equal("Back", AutomationProperties.GetName(back));
        Assert.Equal("Forward", AutomationProperties.GetName(forward));
        Assert.Equal("Reload", AutomationProperties.GetName(reload));

        var toolbarNames = new[]
        {
            AutomationProperties.GetName(back),
            AutomationProperties.GetName(forward),
            AutomationProperties.GetName(reload),
        };
        Assert.Equal(toolbarNames.Length, toolbarNames.Distinct().Count());

        foreach (Button b in new[] { back, forward, reload })
        {
            Assert.True(b.Focusable, $"{AutomationProperties.GetName(b)} must be Focusable.");
            Assert.True(KeyboardNavigation.GetIsTabStop(b),
                $"{AutomationProperties.GetName(b)} must be a tab stop.");
        }

        // Tab order Back(0) -> Forward(1) -> Reload(2).
        Assert.Equal(0, back.TabIndex);
        Assert.Equal(1, forward.TabIndex);
        Assert.Equal(2, reload.TabIndex);
        Assert.True(back.TabIndex < forward.TabIndex && forward.TabIndex < reload.TabIndex,
            "Tab order must be Back -> Forward -> Reload.");

        // ---- Address bar (3.2 lock): non-empty names + input reachable after Reload --------
        var lockIndicator = (DependencyObject)window.FindName(LockIndicatorName)!;
        var addressInput = (TextBox)window.FindName(AddressInputName)!;
        var mdOnlyTag = (DependencyObject)window.FindName(MdOnlyTagName)!;

        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(lockIndicator)),
            "LockIndicator must have a non-empty AutomationProperties.Name.");
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(addressInput)),
            "AddressInput must have a non-empty AutomationProperties.Name.");
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(mdOnlyTag)),
            "MdOnlyTag must have a non-empty AutomationProperties.Name.");

        Assert.True(addressInput.Focusable, "AddressInput must be Focusable.");
        Assert.True(KeyboardNavigation.GetIsTabStop(addressInput), "AddressInput must be a tab stop.");
        Assert.True(addressInput.TabIndex > reload.TabIndex,
            $"AddressInput.TabIndex ({addressInput.TabIndex}) must be strictly after Reload ({reload.TabIndex}).");
        Assert.Equal(3, addressInput.TabIndex); // the Back(0)/Forward(1)/Reload(2)/AddressInput(3) sequence.

        // ---- Content host (3.6 NEW lock): ContentScroll keyboard-reachable -----------------
        var contentScroll = window.FindName(ContentScrollName) as FlowDocumentScrollViewer;
        Assert.True(contentScroll is not null,
            "ContentHost must host a FlowDocumentScrollViewer named 'ContentScroll'.");
        Assert.True(contentScroll!.Focusable,
            "ContentScroll must be Focusable so a keyboard user can reach + scroll the content (narrow additive XAML edit).");
        Assert.True(KeyboardNavigation.GetIsTabStop(contentScroll),
            "ContentScroll must be a tab stop (KeyboardNavigation.IsTabStop != false).");

        // The content host comes AFTER the address bar in tab order (sequence undisturbed).
        Assert.True(contentScroll.TabIndex > addressInput.TabIndex,
            $"ContentScroll.TabIndex ({contentScroll.TabIndex}) must follow AddressInput ({addressInput.TabIndex}) — the 0/1/2/3 sequence stays intact.");

        // ---- Content readability via UIA: host a non-empty page, assert non-empty text -----
        var controller = new ContentHostController(
            contentScroll,
            new FlowDocumentRenderer(), // default ctor -> Basic theme (the no-personality default render).
            new NullImageLoader(),
            _ => Task.CompletedTask);

        controller.ShowMarkdown(
            "# Title\n\nHello world.",
            new Uri("https://themarkdownweb.com/x.md"));

        FlowDocument? document = contentScroll.Document;
        Assert.NotNull(document);
        Assert.NotEmpty(document!.Blocks); // >= 1 block for a non-empty page.

        string readableText = ConcatBlockText(document);
        Assert.False(string.IsNullOrWhiteSpace(readableText),
            "A non-empty page's hosted FlowDocument must expose non-empty text to UI Automation.");
        Assert.Contains("Title", readableText);
        Assert.Contains("Hello world.", readableText);
    }

    // ---- helpers (walk doc.Blocks directly, like ContentHostTests; Rendering.Tests helpers
    //      live in the other assembly) ----------------------------------------------------------

    private static string ConcatBlockText(FlowDocument document)
    {
        var sb = new StringBuilder();
        foreach (Block block in document.Blocks)
        {
            AppendBlock(block, sb);
        }

        return sb.ToString();
    }

    private static void AppendBlock(Block block, StringBuilder sb)
    {
        switch (block)
        {
            case Paragraph paragraph:
                AppendInlines(paragraph.Inlines, sb);
                break;
            case Section section:
                foreach (Block child in section.Blocks)
                {
                    AppendBlock(child, sb);
                }

                break;
            case List list:
                foreach (ListItem item in list.ListItems)
                {
                    foreach (Block child in item.Blocks)
                    {
                        AppendBlock(child, sb);
                    }
                }

                break;
            case Table table:
                foreach (TableRowGroup group in table.RowGroups)
                {
                    foreach (TableRow row in group.Rows)
                    {
                        foreach (TableCell cell in row.Cells)
                        {
                            foreach (Block child in cell.Blocks)
                            {
                                AppendBlock(child, sb);
                            }
                        }
                    }
                }

                break;
        }
    }

    private static void AppendInlines(InlineCollection inlines, StringBuilder sb)
    {
        foreach (Inline inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    sb.Append(run.Text);
                    break;
                case LineBreak:
                    sb.Append('\n');
                    break;
                case Span span:
                    AppendInlines(span.Inlines, sb);
                    break;
            }
        }
    }

    private static Button RequireButton(AppMainWindow window, string name)
    {
        Button? button = ShellTestHelpers.FindButton(window, name);
        Assert.True(button is not null, $"Toolbar button '{name}' was not found in MainWindow.");
        return button!;
    }
}
