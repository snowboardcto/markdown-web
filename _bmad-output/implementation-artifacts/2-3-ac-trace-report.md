# Story 2.3 — AC Trace Report (Inter-file linking and navigation)

- Story: `_bmad-output/implementation-artifacts/2-3-inter-file-linking-and-navigation.md`
- Date: 2026-06-21
- Verification command: `cd web && npx playwright test`
- Result: **66 passed (0 failed)** — 39 prior specs + 27 new `2-3-linking-nav` specs.
- Build: `cd web && npm run build` exits 0, emits one page per `content/**/*.md` plus `dist/404.html`.
- Typecheck: `cd web && npx astro check` → 0 errors.

## Implementation under trace

- `web/src/lib/slug.mjs` — shared `pathToSlug` (single source of truth for route-slug derivation; used by both the route layer and the link plugin).
- `web/src/lib/rehype-md-links.mjs` — build-time rehype plugin: relative `.md` href → resolved `/route` (pass-throughs, decode-per-segment, `..`/`./`/index/fragment/encoded/escape-guard handling).
- `web/src/pages/404.astro` — custom themed JS-free not-found page via the shared `Page` layout.
- `web/astro.config.mjs` — wires `markdown.rehypePlugins: [rehypeMdLinks]`.
- `web/src/pages/[...slug].astro` — route layer; `fileToSlug` delegates to shared `pathToSlug` (no-drift guarantee + duplicate-slug guard).

## AC → Test Trace Matrix

| AC | Description | Covering tests (`web/tests/2-3-linking-nav.spec.ts` unless noted) | Status |
|----|-------------|-------------------------------------------------------------------|--------|
| AC1 | Relative `.md` link → resolved page route `<a href="/route">`, real in-place full-page navigation | `AC1 › [guide](gear-guide.md) renders as <a href="/gear-guide">`; `AC1 › clicking the guide link navigates in place to /gear-guide`; `AC2 › same-dir + nested resolution from /x` | COVERED + GREEN |
| AC2 | Resolution correctness against current page's route: nested / `..` / `./` / fragment / encoded / case / index / vault-root / slug-normalisation; malformed-`%` and escape-guard degrade | `AC2 › same-dir + nested`; `AC2 › cross-file fragment preserved (/gear-guide#heading-one)`; `AC2 › space + percent-encoded → /my-notes`; `AC2 › mixed-case → /gear-guide`; `AC2 › index.md collapses to /sub`; `AC2 › parent `..` from /sub/page`; `AC2 › sibling + leading ./`; `AC2 › `..` chain to vault root → /`; `AC3 › malformed %-escape left unrewritten`; `AC3 › `..` escape left unrewritten`; `F1 › encoded leading slash`; `F1 › encoded interior slash`; `F1 › encoded path-traversal`; `F3 › empty-basename .md left unrewritten`; `F2 › links on index.md page resolve against page dir` | COVERED + GREEN |
| AC3 | Non-internal links pass through unchanged: external `http(s)`, `mailto:`, root-absolute, same-page `#anchor`, non-`.md` asset | `AC3 › external http(s) keeps absolute href`; `AC3 › mailto: untouched`; `AC3 › root-absolute untouched`; `AC3 › pure same-page #anchor untouched`; `AC3 › non-.md asset NOT rewritten (2.4 boundary)` | COVERED + GREEN |
| AC4 | Missing target → custom themed JS-free 404 page with real HTTP 404 status (not soft-200), never a crash; link still rewrites to would-be route | `AC4 › broken link rewrites to /does-not-exist`; `AC4 › missing route returns real 404 (not soft-200)`; `AC4 › custom themed not-found page with way home`; `AC4 › clicking broken link from /x lands on not-found` | COVERED + GREEN |
| AC5 | Browser Back/Forward via plain `<a>` full-page nav (no SPA/client-router) | `AC5 › follow internal link, Back returns to prior page, Forward re-advances` | COVERED + GREEN |
| AC6 | No regression: additive to 2.1 render path + 2.2 theme; all prior specs stay green alongside new linking/nav/404 specs; JS-free, single-`<h1>` semantic shell, theme intact | Full suite — 39 prior specs (`ac1-gfm-core` ×5, `ac2-js-disabled` ×2, `ac3-crawlable-shell` ×4, `ac5-slugging-edge` ×5, `ac6-gfm-extensions` ×4, `2-2-theme` ×19) all green + 27 new `2-3-linking-nav` specs | COVERED + GREEN |

## No-regression guard (existing specs)

| Spec file | Tests | Status |
|-----------|-------|--------|
| `web/tests/ac1-gfm-core.spec.ts` | 5 | GREEN |
| `web/tests/ac2-js-disabled.spec.ts` | 2 | GREEN |
| `web/tests/ac3-crawlable-shell.spec.ts` | 4 | GREEN |
| `web/tests/ac5-slugging-edge.spec.ts` | 5 | GREEN |
| `web/tests/ac6-gfm-extensions.spec.ts` | 4 | GREEN |
| `web/tests/2-2-theme.spec.ts` | 19 | GREEN |
| `web/tests/2-3-linking-nav.spec.ts` | 27 | GREEN |
| **Total** | **66** | **66 passed** |

## Gaps / notes

- **No AC is uncovered.** All 6 ACs map to at least one covering test, all green; the 20-row AC2 edge-case table is satisfied for every fixtured input.
- The four code-review findings (F1 HIGH, F2/F3/F4) are RESOLVED with dedicated regression specs (F1 ×3, F2 ×1, F3 ×1) and a build-time `console.warn` for F4 (VFile-degrade visibility). These confirm the "never silently mis-route / never a crash" contract for inputs outside the original AC2 fixture rows.
- **Deferred (not gaps for this story):**
  - F5 (LOW, deferred) — colon-in-filename mis-classified as a URL scheme; pathological POSIX edge, heuristic accepted.
  - F6 (LOW, deferred) — production Azure SWA 404 status is verified only under `astro preview`; default SWA behavior serves `/404.html` with 404 and no SPA `navigationFallback` was added, but a post-deploy smoke check is recommended.
- **Minor coverage note (not a defect):** AC3 table rows for `tel:` / protocol-relative `//host` / `media/*.jpg` share the exact code path as tested siblings (`mailto:`, root-absolute, `report.pdf`) and are not asserted individually.

## Conclusion

6/6 ACs covered and green; review items resolved or consciously deferred. Story 2.3 meets its Definition of Done.
