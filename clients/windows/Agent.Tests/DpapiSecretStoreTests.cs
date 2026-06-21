using System;
using System.IO;
using System.Security.Cryptography;
using Xunit;

namespace TheMarkdownWeb.Agent.Tests;

/// <summary>
/// AC1 / AC3 — the REAL <see cref="DpapiSecretStore"/> over a TEMP path (the real
/// <c>%LOCALAPPDATA%\TheMarkdownWeb\agent.key</c> is never touched). Proves the set->get->clear->HasApiKey
/// round-trip and that the PLAINTEXT key never appears in the persisted file bytes (only the DPAPI
/// ciphertext). Uses a NON-secret literal key — never a real / sentinel Anthropic key. Capability-guarded:
/// if <c>ProtectedData</c> is unavailable (it is present on windows-latest, so this is defensive), the
/// test SKIPS cleanly via an early return, never a hard failure. It NEVER asserts on the ciphertext shape.
/// RED until the Agent module exists.
///
/// INTENDED API CONTRACT (Step-5 implementer must match; namespace TheMarkdownWeb.Agent):
///
///   public sealed class DpapiSecretStore : ISecretStore {
///       public DpapiSecretStore(string? keyFilePath = null); // default %LOCALAPPDATA%\TheMarkdownWeb\agent.key
///       // ProtectedData.Protect/Unprotect, DataProtectionScope.CurrentUser; never logs plaintext/ciphertext. }
/// </summary>
public class DpapiSecretStoreTests
{
    // A NON-secret literal (never a real key). Distinctive so a plaintext-leak scan is meaningful.
    private const string RoundTripKey = "round-trip-test-key";

    /// <summary>
    /// True when DPAPI <c>ProtectedData</c> is usable on this runner. On windows-latest it is; the guard
    /// is defensive so a non-Windows / restricted runner SKIPS rather than HARD-FAILS.
    /// </summary>
    private static bool DpapiAvailable()
    {
        try
        {
            byte[] probe = ProtectedData.Protect(new byte[] { 1, 2, 3 }, null, DataProtectionScope.CurrentUser);
            _ = ProtectedData.Unprotect(probe, null, DataProtectionScope.CurrentUser);
            return true;
        }
        catch (PlatformNotSupportedException) { return false; }
        catch (CryptographicException) { return false; }
    }

    private static string TempKeyPath() =>
        Path.Combine(Path.GetTempPath(), "TheMarkdownWeb.Tests", Guid.NewGuid().ToString("N"), "agent.key");

    [Fact] // AC3 — set -> get round-trips the key; clear removes it; HasApiKey reflects presence.
    public void RoundTrips_Set_Get_Clear_AndHasApiKey()
    {
        if (!DpapiAvailable()) return; // capability guard — skip cleanly, never fail.

        string path = TempKeyPath();
        try
        {
            var store = new DpapiSecretStore(path);

            Assert.False(store.HasApiKey, "a fresh store with no key file must report HasApiKey == false.");
            Assert.Null(store.GetApiKey());

            store.SetApiKey(RoundTripKey);
            Assert.True(store.HasApiKey, "after SetApiKey the store must report HasApiKey == true.");
            Assert.Equal(RoundTripKey, store.GetApiKey()); // the round-trip: get returns what set stored.

            store.ClearApiKey();
            Assert.False(store.HasApiKey, "after ClearApiKey the store must report HasApiKey == false.");
            Assert.Null(store.GetApiKey());
        }
        finally
        {
            TryCleanup(path);
        }
    }

    [Fact] // AC3 — the plaintext key is NEVER written in the clear; only the DPAPI ciphertext is persisted.
    public void PersistedFile_DoesNotContain_PlaintextKey()
    {
        if (!DpapiAvailable()) return; // capability guard.

        string path = TempKeyPath();
        try
        {
            var store = new DpapiSecretStore(path);
            store.SetApiKey(RoundTripKey);

            Assert.True(File.Exists(path), "SetApiKey must persist the (encrypted) key to the configured path.");
            byte[] persisted = File.ReadAllBytes(path);

            // The plaintext UTF-8 key bytes must NOT appear anywhere in the persisted ciphertext.
            byte[] plaintext = System.Text.Encoding.UTF8.GetBytes(RoundTripKey);
            Assert.False(
                ContainsSubsequence(persisted, plaintext),
                "the plaintext key must never be written in the clear — only the DPAPI ciphertext.");
        }
        finally
        {
            TryCleanup(path);
        }
    }

    [Fact] // AC3 — a second store instance over the same path reads back what the first wrote (CurrentUser scope).
    public void NewInstance_OverSamePath_ReadsBack_PersistedKey()
    {
        if (!DpapiAvailable()) return; // capability guard.

        string path = TempKeyPath();
        try
        {
            new DpapiSecretStore(path).SetApiKey(RoundTripKey);

            var reopened = new DpapiSecretStore(path);
            Assert.True(reopened.HasApiKey);
            Assert.Equal(RoundTripKey, reopened.GetApiKey());
        }
        finally
        {
            TryCleanup(path);
        }
    }

    // Naive byte-subsequence scan (the key is short; the file is small) — sufficient to prove no plaintext leak.
    private static bool ContainsSubsequence(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length) return false;
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return true;
        }
        return false;
    }

    private static void TryCleanup(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            string? dir = Path.GetDirectoryName(path);
            if (dir is not null && Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0)
            {
                Directory.Delete(dir);
            }
        }
        catch
        {
            // Best-effort cleanup; never fail the test on a leftover temp file.
        }
    }
}
