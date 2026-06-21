---
status: final
created: 2026-06-21
updated: 2026-06-21
sources:
  - _bmad-output/planning-artifacts/prds/prd-the-markdown-web-2026-06-21/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/briefs/brief-the-markdown-web-2026-06-21/brief.md
  - _bmad-output/the-markdown-web.md
colors:
  ink: "#0d1117"
  fg: "#1f2328"
  muted: "#59636e"
  border: "#d1d9e0"
  link: "#0969da"
  success: "#3fb950"
  code-bg: "#f6f8fa"
  surface: "#ffffff"
  code:
    keyword: "#cf222e"
    string: "#0a3069"
    comment: "#59636e"
    function: "#8250df"
    number: "#0550ae"
typography:
  sans: "-apple-system, BlinkMacSystemFont, 'Segoe UI', 'Noto Sans', Helvetica, Arial, sans-serif"
  mono: "ui-monospace, SFMono-Regular, Menlo, Consolas, monospace"
  base: "16px"
  line: "1.6"
  measure: "760px"
  scale: { h1: "2.1em", h2: "1.5em", h3: "1.2em", small: "14px", code: "85%" }
rounded:
  code: "6px"
  button: "9px"
  card: "14px"
  window: "12px"
spacing:
  block: "1em"
  section: "1.6em"
  page-x: "24px"
components: [site-header, get-client-cta, pitch-card, code-block, gfm-table, blockquote, task-list, client-titlebar, client-toolbar, address-bar, personality-selector]
---

# DESIGN.md — The Markdown Web

> **What this owns.** The **web (browser) theme** and the **native client *shell* chrome**. It does **not** own native client *content* presentation — that is rendered per-reader by AI personalities and is intentionally undesigned here. EXPERIENCE.md references these tokens by name.

## Brand & Style

Blank-slate brand, kept deliberately understated. Wordmark: **`.md the markdown web`** — a small monospace `.md` chip in `{colors.ink}` beside a lowercase sans wordmark. The aesthetic is **clean, developer-credible, document-first** — close to GitHub's reading surface, dressed a notch better. Not plain: it should look *good* and *cast the vision*, never decorative for its own sake.

Two surfaces, one principle:
- **Web** = a faithful, good-looking GFM render that *also recruits* — every page quietly sells the native client (the upgrade moment).
- **Native client** = a browser-like *shell* we design; the *content* inside is the reader's AI personality's job, not ours.

## Colors

Light is the web default. **Dark mode is a personality/client concern, not a fixed token here.** Core: text `{colors.fg}` (headings `{colors.ink}`), secondary `{colors.muted}`, hairlines `{colors.border}`, links `{colors.link}`, "live"/success `{colors.success}`, code surfaces `{colors.code-bg}` on `{colors.surface}`. Code syntax palette under `{colors.code}` (GitHub-light-aligned).

## Typography

`{typography.sans}` for UI and body; `{typography.mono}` for code, paths, and the `.md` chip. Base `{typography.base}` / line `{typography.line}`; reading measure `{typography.measure}`. Headings `h1 {typography.scale.h1}` and `h2 {typography.scale.h2}` carry a bottom hairline (GitHub-style); inline/blocks code at `{typography.scale.code}`.

## Layout & Spacing

Centered single-column at `{typography.measure}`, page padding `{spacing.page-x}`. Vertical rhythm: `{spacing.block}` between blocks, `{spacing.section}` above headings. Web site-header is sticky, translucent (blur). The pitch card and client window are the only elevated elements.

## Elevation & Depth

Minimal. Web: flat, with one subtle gradient **pitch card** (`{rounded.card}`). Native client: a single soft window shadow (`{rounded.window}`) so it reads as an app; toolbar/address-bar sit flat on a faint fill.

## Shapes

`{rounded.code}` code/inline, `{rounded.button}` buttons & address bar, `{rounded.card}` cards, `{rounded.window}` app window. No sharp corners; no heavy borders — hairlines only.

## Components

- **site-header** *(web)* — sticky translucent bar: wordmark · `the vision` link · **get-client-cta**. This is page content, NOT fake browser chrome.
- **get-client-cta** — solid `{colors.ink}` pill button, `{rounded.button}`.
- **pitch-card** *(web)* — end-of-page recruiting card: headline + vision sentence + **Get the client** + "Why a markdown web?" link. Subtle gradient surface, `{rounded.card}`.
- **code-block / gfm-table / blockquote / task-list** — GitHub-aligned: `{colors.code-bg}` fills, hairline tables with zebra rows, left-rule blockquotes, checkbox task lists.
- **client-titlebar / client-toolbar** *(native shell)* — window title "The Markdown Web"; toolbar with back/forward/reload.
- **address-bar** *(native shell)* — shows `host/path` + a **`.md only`** tag; lock glyph in `{colors.success}`. Loads markdown pages only.
- **personality-selector** *(native shell)* — toolbar chip (avatar + name + ▾) to choose the rendering AI personality.

## Do's and Don'ts

- **Do** keep the web render faithful to GFM and genuinely good-looking.
- **Do** make every web page a recruiting surface for the client — tastefully.
- **Don't** add fake browser chrome to the web (the real browser shows the URL).
- **Don't** design the native client's *content* look — that's the personality's. Only the shell is ours.
- **Don't** hard-code dark mode as brand; light is the web default, themes are a personality/client choice.

## Mockups

- Web basic render — [`mockups/basic-html-render.png`](mockups/basic-html-render.png)
- Web v2 (vision + get-client) — [`mockups/web-v2.png`](mockups/web-v2.png)
- Native client shell — [`mockups/native-client.png`](mockups/native-client.png)

_Spines win on conflict with any mock._
