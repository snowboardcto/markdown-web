---
stepsCompleted: [1, 2, 3, 4]
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
- **Infrastructure as Code (IaC):** Azure resources (hosting, custom domain themarkdownweb.com, TLS) provisioned via IaC — **Bicep** (Azure-native default; Terraform alternative). Established in the walking-skeleton epic.
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

FR-1: Epic 2 — file-as-page (web vault)
FR-2: Epic 2 + Epic 3 — inter-file linking (web render + client render)
FR-3: Epic 2 + Epic 3 — media embedding (web + client)
FR-4: Epic 2 — browsable space / index
FR-5: Epic 2 — server-rendered HTML
FR-6: Epic 2 + Epic 3 — beautiful default presentation (web + client)
FR-7: Epic 2 — crawlable / SEO / born-compatible
FR-8: Epic 2 + Epic 3 — navigation (web + client)
FR-9: Epic 3 — open in native client
FR-10: Epic 4 — per-reader rendering
FR-11: Epic 4 — accessibility & translation as outcomes
FR-12: Epic 4 — local agent
FR-13: Epic 3 — works everywhere (Windows first) / no-Chromium
FR-14: Epic 2 — content negotiation
FR-15: Epic 5 (post-MVP) — Living Link
FR-16: Epic 5 (post-MVP) — Follow / Feed
FR-17: Epic 1 (proven end-to-end) + Epic 2 (real content build) — publish on push
FR-18: Epic 1 — custom domain over HTTPS

## Epic List

### Epic 1: Walking Skeleton — IaC + CI/CD live on Azure
A minimal placeholder page is provisioned and deployed end-to-end: GitHub Actions builds, IaC (Bicep) provisions Azure hosting + custom domain + TLS, and the site is live at themarkdownweb.com over HTTPS, auto-deployed on push. Validates the entire deployment spine; everything else depends on it.
**FRs covered:** FR-17 (end-to-end), FR-18. Plus: monorepo scaffold, IaC, GitHub Actions, Azure hosting.

### Epic 2: Publish & Read on the Web
An author drops markdown into a vault and it goes live at themarkdownweb.com as a beautiful, browsable, crawlable site that also casts the vision and recruits to the client. Built on the Epic 1 skeleton.
**FRs covered:** FR-1, FR-2, FR-3, FR-4, FR-5, FR-6, FR-7, FR-8, FR-14, FR-17 (content build).

### Epic 3: Read Markdown in the Native Client (Windows)
A reader opens a .md URL in the Windows "Markdown Web browser" and sees it rendered beautifully — GitHub-style, native, no Chromium. The rendering bedrock (Markdig AST -> WPF FlowDocument).
**FRs covered:** FR-9, FR-13, and client-side FR-2, FR-3, FR-6, FR-8.

### Epic 4: Personalized Rendering (AI Personalities)
The reader picks an AI personality; their local agent re-renders the page their way — structural personalization plus accessibility/translation as outcomes. Builds on Epic 3 and the open agent-integration architecture decision.
**FRs covered:** FR-10, FR-11, FR-12.

### Epic 5 (post-MVP): Sharing & Feed
Share a Living Link that renders for whoever opens it; follow a vault's Feed. Deferred per PRD MVP scope.
**FRs covered:** FR-15, FR-16.

### Epic 6 (post-MVP): The Markdown Lens (Windows)
Turn the native client outward: default to themarkdownweb.com, open any http(s) URL (not just .md), and discover + render markdown from the wider markdown-native web — saying "no markdown available" when none exists. Positioning: the native reader for the markdown-native web (niche, not universal). Windows-first.
**FRs covered:** FR-19, FR-20, FR-21, FR-22.

### Epic 7 (post-MVP): iOS Reader
Extend the native reading experience to iOS — a native, no-Chromium render path. Deferred; separate architecture fork from the WPF FlowDocument renderer.
**FRs covered:** FR-23.

---

## Epic 1: Walking Skeleton — IaC + CI/CD live on Azure

