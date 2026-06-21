# Story 2.2 — AC Trace Report

**Story:** 2.2 — Apply the GitHub-style default theme
**Story file:** `_bmad-output/implementation-artifacts/2-2-apply-the-github-style-default-theme.md`
**Date:** 2026-06-21
**Trace performed by:** BMAD STEP 10 (AC trace)

## Verification result

- Command: `cd web && npx playwright test`
- Result: **39 passed, 0 failed** (1 spec file `2-2-theme.spec.ts` with 19 theme/contrast/edge-case tests + 20 existing no-regression specs across `ac1`/`ac2`/`ac3`/`ac5`/`ac6`).
- Build gate (recorded in Dev Agent Record): `npm run build` exit 0, 7 pages; `astro check` 0 errors.

## Implementation under test

- `web/src/styles/github.css` — DESIGN tokens as `:root` custom properties, body/typography/measure, light code surface, GFM components, AC5 overflow handling, Shiki two-token AA correction (case-insensitive override).
- `web/src/layouts/Page.astro` — imports `../styles/github.css` as a static asset; semantic shell unchanged.
- `web/astro.config.mjs` — `markdown.shikiConfig.theme: 'github-light'`.

## AC → Test matrix

| AC | Acceptance Criterion (summary) | Covering test(s) in `web/tests/2-2-theme.spec.ts` | Status |
|----|--------------------------------|----------------------------------------------------|--------|
| AC1 | DESIGN tokens applied: sans/mono typography, 16px/1.6 base, centered 760px measure + 24px padding, fg `#1f2328` on surface `#ffffff`, ink headings `#0d1117`, links `#0969da`, h1/h2 bottom hairline `#d1d9e0` | `AC1 — body uses the DESIGN sans font stack`; `base font-size is 16px and line-height is ~1.6`; `content column is centered at the 760px reading measure`; `body text is colors.fg (#1f2328) on the colors.surface (#ffffff) background`; `links are colors.link (#0969da)`; `h1 and h2 carry a bottom hairline border` | PASS |
| AC2 | Code blocks syntax-highlighted in light GitHub palette (not `github-dark`); code surface `#f6f8fa` with 6px corners; no dark token colors | `<pre.astro-code> no longer carries the github-dark class`; `fenced <pre> background is the light code surface #f6f8fa (not dark)`; `fenced <pre> has the 6px code radius`; `token spans use light colors, not the dark github-dark palette` | PASS |
| AC3 | Every text/background pairing meets WCAG 2.1 AA (≥4.5:1), measured on rendered colors; tight function/keyword code tokens pinned | `the WCAG helper is sound (anchored against two reference points)`; `body fg, headings ink, and links all clear 4.5:1 on the surface`; `every emitted Shiki token clears 4.5:1 on the code-bg surface`; `the tight emitted function (#6F42C1) and keyword (#cf222e) tokens clear AA on the rendered #f6f8fa`; `the AA override is case/format-robust — corrects a LOWERCASE-hex emit too` | PASS |
| AC4 | Theme adds visual layer only — server-rendered, JS-free, semantic shell preserved; existing 20 specs still pass unchanged | No-regression guard: 20 existing specs `ac1-gfm-core` (single h1, GFM semantics, table), `ac2-js-disabled` (renders JS-disabled), `ac3-crawlable-shell` (single html/head/body, main/article, doctype), `ac5-slugging-edge`, `ac6-gfm-extensions` — all green; CSS ships as static asset (no `client:*`) | PASS |
| AC5 | Theme degrades gracefully without breaking the 760px measure: (a) long code line scrolls in `<pre>`; (b) inline-vs-block code distinct; (c) nested blockquote keeps per-level rule; (d) wide table scrolls within column | `a long unbreakable code line scrolls inside <pre> (overflow-x), not the body`; `the 760px reading measure is not blown out by the wide code/table`; `inline <code> gets the code-bg chip but block code does not double-apply it`; `a nested blockquote keeps a visible left-rule at each level` | PASS |

## No-regression guard (AC4 detail)

The 20 pre-existing specs encode Story 2.1's crawlable/JS-free/semantic contract and all remain green under the theme, confirming the theme is additive CSS only:

- `ac1-gfm-core.spec.ts` (5 tests) — exactly one `<h1>`, GFM semantic HTML, real table.
- `ac2-js-disabled.spec.ts` (2 tests) — content renders with JS disabled.
- `ac3-crawlable-shell.spec.ts` (4 tests) — single html/head/body shell, meta charset/viewport, main/article, doctype.
- `ac5-slugging-edge.spec.ts` (5 tests) — deterministic slugging + empty-file resilience.
- `ac6-gfm-extensions.spec.ts` (4 tests) — strikethrough, task list, autolink, HTML escaping.

## Gaps

**None.** All 5 ACs map to passing tests; the 20-spec no-regression guard is green. Review verdict was PASS WITH ITEMS and all 4 patch action items were applied (case-insensitive AA override + robustness test, real-emitted-token pinning, dropped `article` coupling / broad link selectors, `box-sizing:border-box` outer measure). The 5 deferred items are non-blocking test-coverage/cosmetic gaps tracked in `deferred-work.md` — no defect blocks ship.

## Conclusion

5/5 ACs covered and green; 39/39 Playwright tests passing; review resolved. Story 2.2 meets its Definition of Done.
