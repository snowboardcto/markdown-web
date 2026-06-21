---
title: The Markdown Web
status: draft
created: 2026-06-21
updated: 2026-06-21
---

# PRD: The Markdown Web
*Working title — confirm.*

## 0. Document Purpose

This PRD is for the builder (naethyn) and any downstream UX/architecture/epics work. It builds on the finalized product brief and addendum (`_bmad-output/planning-artifacts/briefs/brief-the-markdown-web-2026-06-21/`), the manifesto (`_bmad-output/the-markdown-web.md`), and the market research (`_bmad-output/planning-artifacts/research/market-markdown-web-competitive-landscape-research-2026-06-21.md`) — it does not duplicate them. Vocabulary is Glossary-anchored; features are grouped with globally numbered FRs nested under them; tech-how (Azure, rendering engine, HTTP mechanics) lives in `addendum.md`; assumptions are tagged inline and indexed in §9.

## 1. Vision

The Markdown Web is a web where URLs point to `.md` files and presentation is delegated, not dictated. The author publishes pure markdown — structure and meaning. A browser still gets a beautiful page; but a reader running the native client has their *own* local agent render that markdown into whatever shape fits them. One source file, a personal experience per reader.

The first job it does is grounded and selfish: give people who use AI agents — and therefore drown in markdown — a place to store, browse, share, and genuinely *enjoy* their `.md`. From that wedge grows the larger thesis: if markdown is the lingua franca for machines, it can be the lingua franca for humans too, rendered by each human's agent.

v0.1 makes the vision real and visible at themarkdownweb.com: markdown rendered beautifully, dogfooded on the author's own files. The native client, content negotiation, and sharing are committed and sequenced behind it — not cut.

## 2. Target User

