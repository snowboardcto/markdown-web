# Story 2.3: Inter-file linking and navigation

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reader,
I want links between pages to work and navigation to feel natural,
so that I can browse around a vault.

## Acceptance Criteria

1. **Given** a rendered content page containing a relative markdown link to another vault `.md` file — e.g. `content/x.md` (page `/x`) contains `[guide](gear-guide.md)` where `content/gear-guide.md` exists — **When** the page is built and I click the link **Then** I navigate **in place** to the page route `/gear-guide` (a normal web-route navigation, full page load), NOT to a literal dead `gear-guide.md` URL. The link must render in the HTML as `<a href="/gear-guide">` (route-resolved at build time), so it works with JavaScript disabled and is a real, crawler-followable href. *(AC1 — relative `.md` link → resolved page route; FR-2, FR-8, EXPERIENCE.md Interaction Primitives "Internal `.md` link → navigate in-place")*

2. **Given** the resolved internal links rewritten by AC1, with relative paths resolved against the **current page's own route** (not the site root) **When** I click `[nested](sub/page.md)` from `/x` → I reach `/sub/page`; from a nested page `/sub/page`, a link `[home](../x.md)` → resolves to `/x`, and a sibling link `[other](page2.md)` from `/sub/page` → resolves to `/sub/page2`; a link with a fragment `[h2](other.md#heading-two)` → resolves to `/other#heading-two` (`.md` stripped, `#anchor` preserved); a pure same-page fragment `[jump](#heading-two)` is left untouched as `#heading-two` (scroll within page). Filenames are normalised to the **same deterministic slug** the route layer uses (github-slugger per path segment, `index` collapses to its parent route), so `[notes](My Notes.md)` → `/my-notes`. **Path-resolution edge cases are resolved deterministically and identically to the route layer:** (a) a leading `./` is stripped (`./sibling.md` from `/x` → `/sibling`); (b) **encoded characters and spaces** in the target are decoded *before* slugging so `[notes](My%20Notes.md)` and `[notes](My Notes.md)` both → `/my-notes` (run `decodeURIComponent` on the path part, guarded against malformed `%`-sequences which leave the link unrewritten rather than throwing); (c) **case is normalised by github-slugger** (it lower-cases), so `[g](Gear-Guide.md)` → `/gear-guide`; (d) a target that is itself `index.md` or `…/index.md` **collapses to its parent route** (`sub/index.md` from `/x` → `/sub`, and a same-dir `index.md` → the page's own dir route); (e) a `..` chain that resolves *exactly* to the vault root (`../index.md` from `/sub/page` → `/`, i.e. the empty slug → root-absolute `/`) yields `/` (the empty slug must not produce `//` or a bare ``); (f) the fragment split happens on the **first** `#` only, so an anchor that itself contains `#` (rare) keeps the remainder in the fragment, and a `?query` on the path part is dropped from the route (routes are static, no query). *(AC2 — relative resolution honors nested dirs / `..` / `./` / fragments / encoded-chars / case / index / vault-root / slug-normalisation, consistent with the existing route slugging; FR-2, FR-8)*

3. **Given** the same render path, **non-internal** links are left exactly as authored **When** the page renders **Then** an external `http(s)://…` link keeps its absolute href and opens normally (in the browser it is just a normal link), a `mailto:`/`tel:`/protocol-relative `//host` link is untouched, an absolute-path link `/already-a-route` is untouched, and a link to a non-`.md` asset (e.g. `[pdf](report.pdf)` or an image/media path) is **not** rewritten to a route (media embedding is Story 2.4 — do not resolve asset links here). Only relative links whose path resolves to an `.md` file are rewritten. *(AC3 — only relative `.md` links are rewritten; external/anchor/non-md links pass through unchanged; EXPERIENCE.md Interaction Primitives, scope boundary vs 2.4)*

4. **Given** a relative `.md` link whose target file does **not** exist in the vault — e.g. `[missing](does-not-exist.md)` — **When** I click it **Then** I land on a **clear broken-link / page-not-found state, never a crash**: the link still rewrites to the would-be route (`/does-not-exist`), and visiting a non-existent route serves a custom, well-formed **not-found page** (`web/src/pages/404.astro`) that clearly communicates the page was not found and offers a way back (a link home), rendered with the same JS-free, semantic, GitHub-styled shell as content pages — returning the static-host 404 fallback, not an unstyled host error or a build/runtime crash. **The not-found response carries an HTTP 404 status, not 200** (FR-7/NFR-4: a crawler must see 404 for a dead route, not a soft-200 — a 200 "not found" page is an SEO defect). The build must emit `dist/404.html`; `astro preview` (the Playwright harness) serves it with a **404 status** for unmatched paths, which is the contract the AC4 spec asserts. On Azure SWA, an **unmatched route serves `/404.html` with a 404 status by default** — no `staticwebapp.config.json` is *required* for this; the story must **not** introduce a SPA `navigationFallback` rewrite-to-`/index.html` (that would soft-200 every unknown route and destroy the not-found state) and must **not** override the 404 status to 200. If a `staticwebapp.config.json` is added at all (optional), it must live where the **deployed web artifact** root sees it (Astro copies `web/public/staticwebapp.config.json` → `dist/`), NOT at `infra/staticwebapp.config.json`, which is never deployed. *(AC4 — missing target → clear not-found state with a real 404 status, never a crash or soft-200; FR-2, FR-7, EXPERIENCE.md State Patterns "Broken / missing `.md`")*

5. **Given** browser history during navigation, **When** I follow an internal link from page A to page B and then press the browser **Back** button **Then** I return to page A, and pressing **Forward** returns to page B — standard full-page browser history works because internal links are plain server-rendered `<a href>` navigations (no SPA/client-router intercept, no `history.pushState` shimming). *(AC5 — browser back/forward returns to the prior page; FR-8)*

6. **Given** the link-rewrite + 404 changes are purely additive to the Story 2.1/2.2 render path **When** the site builds and the full Playwright suite runs **Then** the build still exits 0 and emits one page per `content/**/*.md`, the page stays server-rendered / JS-free / semantically well-formed (single `<html lang>`→`<head>`→`<body>`→`<main>`/`<article>`, one `<h1>`, doctype), the GitHub theme is unchanged, and **all 39 existing Playwright specs (`web/tests/*.spec.ts`) still pass unchanged** alongside the new linking/nav/404 specs. *(AC6 — no regression to 2.1's crawlable/JS-free contract or 2.2's theme; FR-5, FR-7, NFR-3)*

