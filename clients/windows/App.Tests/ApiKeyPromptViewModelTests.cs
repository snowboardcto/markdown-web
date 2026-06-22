using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using TheMarkdownWeb.Agent;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 4.2 AC4 — the API-key entry UX VM (stores via <see cref="ISecretStore"/>; discloses BYO-key;
/// never crashes on blank/missing). Pure <c>[Fact]</c>s (plain CLR; no WPF, no STA, no real key):
///   • <c>KeyText = "sk-test-123"; TrySave()</c> -> true; <c>store.HasApiKey</c> true;
///     <c>store.GetApiKey() == "sk-test-123"</c> (Save stores via the SAME ISecretStore the engine reads);
///   • <c>KeyText = ""</c> / <c>"   "</c> -> <c>TrySave()</c> false; store UNCHANGED; no throw;
///   • <c>DisclosureText</c> is non-empty (the BYO-key disclosure line);
///   • RE-RENDER AFTER KEY: an empty store + a non-Basic persona yields NeedsKey; after <c>TrySave(key)</c>
///     a re-render TRANSFORMS via the fake LLM (proving the key-entry surface unblocks the transform);
///   • NO LEAK: a non-secret test key never appears in any surfaced notice the coordinator publishes.
///
/// INTENDED API CONTRACT (Step-5 implementer must match; namespace TheMarkdownWeb.App):
///
///   public sealed class ApiKeyPromptViewModel : INotifyPropertyChanged {
///       public ApiKeyPromptViewModel(ISecretStore secretStore);
///       public string KeyText { get; set; }
///       public static string DisclosureText { get; }
///       public bool TrySave(); }   // non-blank -> SetApiKey(trimmed)+true; blank/whitespace -> false, no SetApiKey, no throw
///
/// NEVER a real key: the literal is a non-secret test string. RED until Step 5 adds the App key-UX types.
/// </summary>
public class ApiKeyPromptViewModelTests
{
    private const string TestKey = "sk-test-123"; // a NON-secret test literal (never a real Anthropic key).

    private static readonly Uri PageUrl = new("https://themarkdownweb.com/x.md");

    private static readonly Persona Cozy =
        new("cozy", "Cozy Reader", "You are the Cozy Reader.", IsPassThrough: false);

    // ---- local fakes ---------------------------------------------------------------------------

    private sealed class InMemorySecretStore : ISecretStore
    {
        private string? _key;
        public InMemorySecretStore(string? seed = null) => _key = seed;
        public bool HasApiKey => !string.IsNullOrEmpty(_key);
        public string? GetApiKey() => _key;
        public void SetApiKey(string key) => _key = key;
        public void ClearApiKey() => _key = null;
    }

    private sealed class CountingLlmClient : ILlmClient
    {
        private readonly LlmResult _result;
        public CountingLlmClient(LlmResult result) => _result = result;
        public int Calls { get; private set; }
        public Task<LlmResult> CompleteAsync(string systemPrompt, string pageMarkdown, ReaderContext readerContext, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(_result);
        }
    }

    // ---- tests ---------------------------------------------------------------------------------

    [Fact] // AC4 — TrySave with a non-blank key stores it via ISecretStore.SetApiKey and returns true.
    public void TrySave_NonBlank_StoresKey_AndReturnsTrue()
    {
        var store = new InMemorySecretStore();
        var vm = new ApiKeyPromptViewModel(store) { KeyText = TestKey };

        bool saved = vm.TrySave();

        Assert.True(saved);
        Assert.True(store.HasApiKey);
        Assert.Equal(TestKey, store.GetApiKey());
    }

    [Fact] // AC4 — TrySave with an empty key does NOT store + returns false + does not throw.
    public void TrySave_Empty_DoesNotStore_ReturnsFalse_NoThrow()
    {
        var store = new InMemorySecretStore();
        var vm = new ApiKeyPromptViewModel(store) { KeyText = string.Empty };

        bool saved = vm.TrySave();

        Assert.False(saved);
        Assert.False(store.HasApiKey);
    }

    [Fact] // AC4 — TrySave with a whitespace-only key does NOT store + returns false + does not throw.
    public void TrySave_Whitespace_DoesNotStore_ReturnsFalse_NoThrow()
    {
        var store = new InMemorySecretStore();
        var vm = new ApiKeyPromptViewModel(store) { KeyText = "   " };

        bool saved = vm.TrySave();

        Assert.False(saved);
        Assert.False(store.HasApiKey);
    }

    [Fact] // AC4 — the BYO-key disclosure text is present (non-empty).
    public void DisclosureText_IsNonEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(ApiKeyPromptViewModel.DisclosureText),
            "The key-entry surface must disclose the BYO-key model (a non-empty disclosure line).");
    }

    [Fact] // AC4 / AC2 — empty store + non-Basic -> NeedsKey; after Save(key) a re-render TRANSFORMS.
    public async Task SaveKey_ThenRerender_UnblocksTransform()
    {
        var store = new InMemorySecretStore(); // empty
        var llm = new CountingLlmClient(LlmResult.Success("# Cozy"));
        var gateway = new PersonalizationGateway(new PersonalityEngine(llm, store), () => Cozy);
        var calls = new System.Collections.Generic.List<string>();
        var coordinator = new PersonalityRerenderCoordinator(gateway, (md, _) => calls.Add(md));
        const string heldRaw = "# Original";
        coordinator.SetCurrentPage(heldRaw, PageUrl);

        // No key yet -> NeedsKey, the original rendered, no provider call.
        await coordinator.RerenderAsync();
        Assert.Equal(PersonalizationOutcome.NeedsKey, coordinator.LastOutcome);
        Assert.Equal(heldRaw, calls[^1]);
        Assert.Equal(0, llm.Calls);

        // The reader enters a key through the key-entry surface, then the host re-renders.
        var vm = new ApiKeyPromptViewModel(store) { KeyText = TestKey };
        Assert.True(vm.TrySave());

        await coordinator.RerenderAsync();
        Assert.Equal(PersonalizationOutcome.Transformed, coordinator.LastOutcome);
        Assert.Equal("# Cozy", calls[^1]);
        Assert.Equal(1, llm.Calls); // exactly one provider call, after the key was entered.
    }

    [Fact] // AC4 — no leak: the stored key never appears in any notice the coordinator surfaces.
    public async Task StoredKey_NeverLeaksIntoNotice()
    {
        var store = new InMemorySecretStore(); // start empty so the no-key path produces a notice
        var llm = new CountingLlmClient(LlmResult.Failure("provider error"));
        var gateway = new PersonalizationGateway(new PersonalityEngine(llm, store), () => Cozy);
        var coordinator = new PersonalityRerenderCoordinator(gateway, (_, _) => { });
        coordinator.SetCurrentPage("# Original", PageUrl);

        // NeedsKey notice (no key).
        await coordinator.RerenderAsync();
        Assert.DoesNotContain(TestKey, coordinator.LastNotice ?? string.Empty);

        // FellBack notice (key present, provider fails) — the key still must not appear.
        new ApiKeyPromptViewModel(store) { KeyText = TestKey }.TrySave();
        await coordinator.RerenderAsync();
        Assert.DoesNotContain(TestKey, coordinator.LastNotice ?? string.Empty);
    }
}
