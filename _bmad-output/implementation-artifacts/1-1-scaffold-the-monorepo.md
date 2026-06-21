# Story 1.1: Scaffold the monorepo

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want the monorepo scaffolded with a home for every component,
so that all later work has a consistent, documented place to live.

## Acceptance Criteria

1. **Given** a fresh clone of the repository (after this story is committed) **When** I inspect the tree **Then** `content/` (with `content/media/`), `web/`, `api/` (with `api/negotiate/`), `clients/windows/` (with `App/`, `Rendering/`, `Agent/`), `infra/`, and `.github/workflows/` all exist and survive the clone, each top-level home with a placeholder README describing its purpose. *(AC1 — directory skeleton + placeholder READMEs)* **Note:** git does not track empty directories — every otherwise-empty directory (e.g. `content/media/`, `api/negotiate/`, native subfolders) MUST contain a committed `.gitkeep` (or README) so it is present in a fresh clone; this is part of AC1, not optional.
2. **Given** the scaffolded `web/` directory with deps installed **When** I run `npm run build` (`astro build`) from `web/` **Then** the command exits 0 AND produces `web/dist/index.html` that is non-empty (contains the placeholder page markup). An exit-0 with no `dist/index.html`, or an empty `index.html`, does NOT satisfy this AC. *(AC2 — Astro project builds and emits a real page)*
3. **Given** a fresh clone **When** I read the root `README.md` **Then** it documents the monorepo layout and the component boundaries from the architecture (single-source-of-truth `content/`, isolated `Rendering/`, negotiate-only `api/`) and explicitly records the no-Chromium (NFR-1) native-client constraint. *(AC3 — root README documents layout + boundaries)*

