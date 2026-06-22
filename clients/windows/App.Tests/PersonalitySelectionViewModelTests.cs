using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using TheMarkdownWeb.Agent;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 4.2 AC3 — the selection state that drives the gateway (replaces the 4-1 constant
/// <c>() =&gt; Persona.Basic</c>). Pure <c>[Fact]</c>s (plain CLR; no WPF, no STA):
///   • a fresh <see cref="PersonalitySelectionViewModel"/> has <c>Current == Persona.Basic</c> (no
///     regression: first run = the faithful basic render);
///   • <c>Options</c> == <see cref="PersonaRegistry.Seed"/> (the ComboBox ItemsSource);
///   • <c>Select(other)</c> updates <c>Current</c> and raises <c>SelectionChanged</c> exactly once;
///   • <c>Select(same)</c> is a no-op (no event) — optional-per-story, asserted here as a guard;
///   • a real <see cref="PersonalizationGateway"/> composed with <c>() =&gt; vm.Current</c> over the REAL
///     <see cref="PersonalityEngine"/> + a fake <see cref="ILlmClient"/> resolves with Basic
///     (pass-through, LLM never called) BEFORE Select and with the selected transform persona AFTER
///     Select — proving the selector drives the gateway.
///
/// INTENDED API CONTRACT (Step-5 implementer must match; namespace TheMarkdownWeb.App):
///
///   public interface IPersonalitySelection {
///       Persona Current { get; }
///       event EventHandler? SelectionChanged;
///       void Select(Persona persona); }
///
///   public sealed class PersonalitySelectionViewModel : IPersonalitySelection, INotifyPropertyChanged {
///       public PersonalitySelectionViewModel();                 // Current = Persona.Basic
///       public Persona Current { get; }
///       public IReadOnlyList&lt;Persona&gt; Options { get; }        // = PersonaRegistry.Seed
///       public event EventHandler? SelectionChanged;
///       public void Select(Persona persona); }
///
/// RED until Step 5 adds the App selection types.
/// </summary>
public class PersonalitySelectionViewModelTests
{
    private static readonly Uri PageUrl = new("https://themarkdownweb.com/x.md");

    // ---- local fakes (App.Tests cannot see Agent.Tests' internals; declare local doubles) -------

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

    private sealed class InMemorySecretStore : ISecretStore
    {
        private string? _key;
        public InMemorySecretStore(string? seed = null) => _key = seed;
        public bool HasApiKey => !string.IsNullOrEmpty(_key);
        public string? GetApiKey() => _key;
        public void SetApiKey(string key) => _key = key;
        public void ClearApiKey() => _key = null;
    }

    private static Persona Cozy() => new("cozy", "Cozy Reader", "You are the Cozy Reader.", IsPassThrough: false);

    // ---- tests ---------------------------------------------------------------------------------

    [Fact] // AC3 — default selection is Basic (no regression: first run = faithful basic render).
    public void New_Current_DefaultsToBasic()
    {
        var vm = new PersonalitySelectionViewModel();

        Assert.Same(Persona.Basic, vm.Current);
    }

    [Fact] // AC3 — Options surfaces the seed registry (the ComboBox ItemsSource).
    public void Options_AreTheSeedRegistry()
    {
        var vm = new PersonalitySelectionViewModel();

        Assert.Equal((IReadOnlyList<Persona>)PersonaRegistry.Seed, vm.Options);
    }

    [Fact] // AC3 — Select(other) updates Current and raises SelectionChanged exactly once.
    public void Select_Other_UpdatesCurrent_AndRaisesSelectionChangedOnce()
    {
        var vm = new PersonalitySelectionViewModel();
        Persona cozy = Cozy();
        int raised = 0;
        vm.SelectionChanged += (_, _) => raised++;

        vm.Select(cozy);

        Assert.Same(cozy, vm.Current);
        Assert.Equal(1, raised);
    }

    [Fact] // AC3 — Select(same) is a no-op: Current unchanged, no SelectionChanged (optional-per-story guard).
    public void Select_Same_IsNoOp_NoEvent()
    {
        var vm = new PersonalitySelectionViewModel();
        int raised = 0;
        vm.SelectionChanged += (_, _) => raised++;

        vm.Select(Persona.Basic); // already the current selection

        Assert.Same(Persona.Basic, vm.Current);
        Assert.Equal(0, raised);
    }

    [Fact] // AC3 — a gateway composed with () => vm.Current reads Basic before Select (pass-through, LLM never called).
    public async Task Gateway_ComposedWithSelection_ResolvesBasic_BeforeSelect()
    {
        var vm = new PersonalitySelectionViewModel();
        var llm = new CountingLlmClient(LlmResult.Success("# SHOULD NOT BE USED"));
        var engine = new PersonalityEngine(llm, new InMemorySecretStore(seed: "any-key"));
        var gateway = new PersonalizationGateway(engine, () => vm.Current);

        const string fetched = "# H\n\npara";
        string resolved = await gateway.ResolveMarkdownAsync(fetched, PageUrl, CancellationToken.None);

        Assert.Equal(fetched, resolved); // Basic pass-through: byte-identical.
        Assert.Equal(0, llm.Calls);
    }

    [Fact] // AC3 — after Select(cozy) the SAME gateway reads the selected persona (the selector drives the gateway).
    public async Task Gateway_ComposedWithSelection_ResolvesSelectedPersona_AfterSelect()
    {
        var vm = new PersonalitySelectionViewModel();
        var llm = new CountingLlmClient(LlmResult.Success("# Cozy"));
        var engine = new PersonalityEngine(llm, new InMemorySecretStore(seed: "real-key"));
        var gateway = new PersonalizationGateway(engine, () => vm.Current);

        vm.Select(Cozy());
        string resolved = await gateway.ResolveMarkdownAsync("# H\n\npara", PageUrl, CancellationToken.None);

        Assert.Equal("# Cozy", resolved);
        Assert.Equal(1, llm.Calls);
    }
}
