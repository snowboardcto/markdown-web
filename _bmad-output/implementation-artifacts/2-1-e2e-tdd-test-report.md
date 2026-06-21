# Story 2.1 — E2E TDD Test Report (RED phase)

Story: `2-1-render-a-md-file-to-an-html-page.md`
Phase: RED (failing-first) — tests are authored BEFORE the Astro routing/rendering is implemented.
Date: 2026-06-21
Verification command: `cd web && npx playwright test`

## Summary

| Metric | Value |
| ------ | ----- |
| Total E2E tests | 18 |
| Passing (RED phase) | 0 |
| Failing (RED phase) | 18 |
| Failure cause | `/x`, `/my-notes`, `/sub/page`, `/empty` routes return **HTTP 404** — routing/rendering not implemented yet |
| Harness status | Working (build + preview + navigation succeed; 404s are real server responses, not harness errors) |

The build (`npm run build`) exits **0** with the new fixtures present, but emits only `dist/index.html` (the Epic-1 placeholder) — no content-collection route exists yet. This is the intended RED state: the harness is correct, the feature is absent.

## Harness decisions

- **Test runner:** `@playwright/test` added as a `web/` devDependency (lands in `web/package.json` + lockfile).
- **Config:** `web/playwright.config.ts`. `testDir: ./tests`, single `chromium` project, `baseURL: http://localhost:4321`.
- **Server:** Playwright `webServer` runs `npm run build && npm run preview` (Astro `preview` serves the static `web/dist`). One command (`npx playwright test`) builds and serves. `reuseExistingServer` off in CI.
- **Preview port:** `4321` (Astro preview default; confirmed locally).
- **Browser:** Chromium installed via `npx playwright install chromium`.
- **npm script:** `"test": "playwright test"` added to `web/package.json`.

## Test files

| File | AC | Tests |
| ---- | -- | ----- |
| `web/tests/ac1-gfm-core.spec.ts` | AC1 | 5 |
| `web/tests/ac2-js-disabled.spec.ts` | AC2 | 2 |
| `web/tests/ac3-crawlable-shell.spec.ts` | AC3 | 4 |
| `web/tests/ac5-slugging-edge.spec.ts` | AC5 | 3 |
| `web/tests/ac6-gfm-extensions.spec.ts` | AC6 | 4 |

(AC4 — content-driven routing from the repo-root vault — is exercised implicitly: all routes are sourced from `../content/*.md` fixtures, so any of these tests passing requires the single-source glob loader of AC4 to work. No separate spec, per scope.)

## AC -> test coverage mapping

- **AC1** (file-as-page, GFM semantic HTML at `/x`): heading levels h1..h6; `<strong>`/`<em>`; `<ul>`+`<ol>`+`<li>`; inline `<code>` + `<pre><code>`; real `<table>/<thead>/<tbody>/<tr>/<th>/<td>`.
- **AC2** (readable JS-disabled): `javaScriptEnabled: false` context — content visible + body text present in raw payload, no `astro-island`/`client:*` directive.
- **AC3** (well-formed crawlable shell): single `<html lang>`; `<head>` charset/title/viewport; content inside `<main>`/`<article>`; doctype + balanced structural tags. Every assertion guarded by an HTTP-200 check so it cannot false-pass against Astro's 404 page.
- **AC5** (deterministic slug + empty-file safety): `My Notes.md` -> `/my-notes`; `sub/page.md` -> `/sub/page`; near-empty `empty.md` -> valid `/empty` shell with non-empty `<title>`.
- **AC6** (GFM extensions + escaping): strikethrough -> `<del>`/`<s>`; task list -> `<li>` with disabled `<input type="checkbox">` (1 checked / 1 unchecked); bare URL -> `<a href>`; `<`/`&` escaped to `&lt;`/`&amp;` with no unescaped `<tags>`.

## Fixtures added (content vault — repo root, single source of truth)

- `content/x.md` — representative GFM fixture covering all AC1 + AC6 features.
- `content/empty.md` — near-empty (single `#` heading) for AC5 empty-file resilience.
- `content/My Notes.md` — space/uppercase filename for AC5 slug determinism (`/my-notes`).
- `content/sub/page.md` — nested file for AC5 nested slug (`/sub/page`).

Fixtures only — no Astro routing/rendering implemented (that is the GREEN/dev phase).

## RED confirmation

All 18 tests fail because the target routes return 404 (rendering unimplemented), confirmed by the captured 404 page body (`Path: /x`) in the Playwright error context. The harness successfully builds the site, starts the preview server, and navigates — so failures are feature-absence, not misconfiguration.

## To reach GREEN (dev/implementation phase — out of scope for this report)

Implement Tasks 1-3 of the story: add `remark-gfm` to `astro.config.mjs`, define `web/src/content.config.ts` (glob loader on `../content`), add `web/src/pages/[...slug].astro` + `web/src/layouts/Page.astro`, and resolve the `index.astro` route-collision note. Re-run `cd web && npx playwright test` — all 18 should pass.
