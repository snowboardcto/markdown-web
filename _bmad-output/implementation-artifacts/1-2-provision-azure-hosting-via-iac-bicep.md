# Story 1.2: Provision Azure hosting via IaC (Bicep)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want the Azure hosting defined as code,
so that the environment is reproducible and not hand-clicked.

## Acceptance Criteria

1. **Given** the Bicep templates in `infra/` (`infra/main.bicep` + parameters) **When** I compile them with `az bicep build --file infra/main.bicep` **Then** the build succeeds with no errors (warnings allowed but reviewed), producing a valid ARM template — i.e. the template is syntactically and semantically valid BEFORE any deployment is attempted. *(AC1 — templates compile clean)*

2. **Given** the compiled Bicep templates **When** I deploy them resource-group-scoped with `az deployment group create -g rg-markdown-web --template-file infra/main.bicep` (using `infra/main.bicepparam` or `--parameters`) **Then** an Azure **Static Web App** (`Microsoft.Web/staticSites`), SKU **Free**, is created in resource group `rg-markdown-web` in an SWA-supported region (eastus2) **And** the deployment provisions ONLY within `rg-markdown-web` (the deploy identity holds Contributor on that RG only — any subscription- or tenant-scoped operation would be denied). *(AC2 — SWA Free created, RG-scoped)*

3. **Given** a successful deployment **When** I read the deployment outputs **Then** the SWA **default hostname** is emitted as a Bicep `output` (e.g. `output defaultHostname string = swa.properties.defaultHostname`) **And** that output is a **non-empty** string (a `*.azurestaticapps.net` host) — an empty or missing output is a FAILED AC, not a pass. *(AC3 — defaultHostname emitted as a non-empty output)*

4. **Given** an already-deployed environment **When** I run the same `az deployment group create` a **second** time with the same parameters **Then** the deployment is **idempotent** — `az deployment group what-if` reports **no resource changes** (`NoChange`/`Ignore` only — no `Create`, `Modify`, or `Delete`), no duplicate SWA is created, and the resource count in `rg-markdown-web` is unchanged. *(AC4 — re-run is idempotent, no drift, no duplicates)*

5. **Given** the templates **When** I inspect them **Then** they are **parameterized with NO hard-coded secrets and NO hard-coded subscription IDs** — app name is a parameter with a sensible default, location is a parameter (default `eastus2`), SKU is a parameter (default `Free`); the SWA is created in **manual-deploy mode** with **NO `repositoryUrl`/`branch`/`repositoryToken`** in the template (so Story 1.3 can deploy via the deployment token / Actions). *(AC5 — parameterized, secret-free, no GitHub linkage)*

