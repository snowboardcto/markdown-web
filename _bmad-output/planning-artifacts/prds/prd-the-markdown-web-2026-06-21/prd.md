---
title: The Markdown Web
status: final
created: 2026-06-21
updated: 2026-06-23
---

# PRD: The Markdown Web
*Working title — confirm.*

## 0. Document Purpose

This PRD is for the builder (naethyn) and any downstream UX/architecture/epics work. It builds on, and does not duplicate, the finalized product brief and addendum (`_bmad-output/planning-artifacts/briefs/brief-the-markdown-web-2026-06-21/`), the manifesto (`_bmad-output/the-markdown-web.md`), and the market research (`_bmad-output/planning-artifacts/research/market-markdown-web-competitive-landscape-research-2026-06-21.md`).

Conventions: vocabulary is Glossary-anchored; features are grouped, with globally numbered FRs nested under them; tech-how (Azure, rendering engine, HTTP mechanics) lives in `addendum.md`; assumptions are tagged inline and indexed in §9.

## 1. Vision

The Markdown Web is a web where URLs point to `.md` files and presentation is delegated, not dictated. The author publishes pure markdown — structure and meaning. A browser still gets a beautiful page; but a reader running the native client has their *own* local agent render that markdown into whatever shape fits them. One source file, a personal experience per reader.

The first job it does is grounded and selfish: give people who use AI agents — and therefore drown in markdown — a place to store, browse, share, and genuinely *enjoy* their `.md`. From that wedge grows the larger thesis, and the wedge into the whole industry conversation: everyone says markdown is for models and HTML is for humans — what if markdown were the lingua franca for humans too, rendered by each human's agent?

v0.1 makes the vision real and visible at themarkdownweb.com: markdown rendered beautifully, dogfooded on the author's own files — the manifesto, rendered, *is* the proof of concept. The native client, content negotiation, and sharing are committed and sequenced behind it — not cut.

## 2. Target User

