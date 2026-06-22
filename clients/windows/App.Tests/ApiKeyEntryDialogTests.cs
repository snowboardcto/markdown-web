using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;
using TheMarkdownWeb.Agent;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 4.2 AC4 (UX-DR9) — the API-key entry dialog, CONSTRUCTED but never <c>.Show()</c>/<c>.ShowDialog()</c>'d
/// (headless runner). Mirrors the address-bar a11y [StaFact] discipline:
///   • the dialog constructs (its XAML parses) over an in-memory <see cref="ISecretStore"/>;
///   • a named key input (<c>ApiKeyInput</c>) exists, carries a non-empty <c>AutomationProperties.Name</c>
///     (e.g. "API key"), and is <c>Focusable</c> + a tab stop;
///   • a disclosure element is present (the BYO-key disclosure text — non-empty).
///
/// INTENDED API CONTRACT (Step-5 implementer must match; namespace TheMarkdownWeb.App):
///
///   public partial class ApiKeyEntryDialog : Window {
///       public ApiKeyEntryDialog(ApiKeyPromptViewModel viewModel); }   // or (ISecretStore store)
///
/// The named key input must be x:Name="ApiKeyInput" with AutomationProperties.Name set, and a named
/// disclosure element x:Name="DisclosureText". RED until Step 5 adds the dialog. No real key; no pixels.
/// </summary>
public class ApiKeyEntryDialogTests
{
    private const string KeyInputName = "ApiKeyInput";
    private const string DisclosureName = "DisclosureText";

    private sealed class InMemorySecretStore : ISecretStore
    {
        private string? _key;
        public InMemorySecretStore(string? seed = null) => _key = seed;
        public bool HasApiKey => !string.IsNullOrEmpty(_key);
        public string? GetApiKey() => _key;
        public void SetApiKey(string key) => _key = key;
        public void ClearApiKey() => _key = null;
    }

    private static ApiKeyEntryDialog CreateDialog() =>
        new(new ApiKeyPromptViewModel(new InMemorySecretStore()));

    [StaFact] // AC4 — the dialog constructs (its XAML parses) without being shown.
    public void Dialog_Constructs()
    {
        ApiKeyEntryDialog dialog = CreateDialog();
        Assert.NotNull(dialog);
    }

    [StaFact] // AC4 — the key input exists and is labeled (non-empty AutomationProperties.Name).
    public void KeyInput_Exists_AndIsLabeled()
    {
        ApiKeyEntryDialog dialog = CreateDialog();

        var input = dialog.FindName(KeyInputName) as FrameworkElement;
        Assert.True(input is not null,
            "The dialog must host a named key input ('ApiKeyInput') — a TextBox/PasswordBox.");
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(input!)),
            "The key input needs a non-empty AutomationProperties.Name (e.g. \"API key\").");
    }

    [StaFact] // AC4 — the key input is keyboard-reachable (Focusable + tab stop).
    public void KeyInput_IsKeyboardReachable()
    {
        ApiKeyEntryDialog dialog = CreateDialog();
        var input = (FrameworkElement)dialog.FindName(KeyInputName)!;

        Assert.True(input.Focusable, "The key input must be Focusable.");
        Assert.True(KeyboardNavigation.GetIsTabStop(input), "The key input must be a tab stop.");
    }

    [StaFact] // AC4 — a disclosure element is present (the BYO-key disclosure line, non-empty).
    public void Dialog_HasNonEmptyDisclosureText()
    {
        ApiKeyEntryDialog dialog = CreateDialog();

        var disclosure = dialog.FindName(DisclosureName) as TextBlock;
        Assert.True(disclosure is not null,
            "The dialog must host a named disclosure element ('DisclosureText') describing the BYO-key model.");
        Assert.False(string.IsNullOrWhiteSpace(disclosure!.Text),
            "The disclosure text must be non-empty (the BYO-key disclosure).");
    }
}