### 2.1 Jobs To Be Done
- **Keep my markdown somewhere good** — store the `.md` I (and my agents) generate without force-fitting it into a CMS or losing it in folders/gists.
- **Read it pleasurably** — see my markdown rendered beautifully, browsable like a real site, not raw text.
- **Share it** — hand someone a link that looks great for *them*.
- **(Builder's JTBD)** prove the thesis to myself and the world: a live themarkdownweb.com that renders and casts the vision.
- **(Latent) own how I read** — eventually, have my own agent shape any page to me (reading level, language, format, accessibility).

### 2.2 Non-Users (v1)
- People who want a markdown *editor/authoring* tool — you bring your own `.md`; we render, we don't edit.
- Teams needing accounts, permissions, or collaboration workflows (deferred).
- General "AI browser" users expecting to reformat the *entire existing HTML web* — this is a markdown-native web, not a universal page reformatter.

### 2.3 Key User Journeys

- **UJ-1. naethyn reads his own BMAD output, finally beautiful.**
  Customer zero. He points the system at his folder of `.md` (manifesto, brief, this PRD) and pushes. themarkdownweb.com rebuilds. He opens it on his phone: clean typography, working links between docs, images inline. He reads the manifesto end to end and it feels like a real publication, not a text dump. **Resolution:** his markdown has a home he's proud of; adding a file = a new page. **Realizes FR-1, FR-2, FR-3, FR-5, FR-6, FR-8, FR-17, FR-18.**

- **UJ-2. Dana finds the page from a Google search, with no agent at all.**
  Dana, a curious developer who's never heard of this, lands on a themarkdownweb.com page from search. It loads instantly as normal, beautiful HTML — crawlable, no app required. She reads it, gets the idea, and notices a subtle "open this in the client for a personalized view" affordance. **Resolution:** born-compatible reach; the page recruited her. **Realizes FR-5, FR-7, FR-8.** *Edge case:* no JavaScript / old browser still renders readable content.

- **UJ-3. Theo opens a `.md` in the native client and his agent renders it for him.**
  Theo, an agent-and-markdown power user, opens the same URL in the native client. His local agent renders the page his way — terminal-crisp, his language, the section he cares about surfaced first — and he trusts it because it's *his* agent doing it, locally, on his behalf. **Resolution:** per-reader rendering working end to end; same source, his shape. **Realizes FR-9, FR-10, FR-11, FR-12, FR-13, FR-14.** *(Committed, post-v0.1.)*

## 3. Glossary

- **The Markdown Web** — the system: `.md` files addressable by URL, with presentation delegated to the reader's side.
- **`.md` page** — a single markdown file addressable at a URL; one file = one page.
- **Vault** — an author's collection of `.md` pages plus media, browsable as a linked space.
- **Author** — the person who publishes `.md` pages.
- **Reader** — the person consuming a `.md` page (via browser or native client).
- **HTML client** — the server-side path that renders a `.md` page to clean HTML for browsers.
- **Native client** — the application that hands raw markdown to the reader's local agent for rendering.
- **Local agent** — the reader's own AI agent, running on the reader's side, that performs rendering on their behalf.
- **Per-reader rendering** — presentation generated for an individual reader by their local agent (layout, reading level, language, accessibility, emphasis).
- **Content negotiation** — serving the representation (HTML vs raw markdown) appropriate to the requesting consumer from one URL.
- **Living Link** — a shared `.md` URL that renders for whoever opens it.
- **Feed** — new `.md` pages from a followed vault, delivered to a reader.

## 4. Features

### 4.1 Markdown Content & Vault
**Description:** The canonical content layer. An Author drops `.md` files (plus media) into a Vault; each file becomes an addressable `.md` page. Relative markdown links between files resolve to navigable routes so Readers can browse around. Realizes UJ-1. Use Glossary terms exactly.

**Functional Requirements:**

#### FR-1: File-as-page
Author can add a `.md` file to the Vault and it becomes an addressable `.md` page at a URL (one file = one page).
**Consequences (testable):**
- Adding `gear-guide.md` produces a reachable page at its corresponding URL.
- Removing the file removes the page.

#### FR-2: Inter-file linking
Author can link between `.md` pages using relative markdown links that resolve to navigable routes. Realizes UJ-1.
**Consequences (testable):**
- A `[guide](gear-guide.md)` link navigates to that page in the HTML client.
- A link to a missing file surfaces a clear broken-link state, not a crash.

#### FR-3: Media embedding
Author can embed images and video referenced relatively; media is served alongside the content.
**Consequences (testable):**
- `![](media/powder.jpg)` renders inline; the asset is served from the Vault.

#### FR-4: Browsable space
A Vault (folder of `.md` + media) is presented as a browsable, linked site.
**Consequences (testable):**
- The Vault's structure is navigable without manually typing URLs.

### 4.2 The HTML Client (browser path)
**Description:** When a Reader hits a `.md` page from a browser, the system server-renders clean, beautiful HTML — no agent required. This is the born-compatible path: it works everywhere, is crawlable, and casts the vision. Realizes UJ-1, UJ-2.

**Functional Requirements:**

#### FR-5: Server-rendered HTML
A browser requesting a `.md` page receives clean server-rendered HTML with no agent or app required. Realizes UJ-2.
**Consequences (testable):**
- The page is readable with JavaScript disabled.
- Response is valid HTML a crawler can parse.

#### FR-6: Beautiful default presentation
Rendered pages use a high-quality default theme (typography, spacing, media layout) such that content "looks awesome."
**Consequences (testable):**
- A first-time reader perceives a designed publication, not a raw dump (qualitative; validated via SM-2).

#### FR-7: Crawlable & unfurlable
HTML pages are SEO-friendly and produce rich link previews when shared into chat/social.
**Consequences (testable):**
- A page exposes title/description metadata; a link preview renders in a standard unfurler.

#### FR-8: Navigation
Reader can browse and navigate the Vault via rendered links. Realizes UJ-1, UJ-2.

### 4.3 The Native Client & Per-Reader Rendering *(committed; post-v0.1)*
**Description:** The heart of the product. A Reader opens a `.md` page in the native client; their local agent renders the presentation for them — reflowing, re-leveling, translating, re-emphasizing — without HTML. Trust comes from locality: it's the Reader's *own* agent. Realizes UJ-3. `[ASSUMPTION: native client and per-reader rendering are post-v0.1 per the brief's roadmap, though committed — confirm the MVP boundary.]`

**Functional Requirements:**

#### FR-9: Open in native client
Reader can open a `.md` page in the native client and receive a rendering produced by their local agent. Realizes UJ-3.

#### FR-10: Per-reader rendering
The native client renders presentation shaped to the individual Reader (layout, reading level, emphasis, format).
**Consequences (testable):**
- Two readers with different stated preferences see materially different renderings of the same `.md` page.

#### FR-11: Accessibility & translation as rendering outcomes
Per-reader rendering can produce accessible and translated presentations (e.g. audio, large-text/reflowed, translated) from the same source with no author effort.
**Consequences (testable):**
- A reader configured for translation sees the page in their language; one configured for audio gets a spoken rendering.

#### FR-12: Local-agent trust
Rendering in the native client is performed by the Reader's own local agent; no third party rewrites content in transit.
**Consequences (testable):**
- The raw `.md` the agent received is inspectable/available to the Reader.

#### FR-13: Works everywhere
The native client is available across the Reader's platforms. `[ASSUMPTION: cross-platform form factor (app/extension/CLI/web) is an open decision — see §8; requirement is ubiquity, not a specific shell.]`

### 4.4 Content Negotiation *(committed; post-v0.1)*
**Description:** One URL, audience-appropriate representation. Browsers get HTML (FR-5); agents and the native client get raw markdown — from the same address. Realizes UJ-3.

**Functional Requirements:**

#### FR-14: One URL, two representations
A `.md` page serves HTML to browsers and raw markdown to agents/native client based on the request, with caching that keeps the two from being confused.
**Consequences (testable):**
- A request signaling a preference for markdown receives markdown; a browser receives HTML; caches vary on the request signal.

### 4.5 Sharing *(committed; later)*
**Description:** The Vault turned outward. Realizes the social thesis. `[NON-GOAL for MVP]`

**Functional Requirements:**

#### FR-15: Living Link
Reader can share a `.md` URL that renders for whoever opens it (HTML for browsers, per-reader in the native client).

#### FR-16: Follow / Feed
Reader can follow a Vault and receive its new `.md` pages as a Feed.

### 4.6 Publishing & Hosting
**Description:** How an Author gets a Vault live. Realizes UJ-1. Tech-how (Azure Static Web Apps, CI) in `addendum.md`.

**Functional Requirements:**

#### FR-17: Publish on push
Author publishes by committing/pushing changes; the live site updates automatically.
**Consequences (testable):**
- Pushing a new `.md` results in a new live page without manual deploy steps.

#### FR-18: Custom domain over HTTPS
The Vault is served at a custom domain (themarkdownweb.com) over HTTPS.

## 5. Non-Goals (Explicit)
- **Not a markdown editor/authoring app** — Authors bring their own `.md`.
- **Not a CMS or page-builder** — no WYSIWYG, no author-side presentation controls (that's the whole point).
- **Not a universal AI browser** — it renders the Markdown Web, not the entire existing HTML web.
- **Not an accounts/permissions platform in v1** — no auth, teams, or access control yet.
- **Not monetized yet** — business model deliberately parked.

## 6. MVP Scope

### 6.1 In Scope (v0.1)
- Markdown Content & Vault: FR-1, FR-2, FR-3, FR-4.
- HTML Client: FR-5, FR-6, FR-7, FR-8.
- Publishing & Hosting: FR-17, FR-18 (live at themarkdownweb.com).
- Seed content: the manifesto and planning docs, dogfooded.

### 6.2 Out of Scope for MVP *(committed, sequenced — not cut)*
- Native Client & Per-Reader Rendering: FR-9–FR-13 — *the core differentiator; v0.5.* `[NOTE FOR PM: emotionally load-bearing — naethyn said "we need it all." Sequenced, not abandoned; revisit if v0.1 lands fast.]`
- Content Negotiation: FR-14 — *v0.5, pairs with the native client.*
- Sharing / Feed: FR-15, FR-16 — *later.*
- Accounts / multi-user, monetization.

## 7. Success Metrics

**Primary**
- **SM-1**: Author adoption (dogfood) — naethyn hosts his markdown at themarkdownweb.com and reads it at least weekly without abandoning it after a month. Validates FR-1–FR-4, FR-8, FR-17, FR-18.
- **SM-2**: "Gets it" on sight — a first-time visitor to themarkdownweb.com perceives a beautiful publication and can articulate the vision unprompted. Validates FR-5, FR-6, FR-7.

**Secondary**
- **SM-3**: Per-reader rendering proven — the native client renders the same `.md` materially differently for two readers, end to end (not mocked). Validates FR-9–FR-12.
- **SM-4**: Beyond customer-zero — at least one other agent-and-markdown user publishes a Vault. Validates FR-1–FR-4, FR-17.

**Counter-metrics (do not optimize)**
- **SM-C1**: Don't trade beauty/speed for breadth — core reading experience load time and visual quality must not regress as features are added. Counterbalances SM-3, SM-4.
- **SM-C2**: Don't sacrifice born-compatibility — the no-agent HTML experience (FR-5) must stay first-class while chasing personalization. Counterbalances SM-3.

## 8. Open Questions
1. **MVP boundary** — confirm native client + content negotiation are post-v0.1 (PRD assumes yes).
2. **Native client form factor** — extension vs app vs OS-agent vs CLI vs web (must run a local agent and work everywhere).
3. **Markdown flavor & frontmatter** — which markdown spec (CommonMark/GFM), and how is YAML frontmatter treated (metadata vs rendered)? `[ASSUMPTION: GFM + frontmatter-as-metadata — confirm.]`
4. **Business model** — parked; revisit before scaling.
5. **Identity & discovery** — how readers find each other's Vaults (pairs with Sharing).
6. **Author incentive** — why authors publish `.md` and surrender presentation control.

## 9. Assumptions Index
- §4.3 — Native client & per-reader rendering are post-v0.1 (committed), not MVP. *(Confirm MVP boundary.)*
- §4.3 FR-13 — Native client form factor is undecided; requirement is cross-platform ubiquity, not a specific shell.
- §6.2 — MVP cut = v0.1 (HTML client path) per brief roadmap, despite "we need it all."
- §8.3 — Markdown flavor = GFM; YAML frontmatter treated as metadata.
