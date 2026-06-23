using System;

namespace TheMarkdownWeb.App;

/// <summary>
/// Pure, exhaustive dispatcher from a <see cref="DiscoveryResult"/> to the appropriate render/state
/// action (Story 6.4 AC5, Task 2). Injected sinks are invoked; the dispatcher itself has no I/O.
/// Never throws — all result cases are handled; an unrecognized case maps to the no-markdown state.
///
/// [Fact]-testable: feed each <see cref="DiscoveryResult"/> case and assert the correct sink
/// was invoked via recording delegates.
/// </summary>
public static class DiscoveryOutcomeDispatcher
{
    /// <summary>
    /// Dispatches <paramref name="result"/> to one of the provided sink actions. Exhaustive —
    /// every <see cref="DiscoveryResult"/> case is handled. Never throws.
    /// </summary>
    /// <param name="result">The discovery result to dispatch.</param>
    /// <param name="onPageMarkdown">Called with the markdown text and source URL for a
    ///   <see cref="DiscoveryResult.PageMarkdown"/> result.</param>
    /// <param name="onLlmsIndex">Called with the llms.txt body, link list, and index URL for a
    ///   <see cref="DiscoveryResult.LlmsIndex"/> result.</param>
    /// <param name="onNoMarkdown">Called with the requested URL for a <see cref="DiscoveryResult.NoMarkdown"/> result.</param>
    /// <param name="onBlocked">Called with the requested URL and optional status code for a
    ///   <see cref="DiscoveryResult.Blocked"/> result.</param>
    /// <param name="onInvalid">Called for a <see cref="DiscoveryResult.Invalid"/> result (defensive
    ///   path; 6.2 should not route these).</param>
    public static void Dispatch(
        DiscoveryResult result,
        Action<string, Uri> onPageMarkdown,
        Action<DiscoveryResult.LlmsIndex> onLlmsIndex,
        Action<Uri> onNoMarkdown,
        Action<Uri, int?> onBlocked,
        Action? onInvalid = null)
    {
        if (result is null)
        {
            onNoMarkdown?.Invoke(new Uri("about:blank"));
            return;
        }

        try
        {
            switch (result)
            {
                case DiscoveryResult.PageMarkdown pm:
                    onPageMarkdown?.Invoke(pm.Markdown, pm.SourceUrl);
                    break;

                case DiscoveryResult.LlmsIndex idx:
                    onLlmsIndex?.Invoke(idx);
                    break;

                case DiscoveryResult.NoMarkdown nm:
                    onNoMarkdown?.Invoke(nm.RequestedUrl);
                    break;

                case DiscoveryResult.Blocked bl:
                    onBlocked?.Invoke(bl.RequestedUrl, bl.StatusCode);
                    break;

                case DiscoveryResult.Invalid:
                    onInvalid?.Invoke();
                    break;

                default:
                    // Defensive: treat any unrecognized result as NoMarkdown.
                    onNoMarkdown?.Invoke(new Uri("about:blank"));
                    break;
            }
        }
        catch
        {
            // Total — never lets a sink exception propagate to the UI.
        }
    }
}