> Source: [_bmad-output/planning-artifacts/epics.md#Story 1.1: Scaffold the monorepo] (lines 124–136). This is the foundational greenfield scaffold — no external starter template.

## Tasks / Subtasks

- [x] **Task 0 — Add the root `.gitignore` FIRST** (AC: 1, 2)
  - [x] Before installing any deps or running any build, create/ensure a root `.gitignore` that excludes `node_modules/`, `**/node_modules/`, `web/dist/`, and `dist/`. (Ordering matters: this must exist before Task 3's `npm install` so build artifacts/deps can never be staged. — pre-mortem: committed `node_modules/` is the #1 way this story regresses.)
- [x] **Task 1 — Create the directory skeleton with placeholder READMEs + `.gitkeep` for empty dirs** (AC: 1)
  - [x] Create `content/` with `content/media/`, a placeholder `content/README.md`, and `content/media/.gitkeep` (purpose: the Vault — single source of truth `.md` + media consumed by both `web/` at build time and `api/` at runtime; FR-1–4).
  - [x] Create `api/negotiate/` with `api/negotiate/.gitkeep` and a placeholder `api/README.md` (purpose: Azure Function content negotiation — `Accept` → HTML | raw `.md`, `Vary: Accept`; FR-14. Negotiation only, no content).
  - [x] Create `clients/windows/` with subfolders `App/`, `Rendering/`, `Agent/` (each containing a `.gitkeep`) and a placeholder `clients/windows/README.md` (purpose: .NET 10 WPF native client; `Rendering/` is the pure bedrock — no networking, no AI; `App`/`Agent` depend on it, never the reverse; FR-9–13, NFR-1 no-Chromium).
  - [x] Create `infra/` and a placeholder `infra/README.md` (the README makes the dir committable — no separate `.gitkeep` needed) (purpose: Infrastructure as Code — Bicep templates for Azure Static Web App, custom domain themarkdownweb.com, TLS; established in this walking-skeleton epic).
  - [x] Create `.github/workflows/` and a placeholder `.github/workflows/README.md` (purpose: GitHub Actions — `deploy-web.yml` (Astro → Azure SWA) and `build-windows.yml` (build/test WPF); FR-17).
  - [x] Do NOT pre-create `web/` or `web/README.md` here — `web/` is created in Task 2. (Ordering hazard: `npm create astro` can refuse or overwrite a pre-existing non-empty `web/`. If you hand-author instead, then create `web/` in Task 2, not here.)
  - [x] After this task, every directory listed in AC1 contains at least one committed file (README or `.gitkeep`) so it survives a fresh clone.
- [x] **Task 2 — Scaffold a minimal Astro project in `web/`** (AC: 2)
  - [x] Initialize a minimal Astro 5 project rooted at `web/`. **Preferred (reliable, non-interactive):** hand-author the four minimal files below, OR run the non-interactive form `npm create astro@latest web -- --template minimal --no-install --no-git --yes`. **Avoid the bare interactive `npm create astro@latest`** — it prompts and can hang in an automated/headless run. Keep it minimal: no Tailwind/integrations.
  - [x] Ensure `web/package.json` defines scripts `dev` (`astro dev`), `build` (`astro build`), and `preview` (`astro preview`), with `astro` (v5.x) as a dependency.
  - [x] Ensure `web/astro.config.mjs` exists (default `defineConfig({})` is fine for now).
  - [x] Ensure at least one page exists at `web/src/pages/index.astro` rendering a trivial but non-empty placeholder (e.g. an `<h1>The Markdown Web — coming soon</h1>` inside a valid HTML document) so the built `index.html` is non-empty (AC2). This is the placeholder that Epic 1 Stories 1.3/1.4 will deploy.
  - [x] Add a `web/README.md` placeholder (purpose: Browser/HTML path — Astro + remark/rehype (GFM) + Shiki; GitHub-style stylesheet; FR-5–8).
  - [x] (Deps and build are gitignored via Task 0's root `.gitignore` — no separate `web/.gitignore` is required; add one only if you prefer local scoping.)
- [x] **Task 3 — Verify the Astro build produces real output (hardened gate)** (AC: 2)
  - [x] Confirm Node/npm satisfy Astro 5 (`node -v` >= 18.20.8 / 20.x / 22+; npm present) before installing — fail fast with a clear message if not. (Environment already verified: Node v24.14.1 / npm 11.11.0.)
  - [x] From `web/`, run `npm install` then `npm run build` (i.e. `astro build`). Confirm: (a) the command exits 0, (b) `web/dist/index.html` exists, and (c) `web/dist/index.html` is non-empty and contains the placeholder text. An exit-0 with a missing or empty `index.html` is a FAILED gate — do not mark AC2 done.
  - [x] Verify `web/node_modules/` and `web/dist/` are NOT staged for commit (they must be matched by the root `.gitignore` from Task 0). If `git status` shows either, fix `.gitignore` before proceeding.
- [x] **Task 4 — Author the root `README.md` documenting layout and boundaries** (AC: 3)
  - [x] Replace the existing minimal root `README.md` with documentation of the monorepo layout (the tree from architecture.md "Project Structure & Boundaries").
  - [x] Document component boundaries explicitly: `content/` is the single source of truth (no content in code); `Rendering/` is isolated/pure (no net, no AI; `App`/`Agent` depend on it, never reverse); `api/` only negotiates (browsers → static HTML, clients → raw `.md`).
  - [x] Include the FR → component map and note Windows-first scope and the no-Chromium (NFR-1) constraint for the native client.
- [x] **Task 5 — Final verification against ACs (Definition of Done)** (AC: 1, 2, 3)
  - [x] Confirm all six required directory homes exist with placeholder READMEs, AND every otherwise-empty dir has a committed `.gitkeep` — simulate a fresh clone (`git ls-files` should list a file under each AC1 directory) so no dir vanishes on clone (AC1).
  - [x] Confirm `astro build` exited 0 and produced a **non-empty** `web/dist/index.html` containing the placeholder text (AC2 hardened gate).
  - [x] Confirm `git status` is clean of `node_modules/` and `dist/` (nothing build-generated is staged).
  - [x] Confirm root README documents layout + boundaries AND the no-Chromium (NFR-1) constraint (AC3).
  - [x] Confirm scope was NOT expanded: no `.sln`/.NET projects, no Bicep files, no workflow YAML, no Astro integrations (remark/rehype/Shiki/Tailwind) were added — those belong to later stories. This story is dirs + minimal Astro + root README only.

## Dev Notes

### Target monorepo layout (from architecture)

This is the exact tree to scaffold (per architecture.md "Project Structure & Boundaries", lines 113–140):

```
themarkdownweb/  (repo root)
├── content/                    # the Vault (FR-1–4): seed .md + media; single source of truth
│   ├── media/
│   └── README.md
├── web/                        # Browser/HTML path (FR-5–8): Astro + remark/rehype (GFM) + Shiki
│   ├── astro.config.mjs
│   ├── package.json
│   ├── src/pages/index.astro   # minimal placeholder page
│   └── README.md
├── api/                        # Content negotiation (FR-14): Azure Function — Accept → HTML | raw .md
│   ├── negotiate/
│   └── README.md
├── clients/
│   └── windows/                # Native client (FR-9–13): .NET 10 + WPF
│       ├── App/                # shell, window, navigation, fetch raw .md
│       ├── Rendering/          # BEDROCK: Markdig AST → FlowDocument (pure, no net, no AI)
│       ├── Agent/              # AI-personality transform (later; isolated)
│       └── README.md
├── infra/                      # IaC: Bicep (Azure SWA, custom domain, TLS) (FR-18)
│   └── README.md
├── .github/workflows/          # deploy-web.yml + build-windows.yml (FR-17)
│   └── README.md
├── README.md                   # documents the layout + boundaries (this story)
└── _bmad-output/               # existing planning artifacts (already present — do not touch)
```

Notes vs the architecture diagram: this story creates **directory homes with placeholder READMEs only** — it does NOT create the .NET solution/projects, Bicep files, or workflow YAML (those are later stories: 1.2 Bicep, 1.3 `deploy-web.yml`, 1.4 custom domain; native client is Epic 3). Create the folders so those stories have a place to land. `infra/` is a directory here even though the architecture diagram shows `infra/staticwebapp.config.json` — the file is added in a later story; keep the folder + README now.

### Component boundaries (must be reflected in root README — AC3)

- **`content/` = single source of truth** — both `web/` (build time) and the Windows client (runtime via `api/`) consume the same `.md`. No content lives in code. [Source: architecture.md#Boundaries, line 143]
- **`Rendering/` is isolated** — pure, independently testable bedrock with no networking and no AI. `App/` and `Agent/` depend on it, never the reverse. [Source: architecture.md#Boundaries, line 144]
- **`api/` only negotiates** — browsers → static HTML (Astro/SWA); clients → raw `.md`. [Source: architecture.md#Boundaries, line 145]
- **No-Chromium (NFR-1, hard)** — the native client renders native WPF UI, never an embedded browser/webview. Document this constraint in the `clients/windows/README.md` and root README. [Source: epics.md NFR-1, architecture.md FC-1]

### FR → component map (include in root README — AC3)

| FRs | Lives in |
|---|---|
| 1–4 Vault | `content/` (consumed by `web/` & `api/`) |
| 5–8 HTML client | `web/` (Astro) |
| 9–13 Native client | `clients/windows/` (App + Rendering + Agent) |
| 14 Content negotiation | `api/` |
| 17–18 Publish/host | `.github/workflows/` + `infra/` + Azure SWA |

[Source: architecture.md#FR → component map, lines 147–154]

### Tech stack (versions confirmed by architecture)

- **Web:** Astro 5 + remark/rehype (GFM) + Shiki; GitHub-style stylesheet. This story only needs the minimal Astro project to build; remark/rehype/Shiki/GitHub theme arrive in Epic 2. [Source: architecture.md line 159; epics.md "Web stack"]
- **API:** Azure Functions (content negotiation). Folder home only this story. [Source: epics.md "API stack"]
- **Native client:** .NET 10 + C# + WPF (FlowDocument) + Markdig 1.3.1 + ColorCode. Folder homes only this story. [Source: epics.md "Native client stack"; architecture.md line 159]
- **IaC:** Bicep (Azure-native default). Folder home only this story; templates land in Story 1.2.
- **CI/CD:** GitHub Actions — `deploy-web.yml`, `build-windows.yml`. Folder home only this story; YAML lands in Stories 1.3/1.4.

### Environment / platform notes

- This project runs on **Windows 11**. Node **v24.14.1** and npm **11.11.0** are installed and confirmed available — fully satisfies Astro 5's Node requirement (>=18.20.8 / 20.x / 22+). [Verified at story-creation time via `node -v` / `npm -v`.]
- The Bash tool resets cwd between calls — when running build commands, `cd` into `web/` within the same command (e.g. `cd web && npm install && npm run build`) or use absolute paths.
- Astro init command (confirmed current): `npm create astro@latest` (interactive). For an automated/minimal scaffold prefer the non-interactive form, e.g. `npm create astro@latest web -- --template minimal --no-install --no-git --yes`, then `cd web && npm install`. Hand-authoring the four minimal files (`package.json`, `astro.config.mjs`, `src/pages/index.astro`, `README.md`) is equally acceptable and avoids the network/interactive prompt — choose whichever is more reliable in this environment. Keep it minimal: no Tailwind/integrations.

### Testing standards summary

No automated test framework is established yet (greenfield). For this scaffold story, "tests" = the build-verification gate in Task 3: `astro build` must exit 0 and emit `web/dist/index.html`. No unit-test framework needs to be added in this story. Unit testing first appears for the WPF `Rendering/` library in Epic 3.

### Project Structure Notes

- Repo root currently contains only `.claude/`, `_bmad/`, `_bmad-output/`, `.git/`, and a 14-byte placeholder `README.md`. The root README will be replaced (AC3). Do not modify `_bmad/` or `_bmad-output/`.
- Add/ensure a root `.gitignore` covers `node_modules/`/`web/node_modules/` and `web/dist/` so build artifacts and deps are not committed (AC2 acceptance is "build produces output", not "output is committed"). **Ordering: create `.gitignore` FIRST (Task 0), before `npm install` runs in Task 3** — otherwise a careless `git add -A` stages `node_modules/`.
- Git does not track empty directories. Each AC1 directory must carry a committed file (placeholder README at the top level; `.gitkeep` inside otherwise-empty subdirs like `content/media/`, `api/negotiate/`, `clients/windows/{App,Rendering,Agent}/`) or it will not appear in a fresh clone — which would silently break AC1.
- Ordering hazard: do not pre-create `web/` before Task 2. `npm create astro@latest` may refuse or overwrite a non-empty target directory; let Task 2 own `web/`.
- No conflicts with existing structure detected. The scaffold is purely additive plus the root README replacement.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.1: Scaffold the monorepo] — user story + acceptance criteria (lines 124–136)
- [Source: _bmad-output/planning-artifacts/epics.md#Additional Requirements] — monorepo scaffold component list, stacks (lines 51–60)
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure & Boundaries] — monorepo tree, boundaries, FR→component map (lines 109–154)
- [Source: _bmad-output/planning-artifacts/architecture.md#Foundational Constraints] — no-webview/no-Chromium constraint (FC-1, lines 23–25)

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m] (coordinator-driven implementation, enhanced-automated-sprint pipeline)

### Debug Log References

- `cd web && npm install && npm run build` → Astro 5 static build, 1 page, exit 0; emitted `web/dist/index.html` (265 bytes, contains "The Markdown Web").
- `git status --short` clean of `node_modules/` and `dist/` (covered by root `.gitignore`).
- `git ls-files`-equivalent check: each AC1 directory carries a committed README or `.gitkeep`.

### Completion Notes List

- Ultimate context engine analysis completed — comprehensive developer guide created.
- Scaffolded directory homes for all six components with placeholder READMEs; `.gitkeep` markers in every otherwise-empty subdir (`content/media/`, `api/negotiate/`, `clients/windows/{App,Rendering,Agent}/`) so they survive a fresh clone (AC1).
- Hand-authored a minimal Astro 5 project in `web/` (non-interactive path — avoided `npm create astro` prompt). Build gate hardened: exit 0 AND non-empty `dist/index.html` with placeholder markup (AC2).
- Replaced the 14-byte root README with full monorepo layout, component boundaries, FR→component map, Windows-first scope, and the no-Chromium NFR-1 constraint (AC3).
- Root `.gitignore` created FIRST (before `npm install`) so deps/build artifacts can never be staged. Confirmed clean.
- Scope discipline held: no .NET solution, no Bicep, no workflow YAML, no Astro integrations — all deferred to later stories.

### File List

Created:
- `.gitignore`
- `README.md` (replaced placeholder)
- `content/README.md`, `content/media/.gitkeep`
- `web/package.json`, `web/astro.config.mjs`, `web/tsconfig.json`, `web/src/pages/index.astro`, `web/README.md`
- `web/package-lock.json` (committed — CI runs `npm ci` against it for reproducible deploys in Story 1.3)
- `api/README.md`, `api/negotiate/.gitkeep`
- `clients/windows/README.md`, `clients/windows/App/.gitkeep`, `clients/windows/Rendering/.gitkeep`, `clients/windows/Agent/.gitkeep`
- `infra/README.md`
- `.github/workflows/README.md`

Build artifacts (gitignored, not committed): `web/node_modules/`, `web/dist/`.

### Review Follow-ups (AI)

- [x] (med) Commit `web/package-lock.json` so Story 1.3's GitHub Actions can `npm ci` for reproducible builds. — done (lockfile committed; not gitignored).
- [x] (low) Add Astro-recommended `web/tsconfig.json` (`extends astro/tsconfigs/base`) ahead of Epic 2 TypeScript work. — done.
- [x] (low) De-ambiguate File List re: lockfile commit decision. — done.
- Not actioned (low, optional): tighter astro version pin — lockfile makes builds deterministic, caret range retained per npm convention.

### Open Questions / Clarifications (for human, non-blocking)

1. Astro 5 minimal templates ship with TypeScript config + `tsconfig.json` and an `astro.config.mjs`. The story treats a single `index.astro` placeholder page as sufficient. If you want the placeholder to instead be markdown-driven from day one (`src/pages/index.md`), defer that to Epic 2 Story 2.1 — Story 1.1 only requires that the build produces output.
2. RESOLVED in ACs/Tasks: empty-directory tracking is now a hard AC1 requirement — top-level dirs are made committable by their placeholder README, and every otherwise-empty subdir (`content/media/`, `api/negotiate/`, native `App`/`Rendering`/`Agent`) carries a committed `.gitkeep`. No human decision needed.
3. The native-client subfolders (`App/`, `Rendering/`, `Agent/`) are created as homes only (READMEs/.gitkeep). The actual .NET 10 solution/projects are deliberately out of scope for this story (Epic 3) — confirm that's the intended boundary.