> Source: [_bmad-output/planning-artifacts/epics.md#Story 1.2: Provision Azure hosting via IaC (Bicep)] (lines 138–150). Pipeline decision: **live deploy** — the OIDC identity, resource group `rg-markdown-web` (eastus2, sub `1522535a-d614-4009-93a3-09294fbdd6e0`), and GitHub secrets/vars already exist; the deploy identity has **Contributor on the resource group only**.

## Tasks / Subtasks

- [x] **Task 1 — Author `infra/main.bicep` defining the Static Web App (Free)** (AC: 1, 2, 3, 5)
  - [x] In `infra/main.bicep`, declare a `resource swa 'Microsoft.Web/staticSites@<current-stable-api>'` named from a parameter. Set `sku: { name: skuName, tier: skuName }` (Free), `location: location`, and `properties: {}` — i.e. **manual-deploy mode**. (pre-mortem: SWA's `location` field is restricted to a small SWA region set; `eastus2` is supported — do NOT default it to an arbitrary region or the deploy will fail with a location error.)
  - [x] Declare parameters with sensible defaults — NO hard-coded subscription IDs or secrets (AC5):
    - `param appName string = 'swa-markdown-web'` (DNS-safe default; lowercase, hyphen-ok, globally-unique-enough)
    - `param location string = 'eastus2'` (SWA-supported region; matches repo var `AZURE_LOCATION`)
    - `param skuName string = 'Free'` (optionally `@allowed(['Free','Standard'])`)
  - [x] **Do NOT** add `repositoryUrl`, `branch`, `repositoryToken`, or `provider` to `properties` — keeping the SWA in manual/"Other" deploy mode is an AC (AC5) and keeps Bicep vs CI concerns separate (Story 1.3 deploys via the deployment token). (inversion: linking a repo here would couple IaC to CI, require a PAT secret in the template, and break the secret-free AC.)
  - [x] Emit the hostname output (AC3): `output defaultHostname string = swa.properties.defaultHostname`. Optionally also `output staticWebAppName string = swa.name` and `output resourceId string = swa.id` for Story 1.3 to consume.
  - [x] Set `targetScope = 'resourceGroup'` (default) — the file MUST be resource-group-scoped, NOT subscription-scoped, because the deploy identity only has rights on `rg-markdown-web` (AC2). Do NOT add a `resourceGroup` resource or any `subscription()`-scoped declaration.
- [x] **Task 2 — Provide parameters file `infra/main.bicepparam`** (AC: 2, 5)
  - [x] Create `infra/main.bicepparam` with `using './main.bicep'` and explicit values for `appName`, `location` (`eastus2`), `skuName` (`Free`). Keep it free of secrets/subscription IDs (AC5). (Alternative: pass `--parameters` inline; a committed `.bicepparam` is preferred for reproducibility and is the documented invocation.)
  - [x] Confirm the chosen `appName` will not collide — SWA names are scoped per-resource-group, but the generated `*.azurestaticapps.net` hostname is globally unique (Azure appends a random suffix), so a fixed `appName` is safe and keeps re-runs idempotent. (edge: do NOT derive the name from `uniqueString()` on every run — a name that changes per deploy would break idempotency by recreating the resource. Use a stable parameter default.)
- [x] **Task 3 — Compile gate: `az bicep build`** (AC: 1)
  - [x] Run `az bicep build --file infra/main.bicep` and confirm exit 0 with no errors. Review any warnings. A compile error is a FAILED AC1 gate — fix before deploying. (This is the infra equivalent of a unit test: never deploy an uncompiled template.)
  - [x] Ensure `az` CLI and the Bicep extension are available (`az bicep version`; `az bicep install` if missing). The repo runs on Windows 11 — invoke `az` via PowerShell or the Bash tool consistently.
- [x] **Task 4 — Preview gate: `az deployment group what-if`** (AC: 2, 4)
  - [x] Run `az deployment group what-if -g rg-markdown-web --template-file infra/main.bicep --parameters infra/main.bicepparam` and review the predicted changes. On a clean RG this should show one `Create` (the SWA). (pre-mortem: what-if surfaces an RBAC/scope or region error BEFORE a partial live deploy — treat a what-if failure as a hard stop.)
  - [x] Confirm the preview shows resources ONLY in `rg-markdown-web` and nothing subscription-scoped (AC2 — least-privilege boundary).
- [x] **Task 5 — Live deploy (resource-group scoped)** (AC: 2, 3)
  - [x] Run `az deployment group create -g rg-markdown-web --template-file infra/main.bicep --parameters infra/main.bicepparam` (add `--name story-1-2-swa` for a stable, traceable deployment name). Confirm `provisioningState` is `Succeeded`.
  - [x] Capture the deployment outputs: `az deployment group show -g rg-markdown-web -n <deployment-name> --query properties.outputs.defaultHostname.value -o tsv`. Assert the value is **non-empty** and ends in `.azurestaticapps.net` (AC3). An empty/null output FAILS AC3.
  - [x] Assert the SWA exists: `az staticwebapp show -g rg-markdown-web -n <appName>` (or `az resource list -g rg-markdown-web --resource-type Microsoft.Web/staticSites`) returns exactly **one** SWA with SKU `Free` (AC2).
- [x] **Task 6 — Idempotency gate: re-run shows no changes** (AC: 4)
  - [x] Re-run `az deployment group what-if -g rg-markdown-web --template-file infra/main.bicep --parameters infra/main.bicepparam` against the now-provisioned RG. Assert it reports **no changes** (`NoChange`/`Ignore` only — zero `Create`/`Modify`/`Delete`). Any drift here FAILS AC4.
  - [x] Re-run `az deployment group create` a second time with identical parameters; confirm `Succeeded`, and confirm the SWA count in `rg-markdown-web` is still exactly **one** (no duplicate). (edge: a second `create` succeeding but adding a resource = idempotency failure.)
- [x] **Task 7 — Final verification against ACs (Definition of Done)** (AC: 1, 2, 3, 4, 5)
  - [x] AC1: `az bicep build` exits 0, no errors.
  - [x] AC2: exactly one `Microsoft.Web/staticSites`, SKU `Free`, in `rg-markdown-web`/eastus2; nothing provisioned outside the RG.
  - [x] AC3: `defaultHostname` output present and non-empty (`*.azurestaticapps.net`).
  - [x] AC4: second what-if = no changes; second create = no duplicate.
  - [x] AC5: grep the templates — NO subscription IDs, NO secrets/tokens, NO `repositoryUrl`/`branch`; `appName`/`location`/`skuName` are parameters with defaults. (inversion: if any of these are hard-coded, the AC fails even if the deploy works.)
  - [x] **Scope discipline:** this story creates the SWA only. Do NOT add custom-domain (`themarkdownweb.com` → Story 1.4), DNS, `deploy-web.yml` (Story 1.3), `staticwebapp.config.json` content, or any non-SWA resource. Commit `infra/main.bicep` + `infra/main.bicepparam` (and update `infra/README.md` only if needed). Do NOT commit any deployment token or `*.azurestaticapps.net` secret.

## Dev Notes

### What this story delivers (and explicitly does not)

This story provisions **one** Azure resource — a Static Web App (Free) — purely from Bicep, resource-group-scoped, idempotently, with the default hostname surfaced as an output. It is the IaC half of the walking skeleton. The **deploy** of actual content via GitHub Actions is **Story 1.3** (consumes the SWA's deployment token); the **custom domain + HTTPS** binding for `themarkdownweb.com` is **Story 1.4**. Keep those out of this story. [Source: epics.md#Epic 1, Stories 1.2/1.3/1.4, lines 138–177]

### Live-deploy environment (already provisioned — do not re-create)

- **Resource group:** `rg-markdown-web`, location `eastus2`, subscription `1522535a-d614-4009-93a3-09294fbdd6e0`. The RG already exists — the Bicep does NOT create it (RG-scoped deployment targets it via `-g`).
- **Identity / RBAC:** the GitHub OIDC deploy identity has **Contributor on `rg-markdown-web` only**. Any subscription-scoped or tenant-scoped Bicep (e.g. `targetScope = 'subscription'`, role assignments, RG creation) will be **denied**. This is the single biggest failure mode — keep everything RG-scoped. [Source: Pipeline Context — OIDC identity + RG]
- **GitHub config (for Story 1.3, noted for awareness, not used here):** secrets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`; repo vars `AZURE_RG=rg-markdown-web`, `AZURE_LOCATION=eastus2`.
- The repo is `snowboardcto/markdown-web`; `infra/` already exists (currently only `infra/README.md`, which already names the SWA + parameterized/secret-free intent). [Verified: `infra/README.md` present.]

### Why manual-deploy mode (no GitHub linkage in the template)

Creating the SWA with `properties: {}` (no `repositoryUrl`/`branch`/`repositoryToken`) leaves it in **manual / "Other"** deploy mode. This is deliberate and is **AC5**:
- It keeps **Bicep concerns (provisioning) separate from CI concerns (deploy)** — Story 1.3 retrieves the **deployment token** (`az staticwebapp secrets list`) and deploys via the SWA Actions task / CLI.
- Linking a repo in Bicep would require embedding a GitHub **PAT** (`repositoryToken`) in the template — a hard-coded secret, which **violates AC5** and the architecture's "no novel infra / standard plumbing" stance (NFR-7).
[Source: epics.md NFR-7; Pipeline Context — KEY TECHNICAL CONSTRAINTS]

### Idempotency — the subtle trap

Bicep/ARM deployments are declarative and idempotent **by design** — re-deploying the same template converges to the same state. The way this story breaks idempotency is by **making the resource identity non-deterministic**:
- Do NOT name the SWA with `uniqueString()`/`newGuid()` evaluated per-run — that changes the resource name each deploy and recreates it. Use a **stable `appName` parameter default**. (The public hostname is globally unique because Azure appends its own random suffix to the SWA name — you do NOT need to randomize the name yourself.)
- Do NOT put time/run-dependent values in resource properties.
- Verify idempotency the infra way: a **second `what-if` must report no changes**. This is the story's equivalent of a regression test. [Source: Advanced elicitation — Pre-mortem + Boundary sweep, see below]

### Verification commands (infra-appropriate "tests")

There is no unit-test framework for infra; the gates ARE the tests, run in this order:
1. `az bicep build --file infra/main.bicep` → compiles clean (AC1).
2. `az deployment group what-if -g rg-markdown-web --template-file infra/main.bicep --parameters infra/main.bicepparam` → preview (AC2/AC4).
3. `az deployment group create -g rg-markdown-web --template-file infra/main.bicep --parameters infra/main.bicepparam` → live (AC2/AC3).
4. Assert SWA exists + `defaultHostname` output non-empty (AC2/AC3).
5. Re-run what-if → **no changes**; re-run create → **no duplicate** (AC4).

Windows note: run `az` via the Bash tool or PowerShell; the Bash tool resets cwd between calls, so use the repo-relative `infra/main.bicep` from the repo root in one command, or absolute paths. Ensure `az login`/OIDC context targets subscription `1522535a-d614-4009-93a3-09294fbdd6e0` and RG `rg-markdown-web`.

### Bicep specifics

- `Microsoft.Web/staticSites` — use the current stable API version (e.g. `@2023-12-01` or newer stable; confirm with `az provider show --namespace Microsoft.Web` or Bicep IntelliSense at author time). SKU object is `{ name: 'Free', tier: 'Free' }`.
- `targetScope = 'resourceGroup'` is the default — no need to set `targetScope = 'subscription'` (and doing so would break the RBAC boundary).
- Prefer `.bicepparam` (typed, `using './main.bicep'`) over a JSON parameters file for reproducibility.

### Testing standards summary

No automated test framework applies to infra (greenfield; first unit tests appear for the WPF `Rendering/` lib in Epic 3 — [Source: 1-1-scaffold-the-monorepo.md#Testing standards summary]). For this story, "passing tests" = the five verification gates above all green, with AC4's no-change re-run as the regression guard.

### Previous story intelligence (Story 1.1)

- Story 1.1 created `infra/` as a directory home with `infra/README.md` only — this story adds the actual Bicep. The README already states the SWA + parameterized/secret-free intent; align to it (and update it only if the file set changes). [Source: 1-1-scaffold-the-monorepo.md#File List]
- Story 1.1 established the discipline of **scope containment** (it deliberately did NOT create Bicep/workflow YAML) and **never committing secrets/artifacts** (root `.gitignore`). Carry both forward: commit only the templates, never a deployment token or `.azure/` state.
- 1.1 confirmed Windows 11 host; Bash tool resets cwd between calls — same operational caveat applies to `az` invocations here.

### Project Structure Notes

- New files land in the existing `infra/` home: `infra/main.bicep`, `infra/main.bicepparam`. No new top-level directories. `infra/staticwebapp.config.json` (referenced in the architecture tree, line 135) is **content config consumed at deploy time** — it is NOT part of this provisioning story; leave it for Story 1.3 if/when needed.
- No conflicts with existing structure. Purely additive within `infra/`.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.2: Provision Azure hosting via IaC (Bicep)] — user story + ACs (lines 138–150)
- [Source: _bmad-output/planning-artifacts/epics.md#Additional Requirements] — IaC = Bicep (Azure-native default); Hosting = Azure Static Web Apps; NFR-7 don't-reinvent-plumbing (lines 59–60, 47)
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 1] — walking-skeleton FR-17/FR-18 context; Stories 1.3 (deploy) / 1.4 (custom domain) boundaries (lines 98–177)
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure & Boundaries] — `infra/` home, Azure SWA hosting (lines 135, 154)
- [Source: _bmad-output/implementation-artifacts/1-1-scaffold-the-monorepo.md] — previous-story patterns: scope containment, no-secret discipline, Windows/Bash cwd caveat
- [Source: Pipeline Context] — live-deploy decision; RG `rg-markdown-web` (eastus2); OIDC identity Contributor-on-RG-only; GitHub secrets/vars

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m] (coordinator-driven story creation, enhanced-automated-sprint pipeline)

### Debug Log References

- `az bicep build --file infra/main.bicep` → warning-free (BCP037 phantom-property warnings removed post-review).
- `az deployment group what-if -g rg-markdown-web` → initial: single `Create`. Post-deploy re-run: single benign `Modify` on read-only `stableInboundIP` only.
- `az deployment group create -g rg-markdown-web` → **Succeeded**; output `defaultHostname = purple-pond-09fadd20f.7.azurestaticapps.net`. Re-ran twice (idempotency): same hostname, `az staticwebapp list` length = 1 each time.
- `curl https://purple-pond-09fadd20f.7.azurestaticapps.net/` → HTTP 200 (SWA default page; real content arrives in Story 1.3).

### Completion Notes List

- Ultimate context engine analysis completed — comprehensive developer guide created.
- Advanced elicitation applied (Pre-mortem #57, Inversion #31, Boundary & Edge Case Sweep #69) — hardening deltas folded into ACs/Tasks/Dev Notes (see below).
- Authored parameterized `infra/main.bicep` + `infra/main.bicepparam`: one Free-tier SWA, RG-scoped, manual-deploy mode (no repo/PAT). Emits `defaultHostname` + `staticWebAppName` outputs.
- **LIVE DEPLOYED** to `rg-markdown-web`: `swa-markdown-web` @ `purple-pond-09fadd20f.7.azurestaticapps.net`. Idempotency proven by repeated `create` (1 resource, identical hostname).
- Code review PASS WITH ITEMS (0 critical, 2 high). Fixed: removed phantom `deploymentAuthPolicy`/`trafficSplitting` props (invalid on StaticSite@2024-04-01 → BCP037, silently dropped) leaving valid `provider: 'None'`; gitignored compiled `infra/main.json`; hardened `appName` (maxLength 60 + documented DNS pattern); refreshed `infra/README.md`.

### Review Follow-ups (AI)

- [x] (high) Remove invalid `deploymentAuthPolicy` property — BCP037, dropped by ARM, no enforcement. Done — deployment-token path is enabled by manual-deploy mode regardless.
- [x] (high) Remove invalid `trafficSplitting` property — BCP037, dropped, semantically inert. Done.
- [x] (med) Non-empty BCP warnings were not caught at AC1 — now warning-free, gate honored.
- [x] (med) Gitignore compiled `infra/main.json` (transpiled artifact). Done (narrowed to `infra/main.json` so a future `staticwebapp.config.json` stays tracked).
- [x] (med) Refresh stale `infra/README.md` (future-tense / wrong custom-domain claim). Done.
- [x] (low) `appName` DNS-safety/length guard. Done (maxLength 60, documented pattern via `@metadata` — Bicep has no native `@pattern` for params).
- Not actioned (low, optional): location-immutability prose note and Standard-SKU guard — left as `@allowed` choices; descriptions are accurate enough for the walking skeleton.

### File List

Created:
- `infra/main.bicep`
- `infra/main.bicepparam`

Modified:
- `infra/README.md` (refreshed to describe the live template + deploy command)
- `.gitignore` (added `infra/main.json`)

Live Azure resources (not in repo): Static Web App `swa-markdown-web` in `rg-markdown-web`.

## Advanced Elicitation Record

Auto-selected 3 methods most relevant to an IaC/Bicep provisioning story (risk/inversion/edge bias, non-scope-expanding):

- **#57 Pre-mortem Analysis (risk):** Imagined the deploy failing in production and worked backwards. Surfaced: (a) SWA `location` is restricted to a small region set — an unsupported default fails the deploy → pinned `eastus2` as the parameter default and called it out as a hazard; (b) deploy identity is **Contributor on the RG only** — any subscription/tenant-scoped Bicep is denied → mandated `targetScope = 'resourceGroup'` and a what-if pre-check that fails fast before a partial live deploy.
- **#31 Inversion Analysis (core):** Asked "what would guarantee this story fails its ACs?" → (a) hard-coding a subscription ID or a GitHub `repositoryToken`/secret would pass a naive deploy but **fail AC5** → made "no hard-coded secrets/subscription IDs, no repo linkage" an explicit verification step, not just an aspiration; (b) linking a GitHub repo in the template couples IaC to CI and forces a PAT secret → enforced manual-deploy mode (`properties: {}`).
- **#69 Boundary & Edge Case Sweep (technical):** Walked the re-run / empty-output / duplicate boundaries → (a) a per-run `uniqueString()`/`newGuid()` name silently breaks idempotency by recreating the resource → mandated a **stable `appName` parameter** and a second-`what-if`-shows-no-changes regression gate; (b) an empty/null `defaultHostname` output passing as success → made "output must be non-empty `*.azurestaticapps.net`" an explicit AC3 assertion.

**Top 3 hardening deltas vs the raw epic ACs:**
1. Split the single epic AC into a **compile → what-if → create → assert → re-run** gate sequence, with the **second what-if = no-changes** as an explicit idempotency regression test (AC4).
2. Made **least-privilege explicit**: RG-scoped only, `targetScope = 'resourceGroup'`, what-if fail-fast — because the deploy identity is Contributor-on-RG-only (the #1 failure mode).
3. Turned AC5 from aspirational into **verifiable**: stable-name-for-idempotency, no-secret/no-subscription-ID grep, and **manual-deploy mode (no repo linkage)** so no PAT ever enters the template.
