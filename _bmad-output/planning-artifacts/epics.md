---
stepsCompleted: [1]
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-the-markdown-web-2026-06-21/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/ux-designs/ux-the-markdown-web-2026-06-21/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-the-markdown-web-2026-06-21/EXPERIENCE.md
---

# The Markdown Web - Epic Breakdown

## Overview

This document decomposes the PRD, UX (DESIGN.md + EXPERIENCE.md), and Architecture into implementable stories. MVP per PRD = FR-1–FR-14 (both clients + content negotiation); Sharing (FR-15–16) deferred. Windows-first native client.

## Requirements Inventory

### Functional Requirements

FR-1: File-as-page — adding a `.md` file to the Vault makes it an addressable page at a URL (one file = one page).
FR-2: Inter-file linking — relative markdown links between `.md` pages resolve to navigable routes; missing target → clear broken-link state.
FR-3: Media embedding — relatively-referenced images/video served alongside content.
FR-4: Browsable space — a Vault is a browsable, linked site with an entry/index surface.
FR-5: Server-rendered HTML — a browser receives clean server-rendered HTML, no agent/app required.
FR-6: Beautiful default presentation — the default render looks genuinely good (GitHub-aligned, not plain).
FR-7: Crawlable & born-compatible — readable with JS disabled; valid HTML a crawler can parse (SEO).
FR-8: Navigation — browse/navigate the Vault via rendered links (in-page nav, back/forward).
FR-9: Open in client — open a `.md` page in the native client.
FR-10: Per-reader rendering — the native client renders presentation shaped to the individual reader; differences are structural (ordering/level/language/format), not cosmetic ("Zero Shared Pixels for humans").
FR-11: Accessibility & translation as rendering outcomes — audio, large-text/reflow, translation produced from the same source with no author effort.
FR-12: Local agent — per-reader rendering runs via the reader's own local agent (trust by locality).
FR-13: Works everywhere — native client available across platforms (Windows first); NO Chromium dependency.
FR-14: Content negotiation — one URL serves HTML to browsers and raw markdown to agents/native client (`Vary: Accept`).
FR-15: Living Link (post-MVP) — a shared `.md` URL renders for whoever opens it.
FR-16: Follow / Feed (post-MVP) — follow a Vault and receive its new pages.
FR-17: Publish on push — author publishes by committing/pushing; the live site updates automatically.
FR-18: Custom domain over HTTPS — served at themarkdownweb.com over HTTPS (HTTP→HTTPS redirect).

### NonFunctional Requirements

NFR-1: No-Chromium (hard) — the native client must not depend on Chromium/WebView2/any embedded browser engine; it renders to native UI.
NFR-2: Works-everywhere — cross-platform reach (Windows first; iOS/Android/other desktop are roadmap).
NFR-3: Beauty + performance budget (SM-C1) — rendering must not regress the reading experience; web is fast (static, zero/low JS), native is responsive.
NFR-4: Born-compatibility / SEO (SM-C2) — the agentless HTML path stays first-class and crawlable.
NFR-5: Local-agent trust (FR-12) — no server-side rewriting of human-facing content; rendering is local to the reader.
NFR-6: Accessibility floor — web semantic HTML + WCAG AA contrast + keyboard/focus; native WPF UI Automation labeled, keyboard-reachable controls.
NFR-7: Don't reinvent commodity plumbing — use standard Astro / Azure SWA / content negotiation; no novel infrastructure.

### Additional Requirements

- **Monorepo scaffold (greenfield, Epic 1 Story 1):** `content/`, `web/` (Astro), `api/` (Azure Function), `clients/windows/` (.NET 10 WPF: App + Rendering + Agent), `infra/`, `.github/workflows/`. No external starter template; this is the foundational scaffold.
- **Web stack:** Astro + remark/rehype (GFM) + Shiki; GitHub-style stylesheet.
- **API stack:** Azure Functions — content negotiation (`Accept` → HTML | raw `.md`, `Vary: Accept`).
- **Native client stack:** .NET 10 + C# + WPF (FlowDocument) + Markdig 1.3.1; render Markdig AST → FlowDocument; ColorCode highlighting.
- **Canonical spec:** GFM (CommonMark 0.31.2 + GFM extensions); web (remark) and client (Markdig) held to the same spec.
- **Rendering boundary:** `clients/windows/Rendering` is pure (no networking, no AI), independently testable; `App`/`Agent` depend on it, never reverse.
- **Foundational constraint:** native client renders native UI, no webview; AI personality (Agent/) emits a declarative UI structure (later), never HTML.
- **CI/CD:** GitHub Actions — `deploy-web.yml` (Astro → Azure SWA), `build-windows.yml` (build/test WPF).
- **Hosting:** Azure Static Web Apps + custom domain themarkdownweb.com + free SSL.

### UX Design Requirements

UX-DR1: Web GitHub-style default theme — implement DESIGN.md tokens (colors, typography, layout/measure 760px, code syntax palette) as the basic render; light default (dark = personality concern).
UX-DR2: `site-header` component (web) — sticky translucent header: `.md the markdown web` wordmark · "the vision" link · get-client CTA. NOT fake browser chrome.
UX-DR3: `pitch-card` component (web) — end-of-page recruiting card: vision headline + body + "Get the client" + "Why a markdown web?" link.
UX-DR4: Native client shell — window titlebar ("The Markdown Web") + toolbar (back/forward/reload).
UX-DR5: `address-bar` (native) — loads `.md` URLs only; shows lock + host/path + `.md only` tag; non-`.md` declined.
UX-DR6: `personality-selector` (native) — toolbar chip to choose the rendering personality; switching re-renders the current page in place.
UX-DR7: State patterns — loading; broken/missing `.md`; non-`.md` address → decline + offer system browser; agentless web = full experience; no-personality first-run → fallback to basic render.
UX-DR8: Interaction primitives — internal `.md` link → in-place nav; `#anchor` → scroll; external `http(s)` → system browser; personality switch → re-render.
UX-DR9: Accessibility floor — web semantic HTML/contrast/keyboard/focus; native labeled, keyboard-reachable WPF controls.
UX-DR10: Voice/microcopy — pitch headline/body, `.md only` hint, "Your personality summarized this" label; plain, confident, developer-credible.

### FR Coverage Map

{{requirements_coverage_map}}

## Epic List

{{epics_list}}
