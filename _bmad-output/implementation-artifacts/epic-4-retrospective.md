# Epic 4 Retrospective — Personalized Rendering (AI Personalities)

**Closed:** 2026-06-22 | **CI run (final):** 27925189102 (33292e3) — 293 tests green on windows-latest

---

## 1. Summary

Epic 4 delivered the personalization promise at the heart of the Markdown Web manifesto: a reader's own local agent re-renders pages their way.

**What shipped across four stories:**

- **4.1** — The `Agent` module foundation: `ILlmClient`/`AnthropicLlmClient` (Anthropic Messages API), `ISecretStore`/`DpapiSecretStore` (DPAPI/CurrentUser, key never logged), `PersonalityEngine` with graceful totality, `Persona.Basic` pass-through, and the `PersonalizationGateway` seam wired between fetch and `FlowDocumentRenderer`. NFR-5 (no server-side rewrite) proven via stub-handler request-shape assertions.
- **4.2** — Toolbar `PersonalitySelector` (the slot reserved since 3.2): seed personas listed, default Basic, labeled + keyboard-reachable (TabIndex 4). Re-render-in-place WITHOUT re-fetching — the held raw markdown is reused, zero network GETs, last-wins generation guard. API-key entry dialog (`ApiKeyEntryDialog`/`ApiKeyPromptViewModel`) for the NeedsKey path.
- **4.3** — Four real structural persona prompts (Cozy Reader / Terminal / TL;DR / Plain Language) in `PersonaRegistry.Seed`, replacing 4.2 placeholders. Proven via a capturing fake (routing) and a systemPrompt-keyed fake (different-output + visible-change). Zero new production types — the only production edit was the four `SystemPrompt` strings.
- **4.4** — Translate persona with a real structure-preserving prompt; engine appends a `"\n\nTarget language: <lang>."` directive sourced from `ReaderContext.PreferredLanguage`; toolbar language picker re-renders in place via the unchanged 4-2 coordinator. Audio persona routes to `ISpeechSynthesizer`/`SapiSpeechSynthesizer` (`System.Speech 10.0.9`, offline, no key) over a pure `ReadingOrderExtractor.Extract(markdown)` in `Agent`. Stop-before-speak wired and CI-asserted.

---

## 2. FR/Goal Closure

| Requirement | Met? | Evidence |
|---|---|---|
| **FR-10** — per-reader structural rendering (not cosmetic) | Yes | 4.3: four personas with pairwise-distinct prompts; systemPrompt-keyed fake proves two personas yield different `PersonalizationResult.Markdown` and different `FlowDocument` for the same source; AC confirmed green CI run #48 |
| **FR-11** — accessibility & translation as rendering outcomes | Yes | 4.4: Translate prompt + language-directive engine composition; `ReadingOrderExtractor` (pure Markdig-AST); `SapiSpeechSynthesizer` (offline, no key); 293 tests green run 27925189102 |
| **FR-12** — local agent (trust by locality) | Yes | 4.1: `AnthropicLlmClient` calls `{BaseUrl}/v1/messages` with the reader's own key; no Markdown-Web host in the transform path; asserted via stub-handler request-shape `[Fact]` |
| **NFR-5** — no server-side rewrite of human-facing content | Yes | 4.1 AC5: the GET fetcher (read) and POST transform (personalize) are distinct clients; the provider host is the reader's chosen endpoint, never a Markdown-Web host; key only in `x-api-key` header |
| **D1** — BYO-key agent | Yes | `DpapiSecretStore` (DPAPI/CurrentUser, `%LOCALAPPDATA%\TheMarkdownWeb\agent.key`); key never logged; sentinel-key no-leak scan across all surfaced strings |
| **D2** — markdown→markdown transform | Yes | All personas produce a markdown string handed to the unchanged Epic-3 `FlowDocumentRenderer`; `Rendering` purity tests stayed green throughout |
| **D3** — App→Agent→Rendering boundary | Yes | `Agent` owns net+AI; `Rendering` pure (allowlist `{Markdig, ColorCode.Core}`); `Agent_DoesNotReference_App` + `App_References_Agent` boundary `[Fact]`s green; `NoEmbeddedBrowserTests` green |
| **D4** — Windows-CI-only verification, no real key/network/socket | Yes | `ILlmClient`, `ISecretStore`, `ISpeechSynthesizer` all faked in CI; stub `HttpMessageHandler` for `AnthropicLlmClient`; 293 tests on windows-latest, no real API call ever made |
| **D5** — Audio via SAPI (offline, no key) | Yes (with one deferral) | `SapiSpeechSynthesizer`/`System.Speech 10.0.9` ships; full-body reading-order via pure `ReadingOrderExtractor`; Stop-before-speak CI-asserted. **Deferred:** D5's optional LLM-cleaned reading-order script stage — the minimum extract-and-speak path ships; the LLM refinement is in Deferred-Work-Log |

---

## 3. What Went Well

- **The App→Agent→Rendering boundary held throughout.** Established in Epic 3 and respected across all four Epic-4 stories: `Rendering` gained zero new dependencies (purity tests stayed green on every CI run), `Agent` never referenced `App`, and the Epic-3 `FlowDocumentRenderer` was passed to unchanged and rendered whatever markdown the engine produced. The architecture decision to make personality a markdown→markdown transform was the right call.