Prove the entire deployment spine end-to-end with trivial content before any feature is built on it.

### Story 1.1: Scaffold the monorepo

As a developer,
I want the monorepo scaffolded with a home for every component,
So that all later work has a consistent, documented place to live.

**Acceptance Criteria:**

**Given** a fresh clone of the repository
**When** I inspect the tree
**Then** `content/`, `web/`, `api/`, `clients/windows/`, `infra/`, and `.github/workflows/` exist with placeholder READMEs
**And** `web/` contains a minimal Astro project that builds successfully (`astro build` produces output)
**And** the root README documents the layout and the component boundaries from the architecture.

### Story 1.2: Provision Azure hosting via IaC (Bicep)

As a developer,
I want the Azure hosting defined as code,
So that the environment is reproducible and not hand-clicked.

**Acceptance Criteria:**

**Given** the Bicep templates in `infra/`
**When** I deploy them to a resource group
**Then** an Azure Static Web App (hosting) is created and its default hostname is emitted as an output
**And** re-running the deployment is idempotent (no drift, no duplicate resources)
**And** the templates are parameterized (no hard-coded secrets or subscription IDs).

### Story 1.3: Deploy a placeholder page via GitHub Actions on push (FR-17)

As a developer,
I want pushes to deploy automatically,
So that "publish on push" is proven before real content exists.

**Acceptance Criteria:**

**Given** the `deploy-web.yml` GitHub Actions workflow and the provisioned hosting
**When** I push a commit that changes the placeholder page
**Then** the workflow builds and deploys it without manual steps
**And** the updated page is reachable at the Static Web App default hostname over HTTPS
**And** a failed build does not deploy (the live site is unchanged).

### Story 1.4: Bind custom domain themarkdownweb.com over HTTPS (FR-18)

As a reader,
I want the site at themarkdownweb.com over HTTPS,
So that the product lives at its real, trusted address.

**Acceptance Criteria:**

**Given** the provisioned hosting and DNS for themarkdownweb.com (configured via IaC where supported)
**When** I visit `https://themarkdownweb.com`
**Then** the placeholder page loads over HTTPS with a valid certificate
**And** a plain `http://` request redirects to `https://`.

---

## Epic 2: Publish & Read on the Web

Build the real, beautiful web experience on the proven skeleton.

### Story 2.1: Render a `.md` file to an HTML page (FR-1, FR-5, FR-7)

As an author,
I want each `.md` file rendered as an HTML page,
So that anyone with a browser can read it.

**Acceptance Criteria:**

**Given** a file `content/x.md` with GFM content
**When** the site builds and deploys
**Then** `/x` is a valid HTML page rendering headings, bold/italic, lists, code, and tables correctly
**And** the page is readable with JavaScript disabled
**And** the HTML is well-formed for crawlers (valid markup, no client-render dependency for content).

### Story 2.2: Apply the GitHub-style default theme (FR-6, UX-DR1, UX-DR9)

As a reader,
I want the page to look genuinely good,
So that reading on the web feels like a real publication, not raw text.

**Acceptance Criteria:**

**Given** a rendered page
**When** it displays
**Then** it applies the DESIGN.md tokens (typography, color, 760px measure, code syntax palette via Shiki)
**And** code blocks are syntax-highlighted
**And** text contrast meets WCAG AA against the surface color.

### Story 2.3: Inter-file linking and navigation (FR-2, FR-8)

As a reader,
I want links between pages to work and navigation to feel natural,
So that I can browse around a vault.

**Acceptance Criteria:**

**Given** a page containing `[guide](gear-guide.md)`
**When** I click it
**Then** I navigate to `/gear-guide`
**And** browser back/forward returns to the prior page
**And** a link to a missing file shows a clear broken-link / not-found state, never a crash.

### Story 2.4: Media embedding (FR-3)

As an author,
I want images and video to render inline,
So that my pages are rich, not text-only.

**Acceptance Criteria:**

