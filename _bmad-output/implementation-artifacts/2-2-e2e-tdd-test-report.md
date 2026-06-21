# Story 2.2 — E2E TDD Test Report (RED phase)

Story: **2.2 — Apply the GitHub-style default theme**
File under test target: `web/src/styles/github.css` (not yet created), `web/astro.config.mjs` Shiki config, `web/src/layouts/Page.astro` (Step 5 implementation — NOT done here).
Date: 2026-06-21
Phase: **RED** (failing-first). Theme CSS / Shiki-light / `github.css` are intentionally **not** implemented; these specs encode the contract that Step 5 must satisfy.

## What was added

- **NEW spec:** `web/tests/2-2-theme.spec.ts` — **19 tests** across 4 `describe` blocks (AC1, AC2, AC3, AC5).
- Includes a deterministic in-test **WCAG 2.1 §1.4.3** relative-luminance + contrast-ratio helper (no a11y dependency) so contrast regressions fail loudly.
- All color assertions read **rendered** values via `getComputedStyle` (through `locator.evaluate` / `page.evaluate`) — never inferred from raw HTML hex — so a drift between the DESIGN token and the rendered color is caught.

### Fixture extension (noted, minimal)

`content/x.md` was minimally extended (the existing fixture lacked the two AC5 edge cases):

1. **Long unbreakable code line** appended inside the existing ` ```js ` block (a 130-char single-token identifier) — exercises `overflow-x` scroll inside `<pre>` and the 760px-measure-not-blown-out assertions.
2. **Nested blockquote** (`>` then `> >`) added under a new `## Quote` section — exercises the per-level left-rule assertion.

These additions do **not** break any of the 20 existing specs (table count still 1, exactly one `<h1>`, 2 task-list checkboxes, etc. — re-verified green). No theme CSS or `astro.config` Shiki change was made (that is Step 5).

## AC → test mapping

| AC | Test (in `2-2-theme.spec.ts`) | Asserts |
|----|-------------------------------|---------|
| **AC1** | body uses the DESIGN sans font stack | `font-family` resolves to `-apple-system`/`BlinkMacSystemFont`/`Segoe UI` |
| AC1 | base font-size 16px and line-height ~1.6 | `font-size:16px`; `line-height` ≈ 25.6px / 1.6 |
| AC1 | content column centered at 760px measure | one of body/main/article computes `max-width:760px` |
| AC1 | body fg `#1f2328` on surface `#ffffff` | `color`=rgb(31,35,40), `background`=rgb(255,255,255) |
| AC1 | links are `#0969da` | link `color`=rgb(9,105,218) |
| AC1 | h1/h2 carry a bottom hairline | `border-bottom-width`>0 and style≠none |
| **AC2** | `<pre.astro-code>` no longer `github-dark` | class does not contain `github-dark` |
| AC2 | fenced `<pre>` background is `#f6f8fa` | `background-color`=rgb(246,248,250) |
| AC2 | fenced `<pre>` has 6px radius | `border-top-left-radius`≈6px |
| AC2 | token spans light, not dark palette | none of `#F97583/#9ECBFF/#B392F0/#E1E4E8/#FFAB70` emitted |
| **AC3** | WCAG helper reproduces reference ratios | fg 15.80 / ink 18.93 / link 5.19 / muted 6.11 / function 4.74 / keyword 5.03 (self-test of the math) |
| AC3 | fg/ink/link clear 4.5:1 on surface | computed ratio ≥ 4.5 for each |
| AC3 | every emitted Shiki token ≥ 4.5:1 on code-bg | ratio of each rendered token color vs rendered `<pre>` bg ≥ 4.5 |
| AC3 | tight function `#8250df` / keyword `#cf222e` pinned on `#f6f8fa` | both clear AA on the **rendered** code-bg AND code-bg = rgb(246,248,250) |
| **AC5** | long line scrolls inside `<pre>` not body | `pre overflow-x ∈ {auto,scroll}`; pre scrollWidth>clientWidth; `document` does not horizontally overflow |
| AC5 | 760px measure not blown out | article/main width ≤ 760 + 2·24 |
| AC5 | inline vs block code distinct | `:not(pre)>code` has `#f6f8fa` chip + `overflow-x:visible`; `pre>code` transparent/inherits (no double chip) |
| AC5 | nested blockquote keeps per-level left-rule | outer and `blockquote blockquote` each have `border-left-width`>0 |

## RED confirmation

Command: `cd web && npx playwright test` (Playwright config builds `dist/` fresh + serves via `astro preview`).

**Result: 23 passed, 15 failed.**

- **Existing 20 specs: all PASS** (re-verified in isolation: `20 passed`). No regression from the fixture extension.
- **New 2.2 specs: 15 FAIL (RED), 4 pass.** The 15 failing tests are the true RED gate — they fail because the theme tokens, light Shiki palette, `github.css`, h1/h2 hairlines, 760px measure, inline-code chip, and blockquote rules are not yet implemented (the current build still emits `class="astro-code github-dark"` with dark inline token colors `#F97583`/`#9ECBFF`/`#B392F0`, no `max-width`, no themed colors).

### The 15 failing (RED) new tests
- AC1: sans font stack, 16px/1.6, 760px measure, fg/surface color, link color, h1/h2 hairline (6)
- AC2: no `github-dark` class, `#f6f8fa` pre bg, 6px radius, no dark token palette (4)
- AC3: fg/ink/link ≥4.5:1 on surface, tight function/keyword pinned on `#f6f8fa` (2)
- AC5: 760px measure not blown out, inline-vs-block code distinct, nested blockquote per-level rule (3)

### The 4 passing new tests (legitimate — not false RED)
- **WCAG helper self-test** — pure math, validates the in-test contrast formula against the story's pre-computed reference ratios (no DOM; correctly always-green).
- **Every emitted Shiki token ≥4.5:1 on its actual rendered bg** — an invariant that holds on both the dark and light surface (dark tokens have high contrast on the dark bg too); the RED weight for AC3 code contrast is carried by the *pinned-on-`#f6f8fa`* test, which fails now.
- **Long line scrolls inside `<pre>`, body doesn't overflow** — passes because Astro's Shiki already injects `overflow-x:auto` inline on `<pre>`; the AC5 RED weight is carried by the *measure-not-blown-out* (currently 1280px-wide article, fails) + inline-vs-block + nested-blockquote tests.

These 4 are intentional invariants/self-tests, not assertions that should be RED — they guard the helper and confirm the already-correct overflow plumbing, while the 15 failing tests drive the actual theme implementation.

## Harness notes

- No harness changes were required; the existing `playwright.config.ts` (build + preview `dist/`, `reuseExistingServer:false`) drives the new spec unchanged.
- All new assertions are deterministic; the AC5 spec pins a 1280×900 viewport so the 760px measure is meaningful.

## Next (Step 5 — GREEN, not done here)
Implement `web/src/styles/github.css` (DESIGN tokens, 760px measure, h1/h2 hairlines, code-bg chip, blockquote rules, overflow handling), wire it into `Page.astro`, and set `markdown.shikiConfig.theme: 'github-light'`. Watch the tightest token (`#8250df` function = 4.74:1 on `#f6f8fa`) — if `github-light` emits a lighter purple, darken just that token. Re-run the full suite to GREEN (20 existing + 19 new).
