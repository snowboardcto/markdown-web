using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;
using AppMainWindow = global::TheMarkdownWeb.App.MainWindow;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC1 — the constructed shell window hosts an address-bar control in the reserved toolbar slot
/// (Grid column 1): a lock indicator, a host/path <see cref="TextBox"/> input, and an exact
/// <c>.md only</c> tag. Each sub-element carries a stable, non-empty <c>AutomationProperties.Name</c>
/// (a glyph / raw text is NOT an acceptable accessible name). The input is keyboard-reachable and its
/// effective tab position sits strictly AFTER Reload, without disturbing 3.1's Back(0)->Forward(1)->
/// Reload(2) sequence.
///
/// All <c>[StaFact]</c> — WPF objects have STA thread affinity. The window is constructed via
/// <see cref="ShellTestHelpers.CreateWindow"/> and NEVER <c>.Show()</c>'d (CI runner is headless);
/// elements are resolved synchronously from the logical-name scope via <see cref="FrameworkElement.FindName"/>.
///
/// RED until Story 3-2 (Step 5) fills the reserved column-1 slot with the named address bar.
///
/// Reuses the existing <see cref="ShellTestHelpers"/>; no helper additions were needed.
/// </summary>
public class AddressBarWindowTests
{
    private const string AddressBarName = "AddressBar";
    private const string LockIndicatorName = "LockIndicator";
    private const string AddressInputName = "AddressInput";
    private const string MdOnlyTagName = "MdOnlyTag";

    [StaFact] // AC1 — the four named address-bar elements exist in the reserved slot.
    public void AddressBar_NamedElements_Exist()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();

        Assert.NotNull(window.FindName(AddressBarName));
        Assert.True(window.FindName(LockIndicatorName) is FrameworkElement,
            "LockIndicator must exist as a FrameworkElement (a glyph/icon) in the address bar.");
        Assert.True(window.FindName(AddressInputName) is TextBox,
            "AddressInput must be a TextBox hosting the typed URL's host + path.");
        Assert.True(window.FindName(MdOnlyTagName) is FrameworkElement,
            "MdOnlyTag must exist as a visible text element in the address bar.");
    }

    [StaFact] // AC1 — the tag text is EXACTLY ".md only" (ordinal).
    public void MdOnlyTag_Text_IsExactlyDotMdOnly()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();

        string? text = window.FindName(MdOnlyTagName) switch
        {
            TextBlock tb => tb.Text,
            ContentControl cc => cc.Content as string,
            _ => null,
        };

        Assert.Equal(".md only", text);
    }

    [StaFact] // AC1 — lock, input, and tag each have a stable, non-empty accessible name.
    public void AddressBar_SubElements_HaveNonEmptyAutomationNames()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();

        var lockIndicator = (DependencyObject)window.FindName(LockIndicatorName)!;
        var addressInput = (DependencyObject)window.FindName(AddressInputName)!;
        var mdOnlyTag = (DependencyObject)window.FindName(MdOnlyTagName)!;

        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(lockIndicator)),
            "LockIndicator needs a stable human-readable AutomationProperties.Name (e.g. \"Secure\") — the glyph alone is not a name.");
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(addressInput)),
            "AddressInput needs an AutomationProperties.Name (e.g. \"Address\") so a screen reader can identify the input.");
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(mdOnlyTag)),
            "MdOnlyTag needs a non-empty AutomationProperties.Name so the tag is announced, not silent.");
    }

    [StaFact] // AC1 — the input is keyboard-reachable (focusable + tab stop).
    public void AddressInput_IsKeyboardReachable()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();
        var input = (TextBox)window.FindName(AddressInputName)!;

        Assert.True(input.Focusable, "AddressInput must be Focusable for keyboard reachability.");
        Assert.True(KeyboardNavigation.GetIsTabStop(input),
            "AddressInput must be a tab stop (KeyboardNavigation.IsTabStop != false).");
    }

    [StaFact] // AC1 — input's tab position is strictly AFTER Reload (>= 3), 3.1 sequence undisturbed.
    public void AddressInput_TabIndex_IsAfterReload()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();
        var input = (TextBox)window.FindName(AddressInputName)!;
        Button reload = RequireButton(window, ShellTestHelpers.ReloadButtonName);

        Assert.True(input.TabIndex > reload.TabIndex,
            $"AddressInput.TabIndex ({input.TabIndex}) must be strictly greater than ReloadButton.TabIndex ({reload.TabIndex}) so it follows Reload in tab order.");
    }

    [StaFact] // AC1 regression guard — building the address bar does NOT add stray nav buttons.
    public void Toolbar_NavStackPanel_StillContainsExactlyBackForwardReload()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();
        Button back = RequireButton(window, ShellTestHelpers.BackButtonName);

        var ordered = ShellTestHelpers.ButtonsInToolbarOrder(back);

        // The nav-button walker is column-0-scoped; a Go affordance (if any) must NOT join the nav StackPanel.
        Assert.Equal(3, ordered.Count);
    }

    private static Button RequireButton(AppMainWindow window, string name)
    {
        Button? button = ShellTestHelpers.FindButton(window, name);
        Assert.True(button is not null, $"Toolbar button '{name}' was not found in MainWindow.");
        return button!;
    }
}
