# Epic 2 Retrospective — Publish & Read on the Web

- **Epic:** Epic 2: Publish & Read on the Web
- **Stories:** 2.1 – 2.7 (all done)
- **Date:** 2026-06-22
- **Facilitator:** Amelia (Senior Software Engineer)

---

## 1. Summary

Epic 2 delivered a complete, beautiful, crawlable web publication layer on top of the Epic 1 skeleton. Starting from a single `.md`-to-HTML render (Story 2.1), the epic layered in a GitHub-style theme (2.2), inter-file navigation with traversal-safe link rewriting (2.3), media embedding with an Astro-assets bypass (2.4), a browsable vault index (2.5), a sticky site-header and end-of-page pitch-card with EXPERIENCE.md-exact microcopy (2.6), and a content-negotiation Azure Function that serves raw markdown at `/api/negotiate/<slug>` with full RFC 9110 §12.5.1 Accept-parsing (2.7). The epic closed all ten FRs in scope — FR-1 through FR-8, FR-14, and FR-17 — using an additive, no-regression discipline that grew the E2E suite from 20 tests at story 2.1 to 157 web specs plus 45 api unit tests at story 2.7. The live site at themarkdownweb.com received its full web experience without a single regression to any prior story's passing suite.

---

## 2. FR / Goal Closure

| FR | Description | Status | Evidence |
|----|-------------|--------|----------|
| FR-1 | File-as-page: `.md` → addressable URL | Met | Story 2.1; 20 E2E green; `content/x.md` → `/x` |
| FR-2 | Inter-file linking; broken-link state | Met | Story 2.3; 66 E2E green; rehype-md-links + 404 page |
| FR-3 | Media embedding (images + video) | Met | Story 2.4; 94 E2E green; `copy-vault-media` + `rehype-md-media` |
| FR-4 | Browsable vault index / entry surface | Met | Story 2.5; 121 E2E green; `/` index page with one link per `.md` |
| FR-5 | Server-rendered HTML; no JS required | Met | Stories 2.1–2.7; JS-disabled spec (`ac2-js-disabled.spec.ts`) green throughout |
| FR-6 | Beautiful default presentation (GitHub-aligned) | Met | Story 2.2; GitHub-light Shiki; `github.css` token suite; 39 E2E green |
| FR-7 | Crawlable / born-compatible; valid HTML | Met | Stories 2.1–2.7; `ac3-crawlable-shell.spec.ts` green; single `<h1>` invariant held |
| FR-8 | Navigation (in-page nav, back/forward) | Met | Story 2.3; browser back/forward; anchor scrolling in web render |
| FR-14 | Content negotiation (`Accept: text/markdown` → raw `.md`) | Met (Option 2) | Story 2.7; 45 api unit tests green; **caveat: true same-URL negotiation DEFERRED** — Azure SWA route rules cannot branch on `Accept` header; raw markdown served at `/api/negotiate/<slug>`, not at `/<slug>` (documented in `deferred-work.md` and 2-7 AC trace) |
| FR-17 | Publish on push (content build) | Met | Stories 2.1–2.7; deploy-web.yml now builds Astro + bundles API; `api_location` wired |

**Epic goal:** "An author drops markdown into a vault and it goes live at themarkdownweb.com as a beautiful, browsable, crawlable site that also casts the vision and recruits to the client." — **Achieved.** The FR-14 same-URL ideal remains a known-deferred item tied to a documented platform limitation, not a defect.

---

## 3. What Went Well

- **E2E suite growth as a safety net.** The test count grew monotonically — 20 → 39 → 66 → 94 → 121 → 157 (web) plus 45 api tests at 2.7. Each story added specs before production code (TDD discipline), and the no-regression rule held: zero prior specs were broken by subsequent stories without an intentional, documented reconciliation (Story 2.6 owned the two chrome-absence flips it required).

- **GitHub-style theme fidelity and design token discipline.** Story 2.2 established `github.css` with named `--md-*` CSS custom properties, and every later story reused those tokens without mutation. The 2-2-theme specs pinned token values; no subsequent story changed them. The DESIGN.md mockup was the canonical reference throughout, producing a visually faithful GitHub-light render.

- **Additive-render discipline — never touching prior code.** The plugin architecture (`rehype-md-links`, `rehype-md-media`, the copy hook, the negotiation function) was layered additively into `astro.config.mjs` and `api/`. Stories 2.3–2.7 each explicitly scoped what they would NOT touch (`slug.mjs`, `github.css` token values, `[...slug].astro` route emission) and enforced this via the regression suite.

- **Shared single source of truth preventing drift.** The `pathToSlug` function in `web/src/lib/slug.mjs` was reused (not re-implemented) by the link rewriter (2.3), the media rewriter (2.4 via `page-path.mjs`), the index (2.5 via `index-entries.mjs`), and the negotiation function (2.7 via direct import). Every slug derivation traced to one canonical implementation.

- **Code review discipline surfacing real bugs before merge.** Story 2.4 caught a CRITICAL finding (slug-vs-verbatim path mismatch for assets under non-slug-stable directories), and Story 2.6's review added `scroll-padding-top` and converted the pitch headline from `<h4>` to a non-heading `<p>` to protect single-`<h1>` invariants. In all, the epic resolved 1 CRITICAL + 3 HIGH + multiple MEDIUM findings across its reviews; 0 CRITICALs remained open at epic close.

