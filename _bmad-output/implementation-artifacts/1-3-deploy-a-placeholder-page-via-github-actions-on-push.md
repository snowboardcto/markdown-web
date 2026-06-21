# Story 1.3: Deploy a placeholder page via GitHub Actions on push (FR-17)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want pushes to `main` that touch the web app to deploy automatically,
so that "publish on push" (FR-17) is proven end-to-end before any real content exists.

## Acceptance Criteria

1. **Given** the repository **When** I inspect `.github/workflows/deploy-web.yml` **Then** the workflow exists and is triggered by `on: push` to branch `main` **path-filtered** to `web/**` and `.github/workflows/deploy-web.yml`, **plus** a `workflow_dispatch` for manual runs — and it is NOT triggered by `pull_request` (no PR/staging-environment flow in this story). *(AC1 — workflow exists with the correct, scoped triggers)*

2. **Given** the workflow **When** the job runs **Then** it performs a **deterministic build then deploy**: `actions/checkout`, `actions/setup-node` (Node 20), `npm ci` and `npm run build` in `web/`, **then** `Azure/static-web-apps-deploy@v1` configured with `skip_app_build: true`, `app_location: "web/dist"`, and `azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}` — Oryx auto-build is NOT relied upon (we upload the pre-built `web/dist`). *(AC2 — deterministic build, then token-authenticated deploy of `web/dist`)*

3. **Given** a commit that makes `npm run build` fail **When** the workflow runs **Then** the job **fails at the build step and the deploy step never executes**, so **no upload occurs and the live site is unchanged** — a broken build can never reach production. *(AC3 — failed build does NOT deploy; build precedes deploy as a hard gate)*

4. **Given** the provisioned SWA `swa-markdown-web` in `rg-markdown-web` **When** the deployment token is retrieved with `az staticwebapp secrets list -n swa-markdown-web -g rg-markdown-web --query "properties.apiKey" -o tsv` **Then** it is stored as the GitHub repository secret `AZURE_STATIC_WEB_APPS_API_TOKEN` via `gh secret set` — and the token is **never echoed to logs, printed, or committed** to the repo. *(AC4 — deployment token wired in as a secret, treated as sensitive)*

5. **Given** the workflow and secret are in place **When** I push a commit to `main` that changes the placeholder page under `web/` **Then** the workflow runs to **success** with no manual steps, **And** `curl -sS -o /dev/null -w "%{http_code}" https://purple-pond-09fadd20f.7.azurestaticapps.net/` returns **HTTP 200**, **And** the response body **contains the placeholder text "The Markdown Web"** — proving OUR built page deployed, not the SWA default holding page. *(AC5 — push deploys; live site serves OUR page over HTTPS at the SWA default hostname)*

