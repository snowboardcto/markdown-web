namespace TheMarkdownWeb.Agent;

/// <summary>
/// The total, closed set of personalization outcomes (Story 4.1 AC4). Exactly four members — the
/// totality closure the engine guarantees: every path resolves to exactly one of these.
/// </summary>
public enum PersonalizationOutcome
{
    /// <summary>The provider returned usable transformed markdown.</summary>
    Transformed,

    /// <summary>A pass-through persona (Basic) — the original markdown, no provider call.</summary>
    PassThrough,

    /// <summary>A non-pass-through persona but no key — the original markdown, no provider call.</summary>
    NeedsKey,

    /// <summary>Any failure (HTTP/timeout/cancel/blank/throw/oversized) — the original markdown.</summary>
    FellBack,
}

/// <summary>
/// The always-renderable result of a personalization (Story 4.1 AC1 / AC4). <see cref="Markdown"/> is
/// ALWAYS a non-null renderable string — the transformed markdown on success, else the original.
/// <see cref="Notice"/> is a non-blocking, KEY-FREE human notice on <see cref="PersonalizationOutcome.NeedsKey"/>
/// / <see cref="PersonalizationOutcome.FellBack"/>, else <c>null</c>.
/// </summary>
public readonly record struct PersonalizationResult(string Markdown, PersonalizationOutcome Outcome, string? Notice);
