using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AppMainWindow = global::TheMarkdownWeb.App.MainWindow;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Shared helpers for the shell/toolbar <c>[StaFact]</c> tests. Constructs the real
/// <see cref="AppMainWindow"/> on the calling STA thread and locates the named toolbar
/// buttons via the logical-name scope (<see cref="FrameworkElement.FindName"/>).
///
/// MUST be called from an STA thread (i.e. inside a <c>[StaFact]</c>) — WPF UI objects have
/// thread affinity and constructing a <see cref="Window"/> off an STA thread throws
/// <c>InvalidOperationException</c>. The window is never <c>.Show()</c>/<c>.ShowDialog()</c>'d
/// (CI runner is headless); only synchronous, same-thread property/peer inspection is done.
/// </summary>
internal static class ShellTestHelpers
{
    internal const string BackButtonName = "BackButton";
    internal const string ForwardButtonName = "ForwardButton";
    internal const string ReloadButtonName = "ReloadButton";

    /// <summary>
    /// Constructs the real MainWindow on the current (STA) thread. Exercises that the XAML
    /// parses with no error — the headless-runner proxy for "the window opens".
    /// </summary>
    internal static AppMainWindow CreateWindow() => new AppMainWindow();

    /// <summary>
    /// Resolves a named toolbar button from the window's logical-name scope. Returns null if
    /// the name is not registered or is not a <see cref="Button"/> (red until Story 3-1 lands).
    /// </summary>
    internal static Button? FindButton(AppMainWindow window, string name)
        => window.FindName(name) as Button;

    /// <summary>
    /// Walks up the logical/visual tree from a button to the nearest container (Panel or ItemsControl
    /// such as a ToolBar) and returns the ordered list of <see cref="Button"/> children found within it,
    /// in logical/visual child order. Used to assert Back -> Forward -> Reload positional order.
    /// </summary>
    /// <summary>
    /// The <see cref="Button"/> children of the navigation StackPanel that DIRECTLY contains
    /// <paramref name="anchor"/> (the Back/Forward/Reload group in toolbar column 0). Scoped to that
    /// single StackPanel only — buttons added to OTHER toolbar columns (e.g. the Story 5.1 "Copy link"
    /// share button) are intentionally NOT counted. Use this for the "nav StackPanel still contains
    /// exactly Back/Forward/Reload" regression guards, which must stay stable as later stories add
    /// non-nav toolbar affordances.
    /// </summary>
    internal static IReadOnlyList<Button> NavStackButtons(Button anchor)
    {
        var result = new List<Button>();
        DependencyObject? parent = LogicalTreeHelper.GetParent(anchor) ?? VisualTreeHelper.GetParent(anchor);
        if (parent is null)
        {
            return result;
        }

        foreach (object child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is Button button)
            {
                result.Add(button);
            }
        }

        return result;
    }

    internal static IReadOnlyList<Button> ButtonsInToolbarOrder(Button anchor)
    {
        DependencyObject? container = FindToolbarContainer(anchor);
        var result = new List<Button>();
        if (container is null)
        {
            return result;
        }

        CollectButtons(container, result);
        return result;
    }

    private static DependencyObject? FindToolbarContainer(DependencyObject node)
    {
        DependencyObject? current = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node);
        DependencyObject? best = null;
        while (current is not null)
        {
            if (current is Panel || current is ItemsControl)
            {
                best = current;
                // Prefer the closest panel/items-host that actually contains all three buttons;
                // climb until the parent stops being a layout container.
                DependencyObject? parent = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
                if (parent is not Panel && parent is not ItemsControl)
                {
                    break;
                }
            }
            current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
        }
        return best;
    }

    private static void CollectButtons(DependencyObject node, List<Button> sink)
    {
        // Prefer logical children (stable without a layout pass); fall back to visual.
        foreach (object child in LogicalTreeHelper.GetChildren(node))
        {
            if (child is Button button)
            {
                sink.Add(button);
            }
            else if (child is DependencyObject dependencyChild)
            {
                CollectButtons(dependencyChild, sink);
            }
        }

        // Visual fallback ONLY when the logical sweep found nothing AND the node is actually a Visual.
        // A Grid's ColumnDefinition/RowDefinition objects ARE logical children of the Grid but are NOT
        // Visuals, so calling VisualTreeHelper on them throws "X is not a Visual or Visual3D". The
        // logical sweep above already collects the toolbar buttons (in the StackPanel), so this guard
        // keeps the walker robust against a Grid-based toolbar that reserves columns for later stories.
        if (sink.Count == 0 && node is Visual)
        {
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(node, i);
                if (child is Button button)
                {
                    sink.Add(button);
                }
                else
                {
                    CollectButtons(child, sink);
                }
            }
        }
    }
}
