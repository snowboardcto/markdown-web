namespace TheMarkdownWeb.Agent;

/// <summary>
/// The minimal-but-real reader context passed at render time (Story 4.1 AC1). Carries the page identity
/// and the reader's preferred language so a persona can frame its transform. Extensible without breaking
/// the engine/client signatures. For the Basic pass-through persona it is carried but unused.
/// </summary>
public readonly record struct ReaderContext(string? PageUrl, string? PreferredLanguage);
