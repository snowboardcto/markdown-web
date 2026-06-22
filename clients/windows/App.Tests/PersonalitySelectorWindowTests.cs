using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;
using TheMarkdownWeb.Agent;
using AppMainWindow = global::TheMarkdownWeb.App.MainWindow;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 4.2 AC1 — the toolbar personality-selector control, over the CONSTRUCTED <see cref="AppMainWindow"/>
/// (construct-not-Show; headless runner). Mirrors <see cref="AddressBarWindowTests"/>:
///   • a named <c>PersonalitySelector</c> exists in the toolbar and is a <see cref="ComboBox"/>;
///   • it has a non-empty, stable <c>AutomationProperties.Name</c> ("Personality" — NOT a bare glyph);
///   • it is <c>Focusable</c> + a tab stop;
///   • <c>TabIndex == 4</c> (the PINNED Q-Token-Tab default) and <c>&gt; AddressInput.TabIndex (3)</c>;
///   • its items reflect <see cref="PersonaRegistry.Seed"/> (count + Basic-first + the DisplayNames);
///   • the default selection is <see cref="Persona.Basic"/> (first run = the faithful basic render);
///   • REGRESSION: the nav StackPanel still contains EXACTLY Back/Forward/Reload (the selector did NOT
///     join it — Q-Placement put it in a NEW Auto column 2), and the address-bar named subtree is intact.
///
/// All <c>[StaFact]</c> — WPF objects have STA affinity; the window is never <c>.Show()</c>'d. RED until
/// Step 5 adds the <c>PersonalitySelector</c> ComboBox to MainWindow.xaml + bumps ContentScroll to TabIndex 5.
/// </summary>
public class PersonalitySelectorWindowTests
{
    private const string SelectorName = "PersonalitySelector";
    private const string AddressInputName = "AddressInput";
    private const string ContentScrollName = "ContentScroll";

    private static ComboBox RequireSelector(AppMainWindow window)
    {
        var selector = window.FindName(SelectorName) as ComboBox;
        Assert.True(selector is not null,
            "MainWindow must host a ComboBox named 'PersonalitySelector' in the toolbar.");
        return selector!;
    }

    private static IReadOnlyList<Persona> SelectorPersonas(ComboBox selector)
    {
        // The selector's items reflect PersonaRegistry.Seed (via ItemsSource or materialized Items).
        if (selector.ItemsSource is IEnumerable<Persona> source)
        {
            return source.ToList();
        }

        return selector.Items.Cast<object>().OfType<Persona>().ToList();
    }

    [StaFact] // AC1 — the named selector exists and is a ComboBox.
    public void PersonalitySelector_Exists_AndIsComboBox()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();

        ComboBox selector = RequireSelector(window);
        Assert.NotNull(selector);
    }

    [StaFact] // AC1 — the selector is labeled (a stable, non-empty AutomationProperties.Name).
    public void PersonalitySelector_HasNonEmptyAutomationName()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();
        ComboBox selector = RequireSelector(window);

        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(selector)),
            "PersonalitySelector needs a stable AutomationProperties.Name (e.g. \"Personality\") — not a bare glyph.");
    }

    [StaFact] // AC1 — the selector is keyboard-reachable (Focusable + tab stop).
    public void PersonalitySelector_IsKeyboardReachable()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();
        ComboBox selector = RequireSelector(window);

        Assert.True(selector.Focusable, "PersonalitySelector must be Focusable for keyboard reachability.");
        Assert.True(KeyboardNavigation.GetIsTabStop(selector),
            "PersonalitySelector must be a tab stop (KeyboardNavigation.IsTabStop != false).");
    }

    [StaFact] // AC1 — tab position: selector after AddressInput; ContentScroll after the selector.
    public void PersonalitySelector_TabIndex_IsAfterAddressInput_AndBeforeContentScroll()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();
        ComboBox selector = RequireSelector(window);
        var addressInput = (TextBox)window.FindName(AddressInputName)!;
        var contentScroll = (FlowDocumentScrollViewer)window.FindName(ContentScrollName)!;

        Assert.True(selector.TabIndex > addressInput.TabIndex,
            $"PersonalitySelector.TabIndex ({selector.TabIndex}) must follow AddressInput ({addressInput.TabIndex}).");
        Assert.True(contentScroll.TabIndex > selector.TabIndex,
            $"ContentScroll.TabIndex ({contentScroll.TabIndex}) must follow PersonalitySelector ({selector.TabIndex}).");
        Assert.Equal(4, selector.TabIndex); // the PINNED default (Q-Token-Tab: selector 4, ContentScroll 5).
    }

    [StaFact] // AC1 — the items reflect PersonaRegistry.Seed (count + Basic-first + DisplayNames).
    public void PersonalitySelector_Items_ReflectSeedRegistry()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();
        ComboBox selector = RequireSelector(window);

        IReadOnlyList<Persona> items = SelectorPersonas(selector);

        Assert.Equal(PersonaRegistry.Seed.Count, items.Count);
        Assert.Same(Persona.Basic, items[0]); // Basic first.
        Assert.Equal(
            PersonaRegistry.Seed.Select(p => p.DisplayName).ToArray(),
            items.Select(p => p.DisplayName).ToArray());

        // The DisplayName is the item text (DisplayMemberPath="DisplayName").
        Assert.Equal("DisplayName", selector.DisplayMemberPath);
    }

    [StaFact] // AC1 — the default selection is Basic (first run = faithful basic render, no regression).
    public void PersonalitySelector_DefaultSelection_IsBasic()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();
        ComboBox selector = RequireSelector(window);

        Assert.Same(Persona.Basic, selector.SelectedItem as Persona);
    }

    [StaFact] // AC1 regression — the nav StackPanel still has exactly Back/Forward/Reload (selector not in it).
    public void Toolbar_NavStackPanel_StillContainsExactlyBackForwardReload()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();
        Button back = RequireButton(window, ShellTestHelpers.BackButtonName);

        var ordered = ShellTestHelpers.ButtonsInToolbarOrder(back);
        Assert.Equal(3, ordered.Count);
    }

    [StaFact] // AC1 regression — the address-bar named elements are intact (Q-Placement kept the subtree byte-for-byte).
    public void AddressBar_NamedElements_StillIntact()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();

        Assert.NotNull(window.FindName("AddressBar"));
        Assert.True(window.FindName("LockIndicator") is FrameworkElement);
        Assert.True(window.FindName(AddressInputName) is TextBox);
        Assert.True(window.FindName("MdOnlyTag") is FrameworkElement);
    }

    private static Button RequireButton(AppMainWindow window, string name)
    {
        Button? button = ShellTestHelpers.FindButton(window, name);
        Assert.True(button is not null, $"Toolbar button '{name}' was not found in MainWindow.");
        return button!;
    }
}
