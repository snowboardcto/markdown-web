# Architecture Addendum — Epic 4: Personalized Rendering (AI Personalities)

status: decided
created: 2026-06-21
decided_by: product owner (naethyn)
resolves: the open "agent-integration model" decision flagged in epics.md (Epic 4 NOTE FOR PM)
sources:
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/epics.md (Epic 4, Stories 4.1–4.4; FR-10/11/12, NFR-5)
  - _bmad-output/the-markdown-web.md (manifesto — "the reader's own agent renders it")

This addendum resolves the foundational decisions for Epic 4. Every Epic-4 story (4.1–4.4) is bound by it.

---

## D1 — Agent-integration model: **Bring-Your-Own-Key (BYO-key)**

The reader supplies their **own LLM API key**; the client acts as the reader's agent and calls the LLM
API **from the reader's machine, with the reader's key**. No Markdown-Web-operated inference, no bundled
model in v1.

- **Default provider/model:** Anthropic **Claude** (latest capable model, e.g. a current Sonnet for
  quality or Haiku for speed/cost — the reader pays, so default to a strong general model and allow
  model selection later). The API call uses the Anthropic Messages API. (A second provider, e.g. OpenAI,
  and a local-model path via Ollama are explicitly **deferred** — see Deferred-Work-Log.)
- **Trust model (NFR-5 / manifesto):** the agent runs **on the reader's machine** under the reader's
  control and key — there is no Markdown-Web platform between the reader and the page. "Local" here means
  *the reader's agent, locally controlled*, not *offline inference* (offline = the deferred Ollama path).
  **No human-facing content is rewritten server-side** — personalization happens client-side at render time.
- **Key storage:** the API key is stored **encrypted, per-user, locally** — Windows **DPAPI**
  (`ProtectedData`, CurrentUser scope) via an `ISecretStore` abstraction. The key is **never logged**, never
  sent anywhere except the chosen provider's API endpoint over TLS. The UI discloses that page content +
  the persona prompt are sent to the reader's chosen API with the reader's key.

## D2 — How a personality personalizes a page: **markdown → markdown transform**

A personality is a **markdown-to-markdown transform** performed by the LLM, after which the **existing
pure `FlowDocumentRenderer` (Epic 3) renders the transformed markdown**. This reuses the entire Epic-3
render + highlight + theme chain and keeps `Rendering` pure and deterministic.

```
fetch .md  ──►  Agent.PersonalityEngine (LLM transform, BYO-key)  ──►  transformed .md  ──►  FlowDocumentRenderer  ──►  FlowDocument
                 (persona prompt + page markdown + reader context)        (pure markdown)        (pure, Epic 3)
```

- **A personality = a named persona prompt** that instructs the transform. Seed set:
  **Basic** (no transform — the Epic-3 faithful render, the default/Story-3.6 baseline), **Cozy Reader**
  (warm, reflowed, adds a TL;DR), **Terminal** (terse, monospace-friendly), **TL;DR** (summarize),
  **Plain Language** (lower reading level), **Translate → <language>** (FR-11). The output of every persona
  is **valid markdown** so the deterministic renderer + GFM/highlighting/theme still apply.
- **Structural, not cosmetic (FR-10):** because the transform changes the *markdown* (ordering, headings,
  reading level, language, emphasis, omission), two personalities yield **structurally different** renders
  of the same source — satisfying Story 4.3.