**Given** `![](media/powder.jpg)` in a page with the asset in the vault
**When** the page renders
**Then** the image appears inline and the asset is served from the vault path.

### Story 2.5: Browsable vault index (FR-4)

As a reader,
I want an entry surface that lists the vault's pages,
So that I can find my way without typing URLs.

**Acceptance Criteria:**

**Given** a vault of several `.md` pages
**When** I visit the index
**Then** it lists or links the vault's pages
**And** I can reach any page by navigation alone.

### Story 2.6: Site header and pitch card (UX-DR2, UX-DR3, UX-DR10)

As a first-time visitor,
I want the page to convey the vision and how to get the client,
So that the web recruits me to the native experience.

**Acceptance Criteria:**

**Given** any rendered web page
**When** it displays
**Then** a sticky site-header shows the `.md the markdown web` wordmark, a "the vision" link, and a "Get the client" CTA
**And** an end-of-page pitch-card shows the vision headline + body + "Get the Markdown Web client" + "Why a markdown web?" link
**And** the microcopy matches EXPERIENCE.md Voice and Tone.

### Story 2.7: Content negotiation — one URL, two representations (FR-14)

As an agent or native client,
I want the same URL to serve raw markdown on request,
So that clients can fetch the source while browsers get HTML.

**Acceptance Criteria:**

**Given** a `.md` page URL served by the Azure Function
**When** a request sends `Accept: text/markdown`
**Then** the response is the raw `.md` with `Content-Type: text/markdown` and `Vary: Accept`
**And** a request with `Accept: text/html` (a browser) receives the HTML page
**And** caches never serve a markdown response to a browser.

---

## Epic 3: Read Markdown in the Native Client (Windows)

The rendering bedrock: a native Windows "Markdown Web browser" that renders GFM beautifully, no Chromium.

### Story 3.1: WPF app shell with toolbar (FR-13, NFR-1, UX-DR4)

As a reader,
I want a native Windows app window with a browser-like toolbar,
So that I have a place to read Markdown Web pages.

**Acceptance Criteria:**

**Given** the built .NET 10 WPF client
**When** I launch it
**Then** a native WPF window opens with a titlebar ("The Markdown Web") and a toolbar (back/forward/reload)
**And** the application has no Chromium / WebView2 / embedded-browser dependency (verified in project references).

### Story 3.2: `.md`-only address bar and fetch (FR-9, FR-14 consume, UX-DR5, UX-DR7)

As a reader,
I want to type a `.md` URL and have it loaded,
So that I can open any Markdown Web page in the client.

**Acceptance Criteria:**

**Given** the client toolbar
**When** I enter a `.md` URL
**Then** the address bar shows a lock, host/path, and a `.md only` tag, and the client fetches the raw markdown via `Accept: text/markdown`
**And** entering a non-`.md` URL is declined with an option to open it in the system browser instead.

### Story 3.3: Markdig AST → FlowDocument rendering (FR-6, GFM bedrock)

As a reader,
I want markdown rendered faithfully to native UI,
So that pages look like a clean, GitHub-style document.

**Acceptance Criteria:**

**Given** the pure `Rendering` library and a `.md` string
**When** it renders
**Then** it parses GFM with Markdig and produces a WPF FlowDocument mapping headings, bold/italic/strikethrough, paragraphs, inline + fenced code, lists, task lists, blockquotes, GFM tables, and images per DESIGN.md
**And** the `Rendering` library has no networking or AI dependencies and is covered by unit tests.

### Story 3.4: Code syntax highlighting (FR-6)

As a reader,
I want fenced code blocks highlighted,
So that code is readable and the render feels like GitHub.

**Acceptance Criteria:**

**Given** a fenced code block with a language tag
**When** it renders in the FlowDocument
**Then** tokens are syntax-highlighted via ColorCode
**And** an unknown or missing language degrades to plain monospace, not an error.

### Story 3.5: In-client links, media, navigation (FR-2, FR-3, FR-8, UX-DR8)

