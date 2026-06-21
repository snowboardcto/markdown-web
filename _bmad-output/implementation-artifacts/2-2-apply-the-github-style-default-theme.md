# Story 2.2: Apply the GitHub-style default theme

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reader,
I want the page to look genuinely good,
so that reading on the web feels like a real publication, not raw text.

## Acceptance Criteria

1. **Given** a rendered content page (e.g. `/x`) **When** it displays **Then** it applies the DESIGN.md design tokens to the GFM body: `typography.sans` for body/UI text and `typography.mono` for code/paths, base `16px` / line-height `1.6`, the centered single-column reading **measure of `760px`** with `24px` page padding, body text in `colors.fg` (`#1f2328`) on the `colors.surface` (`#ffffff`) background, headings in `colors.ink` (`#0d1117`), links in `colors.link` (`#0969da`), and `h1` (`2.1em`) + `h2` (`1.5em`) carrying a bottom hairline in `colors.border` (`#d1d9e0`) — i.e. the page reads as a faithful, good-looking GitHub-style document, not unstyled HTML. *(AC1 — DESIGN tokens applied: typography, color, 760px measure; FR-6, UX-DR1)*
2. **Given** a page containing a fenced code block with a language tag (the `js` block in `content/x.md`) **When** it renders **Then** the code is **syntax-highlighted with a light GitHub-aligned palette** (not Astro's default `github-dark`): the code surface is `colors.code-bg` (`#f6f8fa`) with `rounded.code` (`6px`) corners, and token colors come from a light theme consistent with DESIGN's `colors.code` family (keyword ≈ `#cf222e`, string ≈ `#0a3069`, comment `#59636e`, function ≈ `#8250df`, number ≈ `#0550ae`); the emitted `<pre class="astro-code …">` no longer carries the `github-dark` class or dark inline `color:` values. *(AC2 — code blocks syntax-highlighted in the light GitHub palette; FR-6, UX-DR1)*
3. **Given** the themed page **When** body text, headings, links, muted/secondary text, and code are measured against their background surface **Then** every text/background pairing meets **WCAG 2.1 AA contrast** (≥ 4.5:1 for normal text, ≥ 3:1 for large text ≥ 24px/18.66px-bold): `colors.fg` `#1f2328` on `#ffffff` (≈ 16.8:1), `colors.ink` headings on `#ffffff`, `colors.link` `#0969da` on `#ffffff` (≈ 4.5:1 — verify it clears AA for normal-size link text), `colors.muted` `#59636e` on `#ffffff` (≈ 5.1:1), and code-token colors on `colors.code-bg` `#f6f8fa`. Any pairing that would fail AA must be corrected (darken the token) rather than shipped. *(AC3 — text contrast meets WCAG AA against the surface color; UX-DR9, NFR-6)*
4. **Given** the themed `Page.astro` layout and the new stylesheet **When** the site builds and renders **Then** the page is still **server-rendered, JS-free, semantically well-formed HTML** — the stylesheet ships as a static `<link>`/`<style>` in `<head>` (no `client:*` island, no runtime JS for styling), the single `<html lang>` → `<head>` → `<body>` → `<main>`/`<article>` shell is preserved, exactly one `<h1>`, and the existing 20 Playwright specs (`web/tests/*.spec.ts`) still pass unchanged. *(AC4 — theme adds visual layer only; no regression to 2.1's crawlable/JS-free contract; FR-5, FR-7, NFR-3)*

> Source: [_bmad-output/planning-artifacts/epics.md#Story 2.2: Apply the GitHub-style default theme] (lines 199–211): the epic ACs are (a) apply DESIGN.md tokens — typography/color/760px measure/code syntax palette via Shiki, (b) code blocks syntax-highlighted, (c) text contrast meets WCAG AA against the surface color. AC1/AC2/AC3 map 1:1 to those three. AC4 is a derived **regression guardrail** — Story 2.2 is purely additive theming on top of Story 2.1's render path, and FR-5/FR-7/NFR-3 (server-rendered, JS-free, perf budget) plus the 20 existing tests must not break. Token values are read directly from DESIGN.md frontmatter (lines 10–41).

## Tasks / Subtasks

- [ ] **Task 1 — Author the GitHub-style stylesheet as a DESIGN-token CSS file** (AC: 1, 3, 4)
  - [ ] Create `web/src/styles/github.css` (this is the architecture-named path — `architecture.md` line 123 declares `web/src/styles/github.css` # GitHub-style stylesheet (FR-6)). Define the DESIGN.md frontmatter values as CSS custom properties on `:root` so the file is a single source of token truth: `--md-surface:#ffffff; --md-fg:#1f2328; --md-ink:#0d1117; --md-muted:#59636e; --md-border:#d1d9e0; --md-link:#0969da; --md-success:#3fb950; --md-code-bg:#f6f8fa;` plus `--md-sans`, `--md-mono`, `--md-measure:760px`, `--md-page-x:24px`, `--md-radius-code:6px`. [Source: DESIGN.md frontmatter lines 10–41]
  - [ ] Apply the **layout/measure** tokens: center the content column at `max-width: var(--md-measure)` (760px) with horizontal page padding `var(--md-page-x)` (24px), `margin-inline:auto`. Body background `var(--md-surface)`, body text `color: var(--md-fg)`, base `font: 16px/1.6 var(--md-sans)`. [Source: DESIGN.md#Layout & Spacing line 66; typography lines 25–31]
  - [ ] Apply **typography** tokens to GFM elements: body/UI in `var(--md-sans)`, code/`pre`/inline-`code`/paths in `var(--md-mono)` at `85%` (`typography.scale.code`); headings in `color: var(--md-ink)` with `h1 { font-size:2.1em }`, `h2 { font-size:1.5em }`, `h3 { font-size:1.2em }`; `h1` and `h2` carry a **bottom hairline** (`border-bottom:1px solid var(--md-border)`, GitHub-style). Vertical rhythm: `1em` between blocks, `1.6em` above headings (`spacing.block` / `spacing.section`). [Source: DESIGN.md#Typography line 62; #Layout & Spacing line 66; frontmatter scale lines 31, 37]
  - [ ] Style the GitHub-aligned block components from DESIGN's component list **that the 2.1 fixture already produces** — `code-block`/inline code (fill `var(--md-code-bg)`, `border-radius:var(--md-radius-code)` 6px), `gfm-table` (hairline borders in `var(--md-border)`, zebra rows), `blockquote` (left-rule in `var(--md-border)`, muted text), `task-list` (checkbox list), and links in `var(--md-link)`. Scope these styles under the `<article>`/content container so they only theme rendered markdown. [Source: DESIGN.md#Components lines 79–84; #Shapes line 74]
  - [ ] Scope the stylesheet so it themes the rendered markdown body, NOT global resets that could leak. Do **not** introduce `site-header`, `get-client-cta`, or `pitch-card` styles — those components are **Story 2.6** (UX-DR2/UX-DR3), out of scope here.
- [ ] **Task 2 — Wire the stylesheet into the Page layout (server-rendered, JS-free)** (AC: 1, 4)
  - [ ] In `web/src/layouts/Page.astro`, import the stylesheet so Astro bundles it into a static `<link rel="stylesheet">` in `<head>` (e.g. `import '../styles/github.css';` in the frontmatter, or a `<link>` — use Astro's standard CSS handling so it ships as a hashed static asset, NOT inline runtime JS). Update the Story-2.1 header comment that currently says "No GitHub theme … (Stories 2.2/2.6)" to reflect that 2.2 now adds the reading theme (but still NOT the site-header/pitch-card chrome). [Source: web/src/layouts/Page.astro lines 1–30]
  - [ ] **Preserve the exact semantic shell** — keep the single `<html lang="en">` → `<head>` (charset, viewport, title) → `<body>` → `<main>` → `<article>` → `<slot />` structure unchanged so AC3-crawlable-shell + AC1-semantic tests still pass. Apply the centered 760px column on `<main>`/`<article>` (or `body`), not by adding extra wrapper divs that would change the asserted structure. [Source: web/tests/ac3-crawlable-shell.spec.ts — asserts single html/head/body, main/article, doctype]
  - [ ] Do NOT add any `client:*` directive and do NOT add runtime JS — styling must be pure CSS so the page stays readable JS-disabled (FR-5/FR-7, asserted by `ac2-js-disabled.spec.ts`). [Source: web/tests/ac2-js-disabled.spec.ts]
- [ ] **Task 3 — Switch Shiki to the light GitHub code theme** (AC: 2, 3)
  - [ ] In `web/astro.config.mjs`, set the Shiki theme to a light GitHub-aligned theme under `markdown.shikiConfig` — `shikiConfig: { theme: 'github-light' }` (Astro bundles Shiki and the `github-light` built-in theme; this replaces the current default `github-dark` confirmed in the built output, where `/x` emits `<pre class="astro-code github-dark">` with dark inline `color:` values like `#F97583`). Keep `gfm:true` + `remarkPlugins:[remarkGfm]` intact. [Source: web/astro.config.mjs lines 11–16; DESIGN.md frontmatter colors.code lines 19–24 — light palette]
  - [ ] Confirm the resulting code surface matches DESIGN: Shiki's `github-light` sets per-token inline colors; the **code-block background + radius** (`#f6f8fa` / 6px from `colors.code-bg` / `rounded.code`) comes from the `github.css` `pre` rule in Task 1 (Astro's Shiki `<pre>` gets a theme bg, so override it with `background: var(--md-code-bg)` if needed so it matches the DESIGN token rather than the theme's default). Verify the emitted `<pre>` no longer carries `github-dark` and no longer uses dark token colors. [Source: build output `dist/x/index.html` — current `astro-code github-dark`]
  - [ ] If `github-light` token colors drift materially from DESIGN's `colors.code` (keyword `#cf222e`, string `#0a3069`, comment `#59636e`, function `#8250df`, number `#0550ae`), prefer the stock `github-light` theme over hand-rolling a custom Shiki theme (don't reinvent — NFR-7); only build a custom token theme if AA contrast (Task 4) actually fails on `#f6f8fa`. Document the choice in Dev Agent Record.
- [ ] **Task 4 — Verify WCAG AA contrast for every text/surface pairing** (AC: 3)
  - [ ] Compute (or assert via test) the WCAG 2.1 contrast ratio for each pairing against its surface: `--md-fg` `#1f2328` on `#ffffff`; `--md-ink` headings on `#ffffff`; `--md-link` `#0969da` on `#ffffff`; `--md-muted` `#59636e` on `#ffffff`; and each Shiki code-token color on `--md-code-bg` `#f6f8fa`. Normal text must be ≥ 4.5:1; large text (≥ 24px or ≥ 18.66px bold) ≥ 3:1. [Source: DESIGN.md#Colors line 58; UX-DR9 / NFR-6 WCAG AA]
  - [ ] Pay special attention to **borderline pairings**: `colors.link` `#0969da` on white is ≈ 4.5:1 (right at the AA threshold for normal text) and `colors.muted` `#59636e` ≈ 5.1:1 — confirm the link clears AA at the actual rendered link font-size; if any token (link, muted, or a light code-token on `#f6f8fa`) falls below threshold, **darken that token** in `github.css` (or pick a darker Shiki token color) and re-verify, rather than shipping a failing pairing. [Source: DESIGN.md frontmatter colors lines 10–24]
  - [ ] Record the computed ratios in the Dev Agent Record so the AA claim is auditable (the AC says contrast "meets WCAG AA" — it must be demonstrably true, not asserted).
- [ ] **Task 5 — Add Playwright theme + contrast tests (extend the existing harness)** (AC: 1, 2, 3, 4)
  - [ ] Add a new spec (e.g. `web/tests/ac-theme.spec.ts`) that loads `/x` and asserts the **typography/measure tokens are applied**: the content container's computed `max-width` is `760px` (the measure), body `font-family` resolves to the sans stack and code/`pre` to the mono stack, body text color is `rgb(31, 35, 40)` (`#1f2328`), and `h1`/`h2` have a bottom border (hairline). Use `getComputedStyle` via `page.evaluate` / `locator.evaluate`. [Source: DESIGN.md tokens]
  - [ ] Assert **code highlighting is light**: the `<pre class="astro-code …">` for the `js` block no longer has the `github-dark` class, has a light theme (e.g. class contains `github-light` OR the `<pre>` background computes to `#f6f8fa`/light), and the inner token `<span>`s carry GitHub-light token colors (not the dark `#F97583`/`#9ECBFF` set). [Source: build output current dark classes]
  - [ ] Add an **automated AA contrast assertion**: either compute the ratio in-test for the key pairings (fg/ink/link/muted on surface; tokens on code-bg) using a small luminance helper, OR integrate an a11y assertion. Prefer a deterministic in-test contrast computation so the test fails loudly if a token regresses below AA. [Source: UX-DR9]
  - [ ] Run the FULL suite — `cd web && npx playwright test` — and confirm **all prior 20 specs still pass** (no regression) PLUS the new theme/contrast assertions. The existing structural specs (`ac1`, `ac2`, `ac3`, `ac5`, `ac6`) must remain green; if any breaks, the theme changed the semantic shell and must be corrected (theme is additive only). [Source: 2-1 story — 20/20 passing]
- [ ] **Task 6 — Final verification against ACs (Definition of Done)** (AC: 1, 2, 3, 4)
  - [ ] `cd web && npm run build` exits 0 and emits all 7 pages (no new build error from the stylesheet or Shiki theme change).
  - [ ] The rendered page applies DESIGN tokens — 760px centered measure, sans body / mono code, fg/ink/link/border colors, h1/h2 hairlines (AC1).
  - [ ] Fenced code is syntax-highlighted in the **light** GitHub palette on `#f6f8fa` with 6px corners; no `github-dark` remains (AC2).
  - [ ] Every text/surface pairing meets WCAG AA, with computed ratios recorded (AC3).
  - [ ] Page stays server-rendered, JS-free, semantically well-formed; CSS ships as a static asset; the single-h1/`<main>`/`<article>`/doctype shell is intact (AC4).
  - [ ] `cd web && npx playwright test` → all 20 prior specs + new theme/contrast specs pass; `cd web && npx astro check` → 0 errors (typecheck gate).
  - [ ] **Scope discipline held:** NO `site-header`/`get-client-cta`/`pitch-card` (Story 2.6), NO inter-file `.md` link resolution (2.3), NO media embedding (2.4), NO vault index (2.5), NO content negotiation / `api/` changes (2.7), NO dark mode (DESIGN: light is the web default; dark is a personality/client concern). This story is the reading/typography theme + light code highlighting + AA contrast ONLY. [Source: epics.md Epic 2 stories 2.3–2.7; DESIGN.md#Colors line 58, Do's/Don'ts line 92]

## Dev Notes

### What exists right now (read before coding)

- `web/` is the Astro 5 project from Stories 1.1 + 2.1. Story 2.1 shipped the **render path** (`remark-gfm`, a `glob()` content collection over the repo-root `../content` vault, the `[...slug].astro` route, and a **deliberately near-bare** `Page.astro` shell) and an established **Playwright harness with 20 passing specs**. 2.2 layers the *visual theme* on top of that already-working render — it does NOT change routing, the content collection, or the markdown→HTML pipeline. [Source: 2-1-render-a-md-file-to-an-html-page.md; web/ tree]
- `web/src/layouts/Page.astro` is the file 2.2 modifies for styling. It currently emits the minimal `<!doctype html>` → `<html lang="en">` → `<head>`(charset/viewport/title) → `<body>` → `<main>` → `<article>` → `<slot />` shell with **no CSS at all** and a comment explicitly deferring the GitHub theme to "Stories 2.2/2.6". This story fulfills the 2.2 half (reading theme) but NOT the 2.6 half (site-header/pitch-card). [Source: web/src/layouts/Page.astro lines 1–30]
- `web/astro.config.mjs` currently sets only `markdown: { gfm: true, remarkPlugins: [remarkGfm] }`. There is **no `shikiConfig`**, so Astro uses its default Shiki theme — verified by building: `/x` emits `<pre class="astro-code github-dark">` with dark inline token colors (`#F97583`, `#9ECBFF`, `#B392F0`, …). 2.2 must add `shikiConfig.theme: 'github-light'` to flip this to a light GitHub palette (the comment on line 8–9 already anticipates "the GitHub-style syntax palette/theme is intentionally deferred to Story 2.2"). [Source: web/astro.config.mjs; build output `dist/x/index.html`]
- The build fixture `content/x.md` already contains a fenced ```` ```js ```` code block (a `greet()` function), inline `code`, a GFM table, blockquote-adjacent content, a task list, headings h1–h6, and emphasis — so **no new content fixture is needed** to prove the theme. Theme against the existing `/x` page. [Source: content/x.md]
- `.gitignore` already excludes `web/dist/`, `web/node_modules/`, `web/.astro/`. Do not commit build artifacts. Commit `web/package.json`/`package-lock.json` only if a new dep is added (none is expected — `shikiConfig` uses Astro's bundled Shiki + built-in `github-light` theme). [Source: 2-1 Dev Notes]

### The architecture-named target file (do not invent a different path)

- The architecture **explicitly names the stylesheet**: `web/src/styles/github.css   # GitHub-style stylesheet (FR-6)` (architecture.md line 123). Create the theme there — do not put styles inline-only in `Page.astro` or under a different name. Importing `../styles/github.css` from `Page.astro` lets Astro bundle/hash it as a static CSS asset (server-rendered, JS-free), satisfying both FR-6 and the JS-free contract. [Source: architecture.md line 123]

### DESIGN.md token map (the source of truth for every value)

Read DESIGN.md frontmatter (lines 10–41) and the Colors/Typography/Layout sections. The exact token → CSS mapping for this story:

| DESIGN token | Value | Applies to |
|---|---|---|
| `colors.surface` | `#ffffff` | body background |
| `colors.fg` | `#1f2328` | body text |
| `colors.ink` | `#0d1117` | headings, `.md` chip |
| `colors.muted` | `#59636e` | secondary/muted text |
| `colors.border` | `#d1d9e0` | hairlines: h1/h2 underline, table borders, blockquote rule |
| `colors.link` | `#0969da` | links |
| `colors.success` | `#3fb950` | "live"/success (lock glyph — mostly native; not required on web body) |
| `colors.code-bg` | `#f6f8fa` | inline + fenced code surface |
| `colors.code.*` | keyword `#cf222e`, string `#0a3069`, comment `#59636e`, function `#8250df`, number `#0550ae` | code syntax palette (delivered via Shiki `github-light`) |
| `typography.sans` | `-apple-system, BlinkMacSystemFont, 'Segoe UI', 'Noto Sans', Helvetica, Arial, sans-serif` | body/UI |
| `typography.mono` | `ui-monospace, SFMono-Regular, Menlo, Consolas, monospace` | code/paths/chip |
| `typography.base` / `line` | `16px` / `1.6` | base font + line-height |
| `typography.measure` | `760px` | centered reading column max-width |
| `typography.scale` | h1 `2.1em`, h2 `1.5em`, h3 `1.2em`, small `14px`, code `85%` | heading sizes, code size |
| `rounded.code` | `6px` | code/inline-code corners |
| `spacing.block` / `section` / `page-x` | `1em` / `1.6em` / `24px` | block rhythm, space above headings, page padding |

[Source: DESIGN.md frontmatter lines 10–41; #Colors line 58; #Typography line 62; #Layout & Spacing line 66; #Shapes line 74]

DESIGN says: *headings `h1`/`h2` carry a bottom hairline (GitHub-style)*; *centered single-column at the measure, page padding `page-x`*; *`rounded.code` code/inline*; *light is the web default — dark mode is a personality/client concern, NOT a fixed token here* (so do NOT add a dark theme or `prefers-color-scheme: dark` block). [Source: DESIGN.md lines 62, 66, 74, 92]

### Shiki theme decision (the load-bearing technical choice)

- The epic AC literally says "code syntax palette **via Shiki**" — so deliver code colors through Shiki, not by hand-coloring spans. Astro bundles Shiki and its built-in themes; setting `markdown.shikiConfig.theme: 'github-light'` is the idiomatic, no-new-dependency way to get a GitHub-light palette that closely matches DESIGN's `colors.code` family. Astro re-highlights on the next build (HTML is static, so a config change requires a rebuild — already part of the build/test loop). [Source: epics.md Story 2.2 line 209; architecture.md line 70 "Shiki for code highlighting, GitHub-style stylesheet"]
- **Background note:** Shiki themes set their own `<pre>` background. DESIGN's `colors.code-bg` is `#f6f8fa`; `github-light`'s default code bg is also `#fff`/light but may not be exactly `#f6f8fa`. Override the `<pre.astro-code>` background to `var(--md-code-bg)` in `github.css` so the surface matches the DESIGN token precisely (and so inline `code` and fenced `pre` share the same fill). [Source: DESIGN.md colors.code-bg line 17]
- **Do not hand-roll a custom Shiki theme JSON** unless `github-light` actually fails AA on `#f6f8fa` (Task 4) — that would reinvent commodity plumbing (NFR-7). If a token must change for contrast, the smallest fix is a targeted CSS override of that one token color or a darker built-in theme, documented in the Dev Agent Record.

### Regression guardrails — what 2.2 must NOT break

The 20 existing specs encode 2.1's contract. The theme is **additive CSS + one Shiki config line**; it must not alter the asserted structure:
- `ac1-gfm-core.spec.ts` — asserts exactly **one `<h1>`**, visible h2–h6, `<strong>`/`<em>`, `<ul>`/`<ol>`/`<li>`, `<code>`/`<pre><code>`, one `<table>` with thead/tbody/tr/th/td. → Style these elements; do **not** add a second `<h1>` (e.g. a wordmark `<h1>` would break this — and the wordmark is 2.6 anyway), do not restructure the table.
- `ac2-js-disabled.spec.ts` — content must render JS-disabled. → CSS only, no `client:*`, no JS.
- `ac3-crawlable-shell.spec.ts` — single `<html lang>`/`<head>`/`<body>`, `<meta charset>`+viewport, content inside `<main>/<article>`, `<!doctype html>`, balanced structural-tag counts. → Keep the exact shell; apply the 760px column on existing elements, don't inject new wrapper structure that changes those counts/locators.
- `ac5-slugging-edge.spec.ts`, `ac6-gfm-extensions.spec.ts` — slug/title + GFM-extension assertions; unaffected by CSS. → Just keep them green.
[Source: web/tests/*.spec.ts — 5 spec files, 20 tests, all green per 2-1]

### Architecture compliance / guardrails

- **Beauty + performance budget (NFR-3):** web is "static, zero/low JS." The theme is pure static CSS — no JS, no hydration. A web font download would add latency; DESIGN's `typography.sans`/`mono` are **system-font stacks** (no web-font fetch), so honor that — do NOT add `@font-face`/Google Fonts. [Source: epics.md NFR-3; DESIGN.md typography line 26]
- **Born-compatibility / SEO (FR-7, NFR-4):** the agentless HTML path stays crawlable and JS-free. CSS-only theming preserves this. [Source: epics.md FR-7/NFR-4]
- **Don't reinvent plumbing (NFR-7):** use Astro's built-in Shiki + a built-in theme; standard CSS. No CSS framework (Tailwind etc.), no custom markdown/highlight pipeline. [Source: epics.md NFR-7; architecture.md line 70]
- **Accessibility floor (UX-DR9 / NFR-6):** web = semantic HTML + WCAG AA contrast + keyboard/focus. Contrast is the explicit AC3; also keep `:focus` visible (don't `outline:none` without a replacement) and don't drop semantic elements. [Source: epics.md UX-DR9, NFR-6]

### Scope boundaries — what this story is NOT (prevent scope creep)

This story is the **reading/typography theme + light code highlighting + AA contrast** ONLY. The following are explicitly OTHER stories — do not pull them in:
- **Story 2.3** — inter-file `[link](other.md)` resolution + back/forward nav + broken-link state. (You may *style* links, but do not implement link resolution.)
- **Story 2.4** — `![](media/x.jpg)` image/video embedding.
- **Story 2.5** — browsable vault index / entry surface.
- **Story 2.6** — `site-header` (sticky translucent wordmark + "the vision" link + get-client CTA) and `pitch-card` (end-of-page recruiting card) + EXPERIENCE.md microcopy. **These chrome COMPONENTS are 2.6, even though DESIGN.md lists them** — 2.2 does not add them. The DESIGN component bullets you implement now are only `code-block`/`gfm-table`/`blockquote`/`task-list` (the in-body GFM elements). [Source: DESIGN.md#Components lines 78–84]
- **Story 2.7** — content negotiation (`Accept` → raw `.md`) in `api/`. Do not touch `api/`.
- **Dark mode** — DESIGN.md: light is the web default; dark is a personality/client concern, not a token here. Do not add a dark theme. [Source: DESIGN.md#Colors line 58, Do's/Don'ts line 92]
[Source: epics.md Epic 2 stories 2.3–2.7 lines 213–278]

### Testing standards summary

- Verification command: `cd web && npx playwright test` (run the FULL suite — prove 20 prior + new theme specs all pass). Typecheck gate: `cd web && npx astro check`. Build gate: `cd web && npm run build`. No lint command configured. [Source: 2-1 Dev Notes]
- Test the **built/preview output** (the Playwright config already builds + previews `dist/`), since the AC is about the rendered HTML. Use `getComputedStyle` (via `locator.evaluate`/`page.evaluate`) to assert applied token values (max-width 760px, colors, font stacks, hairlines) — CSS application is only verifiable on the rendered page, not in the raw HTML. [Source: web/playwright.config.ts — builds then previews]
- For AA contrast, prefer a deterministic in-test luminance/ratio computation over a heavy a11y dependency, so the gate fails loudly if a token regresses. [Source: UX-DR9]
- Bash tool resets cwd between calls — chain commands (`cd web && npm run build && npx playwright test`) or use absolute paths. [Source: 2-1 Dev Notes]

### Previous story intelligence (Story 2.1 — basic render)

- 2.1 established the Playwright harness and the rule "**an exit-0 build with a missing/empty page is a FAILED gate**" — apply the same rigor: a build that exits 0 but ships `github-dark` or fails a contrast assertion is a failed gate. [Source: 2-1 Dev Notes / Task 7]
- 2.1's `playwright.config.ts` sets `reuseExistingServer: false` so the harness always rebuilds + serves fresh `dist/` — important here because the Shiki theme change only takes effect after a rebuild; the harness handles that automatically. [Source: 2-1 Review Findings — Patch #3]
- 2.1 committed `package-lock.json` for reproducible CI `npm ci`. If you add a dep (not expected), commit the lockfile. The `shikiConfig.theme` change adds **no** new dependency (Shiki + `github-light` are bundled with Astro). [Source: 2-1 Dev Notes]
- 2.1 left `web/src/pages/index.astro` (the Epic-1 "coming soon" placeholder) serving `/`. It is NOT a content page and is out of scope for theming here — theme the content render path (`Page.astro` → `/x` etc.); do not redesign the placeholder index (that's Story 2.5's surface). [Source: 2-1 Completion Notes]

### Git / pipeline intelligence

- `deploy-web.yml` builds `web/` (`npm ci` + `npm run build`) and uploads `web/dist`; it triggers on `web/**`. Adding `web/src/styles/github.css` and editing `web/astro.config.mjs` + `web/src/layouts/Page.astro` are all under `web/**`, so this story's changes WILL trigger a deploy normally — no workflow change needed for 2.2. (The separate `content/**` trigger gap is 2.1 Open Question #2, not this story.) [Source: 2-1 Git intelligence; .github/workflows/deploy-web.yml]

### Project Structure Notes

- New/changed files expected: `web/src/styles/github.css` (NEW — the GitHub-style theme), `web/astro.config.mjs` (UPDATE — add `shikiConfig.theme: 'github-light'`), `web/src/layouts/Page.astro` (UPDATE — import the stylesheet, refresh the deferral comment), `web/tests/ac-theme.spec.ts` (NEW — theme + contrast assertions). No content fixture change (theme against existing `content/x.md`). Do not modify `api/`, `clients/`, `infra/`, `_bmad/`, or the content collection/routing files. [Source: architecture.md line 123; web/ tree]
- No conflict with the established layout — this is the intended Epic 2 theming step described in architecture.md (the `web/src/styles/github.css` slot) and DESIGN.md (UX-DR1 GitHub-style default theme). [Source: architecture.md lines 119–123; epics.md UX-DR1 line 64]

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 2.2: Apply the GitHub-style default theme] — user story + ACs (lines 199–211): DESIGN tokens (typography/color/760px/Shiki palette), code highlighted, WCAG AA contrast
- [Source: _bmad-output/planning-artifacts/epics.md#UX Design Requirements] — UX-DR1 GitHub-style default theme / DESIGN tokens / light default (line 64); UX-DR9 accessibility floor / WCAG AA contrast (line 72)
- [Source: _bmad-output/planning-artifacts/epics.md#Requirements Inventory] — FR-6 beautiful default presentation (line 25); FR-5 server-rendered (line 24); FR-7 crawlable (line 26); NFR-3 beauty+perf budget (line 43); NFR-6 accessibility floor / WCAG AA (line 46); NFR-7 don't reinvent plumbing (line 47)
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-the-markdown-web-2026-06-21/DESIGN.md] — frontmatter tokens colors/typography/rounded/spacing (lines 10–41); #Colors light-default + code palette (line 58); #Typography (line 62); #Layout & Spacing 760px measure (line 66); #Shapes radii (line 74); #Components GitHub-aligned code-block/table/blockquote/task-list (lines 79–84); Do's/Don'ts no-fake-chrome / light-default (lines 88–92)
- [Source: _bmad-output/planning-artifacts/architecture.md] — Browser path: Astro + remark/rehype (GFM) + Shiki + GitHub-style stylesheet (line 70); `web/src/styles/github.css` # GitHub-style stylesheet (FR-6) (line 123)
- [Source: _bmad-output/implementation-artifacts/2-1-render-a-md-file-to-an-html-page.md] — existing render path, Page.astro shell, 20-spec Playwright harness, scope-boundary precedent
- [Source: web/astro.config.mjs, web/src/layouts/Page.astro, web/src/pages/[...slug].astro, content/x.md, web/tests/*.spec.ts, dist/x/index.html] — current repo state (default Shiki = `github-dark`)

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m]

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed — comprehensive developer guide created.

### File List