---

## 4. What Was Hard / What We Would Change

- **The Astro-assets bypass in Story 2.4 was the most non-obvious implementation challenge.** Astro's internal `rehypeImages` unconditionally calls `decodeURI(src)` on every `<img>` after user plugins, which throws on the `bad%zz.jpg` malformed-`%` smuggle fixture and redirects valid relative images through the `/_astro/*.webp` optimisation pipeline. The solution (run `rehype-raw` first, rewrite to root-absolute paths in `rehype-md-media`, then clear `localImagePaths` to make Astro's `rehypeImages` early-return) required reading Astro internals and probing real raw-node boundaries. The RED baseline took 16 failing specs to find; the investigation is now documented but was the sharpest friction point in the epic.

- **True same-URL content negotiation was blocked by a documented Azure SWA platform limitation.** Azure Static Web Apps route rules (`rewrite`/`redirect`) are path-only and cannot branch on the `Accept` header. Story 2.7 confronted this after full AC elaboration and pivoted to Option 2 (markdown at `/api/negotiate/<slug>`). This was the right pragmatic call but required a mid-story decision that changed the AC1/AC7 scope. The native client (Epic 3 Story 3.2) was designed to fetch from the alternate URL, so MVP is unblocked — but any future "true one-URL" delivery will require Azure Front Door or Function-fronted routing, both noted in `deferred-work.md`.

- **E2E test count reconciliation accumulated overhead.** Story 2.6 introduced the first intentional prior-spec breakage — two "chrome absence" assertions from Story 2.5 had to be inverted, and one `a[href="/"]` count assertion in the 2.3 spec required verification (the wordmark was deliberately a non-link `<span>` to avoid the conflict). This was anticipated and documented in 2.5's Dev Notes, but the reconciliation cost grew as the suite widened. By 2.7, confirming "157 web specs still green" was a meaningful verification pass, not a quick check.

- **`rehype-raw` with no sanitizer is an accepted but deferred risk.** Story 2.4 enabled raw HTML passthrough in the markdown pipeline (required for `<video>`/`<audio>` embedding) with no `rehype-sanitize` allowlist. For the current single-author trusted vault this is acceptable, but it is explicitly deferred: if the vault ever becomes multi-author, a stored-XSS path via `<img onerror>` or `<script>` is possible. This is logged in `deferred-work.md` and should be revisited before any multi-author scenario.

- **API test infrastructure was net-new.** Story 2.7 introduced the first non-web test tier (`api/` with `node --test`), a two-tier testing philosophy (CI-always-green pure handler + gated emulator endpoint tests), and a new deploy path (`api_location` in the CI workflow). The setup was clean but required its own conventions, documentation (`api/README.md`), and a bundled content-copy step (`scripts/build-content.mjs`) that has no prior precedent in the monorepo.

---

## 5. Lessons / Carry-Forward

- **Plant "future story" test guards explicitly.** Story 2.5 planted the `'listing-only: no 2.6 site-header / pitch / get-client chrome yet'` assertion with a comment naming Story 2.6 as the reconciler. This made the 2.6 handoff mechanical. Whenever a story knowingly defers a behavior to a future story, add a named guard assertion that the future story owns. This transforms implicit intent into a failing test that cannot be forgotten.

- **AC decision banners at the top of the story file.** Story 2.7's same-URL deferral was expressed as a prominent banner at the top of the AC block, not buried in Dev Notes. This pattern — "decision + rationale + what shipped instead + what is deferred and where" at the point of the AC — made the scope change immediately visible to any reader and kept the AC trace report honest.

- **Factor shared helpers before the second consumer, not after.** `pathToSlug` was refactored into a shared module when Story 2.4 needed it (creating `page-path.mjs`). Waiting until the second story needed it was the right timing — it avoided premature abstraction — but the refactor came mid-story and required re-verifying the 66 prior specs. The carry-forward rule: as soon as two stories need the same logic, extract it immediately rather than copying it.

- **Two-tier testing for server-side components.** The CI-always-green pure-function tier (`negotiate(slug, acceptHeader, readMd) → { status, headers, body }`) plus the locally-runnable gated emulator tier (flagged with `RUN_SWA_E2E`) is a pattern worth reusing for any future server-side logic that cannot be exercised by the Astro preview harness. Document the local-run command in the relevant README at story close.

- **Verify Astro pipeline internals early for any custom rehype plugin.** The 2.4 Astro-assets bypass was discovered only after a RED baseline run. For any story adding a new rehype plugin, verify interaction with Astro's built-in pipeline (especially `rehypeImages`, `rehypeRaw`, and the VFile shape contract) with a spike before full task elaboration, to surface ordering conflicts before they become 16-spec failures.

---

## 6. Success Assessment

**Epic 2 delivered its goal in full.** All ten FRs are met (FR-14 with a documented pragmatic variant), 157 web E2E plus 45 api unit tests are green, zero CRITICAL findings remain open, and the live site at themarkdownweb.com is a beautiful, browsable, crawlable Markdown Web publication layer ready for Epic 3 to consume.
