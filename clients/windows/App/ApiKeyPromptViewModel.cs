using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheMarkdownWeb.Agent;

namespace TheMarkdownWeb.App;

/// <summary>
/// The API-key entry UX view-model (Story 4.2 AC4). Stores the entered key via the SAME
/// <see cref="ISecretStore"/> the engine reads, discloses the BYO-key model, and never crashes on a
/// blank/missing key. The key is opaque — it is NEVER logged or surfaced in any string the VM produces.
/// </summary>
public sealed class ApiKeyPromptViewModel : INotifyPropertyChanged
{
    private readonly ISecretStore _secretStore;
    private string _keyText = string.Empty;

    public ApiKeyPromptViewModel(ISecretStore secretStore)
    {
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The key the reader is entering (never logged/surfaced).</summary>
    public string KeyText
    {
        get => _keyText;
        set
        {
            if (_keyText != value)
            {
                _keyText = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The plain, key-free BYO-key disclosure: the key is stored locally + encrypted (per-user) and sent
    /// only to the reader's chosen provider over TLS — never to a Markdown-Web server.
    /// </summary>
    public static string DisclosureText =>
        "Your API key is stored locally and encrypted on this device, and is sent only to your chosen " +
        "AI provider over a secure connection. It is never sent to a Markdown Web server.";

    /// <summary>
    /// Saves a non-blank (trimmed) key via <see cref="ISecretStore.SetApiKey"/> and returns <c>true</c>.
    /// A blank/whitespace key does NOT store anything, returns <c>false</c>, and never throws.
    /// </summary>
    public bool TrySave()
    {
        if (string.IsNullOrWhiteSpace(_keyText))
        {
            return false;
        }

        _secretStore.SetApiKey(_keyText.Trim());
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
