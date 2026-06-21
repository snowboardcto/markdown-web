using System;

namespace TheMarkdownWeb.Agent;

/// <summary>
/// The Anthropic Messages API configuration (Story 4.1 AC1 / AC5). The defaults ARE the working config;
/// every property is an overridable <c>init</c> default. CI asserts the request SHAPE + success-parse,
/// not the model string, so the model id is freely overridable.
/// </summary>
public sealed record AnthropicOptions
{
    /// <summary>The provider base URL. The transform POSTs to <c>{BaseUrl}/v1/messages</c>.</summary>
    public string BaseUrl { get; init; } = "https://api.anthropic.com";

    /// <summary>A current capable Claude model id (overridable). Model SELECTION is deferred.</summary>
    public string Model { get; init; } = "claude-sonnet-4-6";

    /// <summary>The output cap for the transform.</summary>
    public int MaxTokens { get; init; } = 8192;

    /// <summary>The pinned Anthropic API version header value.</summary>
    public string AnthropicVersion { get; init; } = "2023-06-01";

    /// <summary>The per-request timeout (bounds a hung provider call into a Failure).</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(100);
}
