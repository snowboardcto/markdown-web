---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-the-markdown-web-2026-06-21/prd.md
  - _bmad-output/planning-artifacts/prds/prd-the-markdown-web-2026-06-21/addendum.md
  - _bmad-output/planning-artifacts/briefs/brief-the-markdown-web-2026-06-21/brief.md
  - _bmad-output/planning-artifacts/research/market-markdown-web-competitive-landscape-research-2026-06-21.md
workflowType: 'architecture'
lastStep: 8
status: 'complete'
completedAt: '2026-06-21'
project_name: 'The Markdown Web'
user_name: 'naethyn'
date: '2026-06-21'
---

# Architecture Decision Document — The Markdown Web

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Foundational Constraints (confirmed before decisions)

**FC-1 — Two render paths, deliberately different engines:**
- **HTML render path (FR-5/FR-6, browser audience):** server-rendered HTML, consumed by browsers — Chromium and all engines. Chromium is the *target*, by design. Compatibility layer.
- **Native client render path (FR-9–FR-13):** renders markdown to **native UI**, via a **non-HTML, non-webview** path. No embedded browser engine (Chromium, WebKit, WebView2) — embedding any HTML webview reproduces the browser ("just the same thing") and defeats the client's reason to exist.

**FC-2 — Agent output contract:** because the native client draws native UI, the **local agent emits a declarative UI structure (or native primitives), never HTML.** Shape: `markdown + reader context → agent → declarative UI description → native renderer → native widgets`. Candidate pattern to weigh: A2UI-style declarative UI (from market research) — agent emits structured UI, rendered natively per platform.

_Confirmed by naethyn, 2026-06-21._

## Project Context Analysis

### Requirements Overview

**Functional Requirements:** 18 FRs in 6 groups. MVP = FR-1–FR-14 (Vault + HTML client + native client + content negotiation); Sharing (FR-15/16) deferred. Three runtime surfaces:
- **Static publish path** (FR-1–8, 17–18): markdown → HTML, Azure static hosting, push-to-deploy. Low risk.
- **Content-negotiation backend** (FR-14): one URL → HTML for browsers / raw markdown for agents+native client. Small, standard.
- **Native client + local agent** (FR-9–13): non-HTML native rendering driven by the reader's local agent. High novelty — the innovation budget goes here.

**Non-Functional Requirements (architecture drivers):**
- No embedded browser engine in the native client (FC-1, hard).
- Cross-platform reach ("works everywhere") without Chromium.
- Beauty + performance budget; reading experience must not regress (SM-C1).
- Born-compatibility/SEO: no-agent HTML path stays first-class (SM-C2).
- Local-agent trust: rendering by the reader's own local agent (FR-12).
- Accessibility & translation as render outcomes (FR-11), not author work.

### Scale & Complexity
- Primary domain: full-stack (static web + serverless + native client + agent integration).
- Complexity: medium overall; high on the native client. No multi-tenancy, compliance, or real-time — surfaces 1–2 stay cheap.
- Estimated architectural components: ~5 (markdown parse layer, static site builder, content-negotiation function, native client renderer, local-agent integration).

### Technical Constraints & Dependencies
- Azure hosting; GitHub Actions CI/CD (confirmed shape, addendum).
- Markdown flavor default GFM + frontmatter-as-metadata (open).
- No accounts/multi-user/monetization in v1.
- Domain themarkdownweb.com (owned).

### Cross-Cutting Concerns
1. Shared markdown parse layer / AST — one parser feeding both render paths to prevent divergence.
2. Agent-output contract (declarative-UI schema) — linchpin of the native client; define early.
3. Local-agent integration model — bundled / BYO-key / local model; drives "works everywhere," cost, trust.
4. Link + media resolution — relative links resolved at build time (HTML) vs render time (native).
5. Content-negotiation caching — `Vary: Accept` correctness.

## Foundation & Stack Decisions