> Source: [_bmad-output/planning-artifacts/epics.md#Story 2.3: Inter-file linking and navigation] (lines 213–225): the epic ACs are (a) `[guide](gear-guide.md)` → click → navigate to `/gear-guide`, (b) browser back/forward returns to the prior page, (c) a link to a missing file shows a clear broken-link / not-found state, never a crash. **AC1** is epic-(a). **AC5** is epic-(b). **AC4** is epic-(c). **AC2** is the derived **resolution-correctness** bar — epic-(a) shows only the simplest same-dir case; EXPERIENCE.md (line 56) and architecture.md (#Link resolution line 103) require relative resolution "against the current page's slug" with nested dirs, `..`, `#anchor`, `index`, and slug-normalisation handled, so each is made an explicit testable case rather than left implicit. **AC3** is the derived **scope/pass-through** bar — EXPERIENCE.md Interaction Primitives (lines 56–58) distinguish internal `.md` / `#anchor` / external `http(s)`; only relative `.md` links may be rewritten, and asset links are Story 2.4, so non-`.md`/external/anchor pass-through is an explicit acceptance bar. **AC6** is the derived **regression guardrail** — 2.3 is additive to the 2.1 render path + 2.2 theme, and FR-5/FR-7/NFR-3 plus the 39 existing tests must not break.

## Tasks / Subtasks

- [x] **Task 1 — Write a rehype plugin that rewrites relative `.md` links to page routes** (AC: 1, 2, 3)
  - [x] Create `web/src/lib/rehype-md-links.mjs` (NEW — a small local rehype plugin; `.mjs` so `astro.config.mjs` can `import` it directly). It is a `unified`/rehype transformer: `export default function rehypeMdLinks() { return (tree, file) => { … } }`. Use `unist-util-visit` (already available transitively via Astro's remark/rehype stack — confirm `web/node_modules/unist-util-visit` resolves; if not present as a resolvable import, write a tiny manual recursive HAST walker instead of adding a dependency, since adding a top-level dep needs a lockfile commit) to visit every `element` node with `tagName === 'a'`. [Source: architecture.md line 70 "remark/rehype (GFM)"; line 103 "Link resolution (FR-2): relative `.md` → navigate in-client … missing target → clear broken-link state, never a crash"]
  - [x] For each `<a>`, read `node.properties.href`. **Skip (leave unchanged) when** the href is missing/empty, starts with a scheme (`/^[a-z][a-z0-9+.-]*:/i` matches `http:`, `https:`, `mailto:`, `tel:`, etc.), is protocol-relative (`//…`), is root-absolute (`/…`), or is a pure fragment (`#…`). These are AC3 pass-throughs / AC2 same-page anchors. [Source: EXPERIENCE.md Interaction Primitives lines 56–58; AC3]
  - [x] For a **relative** href, split off the `#fragment` on the **first `#` only** (`const i = href.indexOf('#'); const fragment = i === -1 ? '' : href.slice(i); const beforeHash = i === -1 ? href : href.slice(0, i);`) and then drop any `?query` from the remaining path part (routes are static — no query survives into the route). Then test the path part: **only rewrite when the path ends in `.md`** (case-insensitive, after stripping query/fragment). A relative non-`.md` path (e.g. `report.pdf`, `media/powder.jpg`) is left unchanged — media/asset resolution is Story 2.4, NOT this story. [Source: AC3; AC2; epics.md Story 2.4 line 227 — media is a separate story]
  - [x] **Decode the path part before slugging** so encoded/spaced targets normalise to the same route as their literal form: `decodeURIComponent(pathNoQuery)` (so `My%20Notes.md` and `My Notes.md` both → `/my-notes`), wrapped in try/catch — a **malformed `%`-escape** (e.g. `%zz`) must NOT throw and crash the build; on a decode error, **leave the link unrewritten** (degrade gracefully) and note it in the Dev Agent Record. github-slugger already lower-cases each segment, so case-normalisation (`Gear-Guide.md` → `/gear-guide`) is free; do not add a separate `.toLowerCase()`. [Source: AC2 (encoded chars/spaces/case); github-slugger behavior]
  - [x] Resolve the `.md` path **relative to the current page's route**, then slug-normalise it to match the route layer exactly. The current source file path comes from the rehype `file` (vfile) — Astro exposes the source `.md` path on the VFile (`file.path` / `file.history[0]`, an absolute path under the repo-root `content/` vault); derive the page's *directory slug* from it (the slug of the file minus its last segment), `path.posix.join(...)` the relative href against that dir, then apply the **same slug derivation the route uses** to the joined path. Reuse the exact algorithm from `web/src/pages/[...slug].astro` `fileToSlug` — note the existing route imports it as `import { slug as githubSlug } from 'github-slugger'` (the **named** `slug` export, github-slugger is already a resolvable dep): drop the `.md` extension, `githubSlug()` each path segment, join with `/`, and `.replace(/\/index$/, '')` so an `index.md` target collapses to its parent route. Set `node.properties.href = '/' + resolvedSlug + fragment` (lead with `/` so it is a root-absolute route, and re-append the preserved `#fragment`). **When `resolvedSlug` is the empty string** (a `..`-chain or `index.md` that resolves exactly to the vault root), emit `'/' + fragment` (i.e. `/` or `/#frag`), never `//` or a bare empty href. [Source: web/src/pages/[...slug].astro lines 37–45 `fileToSlug` + line 6 import; EXPERIENCE.md line 56 "resolve relative to the current page's slug"; architecture.md line 103]
  - [x] Handle the nested/`..`/`./`/index cases of AC2 by relying on `path.posix.join` + `path.posix.normalize` semantics on the **slug space** (POSIX, forward-slash) — `path.posix.join` already collapses a leading `./` and resolves interior `..`. From `/sub/page`: `../x.md` joins to `x` → `/x`; `page2.md` joins to `sub/page2` → `/sub/page2`; `./sibling.md` → `/sub/sibling`; `dir/index.md` → `/dir`; `../index.md` joins to `index` → fileToSlug drops `/index` → empty slug → `/` (vault root, per the empty-slug rule above). Compute against POSIX paths (never `node:path` `sep`-dependent `join` — the existing route uses `sep`-split on `relative()` output, which is correct for an absolute fs path, but the **slug-space resolution here is forward-slash only**) so it is deterministic on every OS/CI. **Guard a `..` that escapes the vault root**: after `posix.join`/`posix.normalize`, if the result starts with `..` (or is exactly `..`), the target points above `content/` — leave the link **unrewritten** (do NOT emit a broken `/../…` href and do NOT clamp it to root, which could silently mis-route). [Source: AC2; [...slug].astro slug algorithm]
  - [x] Keep the plugin pure and dependency-light: no network, no fs reads (it does NOT need to check whether the target file exists — see Task 3; existence is enforced by the route layer + 404, so a missing target still rewrites to its would-be route and lands on 404). Add a header comment documenting the resolution rules and that this is the web half of FR-2 (the native client does the same at render time in Epic 3 Story 3.5). [Source: AC4; architecture.md line 63 "relative links resolved at build time (HTML) vs render time (native)"]

- [x] **Task 2 — Wire the rehype plugin into the Astro markdown pipeline** (AC: 1, 2, 3, 6)
  - [x] In `web/astro.config.mjs`, import the plugin (`import rehypeMdLinks from './src/lib/rehype-md-links.mjs';`) and add it under `markdown.rehypePlugins: [rehypeMdLinks]`, keeping the existing `markdown: { gfm: true, remarkPlugins: [remarkGfm], shikiConfig: { theme: 'github-light' } }` intact. A **rehype** plugin (HAST, post-MDAST→HAST) is correct here because links are already `<a href>` elements at that stage, so we rewrite `properties.href` directly without re-parsing markdown link syntax. Do NOT replace or reorder remark-gfm; rehype runs after it. [Source: web/astro.config.mjs lines 15–23; architecture.md line 70]
  - [x] Update the `astro.config.mjs` header comment to note Story 2.3 adds relative `.md`→route link rewriting via the rehype plugin (in addition to 2.1 GFM render + 2.2 github-light theme). Confirm the change is JS-free at runtime: the rewrite happens at **build time**, the emitted HTML is a plain `<a href="/route">` — no `client:*`, no runtime script. [Source: web/astro.config.mjs lines 1–14; FR-5/FR-7]
  - [x] Verify the source-file path is actually available to the plugin on this Astro version (5.18.2). Astro passes the VFile to rehype plugins; resolve the source path with an **explicit fallback chain**: `file.path` (the standard unified VFile contract, absolute) → `file.history?.[0]` (the original input path before any rename) → `file.data?.astro?.frontmatter?.url` / other Astro-provided seam → otherwise **no usable path**. Establish the chain once at the top of the transformer; do not assume `file.path` is always populated. [Source: unified VFile contract; Astro 5 markdown plugin API]
  - [x] **Path-contract fallback must degrade, never crash:** if NONE of the fallbacks yields a usable absolute path under the vault `content/` dir, the plugin **leaves ALL links on that page unrewritten** (returns early) rather than throwing, computing a wrong slug, or resolving against a guessed root. Two robustness checks: (1) the resolved path must be **inside** the vault `content/` dir (normalise + `startsWith(contentDir)`) — a path outside it is treated as "no usable path" (degrade); (2) record any page where the path was unusable in the Dev Agent Record so a silent regression (links not rewritten because the VFile contract changed) is **visible**, not invisible. This makes a future Astro VFile-shape change a graceful no-op-with-a-trail, not a broken build or silently-wrong hrefs. Note: a degraded page emits literal `foo.md` hrefs (which 404 → land on the 404 page), so the failure mode is still "clear not-found, never a crash" — consistent with AC4. [Source: unified VFile contract; AC4 never-a-crash; 2-1 "exit-0 with bad output = failed gate"]

- [x] **Task 3 — Add a custom not-found (404) page as the broken-link state** (AC: 4)
  - [x] Create `web/src/pages/404.astro` (NEW — Astro's convention for the not-found page; Astro emits it as `dist/404.html`, which Azure Static Web Apps serves for unmatched routes). Render it through the **same `Page` layout** (`web/src/layouts/Page.astro`) so it inherits the GitHub theme, the semantic `<html lang>`→`<head>`→`<body>`→`<main>`/`<article>` shell, the single-`<h1>` discipline, and the JS-free contract. Pass a clear `title` (e.g. `"Page not found"`). [Source: EXPERIENCE.md State Patterns line 49 "clear 'page not found / not a markdown page' state, never a crash"; web/src/layouts/Page.astro; architecture.md line 135 `infra/staticwebapp.config.json`]
  - [x] Content of the 404 body: a clear not-found message in plain, developer-credible voice (EXPERIENCE.md Voice and Tone) — communicate that the page/`.md` was not found, and provide a way back (a link to `/`). Keep it minimal and on-theme; do NOT add site-header/pitch-card chrome (that is Story 2.6) and do NOT add client JS. Exactly one `<h1>`. [Source: EXPERIENCE.md lines 32–37 Voice and Tone, line 49 State Patterns; scope boundary vs 2.6]
  - [x] Confirm Azure SWA actually serves this `404.html` for unmatched routes. **Reality check:** `infra/staticwebapp.config.json` is **architecture-named (architecture.md line 135) but does NOT exist in the repo today** (`infra/` currently holds only `README.md`, `main.bicep`, `main.bicepparam`), and there is no `web/public/`. **SWA does not read a config from `infra/`** — it reads `staticwebapp.config.json` only from the **deployed app-artifact root**. For an Astro SWA deploy that root is `dist/`, so a config would have to live at `web/public/staticwebapp.config.json` (Astro copies `public/**` verbatim into `dist/`). A file left at `infra/staticwebapp.config.json` would never ship. [Source: architecture.md line 135; infra/ tree; Astro static-assets behavior]
  - [x] **No SWA config file is required to satisfy AC4.** Azure SWA's **default** behavior already serves `/404.html` with a **404 status** for any unmatched route. So the deliverable is the good `dist/404.html` (Astro builds it from `404.astro`), not a config file. If you nonetheless add `web/public/staticwebapp.config.json`, it MUST: (a) NOT add a SPA `navigationFallback` that rewrites unknown routes to `/index.html` (that soft-200s every dead route and defeats AC4); (b) NOT override the 404 status to 200; (c) at most, only make the existing `404.html`/404-status behavior explicit (a `responseOverrides."404"` pointing at `/404.html`). Prefer **adding nothing** unless a build/preview check shows the default fails. Do NOT create `infra/staticwebapp.config.json` (wrong location, never deployed). [Source: AC4; SWA default 404 behavior; architecture.md line 135]
  - [x] The build-output `dist/404.html` is the source of truth the Playwright test asserts against locally — `astro preview` serves `404.html` for unknown paths **with a 404 status**, so the AC4 spec asserts both `response.status() === 404` and the custom page content. Verify the status locally (preview), since this is the same not-found-with-404 contract SWA provides in production. [Source: web/playwright.config.ts preview; AC4]

- [x] **Task 4 — Add a content fixture that exercises the link cases (TDD-friendly)** (AC: 1, 2, 3, 4)
  - [x] Add the minimal vault content needed to prove each AC without bloating the suite. Create `content/gear-guide.md` (NEW — the canonical epic example: a real existing target so `[guide](gear-guide.md)` → `/gear-guide` resolves to a live 200 page). Keep it tiny (one `# Gear Guide` H1 + a sentence), and have it link **back** (`[home](x.md)` → `/x`) and to the nested page (`[nested](sub/page.md)` → `/sub/page`) so the round-trip + back/forward (AC5) is exercisable. [Source: epics.md Story 2.3 line 221 `[guide](gear-guide.md)`]
  - [x] Extend `content/x.md` with a dedicated **"Links (AC: 2.3)"** section containing each link class as a real authored link so the rewrite is asserted on rendered HTML: a same-dir internal `[guide](gear-guide.md)`; a nested internal `[nested](sub/page.md)`; an internal-with-fragment `[gfm extensions](x.md#gfm-extensions-ac6)` (or a cross-file fragment to gear-guide); a pure same-page anchor `[jump to lists](#lists)`; an external `[the site](https://themarkdownweb.com)`; a non-`.md` asset link `[a pdf](report.pdf)` (must NOT be rewritten — proves AC3 / 2.4 boundary); and a **broken** internal `[missing](does-not-exist.md)` (proves AC4 — rewrites to `/does-not-exist`, which 404s). Add a `[home](../x.md)`-style `..` case from the nested fixture: extend `content/sub/page.md` with `[home](../x.md)` (→ `/x`) and `[guide](../gear-guide.md)` (→ `/gear-guide`) to prove parent-relative resolution. [Source: AC1, AC2, AC3, AC4; content/x.md, content/sub/page.md current state]
  - [x] Do NOT introduce a slug collision (the route layer throws on duplicate slugs — see `[...slug].astro` lines 50–62). `gear-guide.md` slugs to `/gear-guide`, distinct from existing fixtures (`/x`, `/sub/page`, `/my-notes`, `/no-h1`, `/empty`). Verify the new fixtures keep the build emitting a clean set of pages (now 8 content pages: x, sub/page, my-notes, no-h1, empty, gear-guide + any others). [Source: [...slug].astro duplicate-slug guard lines 50–62]

- [x] **Task 5 — Add Playwright specs for linking, navigation, and the broken-link state** (AC: 1, 2, 3, 4, 5, 6)
  - [x] Add a new spec `web/tests/2-3-linking-nav.spec.ts` (follow the existing harness conventions — `import { test, expect } from '@playwright/test'`, `test.describe`, guard route assertions with a 200-status `beforeEach` like `ac3-crawlable-shell.spec.ts`). [Source: web/tests/ac3-crawlable-shell.spec.ts pattern]
  - [x] **AC1/AC3 (href rewrite, build-time, JS-free):** load `/x`, assert the rendered HTML has `<a href="/gear-guide">` for the `[guide](gear-guide.md)` link (resolved route, NOT `gear-guide.md`); assert the external link still has `href="https://themarkdownweb.com"`; assert the same-page anchor link is `href="#lists"` (untouched); assert the non-`.md` asset link is still `href="report.pdf"` (NOT rewritten). Read hrefs from the static HTML / `locator.getAttribute('href')` so it proves the build-time rewrite (no JS needed). [Source: AC1, AC3]
  - [x] **AC2 (resolution correctness — cover the edge-case table):** assert `/x`'s `[nested](sub/page.md)` → `href="/sub/page"`; assert the cross-file fragment link → `/<route>#<anchor>` (`.md` stripped, fragment kept, split on first `#`); assert the **space/encoded** case → `[notes](My Notes.md)` AND/OR `[notes](My%20Notes.md)` both → `href="/my-notes"` (proves decode + slug); assert the **case-normalised** link (e.g. `Gear-Guide.md`) → `/gear-guide`; load `/sub/page` and assert its `[home](../x.md)` → `href="/x"` and `[guide](../gear-guide.md)` → `href="/gear-guide"` (parent-relative `..`). Where a fixture is added for the **vault-root** (`../index.md` → `/`) or **escape-guard** (`../escape.md` left unrewritten) cases, assert those too; at minimum assert the escape-guard link is NOT rewritten to a `/../…` route. Read each assertion from the rendered `getAttribute('href')` (build-time output), never from a re-implementation of the slug fn (avoid the 2.2 tautological-test trap). [Source: AC2; edge-case table; 2-2 Review Findings #2]
  - [x] **AC1 + AC5 (real navigation + back/forward):** from `/x`, `page.getByRole('link', { name: 'guide' }).click()`, assert `page.url()` ends in `/gear-guide` and the `gear-guide` H1 is visible; then `page.goBack()` and assert URL is `/x` again and `Heading One` visible; `page.goForward()` and assert back on `/gear-guide`. This proves in-place route nav + browser history. [Source: AC1, AC5]
  - [x] **AC4 (broken-link → not-found, real 404 status, never a crash):** navigate to `/does-not-exist` (the would-be route of the broken `[missing](does-not-exist.md)` link) and assert `response.status() === 404` (NOT 200 — explicitly assert it is not a soft-200 SPA fallback) AND the served page is the **custom** 404 (assert the not-found heading/text is visible and a link home `a[href="/"]` exists) — not a host error page. Read the status from the real navigation response (`const res = await page.goto('/does-not-exist')`), not from page content, so the SEO-relevant status is actually verified. Optionally also `click()` the broken link from `/x` and assert it lands on the not-found page. Confirm the page renders JS-disabled-safe (semantic, themed, one `<h1>`). [Source: AC4; FR-7 born-compat]
  - [x] **AC6 (no regression):** run the FULL suite `cd web && npx playwright test` and confirm all **39 prior specs** (`ac1`, `ac2`, `ac3`, `ac5`, `ac6`, `2-2-theme`) stay green alongside the new `2-3-linking-nav` specs. If any prior spec breaks, the link rewrite or 404 changed the semantic shell/theme and must be corrected (2.3 is additive). [Source: 39/39 baseline; AC6]

- [x] **Task 6 — Final verification against ACs (Definition of Done)** (AC: 1, 2, 3, 4, 5, 6)
  - [x] `cd web && npm run build` exits 0 and emits one page per `content/**/*.md` (now including `/gear-guide`), with no new build error from the rehype plugin or the 404 page, and a `dist/404.html` present. [Source: AC6]
  - [x] Relative `.md` links in the emitted HTML are rewritten to their page routes (`[guide](gear-guide.md)` → `<a href="/gear-guide">`), resolved relative to the current page's slug, with nested dirs, `..`, `#anchor`, `index`, and slug-normalisation all correct (AC1, AC2). [Source: AC1, AC2]
  - [x] External `http(s)`/`mailto:`/protocol-relative/root-absolute links, same-page `#anchor` links, and non-`.md` asset links are left unchanged (AC3). [Source: AC3]
  - [x] Visiting a missing route serves the custom, themed, JS-free `404.astro` not-found page with a clear message + link home, and a 404 status — never a crash or unstyled host error (AC4). [Source: AC4]
  - [x] Clicking an internal link navigates in place to the route; browser Back returns to the prior page and Forward re-advances (AC5). [Source: AC5]
  - [x] Page stays server-rendered, JS-free, semantically well-formed; single `<h1>`/`<main>`/`<article>`/doctype shell and 2.2 theme intact; `cd web && npx playwright test` → all 39 prior specs + new linking/nav/404 specs pass; `cd web && npx astro check` → 0 errors. [Source: AC6]
  - [x] **Scope discipline held:** NO media/asset embedding or asset-path resolution (Story 2.4), NO vault index / entry surface (Story 2.5), NO site-header/get-client-cta/pitch-card (Story 2.6), NO content negotiation / `api/` changes (Story 2.7), NO native-client link handling (Epic 3 Story 3.5), NO SPA/client-router/View-Transitions (would break the JS-free + back/forward-via-full-load contract). This story is relative `.md` link rewriting + the not-found state + browser-history nav ONLY. External-link `target="_blank"` is optional/minimal and NOT required. [Source: epics.md Epic 2 stories 2.4–2.7 lines 227–278; EXPERIENCE.md Interaction Primitives lines 56–58]

## Dev Notes

### What exists right now (read before coding)

- `web/` is the Astro **5.18.2** project from Stories 1.1 + 2.1 + 2.2. Story 2.1 shipped the render path (the `glob()` content collection over the repo-root `../content` vault, the `[...slug].astro` catch-all route with a deterministic `fileToSlug`, and the `Page.astro` shell). Story 2.2 layered the GitHub theme (`web/src/styles/github.css`, `shikiConfig.theme: 'github-light'`). 2.3 adds **link rewriting** + a **404 page** on top — it does NOT change routing, the content collection, or the theme. [Source: 2-1 and 2-2 story files; web/ tree]
- `web/astro.config.mjs` currently sets `markdown: { gfm: true, remarkPlugins: [remarkGfm], shikiConfig: { theme: 'github-light' } }`. 2.3 adds `rehypePlugins: [rehypeMdLinks]` alongside these. [Source: web/astro.config.mjs lines 15–23]
- `web/src/pages/[...slug].astro` **already contains the exact slug algorithm** to reuse (`fileToSlug`, lines 37–45): drop the `.md` extension, `slug()` each path segment with `github-slugger`, join with `/`, `.replace(/\/index$/, '')`. The route is `params: { slug: entry.id }` where `entry.id` is Astro's glob id — so the route a `.md` file lives at is exactly this slug. **Match this algorithm in the rehype plugin** so links resolve to the routes that actually exist; any divergence yields links that 404. `github-slugger` is a resolvable import (present in `web/node_modules`). [Source: web/src/pages/[...slug].astro lines 37–45; node_modules check]
- `web/src/layouts/Page.astro` emits `<!doctype html>` → `<html lang="en">` → `<head>`(charset/viewport/`<title>`) → `<body>` → `<main>` → `<article>` → `<slot />`, importing `../styles/github.css`. **Reuse this layout for `404.astro`** so the not-found page is themed + semantic + JS-free with one `<h1>`. It takes a single `title` prop. [Source: web/src/layouts/Page.astro]
- `web/src/pages/index.astro` is the Epic-1 "coming soon" placeholder still serving `/` (its own inline shell, NOT the Page layout). It is out of scope — do not redesign it (that's Story 2.5's index surface). [Source: web/src/pages/index.astro; 2-2 Dev Notes]
- **No `404.astro` exists yet** — `web/src/pages/` has only `[...slug].astro` and `index.astro`. Astro currently emits a generic default 404; this story replaces it with the custom themed one. [Source: ls web/src/pages]
- Content fixtures today: `content/x.md` (the big GFM fixture), `content/sub/page.md`, `content/no-h1.md`, `content/My Notes.md`, `content/empty.md`, `content/README.md`, `content/media/.gitkeep`. None currently contains an inter-file link, so 2.3 must add link fixtures (Task 4). [Source: content/ tree]

### The load-bearing technical decision — rehype, not remark; route-resolution at build time

- **Why rehype (HAST), not remark (MDAST):** at the rehype stage, markdown links are already `<a>` elements with a `properties.href` string. Rewriting `href` there is a one-line property mutation and avoids re-implementing markdown link/destination parsing (reference links, autolinks, titles). A remark plugin would have to handle `link` *and* `linkReference`/`definition` nodes and escape edge cases — more surface, more bugs. Architecture says "remark/rehype (GFM)" (line 70) and "relative links resolved at **build time** (HTML)" (line 63) — a build-time rehype transform is exactly that. [Source: architecture.md lines 63, 70, 103]
- **The plugin's only job:** for each `<a>`, decide internal-relative-`.md` vs everything-else, and if internal, set `href` to `'/' + slug(resolve(currentPageDir, relativeHref)) + fragment`. It MUST mirror `[...slug].astro`'s `fileToSlug` so the rewritten href equals a route that `getStaticPaths` actually emits. Pseudocode:
  ```
  if (!href || isScheme(href) || href.startsWith('//') || href.startsWith('/') || href.startsWith('#')) return; // pass-through (external/protocol-rel/root-abs/same-page anchor)
  const currentFile = file.path ?? file.history?.[0] ?? astroSeam(file); // explicit fallback chain
  if (!currentFile || !isInsideVault(currentFile, contentDir)) { record(file); return; } // no usable path → leave page unrewritten (degrade, never crash)
  const hashAt = href.indexOf('#');                        // split on FIRST '#' only
  const fragment   = hashAt === -1 ? '' : href.slice(hashAt);
  const beforeHash = hashAt === -1 ? href : href.slice(0, hashAt);
  const pathNoQuery = beforeHash.split('?')[0];            // drop ?query (routes are static)
  if (!/\.md$/i.test(pathNoQuery)) return;                 // non-.md asset → Story 2.4, skip
  let decoded; try { decoded = decodeURIComponent(pathNoQuery); } catch { record(file); return; } // malformed % → leave as-is
  const pageDirSlug = fileToSlug(currentFile).split('/').slice(0, -1).join('/'); // page dir in slug space
  const joined = posix.normalize(posix.join(pageDirSlug, decoded));  // resolve ./, .., nested dirs (forward-slash only)
  if (joined === '..' || joined.startsWith('../')) return; // escaped vault root → leave as-is (do NOT clamp to root)
  const routeSlug = fileToSlug(joined);                    // strip .md, github-slug each seg (lower-cases), drop /index
  node.properties.href = '/' + routeSlug + fragment;       // routeSlug='' → '/' + fragment (vault root); never '//'
  ```
  Factor `fileToSlug` so the SAME function is used by both `[...slug].astro` and the plugin if practical (e.g. extract to `web/src/lib/slug.mjs` and import from both), to guarantee they never drift — OR copy it verbatim with a comment pointing at the canonical source. Prefer extraction if it does not complicate the Astro frontmatter import. [Source: [...slug].astro fileToSlug lines 37–45; AC1/AC2]
- **Getting the current page's path inside the plugin:** unified passes the VFile as the 2nd transformer arg; Astro populates the source `.md` absolute path on `file.path` / `file.history[0]`. Compute the page's directory-slug from it (relative to `content/`), since the relative link is resolved against the *page's location*, not the site root. Validate this on Astro 5.18.2 during dev — if `file.path` is empty, see Task 2's fallback. [Source: unified VFile contract; Task 2]
- **`#anchor` IDs:** GFM headings get github-slugger ids via Astro's default `rehype-slug`-style heading anchoring (Astro auto-generates heading `id`s). A cross-file fragment like `other.md#heading-two` keeps `#heading-two` verbatim — the plugin does not re-slug the fragment (it is already an author-provided anchor; matching it to the heading id is the author's responsibility, same as GitHub). A same-page `#heading-two` is pass-through untouched. [Source: AC2; Astro markdown heading-id behavior]

### Path-resolution edge-case table (the AC2 contract, made exhaustive)

Each row is a deterministic input→output the plugin must satisfy and the spec should (where practical) assert. "Current page" is the page the link is authored on. All resolution is in **slug space, POSIX forward-slash**.

| # | Current page | Authored href | Resolved `href` | Rule exercised |
|---|---|---|---|---|
| 1 | `/x` | `gear-guide.md` | `/gear-guide` | same-dir `.md` (epic example) |
| 2 | `/x` | `sub/page.md` | `/sub/page` | nested dir |
| 3 | `/sub/page` | `../x.md` | `/x` | parent `..` |
| 4 | `/sub/page` | `page2.md` | `/sub/page2` | sibling in nested dir |
| 5 | `/sub/page` | `./sibling.md` | `/sub/sibling` | leading `./` collapse |
| 6 | `/x` | `other.md#heading-two` | `/other#heading-two` | `.md` stripped, fragment kept |
| 7 | `/x` | `#lists` | `#lists` (untouched) | pure same-page anchor → pass-through |
| 8 | `/x` | `My Notes.md` | `/my-notes` | space → github-slug |
| 9 | `/x` | `My%20Notes.md` | `/my-notes` | percent-encoded space, decoded then slugged |
| 10 | `/x` | `Gear-Guide.md` | `/gear-guide` | case → github-slug lower-cases |
| 11 | `/x` | `sub/index.md` | `/sub` | `index.md` collapses to parent route |
| 12 | `/sub/page` | `../index.md` | `/` | resolves to vault root → empty slug → `/` (never `//`) |
| 13 | `/x` | `../escape.md` | `../escape.md` (unrewritten) | `..` escapes vault root → leave as-is |
| 14 | `/x` | `report.pdf` | `report.pdf` (unrewritten) | non-`.md` asset → Story 2.4 |
| 15 | `/x` | `media/powder.jpg` | unrewritten | non-`.md` relative asset → Story 2.4 |
| 16 | `/x` | `https://themarkdownweb.com` | unchanged | external scheme → pass-through |
| 17 | `/x` | `mailto:a@b.com` / `tel:+1` / `//host/p` | unchanged | mailto/tel/protocol-relative → pass-through |
| 18 | `/x` | `/already-a-route` | unchanged | root-absolute → pass-through |
| 19 | `/x` | `does-not-exist.md` | `/does-not-exist` (→ 404 page) | broken target still rewrites; lands on 404 (AC4) |
| 20 | `/x` | `bad%zz.md` | `bad%zz.md` (unrewritten) | malformed `%`-escape → decode fails → leave as-is, never throw |

Rows 9–13, 20 are the **newly-hardened edge cases** (encoded/space, case, index-collapse, vault-root empty-slug, escape-guard, malformed-decode). Rows 7, 14–18 are AC3 pass-throughs. Row 19 is the AC4 broken-link path. [Source: AC2, AC3, AC4; [...slug].astro fileToSlug]

### The broken-link / not-found state (AC4)

- The "broken link" state is **the route simply not existing** → the static host serves the 404 page. The plugin rewrites `does-not-exist.md` to `/does-not-exist` like any other `.md` link (it does NOT pre-check existence — that would require fs access and would still not protect against a deleted target later). Visiting `/does-not-exist` finds no `getStaticPaths` entry → Astro/SWA serves `404.html`. So the deliverable is a **good** `404.astro`, not link-time validation. [Source: EXPERIENCE.md State Patterns line 49; architecture.md line 103 "never a crash"]
- `astro preview` (what the Playwright harness runs) serves `dist/404.html` for unknown paths with a 404 status — so the AC4 test asserts both the **404 status** and the custom page content against the local preview. A soft-200 not-found page is an SEO/born-compat defect (FR-7/NFR-4): a crawler must see 404 for a dead route. On Azure SWA, `/404.html` is served for unmatched routes **with a 404 status by default** — no config is required.
- **SWA config reality (do not chase a non-existent file):** `infra/staticwebapp.config.json` is **named in architecture.md (line 135) but does not exist in the repo** (`infra/` has only `README.md`, `main.bicep`, `main.bicepparam`; there is no `web/public/`). SWA reads `staticwebapp.config.json` only from the **deployed app-artifact root** (= Astro's `dist/`), so the only place a config could take effect is `web/public/staticwebapp.config.json` (copied verbatim to `dist/`); a file at `infra/` would never deploy. **Default behavior already satisfies AC4, so prefer adding no config.** If one is added, it must NOT introduce a SPA `navigationFallback` rewrite-to-index (that 200s every unknown route and kills the not-found state) and must NOT override 404→200. Do NOT create `infra/staticwebapp.config.json`. [Source: playwright.config.ts preview; architecture.md line 135; infra/ tree; SWA default 404 + static-assets behavior]

### Navigation + back/forward (AC5) — keep it plain HTML, do NOT add a client router

- Back/forward "just works" precisely because internal links are ordinary `<a href="/route">` full-page navigations — the browser's native history stack handles it. **Do NOT** add Astro `<ClientRouter>`/View Transitions, a SPA router, or any `history.pushState` JS — that would (a) violate the JS-free / crawlable contract (FR-5/FR-7, asserted by `ac2-js-disabled.spec.ts`) and (b) add a moving part the AC does not need. The simplest correct implementation is also the in-scope one. [Source: FR-5/FR-7; EXPERIENCE.md line 60; ac2-js-disabled.spec.ts]

### Regression guardrails — what 2.3 must NOT break (the 39 existing specs)

The 39 existing specs encode the 2.1 contract + 2.2 theme. 2.3 is additive (a build-time href rewrite + a new 404 page + link fixtures); it must not alter the asserted structure/theme:
- `ac1-gfm-core.spec.ts` (5) — exactly one `<h1>`, GFM elements, one `<table>`. → Adding a "Links" section to `content/x.md` must NOT add a second `<h1>` (use `##`/`###`) and must keep the single `<table>` assertion valid (don't add a second table to `/x`). The existing table/headings assertions count elements — keep them satisfiable.
- `ac2-js-disabled.spec.ts` (2) — content renders JS-disabled. → Link rewrite is build-time; 404 is static; no JS added. Safe.
- `ac3-crawlable-shell.spec.ts` (4) — single shell, `<main>/<article>`, doctype, balanced tags, content inside semantic container. → 404 page reuses the same Page layout/shell; don't break `/x`'s shell.
- `ac5-slugging-edge.spec.ts` (5) — slug/title behavior. → New fixtures must not collide slugs (route layer throws on collision); keep titles deriving correctly.
- `ac6-gfm-extensions.spec.ts` (4) — GFM extension assertions on `/x` (strikethrough, task list, autolink, escaping). → The autolink `https://themarkdownweb.com` in `content/x.md` already exists; do not remove it. Adding an external markdown link `[the site](https://…)` is a *different* element (an explicit `<a>`), fine.
- `2-2-theme.spec.ts` (19) — theme tokens/contrast/edge cases on `/x`. → Don't change the theme; the 404 page reuses it. Adding link fixtures doesn't change computed theme styles.
[Source: web/tests/*.spec.ts — 6 spec files, 39 tests, all green per sprint-status 2-2]

### Previous story intelligence (Stories 2.1 + 2.2)

- **Exit-0 build with a missing/empty page is a FAILED gate** (established 2.1). Apply here: a build that exits 0 but emits links that 404, or no `dist/404.html`, is a failed gate. Assert the *rendered output*, not just exit code. [Source: 2-1 / 2-2 Dev Notes]
- `playwright.config.ts` sets `reuseExistingServer: false` and runs `npm run build && npm run preview`, so the harness always rebuilds fresh `dist/` — the rehype rewrite + 404 take effect automatically on each test run; no manual rebuild needed. [Source: web/playwright.config.ts; 2-2 Dev Notes]
- 2.2's review flagged **brittle, tautological tests** (asserting hardcoded values against hardcoded math). Avoid that here: assert hrefs/navigation/status read from the **actual rendered HTML / live page**, not against a re-implementation of the slug function. The AC4 test must read the real HTTP status and real page content. [Source: 2-2 Review Findings #2]
- 2.1 committed `package-lock.json` for reproducible CI `npm ci`. The rehype plugin should add **no** new top-level dependency — prefer reusing `github-slugger` (already resolvable) + `node:path` (posix) + a transitively-available `unist-util-visit` (or a hand-rolled HAST walk). If you genuinely must add a dep, commit the lockfile. [Source: 2-1 / 2-2 Dev Notes]
- Bash tool resets cwd between calls — chain `cd web && …` or use absolute paths. [Source: 2-1 / 2-2 Dev Notes]

### Architecture compliance / guardrails

- **Born-compatibility / SEO (FR-7, NFR-4):** rewritten links are real `<a href="/route">` in static HTML — crawler-followable, JS-free. This is *better* SEO than literal `.md` hrefs (which 404). Preserve it; no JS link interception. [Source: epics.md FR-7/NFR-4]
- **Beauty + performance budget (NFR-3):** the rewrite is build-time; runtime stays static, zero-JS. The 404 page is static HTML + the existing CSS. [Source: epics.md NFR-3]
- **Don't reinvent plumbing (NFR-7):** use the unified/rehype stack Astro already runs and `github-slugger` already in use — no custom markdown parser, no new link framework, no SPA router. [Source: epics.md NFR-7; architecture.md line 70]
- **Shared parse-layer consistency (architecture #Cross-Cutting 4 / FR-2):** the web resolves relative links at build time; the native client (Epic 3 Story 3.5) resolves the same links at render time. Keep the resolution *rule* (strip `.md`, slug, resolve against current page) documented in the plugin so the two paths stay consistent. This story implements ONLY the web half. [Source: architecture.md lines 63, 103, 106–107; epics.md Story 3.5 line 338]

### Scope boundaries — what this story is NOT (prevent scope creep)

This story is **relative `.md` link rewriting + the not-found/broken-link state + browser-history navigation** ONLY. Explicitly OTHER stories — do not pull in:
- **Story 2.4** — `![](media/x.jpg)` image/video embedding and **asset-path resolution**. The plugin must NOT rewrite non-`.md` asset links (leave `report.pdf`, `media/powder.jpg` untouched). [Source: epics.md Story 2.4 line 227]
- **Story 2.5** — browsable vault index / entry surface (the `/` index). Do not build an index or redesign `index.astro`. [Source: epics.md Story 2.5 line 239]
- **Story 2.6** — `site-header` (sticky wordmark + "the vision" + get-client CTA) and `pitch-card` + EXPERIENCE microcopy. The 404 page gets a plain not-found message + link home, NOT this chrome. [Source: epics.md Story 2.6 line 252]
- **Story 2.7** — content negotiation (`Accept` → raw `.md`) in `api/`. Do not touch `api/`. [Source: epics.md Story 2.7 line 266]
- **Epic 3 Story 3.5** — in-client (native WPF) link/media/nav handling. Web-only here. [Source: epics.md Story 3.5 line 338]
- **Client router / View Transitions / SPA nav** — would break JS-free + crawlable + the simple back/forward contract. Do NOT add. [Source: FR-5/FR-7; ac2-js-disabled.spec.ts]
- **External-link `target="_blank"`/new-tab** — minimal/optional, NOT required by the ACs (in the browser an external link is just a normal link, per EXPERIENCE.md line 58). [Source: EXPERIENCE.md line 58; epic scope note]
[Source: epics.md Epic 2 stories 2.4–2.7 lines 227–278; EXPERIENCE.md Interaction Primitives lines 56–58]

### Testing standards summary

- Verification command: `cd web && npx playwright test` (run the FULL suite — prove 39 prior + new linking/nav/404 specs all pass). Typecheck gate: `cd web && npx astro check`. Build gate: `cd web && npm run build`. No lint command configured. [Source: 2-1 / 2-2 Dev Notes]
- Test the **built/preview output** (the Playwright config builds + previews `dist/`), since the ACs are about rendered HTML + real navigation. Read hrefs via `locator.getAttribute('href')`; assert navigation via `page.click()` + `page.goBack()`/`goForward()` + `page.url()`; assert the 404 via `page.goto('/does-not-exist')` and `response.status() === 404` + visible not-found content. [Source: web/playwright.config.ts; ac3-crawlable-shell.spec.ts pattern]
- The harness rebuilds fresh each run (`reuseExistingServer:false`), so the rehype rewrite and 404 are always exercised against the latest build. [Source: web/playwright.config.ts]

### Project Structure Notes

- New/changed files expected:
  - `web/src/lib/rehype-md-links.mjs` (NEW — the rehype link-rewrite plugin) — and optionally `web/src/lib/slug.mjs` (NEW — extracted shared slug fn) if you factor `fileToSlug` out of `[...slug].astro` for reuse.
  - `web/astro.config.mjs` (UPDATE — add `markdown.rehypePlugins: [rehypeMdLinks]`, refresh comment).
  - `web/src/pages/404.astro` (NEW — custom not-found page via the Page layout).
  - `web/src/pages/[...slug].astro` (UPDATE only if extracting `fileToSlug` to a shared module — otherwise unchanged).
  - `content/gear-guide.md` (NEW — link target fixture), `content/x.md` (UPDATE — add "Links" section), `content/sub/page.md` (UPDATE — add `..` links).
  - `web/tests/2-3-linking-nav.spec.ts` (NEW — linking/nav/404 specs).
  - Possibly `web/public/staticwebapp.config.json` (NEW — **only** if SWA needs explicit 404 wiring; default already serves `/404.html` with 404, so prefer adding nothing). Note: it must live under `web/public/` so Astro copies it into `dist/` (the deployed artifact root); `infra/staticwebapp.config.json` is architecture-named but **does not exist and would never deploy** — do NOT create it there.
- Do NOT modify `api/`, `clients/`, `web/src/styles/github.css` (theme is 2.2), `web/src/content.config.ts`, or `_bmad/`. [Source: architecture.md lines 119–135; web/ tree]
- No conflict with the established layout — relative-link resolution is the named FR-2 web responsibility (architecture #Rendering Pipeline line 103, #Cross-Cutting 4 line 63). [Source: architecture.md lines 63, 103]

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 2.3: Inter-file linking and navigation] — user story + ACs (lines 213–225): `[guide](gear-guide.md)` → `/gear-guide`; back/forward; missing → clear broken-link / not-found, never a crash
- [Source: _bmad-output/planning-artifacts/epics.md#Requirements Inventory] — FR-2 inter-file linking / missing → clear broken-link state (line 21); FR-8 navigation / in-page nav + back/forward (line 27); FR-5 server-rendered (line 24); FR-7 crawlable (line 26); NFR-3 beauty+perf budget (line 43); NFR-4 born-compat/SEO (line 44); NFR-7 don't reinvent plumbing (line 47)
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-the-markdown-web-2026-06-21/EXPERIENCE.md] — Interaction Primitives: internal `.md` link → in-place nav, `#anchor` → scroll, external `http(s)` → normal/system-browser (lines 56–58); State Patterns: broken/missing `.md` → clear not-found, never a crash (line 49); Voice and Tone (lines 32–37)
- [Source: _bmad-output/planning-artifacts/architecture.md] — remark/rehype (GFM) browser path (line 70); relative links resolved at build time (HTML) vs render time (native) (#Cross-Cutting 4, line 63); Link resolution (FR-2): relative `.md` → navigate, `#anchor` → scroll, external → system browser, missing → clear broken-link state never a crash (line 103); cross-platform parser consistency (lines 106–107); `web/src/styles/github.css` + `infra/staticwebapp.config.json` (lines 123, 135)
- [Source: web/src/pages/[...slug].astro] — `fileToSlug` slug algorithm (lines 37–45) + duplicate-slug guard (lines 50–62) — the canonical route-slug logic to mirror
- [Source: web/astro.config.mjs] — current markdown config (gfm + remark-gfm + shiki github-light), lines 15–23 — add `rehypePlugins`
- [Source: web/src/layouts/Page.astro] — themed semantic shell to reuse for `404.astro`
- [Source: web/playwright.config.ts; web/tests/ac3-crawlable-shell.spec.ts] — harness (build+preview, reuse:false) + spec conventions (200-guarded beforeEach)
- [Source: _bmad-output/implementation-artifacts/2-1-render-a-md-file-to-an-html-page.md, 2-2-apply-the-github-style-default-theme.md] — render path, Page shell, 39-spec harness, scope-boundary precedent, "exit-0 with bad output = failed gate", avoid tautological tests

## Dev Agent Record

### Agent Model Used

Opus 4.8 (1M context) — claude-opus-4-8[1m]

### Debug Log References

- Baseline RED: `cd web && npx playwright test` → 14 failing / 47 passing (the 8 pass-through-only new cases already passed against the un-rewritten output).
- Final GREEN: `cd web && npx playwright test` → **61 passed** (39 prior + 22 new).
- `cd web && npm run build` → exit 0; 11 pages built incl. `dist/404.html`.
- `cd web && npx astro check` → 0 errors, 0 warnings, 1 hint (the hint is in the pre-existing test spec's `page.waitForNavigation()`, not in production code).

### Completion Notes List

- **VFile path contract result:** On Astro 5.18.2, `file.path` IS populated with the source `.md`'s absolute path, so the primary leg of the fallback chain (`file.path` → `file.history[0]` → Astro frontmatter seam → degrade) is what fires in practice. The plugin still implements the full chain plus an `isInsideVault` (`startsWith(contentDir + sep)`) guard; if none yields a path inside `content/`, ALL links on that page are left unrewritten (degrade, never crash) and the page is recorded via `getUnresolvedPages()` for visibility. No page degraded in this build (every link in the emitted HTML resolved as expected) — confirmed by grepping `dist/x/index.html`, which shows `/gear-guide`, `/sub/page`, `/my-notes`, `/sub`, `/gear-guide#heading-one`, etc., and leaves `report.pdf`, `bad%zz.md`, `../escape.md`, `mailto:…`, `https://…`, `#lists`, `/already-a-route` untouched.
- **Slug-module extraction:** Factored the route-slug core into `web/src/lib/slug.mjs` (`pathToSlug(relPosixPath)` — drop `.md`, github-slug each segment, collapse trailing `/index` AND bare `index` to empty). BOTH `web/src/pages/[...slug].astro` (`fileToSlug` now converts its absolute fs path to a relative POSIX path then delegates) and `web/src/lib/rehype-md-links.mjs` import it, so route emission and link resolution cannot drift. `[...slug].astro`'s `getStaticPaths`/duplicate-slug guard behavior is unchanged (all 5 `ac5-slugging-edge` specs still green).
- **Edge cases:** All 20 rows of the resolution table verified against emitted hrefs. Notable: empty-slug (vault-root) emits `'/' + fragment` never `//`; the `..`-escape guard checks `joined === '..' || joined.startsWith('../')` and leaves the link as authored (no clamp, no `/../`); malformed `%`-escape (`bad%zz.md`) is caught by the `decodeURIComponent` try/catch and left unrewritten.
- **404 / SWA:** Added `web/src/pages/404.astro` through the shared `Page` layout (themed, JS-free, single `<h1>`, link home). Build emits `dist/404.html`; `astro preview` serves it with a real 404 status (AC4 specs assert `res.status() === 404`). Per Task 3's reality check, added **no** `staticwebapp.config.json` (SWA default already serves `/404.html` with a 404 status; a SPA `navigationFallback` would soft-200 and defeat AC4). Did not touch `infra/`.
- **No new dependencies:** reused `unist-util-visit` (resolvable in `web/node_modules`), `github-slugger`, and `node:path` (posix). Lockfile unchanged.
- **Scope held:** no media/asset resolution (2.4), no index surface (2.5), no header/CTA chrome (2.6), no `api/` changes (2.7), no client router / View Transitions. Internal links stay plain `<a href="/route">` full-page navigations (AC5 back/forward green).

### File List

- `web/src/lib/slug.mjs` (NEW — shared `pathToSlug`, single source of truth for route-slug derivation)
- `web/src/lib/rehype-md-links.mjs` (NEW — build-time rehype plugin: relative `.md` → `/route` href rewrite)
- `web/src/pages/404.astro` (NEW — custom themed JS-free not-found page via the Page layout)
- `web/astro.config.mjs` (UPDATE — `markdown.rehypePlugins: [rehypeMdLinks]`; refreshed header comment)
- `web/src/pages/[...slug].astro` (UPDATE — `fileToSlug` now delegates to the shared `pathToSlug`; removed the local github-slugger import)
- `content/gear-guide.md` (NEW — link-target fixture)
- `content/sub/page2.md`, `content/sub/sibling.md` (NEW — sibling/`./` targets for AC2)
- `content/sub/index.md` (NEW — code-review F2 fixture: an `index.md` page (routes to `/sub`) whose relative links must resolve against `sub`, not root)
- `content/x.md` (UPDATE — "Links (AC: 2.3)" section)
- `content/sub/page.md` (UPDATE — parent-`..`/vault-root link cases)
- `web/tests/2-3-linking-nav.spec.ts` (the RED→GREEN linking/nav/404 specs)

## Senior Developer Review (AI)

### Reviewer

naethyn (BMAD consolidated code review — Blind Hunter + Edge Case Hunter + Acceptance Auditor, non-interactive)

### Date

2026-06-21

### Verdict

**PASS WITH ITEMS** — All 6 ACs CONFIRMED against the implemented + tested behavior (61/61 green, build exit 0, `dist/404.html` emitted, no regression to the JS-free crawlable shell or 2.2 theme). The 20-row AC2 edge-case table is satisfied for every input the fixtures exercise. However, the adversarial layers found **3 latent input classes that reach the rewrite body without a guard and mis-route** — none triggered by any current vault fixture (so no test/build regression), but each contradicts the story's own "never silently mis-route / never a crash" contract and the `..`-escape-guard intent. They are correctness/robustness hardening items, not AC failures.

### Critical / total

- **Critical (HIGH): 1** — F1 (`%2F` decode → protocol-relative `//host` off-site href, defeats the pass-through guard).
- **Total action items: 4** (1 high + 2 medium patches + 1 low patch). 2 deferred, 2 dismissed as noise.

### Findings

| # | Source | Severity | Finding | Recommendation |
|---|---|---|---|---|
| F1 | blind+edge | HIGH | **Encoded leading/interior slash escapes slug space.** `decodeURIComponent` runs on the whole path part AFTER pass-through checks, so `[x](%2Ffoo.md)` decodes to `/foo.md`; `posix.join('', '/foo.md')` keeps the leading `/`, the `..`-escape guard does not catch it, and `pathToSlug` yields `/foo` → final `href = '/' + '/foo' = '//foo'` — a **protocol-relative URL** (off-site navigation to host `foo`), the exact case the `href.startsWith('//')` pass-through was meant to prevent. Verified live: `%2Fetc%2Fpasswd.md → //etc/passwd`; `a%2Fb.md → /a/b` (encoded interior slash splits one filename into two route segments). | Decode per-segment (split on `/` first, decode each piece) OR reject/strip a decoded path that is absolute (`startsWith('/')`) before join, so an encoded separator cannot manufacture segments or a protocol-relative href. |
| F2 | blind+edge | MEDIUM | **Links authored on an `index.md` page resolve one directory level too shallow.** `pageDirSlug` is derived by slugging the page path (which index-collapses `sub/index.md → sub`) then `pop()`-ing the last segment → `''` (root) instead of `sub`. Verified: a `[p](page2.md)` on `content/sub/index.md` resolves to `/page2`, not `/sub/page2`. Latent today (no `content/**/index.md` fixture exists), but the route layer explicitly anticipates index files. | Derive the page directory from the **source file's directory path** (relative to `content/`, before index-collapse), not by popping the index-collapsed page slug. |
| F3 | blind+edge | LOW | **Degenerate `.md`-only basenames silently collapse to root.** A target that is exactly `.md` / `...md` passes the `/\.md$/i` gate; after slug it is empty → `href = '/'` (or `'/sub/'` with a trailing empty segment from a nested page). A garbage link is silently rewritten to the home page instead of left alone. Verified live (`.md → /`, `.md` on `/sub → /sub/`). | Distinguish "resolved to vault root via a real `index`/`..` chain" from "basename slugged to empty"; leave the latter unrewritten. |
| F4 | blind | LOW | **VFile-degrade trail is effectively invisible.** Task 2 promised a degraded page (unresolvable VFile path) be "visible, not invisible," but `getUnresolvedPages()` is never wired to any build warning, so a future Astro VFile-shape change would silently disable ALL link rewriting site-wide with a green build. | Emit a build-time `console.warn` (or fail the build) when `unresolvedPages` is non-empty so the no-op-with-a-trail is loud. (Also: module-level Set never resets across a dev-server watch session.) |
| F5 | blind | LOW (defer) | **Colon-in-filename mis-classified as a URL scheme.** The scheme regex `^[a-z][a-z0-9+.-]*:` treats `weird:name.md` as a scheme and leaves it unrewritten. Pathological for a POSIX vault; pre-existing heuristic class. | Defer — accept the heuristic; revisit only if a real filename hits it. |
| F6 | blind | LOW (defer) | **Prod 404 status is host-config-dependent.** AC4's "real 404, not soft-200" is verified only under `astro preview`; production SWA behavior is asserted-by-default but unverified by any test, and a stray SPA `navigationFallback` would soft-200. Spec consciously chose to rely on SWA default and add no config. | Defer — out of this story's preview-tested scope; add a post-deploy smoke check at deploy time. |

### Edge-case JSON (unhandled critical edges)

```json
[
  {
    "id": "F1",
    "severity": "high",
    "location": "web/src/lib/rehype-md-links.mjs — rewrite body (decodeURIComponent -> posix.join -> '/' + routeSlug)",
    "trigger_condition": "Relative .md href whose encoded path contains %2F (or a leading %2F), e.g. [x](%2Ffoo.md) or [x](a%2Fb.md), reaching the rewrite after the pass-through checks (which ran on the still-encoded href).",
    "guard_snippet": "const joined = path.posix.normalize(path.posix.join(pageDirSlug, decoded)); if (joined === '..' || joined.startsWith('../')) return; ... node.properties.href = '/' + routeSlug + fragment;",
    "potential_consequence": "%2Ffoo.md -> href='//foo' (protocol-relative, off-site navigation to host 'foo'); a%2Fb.md -> '/a/b' (one filename split into two route segments). Defeats the href.startsWith('//') pass-through and the ..-escape guard.",
    "verified": "live: node simulation -> %2Ffoo.md='//foo', %2Fetc%2Fpasswd.md='//etc/passwd', a%2Fb.md='/a/b'"
  },
  {
    "id": "F2",
    "severity": "medium",
    "location": "web/src/lib/rehype-md-links.mjs — pageDirSlug derivation (pageSlug.split('/'); segs.pop())",
    "trigger_condition": "A relative .md link authored on a content/<dir>/index.md page (page slug is index-collapsed to <dir> before the last segment is popped).",
    "guard_snippet": "const pageSlug = pathToSlug(relPosix); const segs = pageSlug.split('/'); segs.pop(); const pageDirSlug = segs.join('/');",
    "potential_consequence": "Links resolve one directory level too shallow: [p](page2.md) on content/sub/index.md -> /page2 instead of /sub/page2, landing on 404. Latent (no index.md fixture today).",
    "verified": "live: pathToSlug('sub/index.md')='sub' -> dir after pop='' -> page2.md resolves to '/page2'"
  },
  {
    "id": "F3",
    "severity": "low",
    "location": "web/src/lib/rehype-md-links.mjs — empty-slug branch",
    "trigger_condition": "Target whose basename is exactly '.md' / '...md' (passes the .md gate, slugs to empty).",
    "guard_snippet": "if (!/\\.md$/i.test(pathNoQuery)) return; ... node.properties.href = '/' + routeSlug + fragment;",
    "potential_consequence": "Garbage link silently rewritten to '/' (root) or '/sub/' (trailing empty segment) instead of left unrewritten.",
    "verified": "live: '.md'='/'; '.md' on /sub='/sub/'; '...md'='/'"
  }
]
```

### AC Verification (6 ACs)

- **AC1 — CONFIRMED.** Build-time HAST rewrite, plain `/route` hrefs, no `client:*`; `[guide](gear-guide.md) → /gear-guide` (test asserts rendered `href`, not literal `.md`).
- **AC2 — CONFIRMED (for fixtured inputs).** All 20 edge-case rows produce spec-exact output for the authored fixtures; decode-before-slug, first-`#`-only fragment, `?query` drop, `index.md` collapse, empty-slug→`/`, `..`-escape guard all present and asserted from emitted hrefs (no tautological re-impl). Caveat: F1/F3 are inputs *outside* the fixtured rows.
- **AC3 — CONFIRMED (code).** Scheme / protocol-relative / root-absolute / `#anchor` / non-`.md` pass-throughs correct and tested. Table rows 15 (`media/*.jpg`) and 17 (`tel:`/`//host`) implemented but not individually asserted (same code path as tested siblings — dismissed as noise, not a defect).
- **AC4 — CONFIRMED.** `404.astro` via shared `Page` layout (themed, semantic, single `<h1>`, link home, JS-free); plugin does not fs-check existence; tests assert real `status()===404` (not soft-200), custom content, and click-through. No `staticwebapp.config.json` / `infra/` change (correct per spec).
- **AC5 — CONFIRMED.** No client router / View Transitions / pushState; plain `<a href="/route">`; test exercises click → `goBack` → `goForward` with URL+H1 assertions.
- **AC6 — CONFIRMED (per recorded run).** Additive; `content/x.md` Links section uses `##` (no 2nd `<h1>`, no 2nd table); no slug collisions; recorded 61 passed (39 prior + 22 new), build exit 0, `astro check` 0 errors.

### Scope discipline

No scope creep. No media/asset rewriting (2.4 — `report.pdf`/`media/*.jpg` left alone), no index surface (2.5), no header/pitch chrome on 404 (2.6), no `api/`/content-negotiation (2.7), no native-client handling (3.5), no SPA router. The `slug.mjs` extraction is an in-scope drift-prevention refactor, not creep. Per the review scope mandate, the absence of 2.4–2.7 / native behavior was NOT treated as a defect.

### Review Findings

- [x] [Review][Patch][RESOLVED] F1 (HIGH): Encoded `%2F` in a relative `.md` link decodes to a separator/leading-slash and escapes slug space → emits a protocol-relative `//host` href or splits a filename into segments [web/src/lib/rehype-md-links.mjs rewrite body]. **Fixed:** decode is now **per-segment** (split the encoded path on `/` first, then `decodeURIComponent` each piece); if any decoded segment contains a `/`, the encoded form smuggled in a separator past the pass-through guards → leave the link **UNREWRITTEN**. Plus a belt-and-suspenders re-check rejects a decoded path that starts with `/` or matches a scheme. Verified in the built output: `%2Ffoo.md`, `a%2Fb.md`, `%2Fetc%2Fpasswd.md` are all emitted unchanged (no `//foo`, no `/a/b`, no `//etc/passwd`). New specs in `web/tests/2-3-linking-nav.spec.ts` assert each. Legit `../foo.md` resolution is untouched (the `..`-escape is still handled *after* `posix.join`).
- [x] [Review][Patch][RESOLVED] F2 (MEDIUM): Relative links authored on a `content/<dir>/index.md` page resolve one directory level too shallow (page slug is index-collapsed before the directory is derived) [web/src/lib/rehype-md-links.mjs pageDirSlug]. **Fixed:** the page directory slug is now derived from the **source file path before index-collapse** — take the file's directory (`relPosix` minus its basename) and `pathToSlug` *that*, instead of slugging the page path and popping the last segment. So `content/sub/index.md` yields a page dir of `sub`, and `[p](page2.md)` on it → `/sub/page2` (not `/page2`). Added fixture `content/sub/index.md` (routes to `/sub`) + an F2 spec asserting `/sub/page2` and `/sub/sibling`.
- [x] [Review][Patch][RESOLVED] F3 (LOW): Degenerate `.md`-only basenames (`.md`/`...md`) pass the gate and silently rewrite to `/` or `/sub/` [web/src/lib/rehype-md-links.mjs empty-slug branch]. **Fixed:** when the resolved route slug is empty, only emit the vault-root `/` href if the path actually collapsed to root via a real `index`/`..` chain (`cleaned` is empty/`.` or an `index` route); otherwise leave the link **UNREWRITTEN**. Verified: `[empty basename](.md)` is emitted as `.md`, not `/`. New spec asserts it.
- [x] [Review][Patch][RESOLVED] F4 (LOW): VFile-degrade trail is invisible — `getUnresolvedPages()` is never surfaced, so a future VFile-shape change silently disables all link rewriting site-wide [web/src/lib/rehype-md-links.mjs]. **Fixed:** the plugin now emits a build-time `console.warn` the moment a page's VFile source path cannot be resolved inside the vault, naming the page and explaining the rewrite was skipped — so a future Astro VFile-shape regression is loud, not silent. (No page degrades in the current build, so the warning is dormant in practice.)
- [x] [Review][Defer] F5 (LOW): Colon-in-filename mis-classified as a URL scheme [web/src/lib/rehype-md-links.mjs scheme regex] — deferred, pathological POSIX edge, accept the heuristic.
- [x] [Review][Defer] F6 (LOW): Production SWA 404-status is host-config-dependent and only preview-verified [deploy] — deferred, out of preview-tested scope; add a post-deploy smoke check.
