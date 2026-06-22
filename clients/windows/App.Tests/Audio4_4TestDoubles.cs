using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TheMarkdownWeb.Agent;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Shared Story 4.4 test doubles for the App.Tests suite (App.Tests cannot see Agent.Tests' internals).
/// None of these touches a real audio device, a real socket, or a real key.
/// </summary>
internal sealed class FakeSpeechSynthesizer : ISpeechSynthesizer
{
    /// <summary>The ordered log of operations — used to prove Stop precedes each Speak.</summary>
    public List<string> Ops { get; } = new();

    public string? LastSpokenText { get; private set; }
    public int SpeakCount { get; private set; }
    public int StopCount { get; private set; }

    public Task SpeakAsync(string text, CancellationToken ct = default)
    {
        SpeakCount++;
        LastSpokenText = text;
        Ops.Add("speak");
        return Task.CompletedTask;
    }

    public void Stop()
    {
        StopCount++;
        Ops.Add("stop");
    }
}

/// <summary>Models SAPI being unavailable — <see cref="SpeakAsync"/> throws (the route must catch it).</summary>
internal sealed class UnavailableSpeechSynthesizer : ISpeechSynthesizer
{
    public int StopCount { get; private set; }

    public Task SpeakAsync(string text, CancellationToken ct = default)
        => throw new InvalidOperationException("SAPI voice unavailable (modeled).");

    public void Stop() => StopCount++;
}

/// <summary>
/// A language-aware fake <see cref="ILlmClient"/>: records the EFFECTIVE <c>systemPrompt</c> it received
/// (so a test can assert the chosen language reached the provider) and returns a caller-supplied canned
/// markdown (valid Markdown the deterministic renderer can render). Records its call count.
/// </summary>
internal sealed class LangAwareLlmClient : ILlmClient
{
    private readonly string _cannedMarkdown;

    public LangAwareLlmClient(string cannedMarkdown) => _cannedMarkdown = cannedMarkdown;

    public string? LastSystemPrompt { get; private set; }
    public int Calls { get; private set; }

    public Task<LlmResult> CompleteAsync(
        string systemPrompt, string pageMarkdown, ReaderContext readerContext, CancellationToken ct)
    {
        Calls++;
        LastSystemPrompt = systemPrompt;
        return Task.FromResult(LlmResult.Success(_cannedMarkdown));
    }
}

/// <summary>An in-memory <see cref="ISecretStore"/> (no DPAPI, no disk). Seed a key or leave it empty.</summary>
internal sealed class MemorySecretStore : ISecretStore
{
    private string? _key;
    public MemorySecretStore(string? seed = null) => _key = seed;
    public bool HasApiKey => !string.IsNullOrEmpty(_key);
    public string? GetApiKey() => _key;
    public void SetApiKey(string key) => _key = key;
    public void ClearApiKey() => _key = null;
}