- **The Epic-3 seam:** `FlowDocumentRenderOptions.Theme`/`RenderTheme` stays; personality selection is an
  **App/Agent concern layered on top** (the renderer still just renders whatever markdown it's given).
  `RenderTheme.Basic` remains the no-personality default.

## D3 — Module boundary (extends the Epic-3 boundary)

```
App  ──►  Agent  ──►  (LLM provider API over HTTPS, reader's key)
 │         │
 │         └─ owns: networking + AI. May reference System.Net.Http + the LLM SDK/client. Persona prompts,
 │            the transform orchestration, ISecretStore (key), ILlmClient (provider call). Produces a
 │            markdown string. Depends on neither App nor Rendering.
 │
 └─ owns: UI (personality-selector in the toolbar slot reserved since Story 3.2), key entry/storage UX,
    wiring: page markdown ─► Agent.Personalize(...) ─► resulting markdown ─► FlowDocumentRenderer (Epic 3).

Rendering  ── stays PURE: no networking, no AI, no LLM SDK. Renders markdown only. The existing
              RenderingPurityTests / NoEmbeddedBrowserTests / App→Rendering boundary guards remain green.
              (Agent is the ONLY module allowed networking+AI.)
```

- **NoEmbeddedBrowser still holds** — an LLM API client is not a browser engine; the forbidden-substring
  guard (webview/cef/chromium/…) is unaffected.
- **Agent purity-of-a-different-kind:** Agent does net+AI by design, but must not embed a browser and must
  not depend "up" on App.

## D4 — Windows-CI-only verification (no real network/key in CI)

Same discipline as Epics 2–3. The LLM call and key store are **abstractions injected for test**:

- `ILlmClient` (e.g. `Task<string> CompleteAsync(PersonaPrompt, pageMarkdown, ct)`) — tests use a **fake**
  returning canned transformed markdown; **no real API call, no key, no socket** in CI. The real impl
  (`AnthropicLlmClient`) is exercised only at runtime on the reader's machine.
- `ISecretStore` (get/set/clear the API key) — tests use an **in-memory fake**; the real `DpapiSecretStore`
  is Windows-only and lightly smoke-tested (round-trip protect/unprotect) under `[StaFact]`/`[Fact]` as
  feasible without asserting on ciphertext.
- **Persona prompt templates** + the transform-orchestration logic (model selection, ret(ry)/error →
  graceful fallback to the **Basic** render, timeout/cancel, missing-key → prompt for key) are **pure,
  deterministic `[Fact]`s**. Personality re-render-in-place (Story 4.2) reuses the Epic-3
  `ContentHostController` + a `[StaFact]`.
- **Totality / failure modes:** no key → clear "add your key" state (never crash); API error/timeout/rate-
  limit → fall back to Basic render + a non-blocking notice; oversized page → chunk or degrade gracefully.
  Nothing throws to the UI.

## D5 — Audio personality (Story 4.4) — TTS, no key required for playback

The **audio** personality (FR-11) is two-stage: (1) optionally use the LLM transform to produce a clean
**reading-order script** (reuses D2), then (2) speak it via **Windows built-in TTS** (`System.Speech` /
SAPI) — **offline, no API key, no cost**. Audio playback covers the full body in reading order. (Cloud
neural TTS is deferred.) Translation (FR-11) is the LLM transform of D2 with the target language.

## Deferred-Work-Log (Epic 4)

- Second provider (OpenAI) + provider abstraction beyond Anthropic default.
- **Local/offline model** path (Ollama) — the privacy-max option from the agent-model decision; revisitable.
- Streaming token-by-token render (v1 is non-streaming with a progress affordance).
- Cloud neural TTS / voice selection beyond SAPI.
- Personality authoring/sharing (user-defined personas) — v1 ships the seed set.
- Caching transformed renders per (page, persona, model).

## Story binding

- **4.1 Local agent integration (FR-12/NFR-5):** the `Agent` module + `ILlmClient`/`ISecretStore`/persona
  scaffolding + BYO-key wiring; the client can invoke the reader's agent with the page markdown + reader
  context; no server-side rewrite. Real provider call behind `ILlmClient`.
- **4.2 Personality selector (UX-DR6):** the toolbar personality-selector (reserved slot from 3.2);
  choosing a personality re-renders the current page **in place without re-fetching** (reuses the held
  markdown + `ContentHostController`).
- **4.3 Per-reader structural rendering (FR-10):** two personalities → structurally different markdown →
  different renders of the same source; changing a preference visibly changes the render.
- **4.4 Accessibility & translation outcomes (FR-11):** translation personality (target language, structure
  preserved) + audio personality (SAPI TTS, full body in reading order).
