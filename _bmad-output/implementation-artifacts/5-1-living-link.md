# Story 5.1: Living Link

Status: ready-for-dev

## Story

As a reader,
I want to share a `.md` URL that renders for whoever opens it,
so that sharing carries the per-reader magic.

## Context (Epic 5 — post-MVP; this is the THIN verify + share-affordance framing)

FR-15 (Living Link): "a shared `.md` URL renders for whoever opens it." The underlying capability ALREADY EXISTS:
- **Browser → HTML**: Epic 2 renders any vault `.md` page to a themed HTML page; Story 2.7 added content negotiation (markdown at `/api/negotiate/<slug>` for agents/clients, HTML for browsers).
- **Native client → per-reader personality**: Epic 3 renders the `.md` natively; Epic 4 personalizes it per the recipient's selected personality (BYO-key, client-side — no server-side rewrite, NFR-5).

So the "living link" is just a normal vault `.md` URL — its dual rendering is already real. Story 5.1 therefore does NOT build new rendering machinery. It makes the capability **explicit and shareable** and **locks it with a test**, per the chosen scope (thin: verify + share affordance). Anything heavier (OpenGraph preview cards, dedicated share UI, short links) is OUT (Deferred-Work-Log).

## Decided scope (binding)

- **IN:** (a) a "Copy living link" affordance on the web reader that copies the page's canonical shareable `.md` URL to the clipboard, labeled + keyboard-reachable; (b) a Playwright E2E proving the SAME `.md` URL renders as HTML in a browser; (c) a documented/asserted statement that the same URL opened in the native client renders per the recipient's personality (the native render is proven by Epic 3/4 — 5.1 does not re-prove the WPF render, it asserts the URL contract the native client consumes is the same shareable URL).
- **OUT (Deferred-Work-Log):** OpenGraph/Twitter preview cards for shared links; a dedicated share dialog/menu; URL shortening; any backend; any change to the render pipeline.

## Acceptance Criteria

1. **[The shared `.md` URL renders as HTML in a browser]** **Given** a vault `.md` page's canonical URL, **When** it is opened in a browser, **Then** it renders as the themed HTML reader page (the Epic-2 render) — proven by a Playwright E2E that loads a known vault page URL and asserts the rendered HTML content (heading/body present, themed chrome present). No regression to the existing web E2E suite.

2. **[A "Copy living link" share affordance exists on the web reader]** **Given** the web reader page, **When** the reader activates the "Copy living link" control, **Then** the page's canonical shareable `.md` URL is written to the clipboard. The control is labeled (accessible name e.g. "Copy living link"), keyboard-reachable, and present on vault content pages. Proven by a Playwright E2E (grant clipboard permission, click the control, read `navigator.clipboard` and assert it equals the page's canonical URL). The copied URL is the SAME URL the native client can open (the `.md` address-bar contract from Story 3.2 / the negotiate mapping).

3. **[Same URL → native client renders per the recipient's personality (contract assertion)]** **Given** the copied living-link URL, **When** it is opened in the native client, **Then** it renders per the recipient's selected personality. 5.1 asserts the CONTRACT, not the WPF pixels: a test (web-side or a small doc-backed assertion) confirms the shareable URL is a plain vault `.md` URL that the native client's address bar accepts (Story 3.2) and that the negotiate endpoint serves markdown for it (Story 2.7) — i.e. the link the web "Copy" button produces is exactly what the native client consumes, which Epic 4 then personalizes. The native personalized render itself is already covered by Epic 3/4's windows-CI suite and is NOT re-implemented here.

4. **[No regression]** The full existing web Playwright suite stays green; no change to the render pipeline, theme tokens, or content negotiation. The share affordance is additive.

## Tasks / Subtasks

- [ ] (AC2) Add a "Copy living link" control to the web reader layout/component used by vault content pages (a labeled `<button>` with an accessible name e.g. "Copy living link"; keyboard-reachable). On activate, compute the page's canonical shareable `.md` URL and call `navigator.clipboard.writeText(url)`; show a brief "Copied" affordance (non-blocking). Total — clipboard failure (denied permission) degrades gracefully (no throw).
- [ ] (AC2/AC3) Define the canonical shareable URL for a page in ONE place (the vault `.md` URL — the same value the native `.md` address bar accepts and that `/api/negotiate/<slug>` maps). Reuse the existing slug/URL helper if present; do not fork URL logic.
- [ ] (AC1) Playwright E2E: open a known vault page URL in a browser context, assert the themed HTML render (heading + body + site chrome). 
- [ ] (AC2) Playwright E2E: grant `clipboard-read`/`clipboard-write` permissions, activate the control, read the clipboard, assert it equals the page's canonical `.md` URL.
- [ ] (AC3) Assert the living-link URL contract: a test (or a contract assertion in the api/negotiate test suite) confirming the copied URL maps to markdown via the negotiate endpoint (the representation the native client consumes). Document in the story that Epic 3/4 covers the native personalized render.
- [ ] (AC4) Run the full web Playwright suite + `astro build`; confirm green + no regression.

## Verification

Web-side, LOCAL on Linux: `cd web && npx astro build` (build clean) + `npx playwright test` (all green, incl. the 3 new 5.1 tests). The api/negotiate contract assertion runs in the existing api test suite. No Windows CI needed for 5.1 (no native-client code changes — the native render is already proven by Epic 3/4).

## Dev Notes

- **Clipboard in tests:** Playwright needs `permissions: ['clipboard-read', 'clipboard-write']` on the context; read back via `page.evaluate(() => navigator.clipboard.readText())`. Guard for headless clipboard quirks — if the runner can't read the clipboard, fall back to asserting the control invoked `writeText` with the right URL (spy/shim) rather than skipping the assertion.
- **Canonical URL:** prefer `Astro.url`/the page's own canonical (the existing site already sets canonical/site config for FR-18 domain). The "living link" is that canonical `.md` URL — NOT the `/api/negotiate/...` internal endpoint (that's the representation, not the shareable link).
- **Keep it thin.** No new pipeline, no OG cards, no share menu. One accessible button + three tests + a contract assertion.
