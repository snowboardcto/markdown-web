using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TheMarkdownWeb.Agent;

/// <summary>
/// A real Windows <see cref="ISecretStore"/> backed by DPAPI (Story 4.1 AC1 / AC3). The key is encrypted
/// with <see cref="ProtectedData.Protect"/> under <see cref="DataProtectionScope.CurrentUser"/> and the
/// CIPHERTEXT (never the plaintext) is persisted to a per-user app-data file
/// (default <c>%LOCALAPPDATA%\TheMarkdownWeb\agent.key</c>; the ctor accepts a path override for the CI
/// smoke test). Every operation is total — <see cref="GetApiKey"/> / <see cref="HasApiKey"/> return
/// null/false rather than throwing when the file is absent or undecryptable. The plaintext and the
/// ciphertext are NEVER logged.
/// </summary>
public sealed class DpapiSecretStore : ISecretStore
{
    private readonly string _keyFilePath;

    public DpapiSecretStore(string? keyFilePath = null)
    {
        _keyFilePath = keyFilePath ?? DefaultKeyFilePath();
    }

    /// <inheritdoc />
    public bool HasApiKey => GetApiKey() is { Length: > 0 };

    /// <inheritdoc />
    public string? GetApiKey()
    {
        try
        {
            if (!File.Exists(_keyFilePath))
            {
                return null;
            }

            byte[] cipher = File.ReadAllBytes(_keyFilePath);
            if (cipher.Length == 0)
            {
                return null;
            }

            byte[] plain = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
            string key = Encoding.UTF8.GetString(plain);
            return string.IsNullOrEmpty(key) ? null : key;
        }
        catch (CryptographicException)
        {
            // Undecryptable (wrong user / corrupt) — treat as absent, never throw.
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void SetApiKey(string key)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        string? dir = Path.GetDirectoryName(_keyFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        byte[] plain = Encoding.UTF8.GetBytes(key);
        byte[] cipher = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_keyFilePath, cipher); // ONLY the ciphertext is ever written.
    }

    /// <inheritdoc />
    public void ClearApiKey()
    {
        try
        {
            if (File.Exists(_keyFilePath))
            {
                File.Delete(_keyFilePath);
            }
        }
        catch (IOException)
        {
            // Best-effort — a locked/already-gone file must not throw.
        }
    }

    private static string DefaultKeyFilePath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "TheMarkdownWeb", "agent.key");
    }
}
