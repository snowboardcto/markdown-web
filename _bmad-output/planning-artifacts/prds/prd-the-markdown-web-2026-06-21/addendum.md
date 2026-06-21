# Addendum — The Markdown Web PRD

Tech-how and downstream depth kept out of the capability-level PRD. For architecture/solution-design (Winston) and UX (Sally).

## Mechanism / transport decisions (from brief + research)
- **Hosting / serving:** Azure (brief decision). Recommended: **Azure Static Web Apps** — global CDN, free SSL, custom domain (themarkdownweb.com), GitHub Actions CI/CD.
- **Serving strategy:** "static now, smart server later" — pre-build `.md` → HTML at deploy time for the HTML-client slice (FR-5, FR-17); add request-time content negotiation (FR-14) for the native client. Both clients are MVP ("we need both"), sequenced HTML-first.
- **Rendering engine (HTML client):** off-the-shelf for v0.1 (e.g. markdown-it / Astro / Eleventy), with a path to a custom engine as the product matures. Decision deferred to architecture.
- **Content negotiation (FR-14):** HTTP `Accept` header → HTML for browsers, `text/markdown` for agents/native client; `Vary: Accept` for caches. An established 2025–26 pattern (Cloudflare "Markdown for Agents," Vercel, Mintlify). Implication: do not over-invest in novel plumbing here; the differentiator is per-reader *human-facing* rendering, not the negotiation itself.
- **Linking (FR-2):** relative `.md` links rewritten to routes at build (HTML client) and resolved by the native client at render time.
- **Native client (FR-9–FR-13) — NO Chromium (hard constraint):** must not depend on Chromium — rules out Electron, Chromium webviews, and Chrome extensions. Renders markdown to native UI, not a bundled browser. Candidate stacks for architecture to weigh: native desktop UI (Swift/Kotlin), Rust-native GUI, or CLI/TUI. Must run the reader's local agent and "work everywhere" (PRD §8.1).

## Competitive / defensibility context (carried from brief addendum)
- Plumbing (markdown hosting, content negotiation) is commoditizing; the native-client layer is attackable by Comet/Dia/Atlas/A2UI. Moat is non-technical: category ownership, the publish↔read loop, an opinionated beautiful client, community, the personal-vault wedge.

## Source artifacts
- Brief + addendum: `_bmad-output/planning-artifacts/briefs/brief-the-markdown-web-2026-06-21/`
- Manifesto: `_bmad-output/the-markdown-web.md`
- Market research: `_bmad-output/planning-artifacts/research/market-markdown-web-competitive-landscape-research-2026-06-21.md`
- Brainstorm: `_bmad-output/brainstorming/brainstorming-session-2026-06-20-233910.md`

## Infrastructure & Backend (input to architecture — confirmed shape)

Confirmed by naethyn as the intended shape; specific service choices remain Winston's to finalize.

- **GitHub Actions (CI/CD)** — on push to the content repo: build `.md` → HTML, then deploy to Azure. This is the mechanism behind **FR-17 (publish on push)**.
- **Azure — static layer** — host rendered HTML + raw `.md` + media. Serves **FR-5** (server-rendered HTML to browsers) and **FR-18** (custom domain over HTTPS). `[ASSUMPTION: Azure Static Web Apps, or Blob Storage + CDN — architecture to decide.]`
- **Azure — backend layer** — a serverless function for **content negotiation (FR-14)**: inspect `Accept`, return HTML to browsers / raw `.md` to the native client, with `Vary: Accept`. Also the endpoint the **native client (FR-9–FR-13)** fetches markdown from. `[ASSUMPTION: Azure Functions vs App Service — architecture to decide.]`
- **Sequence note:** the v0.1 HTML-client slice can ship on the static layer alone (no backend); the backend layer arrives with content negotiation + the native client (both MVP, sequenced second).
