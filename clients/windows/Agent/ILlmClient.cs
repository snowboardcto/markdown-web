using System.Threading;
using System.Threading.Tasks;

namespace TheMarkdownWeb.Agent;

/// <summary>
/// The provider-call seam (Story 4.1 AC1). Turns a system prompt + page markdown + reader context into
/// an <see cref="LlmResult"/>. The contract is TOTAL: <see cref="CompleteAsync"/> NEVER throws — every
/// failure (no key, HTTP error, timeout, cancel, malformed response) is returned as
/// <see cref="LlmResult.Failure"/>. The reader's key never appears in any surfaced string.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Completes the transform. Total — never throws; honours cancellation by returning a
    /// <see cref="LlmResult.Failure"/> rather than letting an <c>OperationCanceledException</c> escape.
    /// </summary>
    Task<LlmResult> CompleteAsync(
        string systemPrompt, string pageMarkdown, ReaderContext readerContext, CancellationToken ct);
}
