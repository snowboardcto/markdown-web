using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;
using AppMainWindow = global::TheMarkdownWeb.App.MainWindow;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 6.1 AC3 — <c>[StaFact]</c> tests for the Home toolbar button (construct-not-Show).
/// Mirrors the <see cref="AddressBarWindowTests"/> / <see cref="PersonalitySelectorWindowTests"/>
/// convention: construct the real <see cref="AppMainWindow"/>, never <c>.Show()</c> it, inspect
/// synchronously on the STA thread via <c>FindName</c>.
/// </summary>
public class HomeButtonWindowTests
{
    private const string HomeButtonName = "HomeButton";

    [StaFact]
    public void HomeButton_Exists_InMainWindow()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();

        var homeButton = window.FindName(HomeButtonName) as Button;
        Assert.True(homeButton is not null,
            "MainWindow must host a Button named 'HomeButton' in the toolbar (Story 6.1 AC3).");
    }

    [StaFact]
    public void HomeButton_HasNonEmptyAutomationName()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();
        var homeButton = (Button)window.FindName(HomeButtonName)!;

        string name = AutomationProperties.GetName(homeButton);
        Assert.False(string.IsNullOrWhiteSpace(name),
            "HomeButton must have a non-empty AutomationProperties.Name (e.g. \"Home\").");
        Assert.Equal("Home", name);
    }

    [StaFact]
    public void HomeButton_IsFocusable_AndIsTabStop()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();
        var homeButton = (Button)window.FindName(HomeButtonName)!;

        Assert.True(homeButton.Focusable, "HomeButton must be Focusable for keyboard reachability.");
        Assert.True(KeyboardNavigation.GetIsTabStop(homeButton),
            "HomeButton must be a tab stop.");
    }

    [StaFact]
    public void HomeButton_TabIndex_IsAfterShareLinkButton()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();
        var homeButton = (Button)window.FindName(HomeButtonName)!;
        var shareButton = (Button)window.FindName("ShareLinkButton")!;

        Assert.True(homeButton.TabIndex > shareButton.TabIndex,
            $"HomeButton.TabIndex ({homeButton.TabIndex}) must be strictly greater than " +
            $"ShareLinkButton.TabIndex ({shareButton.TabIndex}).");
    }

    [StaFact]
    public void HomeButton_TabIndex_IsBeforeContentScroll()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();
        var homeButton = (Button)window.FindName(HomeButtonName)!;
        var contentScroll = (FlowDocumentScrollViewer)window.FindName("ContentScroll")!;

        Assert.True(contentScroll.TabIndex > homeButton.TabIndex,
            $"ContentScroll.TabIndex ({contentScroll.TabIndex}) must be strictly greater than " +
            $"HomeButton.TabIndex ({homeButton.TabIndex}).");
    }

    [StaFact]
    public void NavStackPanel_StillContainsExactlyThreeButtons_HomeButtonInSeparateColumn()
    {
        // Story 6.1 DECIDE-AND-DOCUMENT: Home is placed in its OWN column (col 5), NOT in the
        // nav StackPanel, so the three nav-count guards across AddressBarWindowTests,
        // PersonalitySelectorWindowTests, and ShareLinkBuilderTests remain at exactly 3.
        AppMainWindow window = ShellTestHelpers.CreateWindow();
        Button back = RequireButton(window, ShellTestHelpers.BackButtonName);

        var navButtons = ShellTestHelpers.NavStackButtons(back);
        Assert.Equal(3, navButtons.Count);

        // Confirm the Home button exists outside the nav StackPanel (different column).
        var homeButton = (Button)window.FindName(HomeButtonName)!;
        Assert.NotNull(homeButton);
    }

    private static Button RequireButton(AppMainWindow window, string name)
    {
        Button? button = ShellTestHelpers.FindButton(window, name);
        Assert.True(button is not null, $"Button '{name}' must exist in the toolbar.");
        return button!;
    }
}