### Confirmed
- **Canonical content spec: GitHub Flavored Markdown (GFM).** All renderers (browser + every native client) parse GFM identically so "the same `.md`" renders the same structure (intentional per-personality differences come *on top*, never by accident).
- **Browser path: Astro** (static HTML for browsers that reach us instead of the native client). markdown → remark/rehype (GFM) → HTML, Shiki for code highlighting, GitHub-style stylesheet.
- **Native client = "the Markdown Web browser":** an own, per-platform native app (Chrome model — its own native build per device), NOT a cross-platform framework, NOT a webview/Chromium. **First platform: Windows.**
- **AI personalities** = the per-reader rendering layer *inside* the native client, layered on top of the faithful base render (FR-10). Out of bedrock scope; bedrock = faithful GFM rendering first.

### Native client bedrock — Windows stack (verified versions, June 2026)
- **Runtime: .NET 10** (current LTS, supported to Nov 2028).
- **Parser: Markdig 1.3.1** — fast, CommonMark 0.31.2 + GFM (tables, task lists, etc.), produces an AST. The de-facto .NET markdown processor.
- **UI: WPF (FlowDocument)** *(CONFIRMED — WinUI 3 considered and rejected: no FlowDocument, more DIY for tables/rich docs)* — native rendering (no webview), and FlowDocument is a mature document model that maps almost 1:1 to the markdown AST (Paragraph/Run/Bold, headings, List/ListItem, Table, Image, mono code).
- **Render path:** Markdig AST → WPF FlowDocument. Prior art to reuse/learn from: `markdig.wpf`, `Markdig.FlowDocument`, `MdXaml`.


## Rendering Pipeline (bedrock — "render markdown like GitHub")

**Pipeline:** `raw .md → Markdig parse (GFM) → AST → [AI personality transform — later] → FlowDocument → WPF native render`. The base render is deterministic/faithful; the AI personality is an optional transform stage slotted between AST and FlowDocument later, keeping the bedrock pure and testable.

### GFM → FlowDocument element mapping
| Markdown (GFM) | FlowDocument target |
|---|---|
| `#`–`######` headings | `Paragraph` with heading styles (size/weight) |
| bold / italic / strikethrough | `Bold` / `Italic` / `Run` w/ strikethrough |
| paragraph | `Paragraph` |
| inline code | `Run` — mono font + subtle background |
| fenced code block | `Section`/`Paragraph` — mono, background, syntax-highlighted, language preserved |
| lists (ordered/unordered) | `List` + `ListItem` |
| task lists (GFM) | `List` + inline `CheckBox` |
| blockquote | bordered `Section` (left rule) |
| GFM tables | `Table` (native FlowDocument table) |
| images | `InlineUIContainer` → `Image` (resolved from vault) |
| links | `Hyperlink` + click handler |
| horizontal rule | styled separator |

### Three details that make it "feel like GitHub"
1. **Code highlighting:** ColorCode-Universal (.NET → FlowDocument runs) for bedrock; TextMateSharp later for broader grammars. *(Trade-off: simplicity now vs language coverage later.)*
2. **Link resolution (FR-2):** intercept `Hyperlink` clicks — relative `.md` → navigate in-client (fetch+render); `#anchor` → scroll; external `http(s)` → system browser (not a general web browser); missing target → clear broken-link state, never a crash.
3. **Images/media (FR-3):** resolve relative paths against the vault → WPF `Image`; video via `MediaElement` (later concern).

### Cross-cutting: parser consistency across platforms (future fork)
Markdig is .NET-only. Future iOS/Android clients need GFM-conformant parsers each (swift-markdown, etc.). When that lands, decide: shared `cmark-gfm` core via FFI vs hold each client to GFM conformance. Windows-first: Markdig is the choice.

## Project Structure & Boundaries

Monorepo layout:

```
themarkdownweb/
├── content/                    # the Vault (FR-1–4): seed .md + media; single source of truth
│   ├── index.md
│   ├── the-markdown-web.md     # manifesto = page one (dogfood)
│   └── media/
├── web/                        # Browser path / HTML client (FR-5–8): Astro + remark/rehype (GFM) + Shiki
│   ├── astro.config.mjs
│   ├── src/pages/              # .md → routes
│   ├── src/layouts/
│   └── src/styles/github.css   # GitHub-style stylesheet (FR-6)
├── api/                        # Content negotiation (FR-14): Azure Function — Accept → HTML | raw .md, Vary: Accept
│   └── negotiate/
├── clients/
│   └── windows/                # Native client (FR-9–13): .NET 10 + WPF
│       ├── TheMarkdownWeb.sln
│       ├── App/                # shell, window, navigation, fetch raw .md
│       ├── Rendering/          # BEDROCK: Markdig AST → FlowDocument (pure, no net, no AI)
│       │   ├── MarkdownToFlowDocument.cs
│       │   ├── ElementRenderers/
│       │   └── Highlighting/   # ColorCode
│       └── Agent/              # AI-personality transform (later; isolated)
├── infra/staticwebapp.config.json   # Azure SWA config (FR-18)
├── .github/workflows/
│   ├── deploy-web.yml          # build Astro → deploy Azure SWA (FR-17)
│   └── build-windows.yml       # build/test WPF client
└── _bmad-output/               # existing planning artifacts
```

### Boundaries
- **`content/` is the single source of truth** — both `web/` (build time) and the Windows client (runtime via `api/`) consume the same `.md`. No content in code.
- **`Rendering/` is isolated** from `App/` and `Agent/` — pure, independently testable bedrock (no networking, no AI). `App` and `Agent` depend on it, never the reverse. Lets "render like GitHub" land first and personalities slot in later.
- **`api/` only negotiates** — browsers → static HTML (Astro/SWA); clients → raw `.md`.

### FR → component map
| FRs | Lives in |
|---|---|
| 1–4 Vault | `content/` (consumed by `web/` & `api/`) |
| 5–8 HTML client | `web/` (Astro) |
| 9–13 Native client | `clients/windows/` (App + Rendering + Agent) |
| 14 Content negotiation | `api/` |
| 17–18 Publish/host | `.github/workflows/` + `infra/` + Azure SWA |

## Validation

### Coherence ✅ (one watch-item)
- Stack compatible: Node/TS (`web/` Astro + `api/` Functions) and C#/.NET 10 (`clients/windows/` WPF) communicate over HTTP; GFM is the shared contract. Versions current (Astro 5, .NET 10, Markdig 1.3.1).
- ⚠️ Coherence risk: GFM parsed in two engines (remark for web, Markdig for client). Both conform to CommonMark 0.31.2 + GFM; mitigate by holding both to the same spec. Acceptable Windows-first.

### Requirements coverage
- FR-1–4 Vault ✅; FR-5–8 HTML client ✅; FR-9 ✅; FR-12 ✅; FR-14 ✅; FR-17–18 ✅.
- FR-10–11 (per-reader rendering, accessibility/translation): ⚠️ provisioned (seam `AST → [Agent] → FlowDocument`), internals deferred — needs the agent-integration decision.
- FR-13 works-everywhere: ⚠️ partial by design (Windows-first; other platforms roadmap + shared-parser fork).
- FR-15–16 Sharing: out of MVP.

### NFRs ✅
No-Chromium ✅ (WPF native, no webview); beauty/perf ✅ (Astro zero-JS + native WPF); born-compat/SEO ✅ (Astro static); don't-reinvent-plumbing ✅. WPF provides baseline screen-reader accessibility via UI Automation.

### Verdict
MVP **bedrock is coherent and implementation-ready**. Two intentional, named gaps:
1. **AI-personality layer (FR-10/11)** — seam defined; internals deferred pending the **agent-integration decision (bundled model vs BYO-API-key vs local model)**. The clear next architecture session.
2. **Works-everywhere (FR-13)** — Windows-first by choice; other platforms + shared-parser consistency are roadmap.
No silent gaps: everything the bedrock needs is specified; everything deferred is named.

## Open Architecture Decisions (next session)
1. **Agent-integration model** for the AI-personality layer: bundled model / BYO-API-key / local model — drives FR-10/11, "works everywhere," cost, and the local-trust story (FR-12).
2. **Cross-platform parser consistency** when iOS/Android/other-desktop clients arrive: shared `cmark-gfm` core via FFI vs per-client GFM conformance.
3. **Code-highlighting upgrade path:** ColorCode (bedrock) → TextMateSharp (broader grammars).
4. **Markdown frontmatter handling** (GFM default confirmed; frontmatter-as-metadata to confirm in impl).
