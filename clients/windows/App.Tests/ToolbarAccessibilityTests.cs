using System.Collections.Generic;
using System.Linq;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;
using AppMainWindow = global::TheMarkdownWeb.App.MainWindow;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC3 [DERIVED — NFR-6 / UX-DR9 accessibility floor]: each toolbar button exposes a stable,
/// exact UI-Automation name, the names are unique, every button is keyboard-reachable, and the
/// effective tab order is Back -> Forward -> Reload.
///
/// Red-phase until Story 3-1 sets <c>AutomationProperties.Name</c> and names the buttons.
/// All STA tests construct but never <c>.Show()</c> the window.
/// </summary>
public class ToolbarAccessibilityTests
{
    [StaFact] // AC3 — exact, unique automation names (glyph is NOT an acceptable name).
    public void Toolbar_Buttons_HaveExactUniqueAutomationNames()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();

        Button back = RequireButton(window, ShellTestHelpers.BackButtonName);
        Button forward = RequireButton(window, ShellTestHelpers.ForwardButtonName);
        Button reload = RequireButton(window, ShellTestHelpers.ReloadButtonName);

        // Ordinal, case-sensitive equality on AutomationProperties.GetName.
        Assert.Equal("Back", AutomationProperties.GetName(back));
        Assert.Equal("Forward", AutomationProperties.GetName(forward));
        Assert.Equal("Reload", AutomationProperties.GetName(reload));

        var names = new[]
        {
            AutomationProperties.GetName(back),
            AutomationProperties.GetName(forward),
            AutomationProperties.GetName(reload),
        };
        Assert.Equal(names.Length, names.Distinct().Count()); // names unique within the toolbar.
    }

    [StaFact] // AC3 — each button is keyboard-reachable (focusable + tab stop).
    public void Toolbar_Buttons_AreKeyboardReachable()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();

        foreach (string name in new[]
                 {
                     ShellTestHelpers.BackButtonName,
                     ShellTestHelpers.ForwardButtonName,
                     ShellTestHelpers.ReloadButtonName,
                 })
        {
            Button button = RequireButton(window, name);
            Assert.True(button.Focusable, $"{name} must be Focusable for keyboard reachability.");
            Assert.True(
                KeyboardNavigation.GetIsTabStop(button),
                $"{name} must be a tab stop (KeyboardNavigation.IsTabStop != false).");
        }
    }

    [StaFact] // AC3 — effective tab order is Back -> Forward -> Reload.
    public void Toolbar_TabOrder_IsBackForwardReload()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();

        Button back = RequireButton(window, ShellTestHelpers.BackButtonName);
        Button forward = RequireButton(window, ShellTestHelpers.ForwardButtonName);
        Button reload = RequireButton(window, ShellTestHelpers.ReloadButtonName);

        // Effective tab sequence within the toolbar container: order by TabIndex when any button
        // sets it explicitly; otherwise fall back to logical/visual child order.
        IReadOnlyList<Button> containerOrder = ShellTestHelpers.ButtonsInToolbarOrder(back);
        var triple = new[] { back, forward, reload };

        bool anyExplicitTabIndex = triple.Any(b => b.TabIndex != int.MaxValue);

        List<Button> effective = anyExplicitTabIndex
            ? triple
                .OrderBy(b => b.TabIndex)
                .ThenBy(b => PositionalIndex(containerOrder, b))
                .ToList()
            : triple
                .OrderBy(b => PositionalIndex(containerOrder, b))
                .ToList();

        Assert.Equal(new[] { back, forward, reload }, effective);
    }

    private static int PositionalIndex(IReadOnlyList<Button> ordered, Button target)
    {
        for (int i = 0; i < ordered.Count; i++)
        {
            if (ReferenceEquals(ordered[i], target))
            {
                return i;
            }
        }
        return int.MaxValue;
    }

    private static Button RequireButton(AppMainWindow window, string name)
    {
        Button? button = ShellTestHelpers.FindButton(window, name);
        Assert.True(button is not null, $"Toolbar button '{name}' was not found in MainWindow (Story 3-1 not yet implemented?).");
        return button!;
    }
}