- **CI-provable AI integration via fakes.** The discipline of `ILlmClient` / `ISpeechSynthesizer` / `ISecretStore` injection meant every behavior — routing, different-output, NeedsKey, totality, Stop-before-speak — was assertable with deterministic fakes on `windows-latest` without a real API key, real network socket, or real audio device. The pipeline was proven; quality is runtime.

- **BYO-key DPAPI design.** Storing the reader's API key via `ProtectedData` (CurrentUser scope) to `%LOCALAPPDATA%\TheMarkdownWeb\agent.key` gave a simple, Windows-native, privacy-respecting answer to key storage. The sentinel-key no-leak scan proved the key never surfaces in any `Notice`, `Markdown`, or `FailureReason` string.

- **Re-render-in-place without re-fetch.** The 4.2 design — hold the raw fetched markdown, re-run it through the gateway on persona switch, zero network GETs, last-wins generation guard — was the correct call. The `PersonalizationGateway`'s `Func<Persona>` seam meant `PersonalityRerenderCoordinator` could change the persona without touching `NavigationController`.

- **Additive backward-safe gateway extension in 4.4.** The `Func<string?>? preferredLanguage = null` optional constructor parameter left all 14 existing `PersonalizationGateway` call sites compiling and behaving identically. Zero positional break across the test suite; only `MainWindow.xaml.cs:57` was edited.

---

## 4. What Was Hard / What We'd Change

- **Invalid-JSON false-green in 4.1, caught before CI.** The first implementation of `AnthropicLlmClient` hand-rolled the JSON request body using string interpolation. Raw newlines in the page markdown produced invalid JSON, but the placeholder test content didn't trigger it — so the test passed while the real path was broken. Switching to `System.Text.Json` serialization fixed the bug. Lesson: hand-rolled JSON escaping over arbitrary user content is a latent bug; always use a serializer for the request body.

- **Editable-ComboBox tab-stop CI failure in 4.2.** An `IsEditable="True"` `ComboBox` for the persona selector introduced an internal `PART_EditableTextBox` that captured the tab focus, breaking the asserted `TabIndex` sequence (Back/Forward/Reload/AddressInput/PersonalitySelector/ContentScroll). Caught by the `[StaFact]` tab-order assertions, not by visual inspection. Fixed by using a non-editable `ComboBox`. This pattern — WPF control templates stealing tab stops in ways that only appear in automation tests — is a recurring Windows-CI-only discovery.

- **How to CI-test AI quality without a real model.** The capturing/keyed-fake pattern proves routing and different-output, but it cannot verify that Cozy actually reflows warmly or that TL;DR actually compresses. The gap between "the pipeline is wired" and "the output is good" is real and remains unresolved in CI. This is documented as a runtime-only concern, but it means persona quality depends entirely on prompt authoring and manual review on a real key.

- **Transient 529 overloads forcing main-context authoring.** Several stories were authored in the main conversation context (rather than in fresh agent contexts) because the Anthropic API was returning 529 service overloads during the implementation window. This made individual story contexts longer than ideal and occasionally required re-reading prior story files to maintain continuity.

- **System.Speech placement (App only) and the split audio path.** The decision to route the `"audio"` persona BEFORE the `PersonalizationGateway` (App short-circuits to the speech path before the gateway/coordinator) required care to avoid the boundary guard catching `System.Speech` in the wrong assembly. The `ReadingOrderExtractor` is in `Agent` (pure Markdig-AST, no WPF); `SapiSpeechSynthesizer` is in `App` (uses `System.Speech`). The correct placement was unambiguous from D3 but required deliberate verification.

---

## 5. Lessons / Carry-Forward

- **Fakes prove the pipeline, not the quality.** The `ILlmClient`-fake discipline is the right CI strategy for AI integration. State this explicitly in every AI-adjacent story: CI proves routing, boundary, totality, and structural wiring; AI output quality is a runtime/manual concern. Do not attempt to assert quality in CI without a real model — it produces fragile tests and false confidence.

- **Runtime-only vs CI-provable must be documented per story.** The explicit table (introduced in 4.3 and carried into 4.4) — listing each concern with where it is verified and why — prevented over-testing and made the "real-LLM quality is not CI-testable" decision visible rather than accidental.

- **Serializer for anything that crosses a user-data boundary.** Hand-rolled JSON over page markdown (4.1) is the canonical example: it works on clean content and silently breaks on real content. Use `System.Text.Json` (or equivalent) for any request body that carries user-supplied string content.

- **Optional seam parameters for additive extensions of wired interfaces.** The `Func<string?>? preferredLanguage = null` pattern in 4.4 is reusable: when extending an injection point that already has many call sites, an optional parameter with a safe default eliminates the regression surface while keeping the new capability expressible. Prefer this over a new method overload.

- **Tab-order and WPF control-template internals require explicit `[StaFact]` automation assertions.** Visual inspection and compile-time checks do not catch tab-stop capture by WPF control template parts. Every Epic-3/4 story that touched the toolbar benefited from `AutomationProperties.Name`, `TabIndex`, and `IsTabStop` assertions under `[StaFact]`.

---

## 6. Success Assessment

Epic 4 closed the MVP's personalization promise: a reader-controlled BYO-key local agent, a toolbar selector, four real structural personas, translation, and audio accessibility — all CI-green on `windows-latest` with no real key, network, or audio device in CI, and with the `Rendering` boundary unbroken.

---

*Generated: 2026-06-22*