As a reader,
I want links and media to behave correctly inside the client,
So that I can browse around natively.

**Acceptance Criteria:**

**Given** a rendered page in the client
**When** I click a relative `.md` link
**Then** the client fetches and renders that page in place; an `#anchor` scrolls within the page; an external `http(s)` link opens in the system browser
**And** images resolve from the vault and render inline
**And** a broken link shows a clear state, never a crash.

### Story 3.6: Basic faithful default render (no-personality) (FR-6, UX-DR7, UX-DR9)

As a reader without a personality selected,
I want a faithful basic render by default,
So that the client is useful before any AI personalization exists.

**Acceptance Criteria:**

**Given** a first run or no personality selected
**When** I open a page
**Then** it renders with the faithful basic theme (the bedrock render)
**And** the shell's controls are labeled and keyboard-reachable via WPF UI Automation.

---

## Epic 4: Personalized Rendering (AI Personalities)

The differentiator — the reader's own local agent re-renders pages per person. Builds on Epic 3.

> **[NOTE FOR PM] Epic 4 depends on the open architecture decision: agent-integration model (bundled model / BYO-API-key / local model). Resolve before starting Story 4.1.**

### Story 4.1: Local agent integration (FR-12, NFR-5)

As a reader,
I want my own local agent wired into the client,
So that personalization runs on my behalf, locally.

**Acceptance Criteria:**

**Given** the resolved agent-integration model and the `Agent` module
**When** the client renders a page
**Then** it can invoke the reader's local agent with the page markdown/AST plus reader context
**And** no human-facing content is rewritten server-side (rendering is local).

### Story 4.2: Personality selector (UX-DR6)

As a reader,
I want to choose which AI personality renders pages,
So that I control how content meets me.

**Acceptance Criteria:**

**Given** the toolbar personality-selector
**When** I choose a personality
**Then** the current page re-renders in place using that personality without re-fetching the source.

### Story 4.3: Per-reader structural rendering (FR-10)

As a reader,
I want a personality to reshape the page for me,
So that I get my own version of the same source.

**Acceptance Criteria:**

**Given** two different personalities and the same `.md`
**When** each renders it
**Then** the results differ structurally (ordering, reading level, emphasis, or language), not merely cosmetically
**And** changing a preference visibly changes the render.

### Story 4.4: Accessibility and translation outcomes (FR-11)

As a reader with accessibility or language needs,
I want personalities to produce accessible/translated renders,
So that the same source meets me without author effort.

**Acceptance Criteria:**

**Given** a translation personality
**When** it renders a page
**Then** the page appears in the target language with headings, links, and structure preserved
**And** an audio personality produces a spoken rendering covering the full body in reading order.

---

## Epic 5 (post-MVP): Sharing & Feed

Deferred per PRD MVP scope; specified for completeness.

### Story 5.1: Living Link (FR-15)

As a reader,
I want to share a `.md` URL that renders for whoever opens it,
So that sharing carries the per-reader magic.

**Acceptance Criteria:**

**Given** a shared `.md` URL
**When** it is opened in a browser
**Then** it renders as HTML
**And** when opened in the native client, it renders per the recipient's personality.

### Story 5.2: Follow / Feed (FR-16)

As a reader,
I want to follow a vault and receive its new pages,
So that I can keep up with authors I value.

**Acceptance Criteria:**

**Given** I follow a vault
**When** the author adds a new `.md` page
**Then** that page surfaces to me as part of the vault's feed.

## Epic 6 (post-MVP): The Markdown Lens (Windows)

Turn the native Windows client outward — from a reader of *our* Vault into the native reader for the *markdown-native web*. Default to themarkdownweb.com, accept any http(s) URL, discover an available markdown representation, render it through the existing Markdig pipeline, and say "no markdown available" when none exists. Grounded in `_bmad-output/planning-artifacts/research/technical-markdown-discovery-for-arbitrary-websites-research-2026-06-23.md`.

