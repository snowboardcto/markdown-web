---
status: final
created: 2026-06-21
updated: 2026-06-21
sources:
  - _bmad-output/planning-artifacts/prds/prd-the-markdown-web-2026-06-21/prd.md
  - _bmad-output/planning-artifacts/architecture.md
---

# EXPERIENCE.md — The Markdown Web

Visual identity lives in [`DESIGN.md`](DESIGN.md); this owns information architecture, behavior, states, interactions, accessibility, and flows. Tokens referenced as `{path.to.token}`.

## Foundation

**Multi-surface**, two clients off one content source:
- **Web (browser path)** — Astro-rendered static HTML at `themarkdownweb.com`. No app, no agent, works in any browser; its job is to render faithfully *and* recruit to the native client.
- **Native client (Windows first)** — WPF desktop app: a browser *for the Markdown Web*. Renders markdown to native UI (no webview); content presentation is delegated to a reader-chosen **AI personality**.

No accounts in v1. UI system: none external — DESIGN.md is the visual reference; the native shell uses native WPF controls.

## Information Architecture

- **`.md page`** — the atomic unit; one file = one URL = one page.
- **Vault** — an author's collection of `.md` pages + media, browsable via inter-file links.
- **Web surfaces:** the rendered page · the sticky **site-header** (wordmark · the vision · get-client) · the end-of-page **pitch-card**. That's it — content first.
- **Native client surfaces:** **address-bar** (loads `.md` only) · back/forward/reload · **personality-selector** · the personality-rendered page.

IA closes: every stated need has a surface — *keep/read/share my markdown* → web pages + vault; *read it my way* → native client + personality; *get someone onto the client* → pitch-card + get-client-cta.

## Voice and Tone

Microcopy is plain, confident, developer-credible — never salesy. Anchors:
- Pitch headline: **"You're reading one fixed view. There's a better one."**
- Pitch body: *"…your own AI personality renders every `.md` page your way — your layout, your language, your reading level. Same file. Your shape."*
- Address-bar tag: **`.md only`**. Hint: *"Address bar loads `.md` pages only — this is a browser for the Markdown Web."*
- Personality TL;DR label: **"Your personality summarized this."**

## Component Patterns *(behavioral; visuals in DESIGN.md)*

- **address-bar** — accepts and loads **`.md` URLs only**; non-`.md` input is rejected or coerced (see State Patterns). Shows lock + host/path + `.md only` tag.
- **personality-selector** — switches the rendering personality; re-renders the current page in place without re-fetching the source.
- **get-client-cta / pitch-card** — present on every web page; link to the client download. The page's built-in upgrade moment.
- **link** — resolved by context (see Interaction Primitives).

## State Patterns

- **Loading** — fetch raw `.md`, then render; show lightweight progress, not a blank flash.
- **Broken / missing `.md`** — clear "page not found / not a markdown page" state, never a crash (PRD FR-2).
- **Non-`.md` address (native)** — the client declines to load it and explains it loads `.md` only; offers to open it in the system browser instead.
- **Agentless (web)** — default state; full content + the upgrade moment. No degraded experience.
- **No personality / first run (native)** — falls back to a faithful **basic** render (the DESIGN.md web theme) until a personality is chosen.

## Interaction Primitives

- **Internal `.md` link** → navigate **in-place** (web: route; native: load+render in the client).
- **Anchor `#heading`** → scroll within the current page.
- **External `http(s)` link** → open in the **system browser** (the client is not a general web browser).
- **Personality switch** → instant re-render of the current page.
- **Publish (author)** → push to the content repo; the live page updates (PRD FR-17).

## Accessibility Floor

- **Web:** semantic HTML headings/lists/tables, keyboard-navigable links, visible focus, text contrast meets WCAG AA against `{colors.surface}`.
- **Native:** WPF UI Automation gives baseline screen-reader support for free; the shell's controls are labeled and keyboard-reachable.
- **As an outcome:** per-reader rendering makes accessibility a *feature* — a personality can produce audio, large-text/reflowed, or translated presentations from the same source (PRD FR-11).

## Key Flows

- **KF-1. Dana arrives with no agent and leaves wanting one.**
  Dana lands on a `themarkdownweb.com` page from a Google search. It loads instantly as clean, readable HTML — she gets the content. At the foot of the page the **pitch-card** explains: this is one fixed view; the client renders every `.md` *your* way. **Climax:** she clicks **Get the client** — the page recruited her. *(Realizes PRD FR-5, FR-7, FR-13 handoff.)*

- **KF-2. Theo reads a `.md` his way in the native client.**
  Theo types `themarkdownweb.com/guides/powder-day.md` into the client's **address-bar** (it only takes `.md`). He picks his **personality** (`Cozy Reader`); his local agent renders the page — warm serif, a generated TL;DR, his language. He switches to `Terminal` and the same file snaps to a crisp monospace view. **Climax:** same source, his shape — instantly, locally, his own agent. *(Realizes PRD FR-9–FR-12, FR-14.)*

- **KF-3. naethyn publishes and it's just there.**
  naethyn drops a new `.md` into his vault and pushes. The live page appears at `themarkdownweb.com` with working links and inline media — no deploy ritual. **Climax:** add a file, get a page. *(Realizes PRD FR-1–FR-4, FR-17, FR-18.)*

## Mockups
- [`mockups/basic-html-render.png`](mockups/basic-html-render.png) · [`mockups/web-v2.png`](mockups/web-v2.png) · [`mockups/native-client.png`](mockups/native-client.png)

_Spines win on conflict with any mock or import._
