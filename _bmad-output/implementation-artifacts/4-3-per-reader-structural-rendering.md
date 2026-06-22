# Story 4.3: Per-reader structural rendering

Status: ready-for-dev

<!-- VALIDATION (Step 1, vs epics.md Story 4.3, lines 398–409; FR-10; architecture-epic4-agent.md D2 (markdown→markdown), the D2 seed set, D3 boundary, D4 CI-fakes + the 4.3 binding lines 122–123; the COMPLETED 4-1 + 4-2 foundation): RESULT = PASS.
  - AC↔EPIC ALIGNMENT (epics.md Story 4.3 = one Given/When/Then/And):
      • "Given two different personalities and the same `.md`" → AC2 (the REAL distinct system prompts for the structural seed personas — Cozy/Terminal/TL;DR/Plain — pairwise-distinct, non-empty, each encoding a markdown→markdown structural intent) + AC1 (the registry carries them).
      • "When each renders it, Then the results differ structurally (ordering, reading level, emphasis, or language), not merely cosmetically" → AC3 (the engine routes the SELECTED persona's exact SystemPrompt to ILlmClient.CompleteAsync — capturing fake) + AC4 (a fake LLM keyed on systemPrompt → two personas yield DIFFERENT PersonalizationResult.Markdown for the same source, and the rendered FlowDocument differs).
      • "And changing a preference visibly changes the render" → AC5 (switching persona re-renders in place to the different output — reuse the 4-2 PersonalityRerenderCoordinator; the "preference" at 4.3 = the persona choice itself, PINNED — see Q-Preference; no new knob).
    Every epic clause maps to ≥1 AC; no clause unmapped.
  - DERIVED ACs (labeled, justified): AC6 = Basic-default unchanged + Rendering-stays-PURE + the App→Agent→(API) boundary intact (4.3 changes persona PROMPT DATA only; no new type unless the capturing fake; no Rendering edit) — D2/D3 + the standing 4-1/4-2 boundary guards. AC7 = the windows-latest CI gate (STA only for the render-differs-in-host proof; ILlmClient FAKED, keyed on systemPrompt; no real key/network/socket) — D4 + the proven Epic-2/3/4-1/4-2 Windows-CI-only discipline.
  - SCOPE DRIFT: NONE beyond 4.3. EXPLICITLY OUT (each named in-spec): the REAL Translate target-language prompt + the language UX + audio/SAPI (4.4 — Translate stays a minimal placeholder here); prompt authoring/sharing, streaming, model selection, caching, second provider, Ollama (Deferred-Work-Log); REAL-LLM structural-fidelity evaluation (runtime/manual, NOT CI — documented, not unit-tested).
  - TASK COMPLETENESS (each AC ≥1 CI-runnable proof on windows-latest; NO launch-and-look, NO real network/key/socket): AC1/AC2→PersonaRegistry [Fact]s (Cozy/Terminal/TL;DR/Plain prompts non-empty, pairwise-distinct, each contains its intent keyword(s); set + ids + order preserved). AC3→a CAPTURING fake ILlmClient [Fact]s (the engine sends persona.SystemPrompt verbatim; A's prompt != B's; each == the registry prompt). AC4→a systemPrompt-keyed fake [Fact] (two personas → different Markdown for the same source) + a [StaFact] (the rendered FlowDocument text differs). AC5→re-render-in-place [Fact] + [StaFact] (switch persona → host re-renders to the OTHER persona's output; reuse the 4-2 coordinator/seam). AC6→inherited purity/boundary/no-webview green + a Basic-byte-identical [Fact] + a registry-in-Agent [Fact]. AC7→.sln/workflow UNCHANGED + STA/no-parallel discipline.
  - SIZE / SPLIT: SMALL–MEDIUM and coherent (real prompt DATA for 4 personas + a structural-difference proof harness; mostly data + tests, minimal/no new production type). Kept WHOLE — the prompts and the difference-proof are interdependent (a prompt with no routing/different-output proof is unverifiable; a proof with placeholder prompts is a tautology). No split.
  RESULT = PASS (no blocking gap; scope unchanged; the prompt-distinctness + routing + different-output + visible-render-change invariants each have a deterministic CI-runnable proof with FAKES; the real-LLM quality concern is correctly documented as runtime-only).
-->

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reader,
I want a personality to reshape the page for me,
so that I get my own version of the same source.

## Context note (this is the THIRD Epic-4 story — REAL persona prompts + the structural-difference proof)

Story 4.1 (Status: done) shipped the **Epic-4 agent foundation**: the `Agent` module (`ILlmClient`/`AnthropicLlmClient`, `ISecretStore`/`DpapiSecretStore`, `PersonalityEngine`, `Persona`/`Persona.Basic`, `ReaderContext`, `PersonalizationResult`/`PersonalizationOutcome`), BYO-key DPAPI storage with graceful totality, the no-server-side-rewrite (NFR-5) proof, and the App render-time seam (`PersonalizationGateway`).

Story 4.2 (Status: done) shipped the **SELECTOR + re-render-in-place + key UX**: the toolbar `PersonalitySelector` (lists `PersonaRegistry.Seed`, default Basic, labeled, keyboard-reachable), the `PersonalitySelectionViewModel` (the gateway reads `() => _selection.Current`), the `PersonalityRerenderCoordinator` (re-render the HELD RAW markdown in place, ZERO re-fetch, last-wins), and the `ApiKeyPromptViewModel`/`ApiKeyEntryDialog` for the `NeedsKey` path. **At 4.2 the non-Basic personas carry PLACEHOLDER `SystemPrompt`s** (`"You are the Cozy Reader. (Prompt refined in Story 4.3.)"` etc.) — the POINT of 4.2 was the selector/re-render/key wiring, not persona variety.

**Story 4.3 makes the structural seed personas REAL.** It is the third Epic-4 story:
- **4.1 (done)** built the agent + the gateway seam + BYO-key + Basic pass-through.
- **4.2 (done)** built the selector + selection state + re-render-in-place + key UX, with PLACEHOLDER persona prompts.
- **4.3 (this story)** replaces the 4-2 placeholders with **real, distinct system prompts** for the structural seed personas (Cozy Reader / Terminal / TL;DR / Plain Language), each encoding a DISTINCT markdown→markdown structural intent (ordering / reading-level / emphasis / length), and **proves** — deterministically, with a FAKE LLM keyed on the system prompt — that the engine routes the selected persona's exact prompt to the provider and that two personas yield **structurally-different** rendered output for the **same** source, with switching persona visibly changing the render.
- **4.4** adds the REAL Translate target-language prompt + the language UX + the audio/SAPI personality (FR-11). At 4.3 the **Translate persona stays a minimal placeholder** (its real prompt is 4.4).

**The binding architecture is DECIDED** (`_bmad-output/planning-artifacts/architecture-epic4-agent.md`, status: decided). 4.3 implements the **4.3 binding** (lines 122–123: "two personalities → structurally different markdown → different renders of the same source; changing a preference visibly changes the render"), conforms to **D2** (a personality = a named persona prompt that instructs a **markdown→markdown** transform; the output is **valid markdown** so the pure Epic-3 `FlowDocumentRenderer` still applies; "structural, not cosmetic" because the transform changes the *markdown* — ordering/headings/reading-level/emphasis/omission), **D3** (the persona prompts live in `Agent`; `Rendering` stays pure; App→Agent→(API)), and **D4** (Windows-CI-only via the `ILlmClient` fake — here a **capturing / systemPrompt-keyed** fake — no real key/network/socket).

**The seed persona set (architecture-epic4-agent.md D2, lines 46–50):** **Basic** (pass-through; the default/faithful render — UNCHANGED), **Cozy Reader**, **Terminal**, **TL;DR**, **Plain Language**, **Translate → \<language\>**. 4.3 authors the **real prompts for the four STRUCTURAL personas** (Cozy / Terminal / TL;DR / Plain); **Translate stays a minimal placeholder** (4.4). Basic remains `IsPassThrough = true` with an empty prompt — the engine short-circuits before the provider, so the Basic render stays byte-identical to Epic-3 (no regression).

### The CRITICAL constraint on what 4.3 can and cannot test (read before writing any test)

**The LLM is FAKED in CI — there is NO real model.** Therefore 4.3 **cannot** test the REAL structural QUALITY of AI output (whether Cozy *actually* reflows warmly, whether TL;DR *actually* compresses). That is a **runtime / manual** concern, validated on the reader's machine with a real key — **documented here, NOT unit-tested** (see "Runtime-only vs CI-provable" below and AC7).

What 4.3 **CAN** and DOES test deterministically in CI is the **PIPELINE CONTRACT** — the load-bearing, CI-provable surface:
- **(a) Prompt distinctness (AC2):** each structural persona has a **distinct, non-empty, well-formed** `SystemPrompt` that **encodes its transformation intent** (a markdown→markdown transform that PRESERVES valid markdown, with a DISTINCT structural emphasis: ordering / reading-level / emphasis / length). Pairwise-distinct; each mentions/encodes its intent (asserted by keyword presence).
- **(b) Routing (AC3):** the engine routes the **SELECTED** persona's **specific** `SystemPrompt` to `ILlmClient.CompleteAsync(systemPrompt, …)` — asserted via a **capturing fake** that records the `systemPrompt` it received: persona A's captured prompt `!=` B's, and each captured prompt `==` the registry's `persona.SystemPrompt`.
- **(c) Different output + visible change (AC4 + AC5):** with a fake LLM that maps **`systemPrompt → distinct output`**, two personas produce **DIFFERENT** `PersonalizationResult.Markdown` for the **same** source, the rendered `FlowDocument` differs, and **switching** persona re-renders in place to the different output (reuse the 4-2 `PersonalityRerenderCoordinator`). All deterministic; no real LLM.

### Runtime-only vs CI-provable (PINNED — be explicit so downstream agents do not over-test)

| Concern | Where verified | Why |
|---|---|---|
| Prompt is non-empty, pairwise-distinct, encodes its intent (keyword) | **CI [Fact]** (AC2) | pure data assertion over `PersonaRegistry.Seed` |
| Engine sends the selected persona's exact prompt to the provider | **CI [Fact]** (AC3) | capturing fake records the systemPrompt argument |
| Two personas → different rendered markdown / FlowDocument for one source | **CI [Fact]/[StaFact]** (AC4) | systemPrompt-keyed fake makes output deterministic |
| Switching persona visibly changes the render in place | **CI [Fact]/[StaFact]** (AC5) | reuse the 4-2 coordinator + a keyed fake |
| The REAL AI *actually* reorders / lowers reading level / compresses / emphasizes well | **Runtime / manual ONLY** (Deferred-Work-Log note) | no real model in CI; quality is a real-LLM judgment, not a deterministic assertion |

### What 4.3 builds vs what later stories add (hold the line)

- **IN (4.3):**
  - **(a) Real, distinct system prompts** for the four STRUCTURAL personas in `Agent/PersonaRegistry.cs`, replacing the 4-2 placeholders — **Cozy Reader**, **Terminal**, **TL;DR**, **Plain Language** (the exact prompts + intents are PINNED below in "The persona prompt set"). Each prompt: instructs a markdown→markdown transform that **preserves valid markdown** (so the deterministic renderer still applies), encodes a **DISTINCT structural intent**, and is non-empty + meaningfully different from the others.
  - **(b) The structural-difference proof harness** — the test-only **capturing / systemPrompt-keyed fake `ILlmClient`** (in `App.Tests` and/or `Agent.Tests`; a small test double, NOT production code) that records the captured `systemPrompt` and returns a per-prompt-distinct output, used to prove routing (AC3) + different-output (AC4) + visible-change (AC5).
  - **(c) The CI-provable structural-difference ACs** (AC2–AC5) — the load-bearing deliverable.
- **OUT (4.4 / Deferred-Work-Log):** the REAL **Translate** target-language prompt + the language-selection UX + audio/SAPI TTS (4.4 — Translate stays a minimal placeholder at 4.3); prompt authoring/sharing, streaming token-by-token render, model selection, caching transformed renders, a second provider, Ollama (Deferred-Work-Log); **real-LLM structural-fidelity evaluation** (runtime/manual — documented, not CI-tested).

### Q-Preference — what "changing a preference visibly changes the render" means at 4.3 (PINNED)

**RESOLVED: the PERSONA CHOICE ITSELF is the "preference" at 4.3. NO new reader-preference knob is added.**

- **Decision.** The epic's "And changing a preference visibly changes the render" is satisfied by the **persona selection** (already shipped by 4.2 as the `PersonalitySelectionViewModel` + `PersonalityRerenderCoordinator`). Switching from persona A to persona B re-runs the HELD RAW markdown through the gateway with B's prompt and re-renders in place to B's (structurally different) output. **That IS a preference change visibly changing the render** — and it is fully CI-provable with the systemPrompt-keyed fake (AC5).
- **Justification.** (1) The persona choice is unambiguously "a preference" in the manifesto's framing ("**your** agent decides how it should look, read, and feel — based on you"). (2) An extra explicit knob (e.g. a reading-level slider folded into `ReaderContext`) would add a new type + UI + state surface — that is scope 4.3 does NOT need, and the epic does not require a SECOND preference axis. (3) `ReaderContext` already carries `PreferredLanguage` (unused by the structural personas; it is the 4.4 Translate hook) — 4.3 adds NO field. (4) Keeping the "preference" = the persona choice means 4.3 stays **prompt DATA + tests**, with minimal/no new production type — honoring "hold the line".
- **Consequence for downstream agents (BINDING):** Do **NOT** add a reading-level/verbosity knob, a new `ReaderContext` field, or any new selection state. The "preference" is the persona; the visible-change proof is the re-render-in-place (AC5) reusing the UNCHANGED 4-2 coordinator. `ReaderContext` and its `PreferredLanguage` are **untouched** at 4.3 (4.4 may use them).

## The persona prompt set (PINNED — Step-5 authors these into `PersonaRegistry.Seed`)

> These are the **real** `SystemPrompt`s replacing the 4-2 placeholders. They are markdown→markdown transform instructions. Each MUST: (1) instruct the model to emit **valid markdown only** (so the pure renderer still applies); (2) encode a **distinct structural intent**; (3) be **pairwise-distinct** and **non-empty**; (4) contain the **intent keyword(s)** the AC2 tests assert on (listed in the "Distinctness keyword" column). The exact wording is the dev's latitude **as long as** the structural intent + the keyword(s) are present and the four are pairwise-distinct. Basic (pass-through, empty prompt) and Translate (minimal placeholder, 4.4) are unchanged in shape.

| Id | DisplayName | Structural intent (FR-10 axis) | Distinctness keyword(s) the AC2 test asserts (case-insensitive substring) | Prompt sketch (dev may refine wording; intent + keywords are binding) |
|---|---|---|---|---|
| `cozy` | Cozy Reader | **Emphasis + ordering**: warm, reflowed, adds a **TL;DR** lead, foregrounds the key idea | `cozy`, `markdown`, `TL;DR` | "You are the Cozy Reader. Rewrite the page as warm, inviting, reflowed **markdown**. Lead with a short **TL;DR** summary, then the reflowed body in a friendly reading order that foregrounds the main idea. Preserve all meaning. Output **valid markdown only** — no commentary, no code fences around the whole document." |
| `terminal` | Terminal | **Length + emphasis**: terse, monospace/CLI-friendly, minimal prose, dense structure | `terminal`, `markdown`, `terse` (or `concise`) | "You are the Terminal persona. Rewrite the page as **terse**, monospace/CLI-friendly **markdown**: minimal prose, dense bullet lists, short lines, no decorative language. Keep technical detail; strip filler. Output **valid markdown only**." |
| `tldr` | TL;DR | **Length**: summarize / compress to the essential points | `tldr` (or `TL;DR`), `markdown`, `summar` (matches summary/summarize) | "You are the TL;DR persona. **Summarize** the page into its essential points as compact **markdown** — a brief overview then a short bulleted list of the key takeaways. Drop non-essential detail while preserving accuracy. Output **valid markdown only**." |
| `plain` | Plain Language | **Reading level**: lower reading level, simpler/shorter sentences, common words | `plain`, `markdown`, `reading level` (or `simple`/`simpler`) | "You are the Plain Language persona. Rewrite the page at a **lower reading level** in **plain**, **simple** sentences and common words, as well-structured **markdown** with short paragraphs and clear headings. Preserve all meaning. Output **valid markdown only**." |

- **Basic** — UNCHANGED: `Persona.Basic = new("basic", "Basic", string.Empty, IsPassThrough: true)`. The engine short-circuits before the provider; the Basic render stays byte-identical to Epic-3 (no regression).
- **Translate** — UNCHANGED placeholder at 4.3: `new("translate", "Translate", "<minimal placeholder>", IsPassThrough: false)`. Its real target-language prompt + the language UX are **4.4**. (Keep `IsPassThrough = false` so it stays a listed transform persona; the 4-2 `PersonaRegistryTests.Seed_ContainsArchitectureSeedPersonas_IncludingTranslate` continues to pass.) 4.3 does NOT author Translate's structural prompt.
- **Order + ids unchanged:** `Basic, cozy, terminal, tldr, plain, translate` — 4.3 changes the **`SystemPrompt` strings** of cozy/terminal/tldr/plain ONLY; the ordered list, the ids, the DisplayNames, and the `IsPassThrough` flags are unchanged (the 4-2 selector/registry tests stay green).

## The hard rules (do not violate)

- **Rendering stays PURE; transforms produce valid markdown.** A persona prompt instructs a **markdown→markdown** transform; the output is **valid markdown** that the existing pure `FlowDocumentRenderer` (Epic 3) renders unchanged. **4.3 touches NO `Rendering` file**, adds NO package, and the standing `RenderingPurityTests` / `NoEmbeddedBrowserTests` / `DependencyBoundaryTests` (incl. the 4-1 `Agent_DoesNotReference_App` / `App_References_Agent`) MUST stay green.
- **The 4-1/4-2 pipeline is REUSED UNCHANGED.** `PersonalizationGateway`, `PersonalityEngine`, `PersonalitySelectionViewModel`, `PersonalityRerenderCoordinator`, the `PersonalitySelector` control, `ReaderContext`, `ISecretStore` — **all unchanged**. 4.3 changes **persona PROMPT DATA** in `PersonaRegistry.cs` + **adds tests**. **No new production type** (the systemPrompt-keyed/capturing fake is a TEST double, in `*.Tests`). If the dev believes a new production type is needed, that is a scope-creep flag — re-check against this rule first.
- **Basic default unchanged.** The selector defaults to Basic → pass-through → the engine short-circuits before the provider → the render is byte-identical to 4-1/4-2/Epic-3. A `[Fact]` regression-guards this (the four new real prompts must NOT change the Basic path).
- **Determinism — the structural-difference proof is keyed on the systemPrompt, NOT a real model.** The fake `ILlmClient` returns a **deterministic, per-systemPrompt-distinct** output (e.g. it echoes a marker derived from the prompt, or maps a known prompt → a known canned markdown). Two personas → two different captured prompts → two different outputs → two different renders. **No real key, no network, no socket, no model, no pixels, no timing.**
- **Windows-CI-only verification.** Pure `[Fact]` for the registry prompt-distinctness, the routing (capturing fake), and the different-output (keyed fake) proofs. `[StaFact]` ONLY for the render-differs-in-host proof (construct a `ContentHostController` over a real `FlowDocumentRenderer`; never `Show`). `Xunit.StaFact` + `[assembly: CollectionBehavior(DisableTestParallelization = true)]` are already present in `App.Tests` + `Agent.Tests` — do NOT re-add.

## What this story builds ON (do NOT recreate)

- `clients/windows/Agent/PersonaRegistry.cs` (lines 21–29) — the seed list with the **placeholder** prompts. **4.3 EDITS this file**: replace the cozy/terminal/tldr/plain `SystemPrompt` strings with the real prompts (the persona records, ids, order, DisplayNames, `IsPassThrough` flags are UNCHANGED). This is the primary production change of 4.3.
- `clients/windows/Agent/Persona.cs` — the `Persona(Id, DisplayName, SystemPrompt, IsPassThrough)` record + `Persona.Basic`. **UNCHANGED.**
- `clients/windows/Agent/PersonalityEngine.cs` (lines 81–83) — `PersonalizeAsync` calls `_llm.CompleteAsync(persona.SystemPrompt, original, readerContext, ct)`. **UNCHANGED** — this is the exact seam the AC3 capturing fake asserts on (the engine already forwards `persona.SystemPrompt` verbatim; 4.3 proves it does so for the selected persona, and that the prompts differ).
- `clients/windows/Agent/ILlmClient.cs` — `Task<LlmResult> CompleteAsync(string systemPrompt, string pageMarkdown, ReaderContext readerContext, CancellationToken ct)`. **UNCHANGED** — the `systemPrompt` parameter is what the capturing fake records.
- `clients/windows/App/PersonalizationGateway.cs` — `ResolveMarkdownAsync` over `_selectedPersona()`. **UNCHANGED.**
- `clients/windows/App/PersonalitySelectionViewModel.cs` + `clients/windows/App/PersonalityRerenderCoordinator.cs` — the selection state + re-render-in-place path. **UNCHANGED** — AC5 REUSES them (switch persona → `RerenderAsync` → the host re-renders the OTHER persona's output from the held raw).
- `clients/windows/Agent.Tests/TestDoubles.cs` — the existing `CountingLlmClient` (returns a single canned `LlmResult`, does NOT capture the systemPrompt) + `InMemorySecretStore` + `StubHandler`. **4.3 ADDS** a capturing / systemPrompt-keyed fake alongside these (see Dev Notes "The capturing / keyed fake"); the existing fakes are unchanged.
- `clients/windows/Agent.Tests/PersonaRegistryTests.cs` — the 4-2 registry [Fact]s (Basic-first, ids unique, non-empty prompts, contains-translate). **4.3 EXTENDS** this file (or adds a sibling `PersonaPromptTests.cs`) with the prompt-distinctness/keyword/pairwise-distinct [Fact]s. The existing 4-2 [Fact]s MUST stay green (the real prompts are still non-empty, still `IsPassThrough == false`, the set/ids/order unchanged).
- `clients/windows/App.Tests/PersonalizationGatewayTests.cs` / `PersonalityRerenderCoordinatorTests.cs` / `PersonalityRerenderSeamTests.cs` — the gateway + coordinator + App-seam [Fact]/[StaFact] patterns the AC3/AC4/AC5 tests mirror (the seam test already switches Basic↔Cozy in the host; 4.3 adds a Cozy↔Terminal (two transform personas) different-output variant).
- `clients/windows/TheMarkdownWeb.sln` + `.github/workflows/build-windows.yml` — **UNCHANGED.** 4.3 adds NO new project (the new tests land in `Agent.Tests` + `App.Tests`; the registry edit is in `Agent`). The `paths: clients/windows/**` filter already covers every changed/new file.

### ⚠️ ENVIRONMENT CONSTRAINT — read before writing any code or test (drives AC7, Tasks for CI)

**This repo is developed on Linux with NO .NET SDK installed; WPF builds and runs ONLY on Windows.** The dev agent CANNOT build, run, or visually confirm anything locally. **Verification happens exclusively through `build-windows.yml` on `windows-latest`** (restore → build -c Release → test -c Release on `TheMarkdownWeb.sln`). Therefore:

- Every acceptance bar that must be *checked* is checked **either by the compiler (build succeeds) or by an xUnit test that runs in `dotnet test` on `windows-latest`** — never "switch the persona and look at the screen."
- **No real network, no real key, no real socket, no real model in CI.** `ILlmClient` is FAKED — here a **capturing** fake (records the systemPrompt) and a **systemPrompt-keyed** fake (per-prompt-distinct output). `ISecretStore` is the in-memory fake. The render-differs proof constructs a `ContentHostController` (never `Show`n) under `[StaFact]`.
- **Prefer pure `[Fact]`** for the registry prompt-distinctness, the routing (capturing fake), and the different-output (keyed fake) logic (plain CLR, no `DispatcherObject`). Use **`[StaFact]`** ONLY for the render-differs-in-host proof (constructing a `FlowDocument` via the host).
- **No-tautology / no-secret-leak:** assert against the REAL `PersonaRegistry.Seed` prompts + the REAL `PersonalityEngine`/`PersonalizationGateway` routing (the actual `systemPrompt` the engine forwards, the actual rendered output), never a re-declared stub prompt. NEVER write a real API key into a test or a log; the keyed-fake tests use a non-secret literal key in the in-memory store.

## Acceptance Criteria

1. **[The structural seed personas are real transform personas in the registry — order/ids/flags unchanged from 4.2]** **Given** the 4-2 `PersonaRegistry.Seed` lists Basic + cozy/terminal/tldr/plain/translate with **placeholder** prompts, **When** 4.3 lands, **Then** `PersonaRegistry.Seed` still lists the **same ordered set** (`basic`, `cozy`, `terminal`, `tldr`, `plain`, `translate`) with the **same ids, DisplayNames, order, and `IsPassThrough` flags** (Basic first + pass-through; the rest `IsPassThrough == false`), and the four structural personas (cozy/terminal/tldr/plain) now carry **real** (no-longer-placeholder) `SystemPrompt`s. Concretely:
   - The ordered ids are exactly `["basic", "cozy", "terminal", "tldr", "plain", "translate"]`; Basic is `seed[0]` and `IsPassThrough == true` with an empty prompt; cozy/terminal/tldr/plain/translate are `IsPassThrough == false` with non-empty prompts.
   - **Translate is unchanged** (a minimal placeholder transform persona; its real prompt is 4.4).
   - **Every existing 4-2 `PersonaRegistryTests` [Fact] stays green UNCHANGED** (Basic-first, ids-unique, non-empty-prompts, contains-translate) — 4.3 strengthens, never weakens, the registry contract.

   Proven by the **inherited 4-2 `PersonaRegistryTests` [Fact]s staying green** + (if not already covered) a `[Fact]` asserting the ordered id list + per-persona `IsPassThrough`. Pure `[Fact]`; no STA. *(AC1 — architecture-epic4-agent.md D2 seed set + the 4.3 binding line 122; the 4-2 registry the structural prompts refine.)*

2. **[Real, distinct, intent-encoding system prompts for the four structural personas (Cozy / Terminal / TL;DR / Plain)]** **Given** a persona is a markdown→markdown transform described by its `SystemPrompt` (D2), **When** the four structural personas' prompts are inspected, **Then** each is **non-empty**, **pairwise-distinct** from the other three, instructs a transform that **preserves valid markdown** (the prompt mentions markdown / "output markdown"), and **encodes its DISTINCT structural intent** via the pinned keyword(s) (ordering/emphasis for Cozy, length/terseness for Terminal, summarization for TL;DR, reading-level/simplicity for Plain). Concretely (the test contract):
   - **Non-empty:** `cozy/terminal/tldr/plain` each have a `SystemPrompt` that is not null/empty/whitespace (already true at 4.2; re-asserted).
   - **Pairwise-distinct:** the four `SystemPrompt`s are all different from one another (no two equal); and each differs from the 4-2 PLACEHOLDER text (i.e. none still contains `"refined in Story 4.3"` — the placeholders are gone).
   - **Markdown-preserving intent:** each prompt contains `"markdown"` (case-insensitive) — the transform is instructed to emit markdown so the pure renderer still applies.
   - **Distinct intent keyword(s)** (case-insensitive substring, per the pinned table): Cozy contains `cozy` AND `tl;dr`; Terminal contains `terminal` AND (`terse` OR `concise`); TL;DR contains (`tl;dr` OR `tldr`) AND (`summar`); Plain contains `plain` AND (`reading level` OR `simple` OR `simpler`). *(The dev may refine wording; the keyword(s) + the markdown mention + pairwise-distinctness are the binding contract.)*

   Proven by pure `[Fact]`s over `PersonaRegistry.Seed` (look up by id): non-empty; pairwise-distinct (a set/`Distinct().Count()` over the four prompts == 4); no placeholder residue; each contains `"markdown"`; each contains its pinned intent keyword(s). No STA. *(AC2 — the load-bearing prompt-distinctness contract; epics.md Story 4.3 "Given two different personalities and the same .md … differ structurally (ordering, reading level, emphasis, or language)"; architecture-epic4-agent.md D2 "a personality = a named persona prompt" + "structural, not cosmetic".)*

3. **[The engine routes the SELECTED persona's exact SystemPrompt to the LLM — capturing-fake proof]** **Given** `PersonalityEngine.PersonalizeAsync` forwards `persona.SystemPrompt` to `ILlmClient.CompleteAsync(systemPrompt, …)`, **When** the engine personalizes a page with a given non-pass-through persona, **Then** the `systemPrompt` the provider receives is **byte-equal to that persona's `SystemPrompt`** (the engine does not mangle/substitute it), and **two different personas yield two different captured prompts**. Concretely (the routing contract):
   - A **capturing fake `ILlmClient`** records the `systemPrompt` argument of its last `CompleteAsync` call (and returns a canned `LlmResult.Success`).
   - Personalizing with `cozy` → the captured `systemPrompt == PersonaRegistry` cozy `.SystemPrompt`; with `terminal` → the captured prompt `==` terminal's; and the two captured prompts are **not equal** to each other.
   - The pass-through `Basic` persona makes **ZERO** `CompleteAsync` calls (nothing captured) — the engine short-circuits (re-asserts the 4-1 invariant; no regression).
   - The proof goes through the **real `PersonalityEngine`** (and, for one variant, the **real `PersonalizationGateway` over `() => selection.Current`**) — proving the SELECTED persona's prompt reaches the provider, not a hard-coded one.

   Proven by pure `[Fact]`s: `new PersonalityEngine(capturingFake, store-with-key).PersonalizeAsync(src, cozy, ctx, ct)` → `capturingFake.LastSystemPrompt == cozyPrompt`; same for terminal; `cozyCaptured != terminalCaptured`; a Basic [Fact] → `capturingFake.Calls == 0`. Plus a gateway variant: a `PersonalizationGateway` composed with `() => selection.Current` over a `PersonalitySelectionViewModel`; `Select(cozy)` then `ResolveMarkdownAsync` → the capturing fake saw cozy's prompt; `Select(terminal)` → it saw terminal's. No STA. *(AC3 — epics.md Story 4.3 "When each renders it"; the routing half of "results differ structurally"; PersonalityEngine.cs line 82 `persona.SystemPrompt` forwarded to `ILlmClient.CompleteAsync`; the 4-2 selector→gateway seam.)*

4. **[Two personas → structurally-DIFFERENT rendered output for the SAME source (systemPrompt-keyed fake; deterministic)]** **Given** a fake LLM that maps **`systemPrompt → a distinct output`**, **When** the same source markdown is personalized with two different structural personas, **Then** the two `PersonalizationResult.Markdown` values **differ** (not byte-identical), and the rendered `FlowDocument` for each differs — proving "two personalities → different renders of the same source", deterministically, with no real model. Concretely:
   - A **systemPrompt-keyed fake `ILlmClient`** returns a deterministic output that is a function of the `systemPrompt` it received (e.g. it returns canned markdown tagged with a per-persona marker, or a registered `prompt → markdown` map). DIFFERENT prompt → DIFFERENT output.
   - **Engine/gateway level ([Fact]):** the same source `"# Source\n\nbody"` personalized with `cozy` vs `terminal` (key present in the store) → `cozyResult.Markdown != terminalResult.Markdown`; both are non-empty valid markdown (each `Outcome == Transformed`).
   - **Render level ([StaFact]):** feeding each result through a real `ContentHostController` over a real `FlowDocumentRenderer` → the two hosted `FlowDocument`s' text content differ (the cozy-keyed marker appears in one and not the other, and vice-versa) — proving the structural difference SURVIVES to the render. Construct-not-Show; no pixels.
   - **No real model:** the difference is forced by the systemPrompt key, NOT by AI quality (which is runtime-only — see the Runtime-vs-CI table). The test asserts the PIPELINE delivers per-persona-distinct output to the renderer, not that the AI writing is good.

   Proven by a pure `[Fact]` (engine/gateway: cozy-output != terminal-output for the same source) + a `[StaFact]` (the two outputs render to two different `FlowDocument`s). No real key/network/socket/model. *(AC4 — epics.md Story 4.3 "the results differ structurally … not merely cosmetically"; architecture-epic4-agent.md D2 "two personalities yield structurally different renders of the same source"; the D4 fake-LLM discipline.)*

5. **[Switching persona visibly changes the render in place — the "preference change" (reuse the 4-2 coordinator)]** **Given** a page is held and rendered with persona A, **When** the reader switches to persona B (the "preference" — PINNED Q-Preference: the persona choice IS the preference), **Then** the current page **re-renders in place** (reuse the 4-2 `PersonalityRerenderCoordinator`, the HELD RAW markdown, ZERO re-fetch) to **persona B's structurally-different output** — the render visibly changes. Concretely:
   - Using the systemPrompt-keyed fake: `SetCurrentPage(heldRaw, url)`; select cozy → `RerenderAsync` → the render-sink receives cozy's output; select terminal → `RerenderAsync` → the sink receives terminal's (DIFFERENT) output; the two sink outputs differ. (Reuses the UNCHANGED 4-2 coordinator + selection state — the gateway reads `() => selection.Current`.)
   - **App-seam [StaFact]:** the same switch into a real `ContentHostController` → the hosted `FlowDocument` text changes from cozy's to terminal's (and is not the byte-identical raw, since both are transforms) — the visible render changes on a preference change.
   - **Basic round-trip still byte-identical:** switching to Basic re-renders the held RAW byte-identically (pass-through; re-asserts the 4-2 / no-regression invariant — Basic↔cozy↔Basic returns to the original).
   - **No new preference knob** (Q-Preference): `ReaderContext` is untouched; the only "preference" is the persona selection; the proof is the re-render-in-place.

   Proven by a pure `[Fact]` (coordinator: cozy-render != terminal-render, both from the same held raw, zero fetch) + a `[StaFact]` extending `PersonalityRerenderSeamTests` (switch cozy→terminal in the host → the `FlowDocument` text changes). Reuses the 4-2 coordinator UNCHANGED. *(AC5 — epics.md Story 4.3 "And changing a preference visibly changes the render"; the 4-2 re-render-in-place path; Q-Preference (persona = preference).)*

6. **[Basic default unchanged + Rendering stays PURE + the App→Agent→(API) boundary intact (D2/D3)]** **Given** 4.3 changes only persona PROMPT DATA (Agent) + adds tests, **When** the dependency graph + csprojs + the Basic path are inspected, **Then** the boundary + the Basic default still hold: the **persona prompts live in `Agent`**; **`Rendering` is untouched** (no new package, no net/AI/webview, no App/Agent ref — it still renders whatever markdown the engine returns); and the **Basic-default render is byte-identical** to 4-1/4-2/Epic-3 (the new real prompts do NOT change the pass-through path). Concretely:
   - `Rendering` gains NO new `PackageReference` and NO net/AI/webview substring — `RenderingPurityTests` stays green. **4.3 touches no `Rendering` file.**
   - The 4-1 boundary `[Fact]`s stay green UNCHANGED (`Rendering_DoesNotReference_AppOrAgent`, `App_References_Rendering`, `Agent_DoesNotReference_App`, `App_References_Agent`); the persona registry stays in `Agent`. **No new project reference edge.**
   - `NoEmbeddedBrowserTests` stays green (no new webview/cef/chromium substring — 4.3 adds no csproj/package).
   - **Basic byte-identical:** a `[Fact]` (Basic pass-through over the gateway/engine returns the source byte-identical, LLM never called) re-asserts no-regression — the four new real prompts are only reached for non-pass-through personas, never for Basic.

   Proven by the inherited purity/boundary/no-webview guards staying green + a `[Fact]` asserting `typeof(PersonaRegistry).Assembly` is `TheMarkdownWeb.Agent` + a Basic-byte-identical `[Fact]`. No STA. *(AC6 — architecture-epic4-agent.md D2 "valid markdown so the deterministic renderer still applies" + D3 "Rendering stays PURE … Agent owns persona prompts"; the standing 4-1/4-2 boundary + Basic-default invariant.)*

7. **[windows-latest CI gate — STA only for the render-differs proof; ILlmClient FAKED (capturing + keyed); no real key/network/socket/model (D4)]** **Given** verification is exclusively `windows-latest` CI with no real model, **When** `build-windows.yml` runs (`restore` → `build -c Release` → `test -c Release` on `TheMarkdownWeb.sln`), **Then** the whole solution **builds clean** and **all tests pass green**, including the NEW 4.3 prompt-distinctness / routing / different-output / visible-change tests, with all verification using FAKES (a capturing fake `ILlmClient`; a systemPrompt-keyed fake; an in-memory `ISecretStore`; the render-differs proof constructed-not-Shown). Specifically:
   - **No new test project.** The prompt-distinctness [Fact]s land in **`Agent.Tests`** (`PersonaRegistryTests` extension or a sibling `PersonaPromptTests.cs`); the routing / different-output / visible-change [Fact]s + the render-differs [StaFact]s land in **`App.Tests`** (and/or `Agent.Tests` for the engine-level routing). Both projects already carry `Xunit.StaFact` + `[assembly: CollectionBehavior(DisableTestParallelization = true)]`.
   - **STA discipline.** Pure `[Fact]` for the registry / routing / different-output logic (no `DispatcherObject`). `[StaFact]` ONLY for the render-differs-in-host + the switch-changes-host proofs (constructing a `ContentHostController`/`FlowDocument`). No shown `Window`, no `Dispatcher` pump, no socket, no real `Process.Start`, no real secret in logs, no pixels/timing, no real model.
   - **No regression / boundary intact.** Every existing 3.x + 4.1 + 4.2 test stays green UNCHANGED; `Rendering` stays pure (AC6); the Basic-default render is byte-identical. The `.sln` and `build-windows.yml` are **UNCHANGED** (no new project; the `paths: clients/windows/**` filter already covers every changed/new file).
   - **Runtime-only documented, not tested.** The real-LLM structural-fidelity concern is recorded as a Deferred-Work-Log / runtime note (NOT a CI test) — CI proves the PIPELINE contract, not AI quality.
   *(AC7 — DERIVED CI/build gate + architecture-epic4-agent.md D4 "ILlmClient … tests use a fake … no real API call, no key, no socket in CI" + the sole verification per the Environment Constraint; build-windows.yml; the proven 4-1/4-2 STA/no-parallel discipline.)*

> Source: [_bmad-output/planning-artifacts/epics.md#Story 4.3: Per-reader structural rendering (FR-10)] (lines 398–409): **Given** two different personalities and the same `.md` **When** each renders it **Then** the results differ structurally (ordering, reading level, emphasis, or language), not merely cosmetically **And** changing a preference visibly changes the render. **AC1** = the structural personas are real transform personas in the registry (order/ids/flags unchanged from 4.2). **AC2** = real, distinct, intent-encoding prompts for Cozy/Terminal/TL;DR/Plain (the load-bearing prompt-distinctness contract). **AC3** = the engine routes the selected persona's exact SystemPrompt to the LLM (capturing fake). **AC4** = two personas → structurally-different rendered output for the same source (systemPrompt-keyed fake, deterministic). **AC5** = switching persona visibly changes the render in place (reuse the 4-2 coordinator; the persona choice IS the preference — Q-Preference). **AC6** = Basic-default unchanged + Rendering-pure + the App→Agent→(API) boundary (DERIVED). **AC7** = windows-latest CI gate (STA only for the render-differs proof; ILlmClient faked capturing+keyed; no real key/network/socket/model) (DERIVED).

## Tasks / Subtasks

### Task 1 — Author the real structural persona prompts (AC1, AC2)

- [ ] In `clients/windows/Agent/PersonaRegistry.cs`, **replace the placeholder `SystemPrompt`** of `cozy`, `terminal`, `tldr`, `plain` with the **real prompts** per the pinned "persona prompt set" table — each non-empty, pairwise-distinct, mentioning `markdown` (emit-valid-markdown), and containing its pinned intent keyword(s). Keep the **ids, DisplayNames, order, and `IsPassThrough` flags UNCHANGED**.
- [ ] Leave **Basic** (pass-through, empty prompt) and **Translate** (minimal placeholder, `IsPassThrough = false`) **unchanged** — Translate's real prompt is 4.4.
- [ ] Update the `PersonaRegistry` XML-doc to note the structural prompts are now real (Translate still placeholder for 4.4).
- [ ] **CI proof:** the inherited 4-2 `PersonaRegistryTests` [Fact]s stay green; add a `[Fact]` (in `Agent.Tests`, `PersonaRegistryTests` or a new `PersonaPromptTests.cs`) asserting, per the four structural ids looked up from `Seed`: non-empty; pairwise-distinct (the four prompts `Distinct().Count() == 4`); no `"refined in Story 4.3"` placeholder residue; each contains `"markdown"` (case-insensitive); each contains its pinned intent keyword(s) (Cozy: `cozy` + `tl;dr`; Terminal: `terminal` + (`terse`|`concise`); TL;DR: (`tl;dr`|`tldr`) + `summar`; Plain: `plain` + (`reading level`|`simple`|`simpler`)). Pure `[Fact]`.

### Task 2 — Prove routing: the engine sends the selected persona's exact prompt to the LLM (AC3)

- [ ] Add a **capturing fake `ILlmClient`** to the test doubles (`Agent.Tests/TestDoubles.cs` for the engine-level test; a local double in `App.Tests` for the gateway-level test — App.Tests cannot see Agent.Tests internals) that records `LastSystemPrompt` (and a `Calls` count) and returns a canned `LlmResult.Success`.
- [ ] **CI proof (engine-level, `Agent.Tests`):** a `[Fact]` — `PersonalizeAsync(src, cozy, ctx, ct)` with a key-seeded store → `capturing.LastSystemPrompt == PersonaRegistry` cozy `.SystemPrompt`; same for terminal; assert `cozyCaptured != terminalCaptured`; a Basic [Fact] → `capturing.Calls == 0` (short-circuit, no regression).
- [ ] **CI proof (gateway/selector-level, `App.Tests`):** a `[Fact]` — a `PersonalizationGateway` over `() => selection.Current` (a real `PersonalitySelectionViewModel`); `Select(cozy)` + `ResolveMarkdownAsync` → the capturing fake saw cozy's prompt; `Select(terminal)` + resolve → it saw terminal's. Pure `[Fact]`.

### Task 3 — Prove different output: two personas → different rendered markdown / FlowDocument for the same source (AC4)

- [ ] Add a **systemPrompt-keyed fake `ILlmClient`** (test double) whose output is a deterministic function of the received `systemPrompt` (e.g. a `Dictionary<string,string>` prompt→markdown map seeded from the registry prompts, or echo-a-per-prompt-marker). Different prompt → different output.
- [ ] **CI proof (engine/gateway, `[Fact]`):** the same source personalized with `cozy` vs `terminal` (key present) → `cozyResult.Markdown != terminalResult.Markdown`; both `Outcome == Transformed`; both non-empty valid markdown.
- [ ] **CI proof (render, `[StaFact]`):** feed each result through a real `ContentHostController` over a real `FlowDocumentRenderer` (mirror `PersonalityRerenderSeamTests`) → the two hosted `FlowDocument`s' text differ (each carries its own persona marker, absent from the other). Construct-not-Show.

### Task 4 — Prove visible change on a preference change: switch persona re-renders in place to the different output (AC5)

- [ ] **CI proof (coordinator, `[Fact]`):** with the keyed fake + the UNCHANGED 4-2 `PersonalityRerenderCoordinator`: `SetCurrentPage(heldRaw, url)`; cozy + `RerenderAsync` → sink gets cozy's output; terminal + `RerenderAsync` → sink gets terminal's (DIFFERENT) output; assert the two sink outputs differ AND the fetch count stays 0 (reuse the 4-2 by-construction no-fetch). Reuse the coordinator UNCHANGED.
- [ ] **CI proof (App-seam, `[StaFact]`):** extend `PersonalityRerenderSeamTests` — switch cozy→terminal in a real host → the hosted `FlowDocument` text changes from cozy's marker to terminal's; switching to Basic → byte-identical held raw (re-assert the no-regression round-trip). Construct-not-Show.
- [ ] Confirm (in Dev Notes / a comment) that NO new preference knob / `ReaderContext` field / selection state is added (Q-Preference: the persona choice IS the preference).

### Task 5 — Guard the boundary + the Basic default + the CI gate (AC6, AC7)

- [ ] **CI proof:** the inherited `RenderingPurityTests` / `NoEmbeddedBrowserTests` / `DependencyBoundaryTests` (incl. `Agent_DoesNotReference_App` / `App_References_Agent`) stay green UNCHANGED; a `[Fact]` asserts `typeof(PersonaRegistry).Assembly` is `TheMarkdownWeb.Agent`.
- [ ] **CI proof:** a Basic-byte-identical `[Fact]` (gateway/engine Basic pass-through returns the source byte-identical, LLM never called) — the new real prompts do not touch the Basic path.
- [ ] Confirm `TheMarkdownWeb.sln` + `.github/workflows/build-windows.yml` are **UNCHANGED** (no new project; `paths: clients/windows/**` already covers the changed `PersonaRegistry.cs` + the new/extended test files).
- [ ] Confirm STA discipline: pure `[Fact]` for registry/routing/different-output; `[StaFact]` only for the render-differs + switch-changes-host proofs. No real key/network/socket/model/pixels.
- [ ] Record the **runtime-only** note: real-LLM structural fidelity (does the AI *actually* reorder / lower reading level / compress / emphasize well) is validated at runtime on the reader's machine with a real key — NOT a CI test. Add to the Deferred-Work-Log / a Dev Notes line.

## Dev Notes

### The capturing / keyed fake (the ONLY new test infrastructure)

The existing `CountingLlmClient` (TestDoubles.cs) counts calls and returns ONE canned result for ALL prompts — it does NOT distinguish personas. 4.3 needs two small additions (TEST doubles only — never production):

- **Capturing fake (AC3):** records the `systemPrompt` of its last (or each) `CompleteAsync` call. E.g.:
  ```csharp
  internal sealed class CapturingLlmClient : ILlmClient {
      public string? LastSystemPrompt { get; private set; }
      public int Calls { get; private set; }
      public Task<LlmResult> CompleteAsync(string systemPrompt, string pageMarkdown, ReaderContext ctx, CancellationToken ct) {
          Calls++; LastSystemPrompt = systemPrompt;
          return Task.FromResult(LlmResult.Success("# captured"));
      }
  }
  ```
- **systemPrompt-keyed fake (AC4/AC5):** returns a deterministic, per-prompt-distinct output, e.g. echo a marker derived from the prompt, or map known registry prompts → known canned markdown:
  ```csharp
  internal sealed class KeyedLlmClient : ILlmClient {
      public Task<LlmResult> CompleteAsync(string systemPrompt, string pageMarkdown, ReaderContext ctx, CancellationToken ct)
          => Task.FromResult(LlmResult.Success($"# {Marker(systemPrompt)}\n\n{pageMarkdown}"));
      // Marker is a stable per-prompt token (e.g. a hash prefix, or look up the persona id from the registry prompt).
  }
  ```
  Because the engine forwards `persona.SystemPrompt` verbatim (PersonalityEngine.cs line 82), different personas → different `systemPrompt` → different `Marker` → different output. This is the deterministic substitute for a real model.
- `App.Tests` cannot see `Agent.Tests` internals — declare the App-side variants as local private doubles inside the test class (the established 4-2 pattern: `PersonalizationGatewayTests` / `PersonalityRerenderCoordinatorTests` each declare local `CountingLlmClient`/`InMemorySecretStore`).

### Why the routing proof is not a tautology

The engine ALREADY forwards `persona.SystemPrompt` (4-1 code). AC3 is still load-bearing because it pins the **contract for 4.3+**: that the SELECTED persona's prompt (not a constant, not Basic's empty string) reaches the provider, and that the four real prompts are actually DIFFERENT at the provider boundary. The capturing fake asserts the real engine/gateway forwarding against the real registry prompts — not a re-declared stub prompt.

### Scope discipline (BINDING on Step-4/Step-5)

4.3 is **prompt DATA + tests**. The ONLY production edit is the four `SystemPrompt` strings in `PersonaRegistry.cs`. If the implementer finds themselves adding a production type, a `ReaderContext` field, a new selection knob, or a `Rendering` edit — STOP and re-check against "The hard rules" + Q-Preference. The pipeline (engine/gateway/selector/coordinator) is REUSED UNCHANGED.

### Runtime-only vs CI-provable (re-stated for the implementer)

CI proves the PIPELINE: distinct prompts, correct routing, per-persona-distinct output to the renderer, visible re-render on switch. CI does NOT (cannot) prove the AI writes good cozy/terse/summary/plain prose — that is a real-LLM, runtime/manual judgment on the reader's machine. Do not attempt to unit-test AI quality; do not gate CI on a real model.

## Deferred-Work-Log (4.3 additions)

- **Real-LLM structural-fidelity evaluation** — whether the four prompts *actually* produce well-reordered / lower-reading-level / well-compressed / well-emphasized output is a **runtime/manual** concern (real key, real model), not a CI test. Revisit with a manual eval harness if desired.
- **Prompt tuning / authoring / sharing** — the four prompts are a seed; user-authored/shared personas remain deferred (Epic-4 Deferred-Work-Log).
- **Translate real prompt + language UX + audio/SAPI** — Story **4.4** (Translate stays a minimal placeholder at 4.3).

## Open question (non-blocking)

- **Q-Marker (AC4/AC5 keyed-fake output shape):** the keyed fake may either (a) echo a prompt-derived marker (zero coupling to the registry), or (b) map the exact registry prompts → canned markdown (tighter, but couples the test to the prompt strings). **Recommendation:** (a) a prompt-derived marker (e.g. a short stable hash/token of the systemPrompt) — it stays correct even if the dev refines the prompt wording, and still proves "different prompt → different output → different render". Pinned as the default; the dev may choose (b) if they prefer asserting exact canned outputs. Either is deterministic and CI-safe; this does not block dev.
