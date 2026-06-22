using System;
using System.Windows;

namespace TheMarkdownWeb.App;

/// <summary>
/// The small key-entry dialog (Story 4.2 AC4). Hosts a LABELED key input, the BYO-key
/// <see cref="ApiKeyPromptViewModel.DisclosureText"/>, and Save/Cancel. Save stores the key via the VM
/// (→ <see cref="ISecretStore.SetApiKey"/>) and reports success via <see cref="Saved"/>; Cancel / a blank
/// key is a safe no-op (the chosen persona is kept; the held original stays rendered — Q-Revert). The
/// dialog is construct-not-Show friendly: no logic runs on Loaded/Show, so CI can construct it without
/// pumping a window.
/// </summary>
public partial class ApiKeyEntryDialog : Window
{
    private readonly ApiKeyPromptViewModel _viewModel;

    public ApiKeyEntryDialog(ApiKeyPromptViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        InitializeComponent();

        DataContext = _viewModel;
        DisclosureText.Text = ApiKeyPromptViewModel.DisclosureText;
    }

    /// <summary><c>true</c> iff the reader entered a non-blank key and it was saved to the secret store.</summary>
    public bool Saved { get; private set; }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // The PasswordBox keeps the key out of the VM's observable property; push it at Save time.
        _viewModel.KeyText = ApiKeyInput.Password;

        if (_viewModel.TrySave())
        {
            Saved = true;
            DialogResult = true;
        }

        // A blank/whitespace key: TrySave returned false (no store write, no throw). Keep the dialog
        // open so the reader can correct it; nothing is rendered/reverted.
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Saved = false;
        DialogResult = false;
    }
}
