using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Xunit;
using TheMarkdownWeb.Agent;
using AppMainWindow = global::TheMarkdownWeb.App.MainWindow;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 4.4 AC2 — the target-language selection UX → set <c>ReaderContext.PreferredLanguage</c> → re-render
/// in place (reuse the 4.2 coordinator, held RAW, ZERO re-fetch). Pure <c>[Fact]</c>s over the
/// <see cref="LanguageSelectionViewModel"/> + the <see cref="PersonalizationGateway"/> composed with a
/// language Func (Q-Lang-Source) + a lang-aware fake; plus a <c>[StaFact]</c> asserting the toolbar language
/// picker control exists, is labeled, and is a keyboard tab stop in the right order.
/// </summary>
public class LanguageSelectionTests
{
    private static readonly Uri PageUrl = new("https://themarkdownweb.com/x.md");
    private const string Source = "# Title\n\nbody";

    private static Persona Translate => PersonaRegistry.Seed.Single(p => p.Id == "translate");

    // ---- the language-selection state ----------------------------------------------------------

    [Fact] // AC2 — default is "none chosen".
    public void Language_Default_IsNull()
    {
        Assert.Null(new LanguageSelectionViewModel().Current);
    }

    [Fact] // AC2 — Select sets Current and raises the change event.
    public void Language_Select_SetsCurrent_AndRaisesEvent()
    {
        var vm = new LanguageSelectionViewModel();
        int raised = 0;
        vm.SelectionChanged += (_, _) => raised++;

        vm.Select("Spanish");

        Assert.Equal("Spanish", vm.Current);
        Assert.Equal(1, raised);
    }

    [Fact] // AC2 — a blank/whitespace selection collapses to null (the "choose a language" state).
    public void Language_SelectBlank_IsNull()
    {
        var vm = new LanguageSelectionViewModel();
        vm.Select("   ");
        Assert.Null(vm.Current);
    }

    // ---- the gateway sources the language (Q-Lang-Source) ---------------------------------------

    [Fact] // AC2/AC3 — Translate + a chosen language: the gateway routes the language to the provider.
    public async Task Gateway_TranslateWithLanguage_RoutesLanguageToProvider()
    {
        var llm = new LangAwareLlmClient("# Título traducido");
        var engine = new PersonalityEngine(llm, new MemorySecretStore(seed: "real-key"));
        var language = new LanguageSelectionViewModel();
        var gateway = new PersonalizationGateway(engine, () => Translate, () => language.Current);

        language.Select("Spanish");
        string resolved = await gateway.ResolveMarkdownAsync(Source, PageUrl, CancellationToken.None);

        Assert.Contains("Spanish", llm.LastSystemPrompt!, StringComparison.Ordinal);
        Assert.Equal("# Título traducido", resolved);
    }

    [Fact] // AC2 — changing the language re-renders IN PLACE via the 4.2 coordinator (held raw, no re-fetch).
    public async Task ChangingLanguage_RerendersInPlace_ViaCoordinator()
    {
        var llm = new LangAwareLlmClient("# Titre traduit");
        var engine = new PersonalityEngine(llm, new MemorySecretStore(seed: "real-key"));
        var language = new LanguageSelectionViewModel();
        var gateway = new PersonalizationGateway(engine, () => Translate, () => language.Current);

        var rendered = new List<string>();
        var coordinator = new PersonalityRerenderCoordinator(gateway, (md, _) => rendered.Add(md));
        coordinator.SetCurrentPage(Source, PageUrl);   // hold the RAW markdown (the coordinator has no fetcher).

        language.Select("French");
        await coordinator.RerenderAsync();

        Assert.Contains("French", llm.LastSystemPrompt!, StringComparison.Ordinal);
        Assert.Equal("# Titre traduit", rendered.Last()); // the sink received the new-language output.
    }

    [Fact] // AC2 backward-safety — a two-arg gateway (no language Func) behaves exactly as before (null language).
    public async Task TwoArgGateway_StillCompiles_AndKeepsLanguageNull()
    {
        var llm = new LangAwareLlmClient("# done");
        var engine = new PersonalityEngine(llm, new MemorySecretStore(seed: "real-key"));

        // The existing two-arg call shape (no third Func) must still compile and resolve.
        var gateway = new PersonalizationGateway(engine, () => Translate);

        // Translate with a null language is the safe pass-through (no provider call) — proves the gateway
        // sourced a null language (the backward-compatible default).
        string resolved = await gateway.ResolveMarkdownAsync(Source, PageUrl, CancellationToken.None);

        Assert.Equal(0, llm.Calls);
        Assert.Equal(Source, resolved);
    }

    // ---- the toolbar language picker control (constructed MainWindow) ---------------------------

    [StaFact] // AC2 — the language picker exists, is a labeled keyboard tab stop, ordered after the selector.
    public void LanguagePicker_Exists_Labeled_AndIsKeyboardReachable_InOrder()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();

        var picker = window.FindName("LanguagePicker") as ComboBox;
        Assert.True(picker is not null, "MainWindow must host a ComboBox named 'LanguagePicker' in the toolbar.");

        Assert.Equal("Target language", AutomationProperties.GetName(picker!));
        Assert.True(picker!.Focusable, "LanguagePicker must be Focusable for keyboard reachability.");
        Assert.True(KeyboardNavigation.GetIsTabStop(picker), "LanguagePicker must be a tab stop.");

        var selector = (ComboBox)window.FindName("PersonalitySelector")!;
        var contentScroll = (FlowDocumentScrollViewer)window.FindName("ContentScroll")!;

        Assert.True(picker.TabIndex > selector.TabIndex,
            $"LanguagePicker.TabIndex ({picker.TabIndex}) must follow PersonalitySelector ({selector.TabIndex}).");
        Assert.True(contentScroll.TabIndex > picker.TabIndex,
            $"ContentScroll.TabIndex ({contentScroll.TabIndex}) must follow LanguagePicker ({picker.TabIndex}).");
    }
}