> Source: [_bmad-output/planning-artifacts/epics.md#Story 1.3: Deploy a placeholder page via GitHub Actions on push (FR-17)] (lines 152–164). Pipeline decision: **live deploy** — SWA `swa-markdown-web` is LIVE in `rg-markdown-web` at `purple-pond-09fadd20f.7.azurestaticapps.net` (manual-deploy mode, no repo linked). The deployment token is NOT yet a GitHub secret — this story adds it as `AZURE_STATIC_WEB_APPS_API_TOKEN`.

## Tasks / Subtasks

- [x] **Task 1 — Retrieve the SWA deployment token and store it as a GitHub secret** (AC: 4)
  - [x] Retrieve the token (sensitive — do NOT print it): `az staticwebapp secrets list -n swa-markdown-web -g rg-markdown-web --query "properties.apiKey" -o tsv`. (Ensure the `az` context targets subscription `1522535a-d614-4009-93a3-09294fbdd6e0` / RG `rg-markdown-web`.)
  - [x] Pipe it directly into `gh secret set AZURE_STATIC_WEB_APPS_API_TOKEN` (e.g. `az staticwebapp secrets list ... -o tsv | gh secret set AZURE_STATIC_WEB_APPS_API_TOKEN -R snowboardcto/markdown-web`). **Never** assign the token to a shell variable that gets echoed, never paste it into a file, never commit it. (inversion: the one way to fail this story badly is to leak the token — pipe, don't print.)
  - [x] Verify the secret exists without revealing its value: `gh secret list -R snowboardcto/markdown-web` shows `AZURE_STATIC_WEB_APPS_API_TOKEN`. (GitHub secret values are write-only; a name-only confirmation is the correct verification.)
- [x] **Task 2 — Author `.github/workflows/deploy-web.yml` with correctly-scoped triggers** (AC: 1)
  - [x] Create `.github/workflows/deploy-web.yml`. Set `on.push.branches: [main]` with `on.push.paths: ["web/**", ".github/workflows/deploy-web.yml"]`, AND `on.workflow_dispatch: {}`. Do NOT add `pull_request` triggers. (edge: include the workflow file itself in the path filter so changes to the pipeline re-deploy and are validated.)
  - [x] Name the workflow and the job clearly (e.g. `name: Deploy Web (Astro → Azure SWA)`, job `build_and_deploy`). Run on `ubuntu-latest`. Set minimal `permissions` (e.g. `contents: read`) — the SWA deploy authenticates via the token secret, NOT via OIDC, so no `id-token: write` is needed here. (Distinguish from the Bicep workflow which uses OIDC.)
- [x] **Task 3 — Deterministic build step (Node 20, `npm ci` + `npm run build` in `web/`)** (AC: 2, 3)
  - [x] Add steps: `actions/checkout@v4`; `actions/setup-node@v4` with `node-version: 20` and `cache: npm` + `cache-dependency-path: web/package-lock.json`; then `npm ci` and `npm run build` with `working-directory: web` (or `working-directory: ./web` on each run step). (`web/package-lock.json` is committed, so `npm ci` is reproducible — do NOT use `npm install`.)
  - [x] Confirm the build output lands at `web/dist` (Astro 5 default `outDir` is `./dist` relative to the project root; `web/astro.config.mjs` does not override it). This is the directory the deploy step uploads.
  - [x] **Ordering is the AC3 gate:** the `npm run build` step MUST come BEFORE the deploy step in the same job. If `npm run build` exits non-zero the job aborts and the deploy step never runs → no upload → live site unchanged. Do NOT add `continue-on-error` to the build step. (pre-mortem: a build that "soft-fails" and still deploys a stale/empty `dist` would silently violate FR-17's safety promise.)
- [x] **Task 4 — Deploy step: `Azure/static-web-apps-deploy@v1` with `skip_app_build: true`** (AC: 2, 5)
  - [x] Add the deploy step using `Azure/static-web-apps-deploy@v1` with:
    - `azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}`
    - `action: "upload"`
    - `skip_app_build: true` (we already built — do NOT let Oryx rebuild)
    - `app_location: "web/dist"` (upload the pre-built output directory)
    - (no `api_location`; no `output_location` needed since `skip_app_build` uploads `app_location` as-is)
  - [x] Because `skip_app_build: true` uploads `app_location` directly, point `app_location` at the built `web/dist`. (edge: if `app_location` pointed at `web/` with `skip_app_build`, the SWA default page — not our build — could be served; verify AC5's body check catches that.)
  - [x] Do NOT add a `staticwebapp.config.json` requirement here — none is needed for a single static `index.html`; leave SWA routing config to a later story if/when needed (the architecture lists `infra/staticwebapp.config.json` as a future concern).
- [x] **Task 5 — Verify deploy-on-push end-to-end** (AC: 3, 5)
  - [x] Commit `.github/workflows/deploy-web.yml`, then push a commit that touches `web/` (e.g. a trivial copy tweak in the placeholder page) to `main`. Confirm the path filter triggers a run: `gh run list --workflow=deploy-web.yml -R snowboardcto/markdown-web`.
  - [x] Watch the run to completion: `gh run watch -R snowboardcto/markdown-web` (or `gh run view <id> --log`). Confirm the conclusion is **success** and that build ran before deploy.
  - [x] Assert the live site serves OUR page: `curl -sS -o /dev/null -w "%{http_code}\n" https://purple-pond-09fadd20f.7.azurestaticapps.net/` → **200**, AND `curl -sS https://purple-pond-09fadd20f.7.azurestaticapps.net/ | grep -q "The Markdown Web"` → match (AC5). A 200 alone is NOT sufficient — the SWA default holding page also returns 200; the body text proves our deployment.
  - [x] **AC3 evidence (failed build does not deploy):** without permanently breaking `main`, demonstrate the gate — either (a) reason from the workflow structure (build precedes deploy, no `continue-on-error`) and capture a run where a transient build failure aborted before deploy, or (b) run a throwaway branch/`workflow_dispatch` with a deliberately broken build and confirm the deploy step shows as skipped/not-run while the live site is unchanged. Document the evidence in the Dev Agent Record. (Prefer not to leave `main` red.)
- [x] **Task 6 — Final verification against ACs (Definition of Done)** (AC: 1, 2, 3, 4, 5)
  - [x] AC1: `deploy-web.yml` exists; triggers = push-to-`main` path-filtered (`web/**` + the workflow file) + `workflow_dispatch`; NO `pull_request`.
  - [x] AC2: build-then-deploy; Node 20; `npm ci` + `npm run build` in `web/`; deploy uses `skip_app_build: true`, `app_location: "web/dist"`, token secret. Oryx auto-build not relied on.
  - [x] AC3: build step precedes deploy, no `continue-on-error`; a build failure aborts before any upload (evidenced).
  - [x] AC4: `AZURE_STATIC_WEB_APPS_API_TOKEN` present in `gh secret list`; token never echoed/committed.
  - [x] AC5: push deployed without manual steps; `curl` → 200 AND body contains "The Markdown Web".
  - [x] **Scope discipline:** this story adds ONLY `.github/workflows/deploy-web.yml` and the GitHub secret. Do NOT add the custom domain `themarkdownweb.com` / DNS / HTTPS-redirect (→ Story 1.4), real markdown content rendering (→ Epic 2), `build-windows.yml`, a `pull_request`/`close`-event staging flow, or `staticwebapp.config.json`. Commit only the workflow file (and, if you tweaked the placeholder for the verification push, that web change).

## Dev Notes

### What this story delivers (and explicitly does not)

This story proves **publish-on-push (FR-17)** end-to-end with the trivial placeholder page: a push to `main` that touches `web/` automatically builds the Astro app and deploys the built output to the already-live Azure SWA, with the live site serving OUR page over HTTPS. It is the **CI/CD half** of the walking skeleton (the IaC half — provisioning the SWA — was Story 1.2). The **custom domain + HTTPS redirect** for `themarkdownweb.com` is **Story 1.4**; **real content rendering** (remark/rehype/Shiki/theme) is **Epic 2**. Keep those out. [Source: epics.md#Epic 1, Stories 1.3/1.4, lines 152–177; architecture.md#FR → component map, line 154]

### Live environment (already provisioned — do not re-create)

- **SWA:** `swa-markdown-web` in resource group `rg-markdown-web`, default hostname `purple-pond-09fadd20f.7.azurestaticapps.net`, **manual-deploy mode** (no repo linked) — exactly the mode that the token-based `static-web-apps-deploy` action expects. [Source: Pipeline Context; 1-2-...#Debug Log References — hostname `purple-pond-09fadd20f.7.azurestaticapps.net`]
- **The deployment token is NOT yet a GitHub secret** — Story 1.3 adds it as `AZURE_STATIC_WEB_APPS_API_TOKEN` (Task 1). This token is distinct from the OIDC secrets used by the Bicep flow.
- **Already-set GitHub config (do not duplicate):** secrets `AZURE_CLIENT_ID`/`AZURE_TENANT_ID`/`AZURE_SUBSCRIPTION_ID` (OIDC, for Bicep); repo vars `AZURE_RG=rg-markdown-web`, `AZURE_LOCATION=eastus2`. These are NOT used by this workflow — the SWA deploy authenticates via the SWA token, not OIDC.
- Repo: `snowboardcto/markdown-web`, default branch `main`. `.github/workflows/` currently contains only `README.md` (which already names `deploy-web.yml` as landing in this story). [Verified: no workflow YAML present yet.]

### Why deterministic build-then-deploy (not Oryx auto-build)

The standard SWA flow lets Azure's **Oryx** builder run the build inside the deploy action. We deliberately **build ourselves** (`npm ci` + `npm run build` on `actions/setup-node@v4` Node 20) and pass `skip_app_build: true` + `app_location: "web/dist"` so the action only **uploads** the pre-built output:
- **Determinism / reproducibility:** the committed `web/package-lock.json` + `npm ci` pin the exact dependency tree; we control the Node version (20). Oryx's environment is a moving target.
- **AC3 safety gate:** a separate, ordered build step means a build failure **fails the job before the deploy step runs** — Oryx-internal build failures are murkier and couple build+deploy into one step.
- This honors NFR-7 (standard plumbing, no novel infra): `actions/setup-node` + `Azure/static-web-apps-deploy@v1` are the canonical building blocks. [Source: epics.md#Additional Requirements — `deploy-web.yml` (Astro → Azure SWA), line 58; NFR-7, line 47; Pipeline Context — KEY TECHNICAL CONSTRAINTS]

### The "200 is not enough" trap (AC5)

The SWA serves a **default holding page** that ALSO returns HTTP 200 (confirmed in Story 1.2: `curl` to the hostname returned 200 against the default page). So a 200 status check alone would pass even if our deploy never landed. AC5 therefore requires the response **body to contain "The Markdown Web"** — the placeholder text that `web/dist/index.html` renders — as positive proof that OUR build is live. The current placeholder body is "The Markdown Web — coming soon" (`npm run build` → `web/dist/index.html`). [Source: Pipeline Context — placeholder body; 1-2-...#Debug Log References — default page returns 200]

### Failed-build-must-not-deploy — how to guarantee it (AC3)

GitHub Actions runs steps sequentially and **aborts the job on the first failing step** unless `continue-on-error: true` is set. So the guarantee is structural:
1. Order: `checkout` → `setup-node` → `npm ci` → `npm run build` → `static-web-apps-deploy`.
2. The `npm run build` step has **no** `continue-on-error`. If it exits non-zero, the job stops; the deploy step is never reached; **nothing is uploaded**; the live SWA content is untouched.
3. Do NOT split build and deploy into separate jobs unless you wire an explicit `needs:` dependency — keeping them in one ordered job is the simplest correct gate. [Source: Advanced elicitation — Pre-mortem + Inversion, see Advanced Elicitation Record]

### Secret handling — never leak the token (AC4)

The SWA deployment token is a production credential (it authorizes uploads to the live site). Handle it like one:
- Retrieve with `az ... --query "properties.apiKey" -o tsv` and **pipe directly** into `gh secret set` — never echo it, never write it to a file, never paste it into the workflow YAML (use `${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}`).
- GitHub masks secret values in logs automatically, but masking is a backstop, not a license to print. Verify only the **name** via `gh secret list`. (inversion: committing the token or echoing it in a run log is the single highest-impact failure of this story.)

### Out of scope — the PR `close` event / staging environments

When a SWA workflow IS triggered by `pull_request`, the canonical template adds a second job handling the `closed` event to tear down the per-PR **staging environment**. Because this story's trigger is **push-to-`main` only (no `pull_request`)**, no staging environments are ever created, so the `close`-event cleanup job is **NOT required here** and is explicitly out of scope. If PR previews are added later, that cleanup job must be added then. [Source: Pipeline Context — KEY TECHNICAL CONSTRAINTS, close-event note]

### Verification commands (the "tests" for this CI story)

No unit-test framework applies (greenfield; first unit tests appear for the WPF `Rendering/` lib in Epic 3). The gates ARE the tests, in order:
1. `npm ci && npm run build` in `web/` locally → `web/dist/index.html` exists and contains "The Markdown Web" (proves the build step is sound before relying on it in CI).
2. `gh secret list -R snowboardcto/markdown-web` → `AZURE_STATIC_WEB_APPS_API_TOKEN` present (AC4).
3. Push a `web/`-touching commit → `gh run watch` → conclusion **success** (AC1/AC2/AC5).
4. `curl -o /dev/null -w "%{http_code}"` → 200 AND `curl | grep "The Markdown Web"` → match (AC5).
5. Broken-build evidence: deploy step does not run; live site unchanged (AC3).

Windows note: the dev host is Windows 11; the Bash tool resets cwd between calls. Invoke `az`/`gh`/`curl` with the repo at a known path, and prefer single compound commands. `gh` and `az` must be authenticated (`gh auth status`, `az account show`) before Tasks 1/5.

### Previous story intelligence (Story 1.2)

- Story 1.2 **provisioned the SWA in manual-deploy mode on purpose** (`properties: {}`, no `repositoryUrl`/`branch`/`repositoryToken`) precisely so this story could deploy via the **deployment token** + Actions — they are the matched halves. Do NOT try to link the repo to the SWA in the portal/Bicep; use the token. [Source: 1-2-...#Why manual-deploy mode]
- 1.2 confirmed the live hostname `purple-pond-09fadd20f.7.azurestaticapps.net` and that the SWA default page returns 200 — directly motivating AC5's body-text assertion. [Source: 1-2-...#Debug Log References]
- 1.2 reinforced two disciplines to carry forward: **scope containment** (don't pull in 1.4's custom domain or Epic 2 content) and **never commit secrets/artifacts** (the token must only ever live as a GitHub secret). [Source: 1-2-...#Previous story intelligence]
- 1.2's `.gitignore` narrowing kept `staticwebapp.config.json` trackable for a future story — do NOT add it here.

### Project Structure Notes

- New file: `.github/workflows/deploy-web.yml` — lands in the existing `.github/workflows/` home (currently README-only). Matches the architecture tree (`deploy-web.yml # build Astro → deploy Azure SWA (FR-17)`, architecture.md line 137). No new top-level directories.
- Build input: `web/` (Astro 5, `npm run build` → `web/dist`). The workflow does not modify `web/` source except, optionally, the placeholder tweak used to trigger the verification push.
- No conflict with existing structure; purely additive (one workflow file + one GitHub secret that lives outside the repo).

### Testing standards summary

No automated test framework applies to this CI story (greenfield; first unit tests are the WPF `Rendering/` lib in Epic 3 — [Source: 1-1-scaffold-the-monorepo.md#Testing standards summary]). "Passing tests" = the five verification gates above all green, with the **body-text assertion** (not just HTTP 200) as the proof-of-deploy regression guard and the **build-before-deploy ordering** as the no-bad-deploy guard.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.3: Deploy a placeholder page via GitHub Actions on push (FR-17)] — user story + ACs (lines 152–164)
- [Source: _bmad-output/planning-artifacts/epics.md#Additional Requirements] — CI/CD: `deploy-web.yml` (Astro → Azure SWA); NFR-7 don't-reinvent-plumbing (lines 58, 47)
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 1] — walking-skeleton FR-17 end-to-end; Stories 1.2 (provision) / 1.4 (custom domain) boundaries (lines 98–177)
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure & Boundaries] — `.github/workflows/deploy-web.yml` home (line 137); FR → component map FR-17–18 (line 154)
- [Source: _bmad-output/implementation-artifacts/1-2-provision-azure-hosting-via-iac-bicep.md] — live SWA `swa-markdown-web` @ `purple-pond-09fadd20f.7.azurestaticapps.net`, manual-deploy mode, default page returns 200; scope/secret discipline
- [Source: Pipeline Context] — live-deploy decision; SWA hostname; token → `AZURE_STATIC_WEB_APPS_API_TOKEN`; KEY TECHNICAL CONSTRAINTS (build-then-deploy, `skip_app_build`, close-event out of scope)

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m] (coordinator-driven story creation, enhanced-automated-sprint pipeline)

### Debug Log References

- `gh secret set AZURE_STATIC_WEB_APPS_API_TOKEN` ← `az staticwebapp secrets list -n swa-markdown-web` (119-char token piped, never printed).
- Run 27892649594 (initial): **success** in 1m14s. `curl https://purple-pond-09fadd20f.7.azurestaticapps.net/` → HTTP 200, body contains "The Markdown Web"/"Coming soon" (our 265-byte page, not the SWA default).
- Run 27892801957 (post-review-fix): **success**; `skip_api_build` annotation cleared; live page still served. Only residual annotation = GitHub's Node-20 runner-deprecation notice (platform-level, not actionable by us).

### Completion Notes List

- Ultimate context engine analysis completed — comprehensive developer guide created.
- Advanced elicitation applied (Pre-mortem #57, Inversion #31, Boundary & Edge Case Sweep #69) — hardening deltas folded into ACs/Tasks/Dev Notes (see Advanced Elicitation Record).
- Authored `.github/workflows/deploy-web.yml`: push-to-main (path-filtered) + workflow_dispatch; deterministic Node-20 `npm ci`/`npm run build`; `Azure/static-web-apps-deploy` with `skip_app_build` + `app_location: web/dist`.
- Stored SWA deployment token as repo secret `AZURE_STATIC_WEB_APPS_API_TOKEN`. **FR-17 proven live**: pushed → auto-built → auto-deployed → our page reachable over HTTPS, zero manual steps.
- AC3 (failed build does not deploy) verified structurally: build step precedes deploy in a single job, no `continue-on-error`/`if: always()`; a build failure fail-fasts before any upload (not destructively tested to avoid breaking the live site).
- Code review PASS WITH ITEMS (0 critical). Review corrected a false premise: `skip_api_build` is a valid v1 input (just redundant). Fixed: removed redundant `skip_api_build`; added explicit `output_location: ''`; `timeout-minutes: 15`; `cancel-in-progress: false`; SHA-pinned the token-receiving deploy action.

### Review Follow-ups (AI)

- [x] (low) Remove redundant `skip_api_build` (no API in project). Done.
- [x] (low) Add explicit `output_location: ''` to match documented prebuilt-app pattern. Done.
- [x] (low) Add `timeout-minutes: 15`. Done.
- [x] (med) `cancel-in-progress` policy decision → set `false` (queue deploys; don't abort SWA upload mid-flight). Done.
- [x] (low) SHA-pin `Azure/static-web-apps-deploy@1a947af…` (v1) — receives the deploy token. Done.
- Deferred (documented, out of scope): non-empty `dist` guard (#6), broaden path filter (#7), protected `production` environment (#8). Revisit as build complexity grows.

### File List

Created:
- `.github/workflows/deploy-web.yml`

GitHub config (not in repo): repo secret `AZURE_STATIC_WEB_APPS_API_TOKEN` (SWA deployment token).

## Advanced Elicitation Record

Auto-selected 3 methods most relevant to a CI/CD deploy story (risk / inversion / edge bias, non-scope-expanding):

- **#57 Pre-mortem Analysis (risk):** Imagined this deploy pipeline failing in production and worked backwards. Surfaced: (a) **Oryx auto-build drift** — relying on the SWA's built-in builder makes the build environment a moving target → mandated `actions/setup-node` Node 20 + `npm ci` + `skip_app_build: true`, uploading a deterministically pre-built `web/dist`; (b) **silent bad deploy** — a "soft-failing" build (e.g. `continue-on-error`) could still upload a stale/empty `dist` and overwrite the live site → made build-before-deploy ordering with NO `continue-on-error` an explicit AC3 + Task gate, so a build failure aborts before any upload.
- **#31 Inversion Analysis (core):** Asked "what would guarantee this story fails its ACs (or causes harm)?" → (a) **leaking the deployment token** (echoing it, committing it, pasting into YAML) is the highest-impact failure → made "pipe `az` output directly into `gh secret set`, never echo/commit, verify name-only" an explicit step, and referenced it only as `${{ secrets.* }}`; (b) **a green run that didn't actually deploy our page** — the SWA default holding page also returns HTTP 200 → inverted "200 = success" into "200 is necessary but not sufficient," requiring the response **body to contain 'The Markdown Web'** as positive proof (AC5).
- **#69 Boundary & Edge Case Sweep (technical):** Walked the trigger / path-filter / output-dir boundaries → (a) **trigger scope** — an over-broad trigger redeploys on unrelated changes and a `pull_request` trigger would spin up staging environments needing a `close`-event cleanup job → pinned `push` to `main` path-filtered to `web/**` + the workflow file, plus `workflow_dispatch`, and explicitly scoped OUT the `pull_request`/close-event flow; (b) **`app_location` vs `skip_app_build`** — with `skip_app_build: true` the action uploads `app_location` as-is, so pointing it at `web/` instead of `web/dist` would ship the wrong tree (possibly the SWA default) → pinned `app_location: "web/dist"` and made the body-text check the catch-all that would expose a mistargeted upload.

**Top 3 hardening deltas vs the raw epic ACs:**
1. Turned the epic's single "builds and deploys without manual steps" line into a **deterministic build-then-deploy contract** — Node 20, `npm ci`, `skip_app_build: true`, `app_location: "web/dist"` — explicitly NOT relying on Oryx (AC2), for reproducibility and as the foundation of the AC3 safety gate.
2. Made **"a failed build does not deploy"** a *structural, evidenced* gate (build precedes deploy, no `continue-on-error`, deploy step never runs on build failure) rather than an aspiration (AC3) — and required AC3 evidence without leaving `main` red.
3. Hardened verification from "page reachable over HTTPS" into **"200 AND body contains 'The Markdown Web'"** (AC5) — because the SWA default holding page also returns 200 — plus secret-hygiene (pipe-don't-print, name-only verification) for the production deployment token (AC4).