### 2.1 Jobs To Be Done
- **Keep my markdown somewhere good** — store the `.md` I (and my agents) generate without force-fitting it into a CMS or losing it in folders/gists.
- **Read it pleasurably** — see my markdown rendered beautifully, browsable like a real site, not raw text.
- **Share it** — hand someone a link that looks great for *them*.
- **(Builder's JTBD)** prove the thesis to myself and the world: a live themarkdownweb.com that renders and casts the vision.
- **(Latent) own how I read** — eventually, have my own agent shape any page to me (reading level, language, format, accessibility).

### 2.2 Non-Users (v1)
- People who want a markdown *editor/authoring* tool — they bring their own `.md`; we render, we don't edit.
- Teams needing accounts, permissions, or collaboration workflows (deferred).
- General "AI browser" users expecting to reformat the *entire existing HTML web* — this is a markdown-native web, not a universal page reformatter.

### 2.3 Key User Journeys

- **UJ-1. naethyn reads his own BMAD output, finally beautiful.**
  Customer zero. He points the system at his folder of `.md` (manifesto, brief, this PRD) and pushes. themarkdownweb.com rebuilds. He opens it on his phone: clean typography, working links between docs, images inline. He reads the manifesto end to end and it feels like a real publication, not a text dump. **Resolution:** his markdown has a home he's proud of; adding a file = a new page. **Realizes FR-1, FR-2, FR-3, FR-5, FR-6, FR-8, FR-17, FR-18.**

- **UJ-2. Dana finds the page from a Google search, with no agent at all.**
  Dana, a curious developer who's never heard of this, lands on a themarkdownweb.com page from search. It loads instantly as normal, beautiful HTML — crawlable, no app required. She reads it, gets the idea, and notices a subtle "open this in the client for a personalized view" affordance. **Resolution:** born-compatible reach; the page recruited her. **Realizes FR-5, FR-7, FR-8.** *Edge case:* no JavaScript / old browser still renders readable content.

- **UJ-3. Theo opens a `.md` in the native client and his agent renders it for him.**
  Theo, an agent-and-markdown power user, opens the same URL in the native client. His local agent renders the page his way — terminal-crisp, his language, the section he cares about surfaced first — and he trusts it because it's *his* agent doing it, locally, on his behalf. **Resolution:** per-reader rendering working end to end; same source, his shape. **Realizes FR-9, FR-10, FR-11, FR-12, FR-13, FR-14.** *(MVP; sequenced after the HTML-client slice.)*

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
- **Markdown Lens** — the native client used as a reader for the *wider* markdown-native web: point it at any site and, if that site exposes a markdown representation, render it (per-reader) the same way it renders our own Vault.
- **Markdown discovery** — the client-side protocol that determines whether a given URL has an available markdown representation and fetches it (see FR-21).
- **markdown-native web** — the (currently niche, developer/docs-concentrated) set of sites that publish a discoverable markdown representation: `<link rel="alternate" type="text/markdown">`, content negotiation, `.md` siblings, or `llms.txt` adopters.

## 4. Features

### 4.1 Markdown Content & Vault
**Description:** The canonical content layer. An Author drops `.md` files (plus media) into a Vault; each file becomes an addressable `.md` page. Relative markdown links between files resolve to navigable routes so Readers can browse around. Realizes UJ-1.

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
A Vault (folder of `.md` + media) is presented as a browsable, linked site. Realizes UJ-1.
**Consequences (testable):**
- The Vault's structure is navigable without manually typing URLs.
- The Vault exposes an entry/index surface that lists or links its pages.

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
**Consequences (testable):**
- Clicking an in-page link to another `.md` page loads that page.
- Browser back/forward returns to the prior page.

### 4.3 The Native Client & Per-Reader Rendering *(MVP)*
**Description:** The heart of the product. A Reader opens a `.md` page in the native client; their local agent renders the presentation for them — reflowing, re-leveling, translating, re-emphasizing — without HTML. Trust comes from locality: it's the Reader's *own* agent. Realizes UJ-3.

**Functional Requirements:**

#### FR-9: Open in native client
Reader can open a `.md` page in the native client and receive a rendering produced by their local agent. Realizes UJ-3.

#### FR-10: Per-reader rendering
The native client renders presentation shaped to the individual Reader (layout, reading level, emphasis, format). This is "Zero Shared Pixels for humans" — the differences are *substantive* (structure, ordering, reading level, language, format), not merely cosmetic theming.
**Consequences (testable):**
- Two readers with different stated preferences receive renderings that differ structurally (e.g. section ordering, summarization, reading level, or language) — not only color/font.
- A Reader who changes a preference sees the rendering change accordingly.

#### FR-11: Accessibility & translation as rendering outcomes
Per-reader rendering can produce accessible and translated presentations (e.g. audio, large-text/reflowed, translated) from the same source with no author effort.
**Consequences (testable):**
- A reader configured for translation sees the page in their language; one configured for audio gets a spoken rendering.
- Translated output preserves the page's headings, links, and structure; audio rendering covers the full body in reading order.

#### FR-12: Local-agent trust
Rendering in the native client is performed by the Reader's own local agent; no third party rewrites content in transit.
**Consequences (testable):**
- The raw `.md` the agent received is inspectable/available to the Reader.

#### FR-13: Works everywhere
The native client is available across the Reader's platforms. `[ASSUMPTION: cross-platform form factor (app/CLI/native-UI) is an open decision — see §8; requirement is ubiquity, not a specific shell.]`
**Consequences (testable):**
- The native client runs on at least the Reader's primary desktop and mobile platforms.
- No part of the client requires Chromium/Electron to install or run.

**Feature-specific NFRs:**
- **No Chromium dependency** — the native client must not depend on Chromium (rules out Electron, Chromium-based webviews, and Chrome extensions). It renders markdown to native UI, not via a bundled browser engine. *(Hard constraint, naethyn.)*
- **Local execution** — per-reader rendering runs via the Reader's own local agent (FR-12); no server-side rewriting of human-facing content.

### 4.4 Content Negotiation *(MVP)*
**Description:** One URL, audience-appropriate representation. Browsers get HTML (FR-5); agents and the native client get raw markdown — from the same address. Realizes UJ-3.

**Functional Requirements:**

#### FR-14: One URL, two representations
A `.md` page serves HTML to browsers and raw markdown to agents/native client based on the request, with caching that keeps the two from being confused. Realizes UJ-3.
**Consequences (testable):**
- A request signaling a preference for markdown receives markdown; a browser receives HTML; caches vary on the request signal.

### 4.5 Sharing *(committed; later)*
**Description:** The Vault turned outward. Realizes the social thesis. `[NON-GOAL for MVP]`

**Functional Requirements:**

#### FR-15: Living Link
Reader can share a `.md` URL that renders for whoever opens it (HTML for browsers, per-reader in the native client).
**Consequences (testable):**
- The same shared URL renders as HTML in a browser and per-reader in the native client.

#### FR-16: Follow / Feed
Reader can follow a Vault and receive its new `.md` pages as a Feed.
**Consequences (testable):**
- After following a Vault, a newly added `.md` page surfaces to the follower.

### 4.6 Publishing & Hosting
**Description:** How an Author gets a Vault live. Realizes UJ-1. Tech-how (Azure Static Web Apps, CI) in `addendum.md`.

**Functional Requirements:**

#### FR-17: Publish on push
Author publishes by committing/pushing changes; the live site updates automatically. Realizes UJ-1.
**Consequences (testable):**
- Pushing a new `.md` results in a new live page without manual deploy steps.

#### FR-18: Custom domain over HTTPS
The Vault is served at a custom domain (themarkdownweb.com) over HTTPS. Realizes UJ-1, UJ-2.
**Consequences (testable):**
- themarkdownweb.com serves the Vault over HTTPS with a valid certificate.
- Plain HTTP requests redirect to HTTPS.

### 4.7 The Markdown Lens — reading the wider markdown web *(post-MVP; Epic 6, Windows-first)*
**Description:** The native client, today, reads our own Vault. The Lens turns it outward: point the client at *any* URL and, if that site publishes a discoverable markdown representation, render it with the same pipeline (and per-reader rendering) used for our Vault. This extends — and partly relaxes — the native client's original `.md`-only address handling (FR-9).

**Positioning (decided 2026-06-23, research-grounded):** the client is **"the native reader for the markdown-native web"** — docs/dev sites, `llms.txt` adopters, and our own Vault. Markdown availability on the open web is **niche, not universal** (research basket: markdown resolved on developer/docs properties; ordinary marketing/news sites returned nothing, some blocked non-browser requests; ~10% of domains expose `llms.txt`, page-level `.md` rarer still). Therefore **"no markdown available" (FR-22) is an expected, first-class outcome**, not a failure — and we never fall back to reformatting arbitrary HTML (keeps the §5 "not a universal AI browser" non-goal intact). Evidence: `_bmad-output/planning-artifacts/research/technical-markdown-discovery-for-arbitrary-websites-research-2026-06-23.md`.

**Functional Requirements:**

#### FR-19: Client default home
On launch (and on a "home" action), the native client opens `themarkdownweb.com` by default rather than a blank address bar.
**Consequences (testable):**
- A freshly launched client shows themarkdownweb.com content, not an empty state.

#### FR-20: Open any URL
The native client accepts any `http(s)` URL in its address bar, not only `.md` URLs — superseding the `.md`-only restriction of FR-9 / the original address-bar rule. Non-`http(s)` schemes are still declined.
**Consequences (testable):**
- Entering `https://example.com/docs/intro` (no `.md`) is accepted and triggers discovery (FR-21), not an immediate decline.
- A non-`http(s)` scheme (e.g. `ftp:`, `javascript:`) is still declined.

#### FR-21: Markdown discovery
For an entered URL, the client determines whether a markdown representation exists via an ordered, first-hit-wins cascade with a bounded probe budget, then fetches and renders it:
1. GET the URL with `Accept: text/markdown` **and** parse the returned HTML `<head>` for `<link rel="alternate" type="text/markdown" href=...>` (content negotiation + autodiscovery in one round-trip);
2. `.md` sibling probe (`<path>.md`);
3. `/llms.txt` at the site root — treated as a **site index hint, not the page itself**.
Each candidate is validated by `Content-Type` and an HTML-doctype byte-sniff to reject soft-404s / HTML-served-as-markdown. The client uses an honest (non-spoofed) User-Agent and treats an explicit bot-block (e.g. 403) as a distinct outcome.
**Consequences (testable):**
- A site exposing a markdown alternate or `.md` representation renders as markdown.
- A `200 text/html` response to a `.md` probe is rejected (no false-positive render).
- An `llms.txt` hit is surfaced as available markdown resources, not rendered as the page body.

#### FR-22: No-markdown state
When the cascade finds no valid markdown representation, the client clearly states **"no markdown available"** for that URL. There is **no HTML fallback / no reader-mode** for arbitrary HTML.
**Consequences (testable):**
- A site with no discoverable markdown shows the explicit no-markdown state, not a blank page or a crash.
- A bot-blocked fetch is distinguishable from a genuine no-markdown result (distinct messaging).

**Feature-specific NFRs:**
- **Reuse, don't rebuild** — discovery feeds the existing Markdig render pipeline + content-negotiation work (FR-14 / Story 2.7); Rendering stays pure (NFR-1, NFR-5).
- **Politeness & performance** — bounded probe count per URL, sane timeouts, and caching so discovery stays fast and well-behaved.

### 4.8 Reach: additional clients *(post-MVP; Epic 7)*
**Description:** Extend the native reading experience beyond Windows. Realizes the cross-platform ubiquity intent of FR-13 / NFR-2.

**Functional Requirements:**

#### FR-23: iOS native reader
An iOS native client renders `.md` pages (Vault + Markdown Lens) for the Reader, honoring the **no-Chromium** constraint (NFR-1) — a native iOS render path, not a bundled webview. *(Deferred; sequenced after Epic 6.)* `[ASSUMPTION: iOS render-stack choice (e.g. SwiftUI-native vs .NET MAUI) is an architecture decision — the WPF FlowDocument renderer does not port directly; see §8.]`
**Consequences (testable):**
- An iOS Reader can open and read a `.md` page rendered natively (no Chromium/WebView).

## 5. Non-Goals (Explicit)
- **Not a markdown editor/authoring app** — Authors bring their own `.md`.
- **Not a CMS or page-builder** — no WYSIWYG, no author-side presentation controls (that's the whole point).
- **Not a universal AI browser** — it renders the markdown-native web, not the entire existing HTML web. The Markdown Lens (FR-19–22) reads markdown *wherever* a site publishes it, but when none exists it says "no markdown available" (FR-22) — it never reformats or reader-modes arbitrary HTML.
- **Not an accounts/permissions platform in v1** — no auth, teams, or access control yet.
- **Not monetized yet** — business model deliberately parked.
- **Not reinventing commodity plumbing** — markdown hosting and content negotiation are near-commodity patterns (Cloudflare, Vercel, Mintlify); use standard approaches, don't build novel infrastructure. The differentiator is per-reader human-facing rendering, not the pipes.

## 6. MVP Scope

MVP includes **both clients** (naethyn: "we need both"). To keep the build sane, sequence *within* the MVP: HTML-client path first (gets themarkdownweb.com live), native client second — but v1 is not done until both work.

`[NOTE FOR PM: this is a large MVP — a non-Chromium native client running a local agent is a substantial build on top of the static path. The HTML-client slice (FR-1–8, 17–18) is independently shippable and casts the vision; treat it as the internal first milestone even though it is not the MVP boundary.]`

### 6.1 In Scope (MVP)
- Markdown Content & Vault: FR-1, FR-2, FR-3, FR-4.
- HTML Client: FR-5, FR-6, FR-7, FR-8.
- Native Client & Per-Reader Rendering: FR-9, FR-10, FR-11, FR-12, FR-13 (non-Chromium).
- Content Negotiation: FR-14.
- Publishing & Hosting: FR-17, FR-18 (live at themarkdownweb.com).
- Seed content: the manifesto and planning docs, dogfooded.

### 6.2 Out of Scope for MVP *(committed, sequenced — not cut)*
- Sharing / Feed: FR-15, FR-16 — *after both clients are solid.*
- Accounts / multi-user, monetization.

### 6.3 Post-MVP Roadmap *(beyond the v1 boundary)*
- **Delivered:** Sharing / Feed (FR-15, FR-16) — **Epic 5, shipped** (Living Link + static RSS Feed).
- **Epic 6 — The Markdown Lens (Windows-first):** FR-19, FR-20, FR-21, FR-22. Lead with a thin discovery spike (validate the FR-21 cascade against real sites via a real `HttpClient`) before the UX work, per the 2026-06-23 research.
- **Epic 7 — iOS reader:** FR-23. Separate architecture fork (the WPF FlowDocument renderer does not port directly).

## 7. Success Metrics

**Primary**
- **SM-1**: Author adoption (dogfood) — naethyn hosts his markdown at themarkdownweb.com and reads it at least weekly without abandoning it after a month. Validates FR-1–FR-4, FR-8, FR-17, FR-18.
- **SM-2**: "Gets it" on sight — a first-time visitor to themarkdownweb.com perceives a beautiful publication and can articulate the vision unprompted. Validates FR-5, FR-6, FR-7.
- **SM-3**: Per-reader rendering proven — the native client (non-Chromium) renders the same `.md` *structurally* differently for two readers (ordering / reading level / language / format, not just theming), end to end (not mocked). Validates FR-9–FR-14.

**Secondary**
- **SM-4**: Beyond customer-zero — at least one other agent-and-markdown user publishes a Vault. Validates FR-1–FR-4, FR-17.

**Counter-metrics (do not optimize)**
- **SM-C1**: Don't trade beauty/speed for breadth — the core reading experience's load time and visual quality must not regress as features are added. Counterbalances SM-3, SM-4.
- **SM-C2**: Don't sacrifice born-compatibility — the no-agent HTML experience (FR-5) must stay first-class while chasing personalization. Counterbalances SM-3.

## 8. Open Questions
1. **Native client form factor** — given the **no-Chromium** constraint (FR-13 NFR), what shell? Native desktop UI (Swift/Kotlin/Rust-native), CLI/TUI, or a non-Chromium GUI toolkit. Must run a local agent and work everywhere. *(Architecture decision.)*
2. **Markdown flavor & frontmatter** — which markdown spec (CommonMark/GFM), and how is YAML frontmatter treated (metadata vs rendered)? `[ASSUMPTION: GFM + frontmatter-as-metadata — confirm.]`
3. **Local agent integration** — which agent(s) does the native client drive, and how (bundled, BYO-API-key, local model)? Affects "works everywhere." *(Architecture.)*
4. **Business model** — parked; revisit before scaling.
5. **Identity & discovery** — how readers find each other's Vaults (pairs with Sharing).
6. **Author incentive** — why authors publish `.md` and surrender presentation control.
7. **Timing risk (watch)** — the moat is non-technical and *time-sensitive*; an incumbent (Comet/Dia/Atlas/A2UI) could ship "render this `.md` for you." HTML-first sequencing delays the differentiating native-client capability — monitor and protect the native-client timeline.
8. **iOS render stack (Epic 7)** — the WPF FlowDocument renderer does not port to iOS; choose a non-Chromium native path (SwiftUI-native vs .NET MAUI vs shared-core-with-native-render). *(Architecture decision.)*
9. **Markdown discovery robustness (Epic 6, mostly resolved by 2026-06-23 research)** — confirm live behavior of the `Accept: text/markdown` branch with a real `HttpClient` (the research tool couldn't send custom headers), and the bot-block/CDN handling for non-browser fetches, in the discovery spike.

## 9. Assumptions Index
- §4.3 FR-13 — Native client form factor is undecided; requirement is cross-platform ubiquity with **no Chromium dependency** (hard constraint, confirmed).
- §8.2 — Markdown flavor = GFM; YAML frontmatter treated as metadata. *(Default — confirm.)*
- §4.8 FR-23 — iOS render-stack choice (SwiftUI-native vs .NET MAUI vs shared-core) is an open architecture decision; requirement is a native, no-Chromium iOS reader. *(Confirm at Epic 7.)*
- §4.7 FR-20 — Supersedes the original `.md`-only address-bar rule (FR-9 / UX-DR5 "non-`.md` declined"); UX-DR5 must be revised when Epic 6 is cut. *(Logged.)*