Constraints: native client, NO Chromium/WebView (NFR-1); Rendering stays pure (NFR-5); reuse Markdig render + content-negotiation (FR-14). **All stories are .NET/WPF — verified on windows-latest CI (build-windows.yml), not locally.** Sequence: 6.1 → 6.2 → 6.3 → 6.4 (6.3 is the load-bearing discovery story and folds in the discovery spike).

### Story 6.1: Default to themarkdownweb.com on launch (FR-19)

As a reader,
I want the client to open themarkdownweb.com when it starts (and on a Home action),
So that I land somewhere useful instead of a blank address bar.

**Acceptance Criteria:**

**Given** a freshly launched client
**When** the window opens
**Then** it loads and displays themarkdownweb.com content (not an empty state)
**And** a Home affordance returns to themarkdownweb.com from any page.

### Story 6.2: Open any http(s) URL (FR-20)

As a reader,
I want to type any http(s) URL into the address bar, not just .md URLs,
So that I can point the client at any site to read its markdown.

**Acceptance Criteria:**

**Given** the address bar
**When** I enter an http(s) URL that does not end in .md
**Then** the client accepts it and proceeds to markdown discovery (Story 6.3) instead of declining
**And** a non-http(s) scheme (ftp:, javascript:, file:) is still declined
**And** UX-DR5's ".md only" rule is revised to ".md-discoverable" (the ".md only" tag/behavior is updated accordingly) without breaking existing address-bar tests.

### Story 6.3: Markdown discovery service (FR-21)

As a reader,
I want the client to reliably find a URL's markdown representation,
So that the right markdown is rendered and false positives are not.

**Acceptance Criteria:**

**Given** an http(s) URL
**When** discovery runs
**Then** it tries, first-hit-wins within a bounded probe budget: (1) GET with `Accept: text/markdown` and parse `<head>` for `<link rel="alternate" type="text/markdown">`; (2) `.md` sibling (`<path>.md`); (3) `/llms.txt` as a site-index hint (not the page body)
**And** every candidate is validated by Content-Type and an HTML-doctype byte-sniff, rejecting soft-404s and HTML-served-as-markdown (zero false positives)
**And** the service is pure/total over inputs, uses an honest non-spoofed User-Agent, distinguishes a bot-block (e.g. 403) from a genuine miss, and is covered by deterministic unit tests with a fake HTTP handler (plus a gated/manual live-probe test against the research basket).

### Story 6.4: Render discovered markdown + no-markdown state (FR-21 integration, FR-22)

As a reader,
I want discovered markdown rendered like any Vault page, and a clear message when there is none,
So that the Lens feels trustworthy whether or not markdown exists.

**Acceptance Criteria:**

**Given** a URL with discoverable markdown
**When** I open it
**Then** the fetched markdown is rendered through the existing Markdig pipeline (per-reader rendering applies as usual; Rendering stays pure)
**And given** a URL with no discoverable markdown
**Then** the client shows an explicit "no markdown available" state (no HTML fallback / no reader-mode)
**And** a bot-blocked fetch shows a distinct message from a genuine no-markdown result
**And** existing Epic 3/4/5 client behavior and tests are not regressed.

## Epic 7 (post-MVP): iOS Reader

Extend the native reading experience to iOS. Separate architecture fork — the WPF FlowDocument renderer does not port directly; the iOS render-stack choice (SwiftUI-native vs .NET MAUI vs shared-core-with-native-render) is an open architecture decision. Deferred until Epic 6 ships. Stories to be elaborated at epic start.

### Story 7.1: iOS native reader (FR-23)

As a reader on iOS,
I want to open and read a .md page rendered natively,
So that I get the Markdown Web experience on my phone without a bundled browser.

**Acceptance Criteria:**

**Given** an iOS device
**When** I open a .md page (Vault or via the Markdown Lens)
**Then** it renders natively with no Chromium/WebView (NFR-1 honored)
**And** the render stack decision is recorded before implementation.
