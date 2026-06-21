namespace TheMarkdownWeb.Agent;

/// <summary>
/// BYO-key storage (Story 4.1 AC1 / AC3). Holds the reader's opaque API key. The key is stored locally
/// and per-user; it is NEVER logged, traced, or surfaced in any string the store produces. The only
/// place the key is ever read out is for the outgoing provider request's <c>x-api-key</c> header.
/// </summary>
public interface ISecretStore
{
    /// <summary><c>true</c> iff a usable key is present.</summary>
    bool HasApiKey { get; }

    /// <summary>Returns the stored key, or <c>null</c> if none is present/usable.</summary>
    string? GetApiKey();

    /// <summary>Stores (encrypted, per-user) the supplied key.</summary>
    void SetApiKey(string key);

    /// <summary>Removes any stored key.</summary>
    void ClearApiKey();
}
