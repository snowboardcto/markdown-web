using System.Collections.Generic;
using System.Windows.Controls;
using Xunit;
using AppMainWindow = global::TheMarkdownWeb.App.MainWindow;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC1 + AC2: the constructed shell window has the exact titlebar text and a top toolbar
/// containing Back / Forward / Reload buttons, present and in order.
///
/// These are red-phase tests: today MainWindow is an empty Grid with no named buttons, so the
/// AC2 assertions fail until the Story 3-1 toolbar is implemented (Step 5). All tests run on an
/// STA thread (<c>[StaFact]</c>) and construct but never <c>.Show()</c> the window.
/// </summary>
public class ShellWindowTests
{
    [StaFact] // AC1 — single native WPF window, exact titlebar text.
    public void MainWindow_Title_IsExactlyTheMarkdownWeb()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();

        // Ordinal, case-sensitive: NOT Contains, NOT case-insensitive. No page-name suffix at this story.
        Assert.Equal("The Markdown Web", window.Title);
    }

    [StaFact] // AC2 — Back / Forward / Reload present and are Buttons.
    public void Toolbar_HasBackForwardReloadButtons()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();

        Button? back = ShellTestHelpers.FindButton(window, ShellTestHelpers.BackButtonName);
        Button? forward = ShellTestHelpers.FindButton(window, ShellTestHelpers.ForwardButtonName);
        Button? reload = ShellTestHelpers.FindButton(window, ShellTestHelpers.ReloadButtonName);

        Assert.NotNull(back);
        Assert.NotNull(forward);
        Assert.NotNull(reload);
    }

    [StaFact] // AC2 — buttons appear in Back -> Forward -> Reload order within the toolbar container.
    public void Toolbar_Buttons_AreInBackForwardReloadOrder()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();

        Button? back = ShellTestHelpers.FindButton(window, ShellTestHelpers.BackButtonName);
        Button? forward = ShellTestHelpers.FindButton(window, ShellTestHelpers.ForwardButtonName);
        Button? reload = ShellTestHelpers.FindButton(window, ShellTestHelpers.ReloadButtonName);

        Assert.NotNull(back);
        Assert.NotNull(forward);
        Assert.NotNull(reload);

        IReadOnlyList<Button> ordered = ShellTestHelpers.ButtonsInToolbarOrder(back!);

        int backIndex = IndexOf(ordered, back!);
        int forwardIndex = IndexOf(ordered, forward!);
        int reloadIndex = IndexOf(ordered, reload!);

        Assert.True(backIndex >= 0, "BackButton not found within its toolbar container.");
        Assert.True(forwardIndex >= 0, "ForwardButton not found within its toolbar container.");
        Assert.True(reloadIndex >= 0, "ReloadButton not found within its toolbar container.");

        Assert.True(
            backIndex < forwardIndex && forwardIndex < reloadIndex,
            $"Toolbar buttons must be ordered Back -> Forward -> Reload, but positional indices were " +
            $"Back={backIndex}, Forward={forwardIndex}, Reload={reloadIndex}.");
    }

    private static int IndexOf(IReadOnlyList<Button> buttons, Button target)
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            if (ReferenceEquals(buttons[i], target))
            {
                return i;
            }
        }
        return -1;
    }
}
