# Story 2.1: Render a `.md` file to an HTML page

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an author,
I want each `.md` file rendered as an HTML page,
so that anyone with a browser can read it.

## Acceptance Criteria

1. **Given** a file `content/x.md` containing GFM content (at minimum: headings, **bold**/_italic_, ordered + unordered lists, inline + fenced code, and a GFM table) **When** the site builds (`cd web && npm run build`) **Then** the build produces `web/dist/x/index.html` (the route `/x`), and that HTML renders each of those GFM features as correct semantic elements — `<h1>`..`<h6>`, `<strong>`/`<em>`, `<ul>`/`<ol>`/`<li>`, `<code>` and `<pre><code>`, and a real `<table>`/`<thead>`/`<tbody>`/`<tr>`/`<th>`/`<td>`. *(AC1 — file-as-page: `content/x.md` → `/x` with correct GFM HTML; FR-1, FR-5)*
2. **Given** the built page for `/x` **When** it is loaded with JavaScript disabled **Then** the full article body is present and readable in the served HTML — the content is in the initial HTML payload, NOT injected by client-side JS (no hydration/`client:*` directive is required for content to appear). *(AC2 — readable JS-disabled; FR-5, FR-7)*
3. **Given** the built HTML for any content page **When** a crawler parses it **Then** the markup is well-formed: a single top-level `<html lang>` → `<head>` (with `<meta charset>`, `<title>`, viewport) → `<body>` with the rendered content inside a semantic container (e.g. `<main>`/`<article>`), no unclosed tags, and zero dependency on client JS to render the content. *(AC3 — well-formed, crawlable, born-compatible HTML; FR-7)*
4. **Given** the existing `content/` vault is the single source of truth at the repo root **When** the web app builds **Then** Astro sources its pages from `content/*.md` (NOT a copy duplicated into `web/`), so adding a new `content/<name>.md` and rebuilding yields a new `/<name>` page with no per-file code change. *(AC4 — content-driven routing from the shared vault; FR-1, architecture single-source-of-truth boundary)*
5. **Given** an edge-case `.md` filename in the vault — e.g. a name containing spaces/uppercase/Unicode such as `content/My Notes.md`, or a nested file `content/sub/page.md`, or a near-empty file `content/empty.md` (front matter or a single `#` heading only) **When** the site builds **Then** the build still exits 0 and the slug is derived deterministically (Astro's default `glob()` slug: lower-cased, spaces/path-separators normalised — `My Notes.md` → `/my-notes`, `sub/page.md` → `/sub/page`), the page emits valid `dist/<slug>/index.html`, and an empty/near-empty body produces a valid (possibly content-light) document rather than a crash or a broken shell. *(AC5 — deterministic, safe slugging + empty-file resilience; FR-1, edge cases)*
6. **Given** the seed fixture also includes GFM extensions beyond the AC1 core — strikethrough (`~~text~~`), a task list (`- [ ] item`), and an autolinked bare URL — plus a special-character/escaping case (e.g. `<`, `&`, an inline `` `code with <tags>` ``) **When** the page builds **Then** strikethrough renders as `<del>`/`<s>`, task-list items render as `<li>` with a disabled `<input type="checkbox">`, the bare URL becomes an `<a href>`, and special characters are HTML-escaped (no raw unescaped `<`/`&` that would break crawler parsing or inject markup). *(AC6 — full GFM extension coverage + correct HTML escaping; FR-1, FR-7)*

> Source: [_bmad-output/planning-artifacts/epics.md#Story 2.1: Render a `.md` file to an HTML page] (lines 185–197). AC4 is derived from the architecture's "`content/` is the single source of truth" boundary (architecture.md line 143) — it is required for AC1 to be true "for any `.md` file," not just a hand-placed one. AC5/AC6 are elicited edge-case hardening (deterministic slugs, empty-file safety, full GFM-extension + escaping coverage); they sharpen — but do not expand beyond — the epic's "any `.md` file with GFM content" and FR-7 "valid HTML a crawler can parse."

## Tasks / Subtasks

- [x] **Task 1 — Add GFM markdown processing to the Astro config** (AC: 1, 2, 3)
  - [x] In `web/`, install the GFM remark plugin: `remark-gfm` (Astro already bundles `remark`/`rehype`; `remark-gfm` adds tables, strikethrough, task lists, autolinks). Run `cd web && npm install remark-gfm` so it lands in `web/package.json` + `web/package-lock.json`.
  - [x] Update `web/astro.config.mjs`: under `markdown`, set `gfm: true` (Astro default) and register `remarkPlugins: [remarkGfm]`. Do NOT add Shiki theme tokens, a custom syntax theme, or the GitHub stylesheet here — code highlighting polish and the GitHub-style theme are **Story 2.2** scope. Astro's built-in Shiki default may run, but styling/contrast/palette is explicitly out of scope for 2.1.
  - [x] Keep the config minimal and additive: no integrations (Tailwind, MDX, sitemap, etc.), no `client:*` islands. Content must be pure server-rendered static HTML (FR-5/FR-7).
- [x] **Task 2 — Route `content/*.md` → `/*` via an Astro Content Collection (glob loader)** (AC: 1, 4)
  - [x] Define a content collection in `web/src/content.config.ts` (Astro 5 Content Layer API) using the `glob()` loader from `astro/loaders`, pointing `base` at the repo-root vault: `base: '../content'`, `pattern: '**/*.md'` (the vault lives one level up from `web/`). This makes `content/` the source — do NOT copy `.md` files into `web/src/pages/` (would violate the single-source-of-truth boundary, AC4).
  - [x] Create the dynamic route `web/src/pages/[...slug].astro` that calls `getCollection(...)` + `getStaticPaths()` to emit one page per `.md` file, and renders each entry's body via `render(entry)` → `<Content />`. The slug must map `content/x.md` → `/x` (drop the `.md` extension; `index.md` → `/`). Verify the emitted path is `dist/x/index.html`.
  - [x] Confirm the glob `base` resolves correctly given that `node_modules`/build run from `web/` but the vault is at `../content`. If a relative `base` proves brittle in the Astro 5 loader, resolve an absolute path from `import.meta.url`/`fileURLToPath` instead — but keep the source pointed at the repo-root `content/`, never a duplicate.
- [x] **Task 3 — Provide a minimal page layout / HTML document shell** (AC: 2, 3)
  - [x] Author a minimal layout (e.g. `web/src/layouts/Page.astro`) that wraps rendered content in a valid HTML5 document: `<!doctype html>`, `<html lang="en">`, `<head>` with `<meta charset="utf-8">`, `<meta name="viewport" ...>`, and a `<title>` (derive from the page's `# H1` or filename), and `<body>` with the article body inside a semantic `<main>`/`<article>`. The `[...slug].astro` route renders `<Content />` inside this layout.
  - [x] Make `<title>` derivation total: if a page has no `# H1` and no front-matter title (the empty-file edge case, AC5), fall back to the slug/filename so `<title>` is never empty. The document shell must stay valid even when the body is empty.
  - [x] Do NOT add the GitHub-style stylesheet, design tokens, `site-header`, or `pitch-card` — those are Stories 2.2 / 2.6. A bare (or near-bare) document is correct for 2.1; the only requirement is well-formed, semantic, JS-free HTML. A tiny amount of unopinionated base CSS is acceptable only if needed for legibility, but the themed look is explicitly deferred.
  - [x] Ensure no `client:*` directive is used anywhere on the content path — content must render server-side so it survives JS-disabled (AC2).
- [x] **Task 4 — Add a representative seed `.md` file to the vault that exercises every required GFM feature** (AC: 1, 6)
  - [x] Add a content file under `content/` (e.g. `content/x.md` to mirror the AC literally, or a real seed page) containing: multiple heading levels, **bold** and _italic_, an ordered list and an unordered list, inline `code` and a fenced ```code block``` with a language tag, and a GFM `| table |` with a header row. This is the build fixture proving AC1. (If a richer dogfood page like `the-markdown-web.md` is preferred per the architecture's seed-vault note, ensure it still covers all six feature classes.)
  - [x] Also include the GFM-extension + escaping cases for AC6: a `~~strikethrough~~`, a task list (`- [ ] todo` / `- [x] done`), a bare autolink URL (e.g. `https://themarkdownweb.com`), and at least one special character / HTML-escaping case (literal `<`, `&`, and an inline `` `code with <tags> & ampersand` ``). These prove correct `<del>`/checkbox/`<a>` rendering and that `<`/`&` are escaped in the output HTML.
  - [x] Keep media/images OUT of this file — image embedding is **Story 2.4**. Keep cross-file `[links](other.md)` OUT — inter-file linking/navigation is **Story 2.3**. (Plain inline content + bare autolink only for 2.1; an autolinked bare URL is GFM text rendering, NOT the inter-file `.md` link resolution that 2.3 owns.)
- [x] **Task 4b — Add edge-case fixtures proving slug determinism and empty-file safety** (AC: 5)
  - [x] Add a near-empty file (e.g. `content/empty.md` with only a single `# Heading` or only front matter) to prove an empty/content-light body builds to a valid document, not a crash or broken shell.
  - [x] Add at least one slug-edge fixture: a filename with a space/uppercase (e.g. `content/My Notes.md`) and/or a nested file (`content/sub/page.md`). Capture the actual slug Astro's `glob()` loader produces (do NOT hand-roll a custom slugifier) and assert it in Task 5. If a filename truly cannot produce a safe URL slug, document the behaviour rather than crashing the build.
  - [x] Confirm none of these edge fixtures collide with the route at `/` (see the `index.astro` collision note in Dev Notes).
- [x] **Task 5 — Establish a Playwright + build verification harness and assert the ACs** (AC: 1, 2, 3, 5, 6)
  - [x] Add the test tooling the repo does not yet have: `cd web && npm install -D @playwright/test`, add a `playwright.config.ts` in `web/`, and a `test`/e2e npm script. The project verification command is `cd web && npx playwright test` — wire the config so that command runs. Use Playwright's `webServer` to `npm run preview` (serves the built `dist/`) or run assertions against the static build output.
  - [x] Build first (`cd web && npm run build`), then assert: (a) `web/dist/x/index.html` exists and is non-empty; (b) it contains the expected semantic elements — `<h1>`, `<strong>`/`<em>`, `<ul>`+`<ol>`+`<li>`, `<code>`+`<pre>`, and `<table>`+`<th>`+`<td>`; (c) the article body text is present in the **raw HTML** (parse the file / `page.content()` with JS disabled), proving no client-render dependency (AC2/AC3).
  - [x] Add a JS-disabled assertion: load the page with `javaScriptEnabled: false` in a Playwright context and confirm the body content is still visible/readable (AC2).
  - [x] (Optional but recommended) Run an HTML well-formedness check (e.g. assert a single `<html lang>`, presence of `<head>`/`<title>`/`<meta charset>`, no obviously unclosed structural tags) to back AC3.
  - [x] Assert AC6 GFM-extension + escaping: the rendered HTML contains `<del>`/`<s>` (strikethrough), a task-list `<li>` with `<input type="checkbox">`, an `<a href>` for the bare autolink, AND that the special-character case is escaped (the raw HTML contains `&lt;`/`&amp;` — NOT an unescaped `<tags>` that would corrupt the document for a crawler).
  - [x] Assert AC5 edge cases: the near-empty fixture builds to a valid non-crashing `dist/.../index.html` with a complete document shell; and the slug-edge fixture(s) emit at the deterministic Astro-derived path (assert the exact slug, e.g. `dist/my-notes/index.html` and `dist/sub/page/index.html`). The whole `npm run build` must still exit 0 with these fixtures present.
- [x] **Task 6 — Ensure the deploy pipeline still builds the content-driven site (no regression)** (AC: 1, 4)
  - [x] Confirm `.github/workflows/deploy-web.yml` still works: it runs `npm ci` + `npm run build` in `web/` and uploads `web/dist`. Because the vault is at `../content` (one level above `web/`), verify the GitHub Actions checkout (full repo) makes `content/` available to the build — the relative `base: '../content'` must resolve in CI exactly as locally. If the build can't see `content/` in CI, fix the path resolution (Task 2), do NOT special-case CI.
  - [x] Verify `web/node_modules/`, `web/dist/`, and `web/.astro/` are NOT staged (all already covered by the root `.gitignore`). Commit `web/package-lock.json` updates so CI `npm ci` stays reproducible (pattern established in Story 1.1/1.3).
- [x] **Task 7 — Final verification against ACs (Definition of Done)** (AC: 1, 2, 3, 4, 5, 6)
  - [x] `cd web && npm run build` exits 0 and emits `dist/x/index.html` (route `/x`) plus a page per `content/*.md` (AC1, AC4).
  - [x] The built HTML renders all six GFM feature classes as correct semantic elements (AC1).
  - [x] The page is readable with JS disabled and content lives in the static HTML payload (AC2).
  - [x] HTML is well-formed and crawlable: valid document shell, semantic container, no client-render dependency for content (AC3).
  - [x] Pages are sourced from the repo-root `content/` vault, not a copy in `web/` — adding a new `content/<name>.md` and rebuilding yields `/<name>` with no code change (AC4).
  - [x] Edge cases hold: empty/near-empty file builds to a valid document, and slug-edge/nested filenames produce deterministic Astro-derived slugs with a 0-exit build (AC5).
  - [x] GFM extensions render correctly (strikethrough → `<del>`, task list → checkbox `<li>`, bare autolink → `<a>`) and special chars are HTML-escaped, not injected raw (AC6).
  - [x] `cd web && npx playwright test` passes; `cd web && npx astro check` passes (typecheck gate).
  - [x] Scope discipline held: NO GitHub theme/design tokens, NO Shiki palette customization, NO `site-header`/`pitch-card`, NO inter-file `.md` link resolution, NO media embedding (those are Stories 2.2/2.3/2.4/2.6).

## Dev Notes

### What exists right now (read before coding)

- `web/` is a **minimal Astro 5 project** from Story 1.1 (greenfield scaffold). Current files: `web/package.json` (only dep: `astro ^5.0.0`; scripts dev/build/preview/astro), `web/astro.config.mjs` (`defineConfig({})` — a comment explicitly says "remark/rehype (GFM), Shiki, and the GitHub-style theme arrive in Epic 2"), `web/tsconfig.json` (`extends astro/tsconfigs/base`), and `web/src/pages/index.astro` (a placeholder "coming soon" page). [Source: web/ tree, web/astro.config.mjs]
- The placeholder `web/src/pages/index.astro` is the Epic 1 "coming soon" page. **Decide explicitly:** this story replaces the site's content with the real vault render. If `index.md` exists in `content/`, the new `[...slug].astro` route will produce `/`; remove or repurpose the old `index.astro` to avoid two pages claiming `/` (an Astro route collision). If no `content/index.md` is added in this story, leaving `index.astro` is fine — just ensure no slug collision.
- `content/` exists at the **repo root** (NOT inside `web/`) with only `content/README.md` and `content/media/.gitkeep` today. It is declared the single source of truth consumed by both `web/` (build time) and `api/` (runtime). This story is the first to actually render it. [Source: content/README.md; architecture.md#Boundaries line 143]
- No test framework exists yet (greenfield). Story 1.1 notes "Unit testing first appears for the WPF `Rendering/` library in Epic 3" — but the BMAD verification command for this pipeline is `cd web && npx playwright test`, so this story establishes the web e2e harness (Task 5). [Source: 1-1-scaffold-the-monorepo.md#Testing standards summary]
- Root `.gitignore` already covers `node_modules/`, `**/node_modules/`, `dist/`, `web/dist/`, and `.astro/`. Do not commit build artifacts. [Source: .gitignore]

### Tech stack (versions confirmed)

- **Astro 5** (`^5.0.0`, installed; uses the Content Layer / `glob()` loader API and `src/content.config.ts`). [Source: web/package.json; architecture.md line 70]
- **GFM via remark:** `remark-gfm` plugin (tables, strikethrough, task lists, autolinks) registered in `astro.config.mjs`. Astro's `markdown.gfm` defaults true; explicitly registering `remark-gfm` guarantees full GFM table support which AC1 requires. [Source: epics.md "Web stack" line 52; architecture.md line 70]
- **Shiki:** Astro ships Shiki for code highlighting by default. For 2.1, the default is acceptable — the **palette/theme tuning to the DESIGN.md code colors is Story 2.2**. Do not import or configure a custom Shiki theme here. [Source: epics.md Story 2.2 line 209 — "code syntax palette via Shiki" is 2.2]
- **Node:** environment has Node v22.x (satisfies Astro 5 ≥18.20.8/20/22). CI uses Node 20. [Source: deploy-web.yml; local `node -v`]
- **Canonical spec:** GFM = CommonMark 0.31.2 + GFM extensions. The web (remark) and the native client (Markdig) are held to the same spec so the same `.md` renders the same structure. [Source: architecture.md lines 55, 69; epics.md "Canonical spec"]

### Routing approach (the load-bearing decision)

The vault is at `../content` relative to `web/`, so Astro's plain file-based routing (`src/pages/*.md`) does **not** apply — pages would have to be copied into `web/src/pages/`, which violates single-source-of-truth (AC4). Use the **Astro 5 Content Layer**:

1. `web/src/content.config.ts` defines a collection with `loader: glob({ base: '../content', pattern: '**/*.md' })` (`glob` imported from `astro/loaders`). `base` points at the repo-root vault. (If a relative `base` is unreliable in CI, resolve an absolute path via `fileURLToPath(new URL('../content', import.meta.url))` from the config file location — but always source the real `content/`, never a copy.)
2. `web/src/pages/[...slug].astro` uses `getStaticPaths()` over `getCollection()` to emit `/x` for `content/x.md`, then `const { Content } = await render(entry)` and renders `<Content />` inside the layout. Map slug so `index.md` → `/` and `x.md` → `/x`.

This is the idiomatic Astro 5 pattern and the architecture's intent ("`src/pages/` # .md → routes", "Astro renders the `content/` vault to static HTML at build time"). [Source: architecture.md lines 119–123; web/README.md "Astro renders the content/ vault to static HTML at build time"]

### Scope boundaries — what this story is NOT (prevent scope creep)

This story is the **basic render only**. The following are explicitly OTHER stories — do not pull them in:
- **Story 2.2** — GitHub-style theme (DESIGN.md tokens, 760px measure, typography/color), Shiki syntax palette, WCAG AA contrast. → 2.1 ships near-bare, semantic HTML.
- **Story 2.3** — inter-file `[link](other.md)` resolution + back/forward nav + broken-link state. → 2.1 uses plain inline content only.
- **Story 2.4** — `![](media/x.jpg)` image/video embedding. → keep media out of the 2.1 fixture.
- **Story 2.6** — `site-header` and `pitch-card` recruiting components + EXPERIENCE.md microcopy.
- **Story 2.7** — content negotiation (`Accept` → raw `.md`) in `api/`. → 2.1 is the HTML build path in `web/` only; do not touch `api/`.
[Source: epics.md Epic 2 stories 2.2–2.7 lines 199–278]

### Architecture compliance / guardrails

- **Single source of truth:** never duplicate `content/*.md` into `web/`. Source from `../content`. [architecture.md line 143]
- **Born-compatibility / SEO (FR-7, NFR-4):** the agentless HTML path is first-class and must stay crawlable — server-rendered, JS-free content. No `client:*` islands on the content path. [epics.md FR-7, NFR-4; architecture.md line 169 "born-compat/SEO ✅ (Astro static)"]
- **Beauty/perf budget (NFR-3):** web is "static, zero/low JS." Adding hydration/JS for content would regress this. [epics.md NFR-3]
- **Don't reinvent plumbing (NFR-7):** use standard Astro markdown rendering — no custom markdown parser, no hand-rolled HTML stringifier. [epics.md NFR-7; architecture.md line 169]

### Testing standards summary

- Verification command for this pipeline: `cd web && npx playwright test` (this story creates the harness). Typecheck gate: `cd web && npx astro check`. Build gate: `cd web && npm run build`. No lint command configured. [Source: BMAD verification commands]
- Test the **built static output** (and/or `npm run preview`) rather than dev server, since the AC is about the deployed HTML. Assert semantic elements + JS-disabled readability + presence of body text in raw HTML.
- Bash tool resets cwd between calls — chain commands (`cd web && npm run build && npx playwright test`) or use absolute paths.

### Previous story intelligence (Story 1.1 — scaffold)

- Story 1.1 hand-authored the minimal Astro project and hardened the build gate to "exit 0 AND non-empty `dist/index.html`." Apply the same rigor: an exit-0 build with a missing/empty `dist/x/index.html` is a FAILED gate. [Source: 1-1-scaffold-the-monorepo.md Task 3]
- 1.1 committed `web/package-lock.json` precisely so CI `npm ci` is reproducible; keep that — your new deps (`remark-gfm`, `@playwright/test`) must land in the committed lockfile. [Source: 1-1 Review Follow-ups]
- 1.1 left an explicit open question: "If you want the placeholder to be markdown-driven from day one (`src/pages/index.md`), defer that to Epic 2 Story 2.1 — Story 1.1 only requires that the build produces output." → That deferred decision lands here. [Source: 1-1-scaffold-the-monorepo.md#Open Questions item 1]

### Git / pipeline intelligence

- `deploy-web.yml` builds `web/` with `npm ci` + `npm run build` and uploads `web/dist` (prebuilt, `skip_app_build: true`). It triggers on pushes touching `web/**`. NOTE: it does NOT currently trigger on `content/**` changes — since content now drives the build, consider whether `content/**` should be added to the workflow's `paths:` (an author dropping a new `.md` should redeploy). Flag this as a question rather than silently expanding the workflow (FR-17 "publish on push" implies content pushes should deploy). [Source: deploy-web.yml lines on `paths:`]

### Elicitation notes (advanced-elicitation pass)

This story was hardened via three elicitation methods (methods.csv was absent; methods chosen by judgment for a foundational Astro markdown→HTML render):
1. **Edge-case & failure-mode hunting** → added AC5 (empty/near-empty file safety; deterministic slugs for spaces/uppercase/Unicode/nested names) and Task 4b + the Task 3 total-`<title>` fallback.
2. **Acceptance-criteria sharpening (testability)** → ACs phrased as observable, raw-HTML-assertable outcomes; Task 5 now asserts each new AC against the built output (escaped `&lt;`/`&amp;`, exact slug paths, 0-exit build).
3. **Boundary/scope validation** → AC6's bare-autolink is explicitly distinguished from 2.3 inter-file `.md` link resolution; the scope-discipline DoD line now says "inter-file `.md` link resolution" so a GFM autolink is not mistaken for scope creep.
No new scope was introduced — AC5/AC6 only make the epic's "any `.md` file with GFM content" + FR-7 "valid crawlable HTML" verifiable.

### Project Structure Notes

- New/changed files expected in `web/`: `astro.config.mjs` (UPDATE — add GFM), `src/content.config.ts` (NEW), `src/pages/[...slug].astro` (NEW), `src/layouts/Page.astro` (NEW), `playwright.config.ts` (NEW), `tests/` (NEW), `package.json` + `package-lock.json` (UPDATE — deps). In `content/`: a seed `.md` fixture (NEW). The placeholder `web/src/pages/index.astro` may be removed/repurposed (see route-collision note).
- No conflict with the established layout — this is the intended Epic 2 build-out of the `web/` Astro path described in architecture.md lines 119–123. Do not modify `api/`, `clients/`, `infra/`, or `_bmad/`.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 2.1: Render a `.md` file to an HTML page] — user story + ACs (lines 185–197)
- [Source: _bmad-output/planning-artifacts/epics.md#Requirements Inventory] — FR-1 file-as-page (line 20), FR-5 server-rendered HTML (line 24), FR-7 crawlable/born-compatible (line 26); NFR-3 perf budget (line 43), NFR-4 born-compat/SEO (line 44), NFR-7 don't-reinvent plumbing (line 47)
- [Source: _bmad-output/planning-artifacts/epics.md#Web stack] — Astro + remark/rehype (GFM) + Shiki (line 52); canonical spec GFM (line 55)
- [Source: _bmad-output/planning-artifacts/epics.md] — scope boundaries: Story 2.2 theme (199–211), 2.3 linking (213–225), 2.4 media (227–237), 2.6 header/pitch (252–264), 2.7 negotiation (266–278)
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure & Boundaries] — `web/` Astro path renders `content/` vault to static HTML at build time (lines 119–123, 143); single-source-of-truth boundary (line 143)
- [Source: _bmad-output/planning-artifacts/architecture.md#Foundation & Stack Decisions] — Browser path: Astro, remark/rehype (GFM), Shiki, GitHub-style stylesheet (line 70); GFM canonical spec (line 69)
- [Source: _bmad-output/implementation-artifacts/1-1-scaffold-the-monorepo.md] — existing minimal Astro scaffold, build-gate rigor, committed lockfile, deferred markdown-driven-index decision
- [Source: web/astro.config.mjs, web/package.json, web/src/pages/index.astro, .github/workflows/deploy-web.yml, .gitignore] — current repo state

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m]

### Debug Log References

- `cd web && npm run build` → exit 0; emits `dist/{x,empty,my-notes,sub/page,readme}/index.html` + `/` (placeholder).
- `cd web && npx playwright test` → 18/18 passing.
- `cd web && npx astro check` → 0 errors / 0 warnings / 0 hints (after adding `@astrojs/check`, `typescript`, `@types/node` devDeps).

### Completion Notes List

- Ultimate context engine analysis completed — comprehensive developer guide created.
- Implemented the Astro 5 Content Layer render path: `remark-gfm` GFM config, a `glob()` collection sourced from the repo-root `../../content` vault (absolute base resolved via `fileURLToPath(import.meta.url)` for CI robustness), a `[...slug].astro` dynamic route, and a minimal `Page.astro` HTML5 shell. No `client:*` islands; content is pure server-rendered static HTML.
- Title derivation is total: front-matter `title` → first `# H1` (from `render().headings`) → humanized slug → raw id, so `<title>` is never empty (covers the `empty.md` edge case).
- Left the Story 1.1 placeholder `web/src/pages/index.astro` in place: there is no `content/index.md`, so no `/` route collision. The "coming soon" page still serves `/`; it can be retired when a real `content/index.md` lands (Open Question #3).
- DEVIATION (test): AC6's raw-HTML escaping assertion originally required the literal named entities `&lt;`/`&amp;`. Astro's bundled `rehype-stringify` (hardcoded, no `characterReferences` option exposed, and user rehype plugins cannot override the final compiler due to plugin ordering) emits NUMERIC character references (`&#x3C;`/`&#x26;`) instead — which is equally spec-valid escaping and fully satisfies AC6 ("no raw unescaped `<`/`&`"). Updated `ac6-gfm-extensions.spec.ts` to accept either form (`/&lt;|&#x3[cC];|&#60;/` and `/&amp;|&#x26;|&#38;/`); the "no unescaped `code with <tags>`" guard is unchanged. AC6 intent is preserved; only the entity-encoding over-specification was relaxed.
- `@types/node` added as a devDependency: `content.config.ts` imports `node:url` and the pre-existing `playwright.config.ts` references `process`, both of which `astro check` flagged without Node types.

### File List

- `web/astro.config.mjs` (UPDATE — GFM via `remark-gfm`)
- `web/src/content.config.ts` (NEW — `glob()` collection over `../../content`)
- `web/src/pages/[...slug].astro` (NEW — dynamic route, title derivation)
- `web/src/layouts/Page.astro` (NEW — minimal HTML5 document shell)
- `web/tests/ac6-gfm-extensions.spec.ts` (UPDATE — accept numeric char-refs)
- `web/package.json` (UPDATE — add `remark-gfm` dep; `@astrojs/check`, `typescript`, `@types/node` devDeps)
- `web/package-lock.json` (UPDATE — reproducible lockfile for the above)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (UPDATE — 2-1 → review, epic-2 → in-progress)

Note: `content/{x.md, empty.md, My Notes.md, sub/page.md}` fixtures and `web/playwright.config.ts` + the AC1/2/3/5 specs already existed in the repo (RED phase) and were used as-is.

### Open Questions / Clarifications (for human, non-blocking)

1. **AC4 (content-driven routing)** is derived, not literal in the epic. The epic's AC says "`content/x.md` … `/x`"; the architecture says `content/` is the single source of truth consumed by `web/` at build time. AC4 makes that explicit (source from `../content`, not a copy) because otherwise "any `.md` file" routing isn't actually proven. Confirm this is the intended reading (it should be — copying content into `web/` would break the architecture boundary).
2. **`deploy-web.yml` trigger paths:** the workflow currently fires on `web/**` only. Now that `content/**` drives the build, an author adding a new `content/*.md` would not trigger a deploy. Recommend adding `content/**` to the workflow `paths:` to honor FR-17 "publish on push" — flagged rather than changed silently. Is expanding the trigger in scope for this story, or a follow-up?
3. **Placeholder index collision:** if a `content/index.md` is added, the new `[...slug].astro` route produces `/`, colliding with the Story 1.1 `web/src/pages/index.astro`. Plan: remove/repurpose `index.astro`. Confirm the Epic 1 "coming soon" page is fine to retire now (it should be — Epic 2 replaces it with the real render).
4. **Test tooling:** this story introduces `@playwright/test` to `web/` (none existed). The verification command `npx playwright test` requires it. Confirmed in scope per the pipeline verification command.
