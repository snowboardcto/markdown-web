namespace TheMarkdownWeb.Agent;

/// <summary>
/// The outcome of a single LLM completion (Story 4.1 AC1). On success carries the transformed markdown;
/// on failure carries a non-empty, KEY-FREE reason. Mirrors <c>App.FetchResult</c>'s shape so the
/// Agent module reads like the rest of the client. The <see cref="ILlmClient"/> contract is TOTAL:
/// every failure becomes a <see cref="Failure"/> — the client never throws, so this struct is the only
/// channel for an error.
/// </summary>
public readonly record struct LlmResult
{
    private LlmResult(bool isSuccess, string? markdown, string? failureReason)
    {
        IsSuccess = isSuccess;
        Markdown = markdown;
        FailureReason = failureReason;
    }

    /// <summary>Whether the completion produced usable transformed markdown.</summary>
    public bool IsSuccess { get; }

    /// <summary>The transformed markdown on success; <c>null</c> on failure.</summary>
    public string? Markdown { get; }

    /// <summary>A non-empty, key-free reason on failure; <c>null</c> on success.</summary>
    public string? FailureReason { get; }

    /// <summary>Creates a successful result carrying the transformed markdown.</summary>
    public static LlmResult Success(string markdown) => new(true, markdown, null);

    /// <summary>Creates a failed result carrying a non-empty, key-free reason.</summary>
    public static LlmResult Failure(string reason) => new(false, null, reason);
}
