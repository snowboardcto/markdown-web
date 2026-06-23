using System;
using System.Collections.Generic;

namespace TheMarkdownWeb.App;

/// <summary>
/// The discriminated result of <see cref="MarkdownDiscoveryService.DiscoverAsync"/> (Story 6.3 AC1/AC3/AC5).
/// Each case is a sealed record so the compiler enforces exhaustive matching in 6.4's dispatch switch.
/// </summary>
public abstract record DiscoveryResult
{
    // Private constructor — only the nested cases may be instantiated.
    private DiscoveryResult() { }

    /// <summary>
    /// A validated, page-level markdown hit from step-1 (content-negotiation or alternate-link) or
    /// step-2 (<c>.md</c> sibling). <paramref name="SourceUrl"/> is the URL the markdown was fetched
    /// from (may differ from the originally typed URL after a redirect or an alternate-link resolve).
    /// </summary>
    public sealed record PageMarkdown(string Markdown, Uri SourceUrl) : DiscoveryResult;

    /// <summary>
    /// The <c>/llms.txt</c> site-index hit (step 3). The body is a valid markdown site-index document.
    /// <paramref name="Links"/> is a parsed set of markdown-linked URIs from the index body.
    /// NOT the page body — surfaced in the UI as "available markdown resources" (Story 6.4 AC4).
    /// </summary>
    public sealed record LlmsIndex(string Body, IReadOnlyList<Uri> Links, Uri IndexUrl) : DiscoveryResult;

    /// <summary>
    /// A genuine miss: all cascade steps ran and found no validated markdown representation.
    /// </summary>
    public sealed record NoMarkdown(Uri RequestedUrl) : DiscoveryResult;

    /// <summary>
    /// The host refused the discovery probe (e.g. 403 Forbidden, 401 Unauthorized, or a hard network
    /// refusal). Distinct from <see cref="NoMarkdown"/> so 6.4 can surface a different message
    /// ("site blocked the request") (Story 6.3 AC5, Story 6.4 AC3).
    /// </summary>
    public sealed record Blocked(Uri RequestedUrl, int? StatusCode) : DiscoveryResult;

    /// <summary>
    /// Defensive result for null/relative/non-http(s) input passed to the discovery service — 6.2 should
    /// never route these, but the service is defensive (Story 6.3 AC3).
    /// </summary>
    public sealed record Invalid(string Reason) : DiscoveryResult;
}
