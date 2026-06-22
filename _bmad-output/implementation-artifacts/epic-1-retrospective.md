# Epic 1 Retrospective — Walking Skeleton: IaC + CI/CD Live on Azure

**Date:** 2026-06-22
**Epic status:** Done (all 4 stories closed)
**Live URL:** https://themarkdownweb.com

---

## 1. Summary

Epic 1 delivered a complete, end-to-end deployment spine for The Markdown Web: a monorepo scaffold with homes for every component, a Free-tier Azure Static Web App provisioned declaratively via Bicep, a GitHub Actions workflow that builds the Astro app with `npm ci`/`npm run build` and deploys the pre-built output on every push to `main`, and the apex custom domain `themarkdownweb.com` bound over HTTPS with automatic TLS and HTTP→HTTPS redirect. The DNS step required manual GoDaddy intervention (parked records removed, apex A `40.67.153.174` added, `_dnsauth` TXT validation token added), but once propagated Azure auto-validated and auto-issued a managed certificate. The site now serves the placeholder page at `https://themarkdownweb.com` with a valid cert, and a broken build is structurally prevented from deploying. Epics 2–4 were built directly on top of this skeleton without revisiting any of its infrastructure.

---

## 2. FR / Goal Closure

| Requirement | Met? | Evidence |
|---|---|---|
| **FR-17** — Publish on push (Story 1.3) | Yes | `deploy-web.yml` runs on `push: main: paths: web/**`; GitHub Actions run 27892649594 succeeded in 1m14s; `curl https://purple-pond-09fadd20f.7.azurestaticapps.net/` → HTTP 200, body "The Markdown Web". |
| **FR-18** — Custom domain over HTTPS (Story 1.4) | Yes | `az staticwebapp hostname show ... --query status` → `Ready`; `curl https://themarkdownweb.com/` → HTTP 200, valid cert (no `-k`), body "The Markdown Web"; `curl http://themarkdownweb.com/` → HTTP 301 → `https://`. IaC binding applied (`enableCustomDomain=true` deploy succeeded). |
| **Epic goal** — Walking skeleton live end-to-end | Yes | Sprint-status entry `1-4-bind-custom-domain-themarkdownweb-com-over-https: done # live at https://themarkdownweb.com (Ready, TLS issued, http->https 301)`. Monorepo, IaC, CI/CD, and domain/TLS all delivered in 4 sequential stories. |

---

## 3. What Went Well

- **Scope discipline held across all 4 stories.** Each story delivered exactly its slice and stopped: 1.1 created directories and READMEs only, 1.2 provisioned the SWA only, 1.3 added the workflow and secret only, 1.4 added the domain binding only. No scope creep into Epic 2 content or each other's concerns.
- **IaC-first from day one.** Provisioning the SWA via Bicep (`infra/main.bicep`) with `targetScope = 'resourceGroup'` and manual-deploy mode (`properties: {}`) created a clean separation between provisioning and CI — Story 1.3 consumed the deployment token without touching the template. The idempotency gate (second `az deployment group what-if` → no changes) proved the approach sound and carried forward through later Bicep updates.
- **Deterministic build-then-deploy pattern proved robust.** Using `actions/setup-node` + `npm ci` (locked to `web/package-lock.json`) with `skip_app_build: true` and `app_location: "web/dist"` rather than relying on Oryx auto-build gave reproducible outputs and a clean structural gate: a failing build exits non-zero, the deploy step never runs, and the live site is unchanged. This same pattern survived Epic 2's real content without modification.
- **Honest gate on DNS/TLS.** The two-phase structure of Story 1.4 (phase A: initiate binding, capture real token, emit DNS records, update IaC; phase B: verify after propagation) prevented a false "done" while DNS was still unset. The `BLOCKED-PENDING-DNS` label on ACs 5/6 was the right call — the gate closed correctly once GoDaddy records propagated.
- **Root `.gitignore` created first.** The discipline of creating `.gitignore` before `npm install` in Story 1.1 meant `node_modules/` and `web/dist/` were never accidentally staged. This carried forward and never caused a problem in later epics.

---

## 4. What Was Hard / What We'd Change

- **BCP037 phantom property warnings in Bicep.** The initial `infra/main.bicep` included `deploymentAuthPolicy` and `trafficSplitting` properties on the `Microsoft.Web/staticSites` resource — both invalid on the `2024-04-01` API version (silently dropped by ARM, flagged as BCP037 by the compiler). The AC1 compile-clean gate caught them, but they had to be removed in a post-review fix. Better to verify the API schema before authoring properties on an unfamiliar resource type.
- **DNS dependency on an external registrar is the longest pole.** The blocking wait for GoDaddy DNS propagation (plus the need to manually remove the GoDaddy-injected parked-page A record at `76.223.105.230`/`13.248.243.5` before the correct apex A could take effect) was the only step in the epic that could not be automated or accelerated. The parked-record removal warning in the handoff doc (`1-4-dns-records-handoff.md`) was critical — without it the `_0eikqrixy05gpyeji5phm1667xvioua` TXT token would have validated but traffic would have hit GoDaddy's servers.
- **SWA deployment token handling required care.** The `AZURE_STATIC_WEB_APPS_API_TOKEN` secret could only be verified by name (`gh secret list`), not by value — making it impossible to confirm the correct token was set without a live deploy attempt. Piping `az staticwebapp secrets list ... -o tsv` directly into `gh secret set` (never assigning to a shell variable) was the right pattern, but it required discipline in a headless environment where log output is easily leaked.
- **IaC deploy-ordering hazard with custom domain.** The `Microsoft.Web/staticSites/customDomains` Bicep child resource blocks on ARM validation if deployed before the `_dnsauth` TXT record exists. The `enableCustomDomain bool = false` guard parameter was the right mitigation, but the default-off behavior had to be explicitly documented and the flag-on re-deploy had to wait until `az staticwebapp hostname show ... --query status` returned `Ready`. This two-step (live `az` initiation for the real token, then Bicep convergence after DNS) is genuinely awkward.
- **Node 20 runner deprecation notice.** GitHub Actions emitted a platform-level Node-20 deprecation annotation on every `deploy-web.yml` run. It is not actionable by the project (it is a runner-level platform warning), but it clutters the run log. Worth noting for when the runner version is eventually updated.

---

## 5. Lessons / Carry-Forward

- **Compile-clean gates are load-bearing for infra.** Treating `az bicep build` exit-0-with-no-warnings as the AC1 gate (not just "deploys without error") caught the BCP037 properties before they silently eroded trust in the template. Apply the same zero-warnings standard to any future Bicep changes.
- **Idempotency tests should be explicit, not assumed.** Re-running `az deployment group what-if` against the provisioned RG and asserting `NoChange`/`Ignore` only (not `Create`/`Modify`/`Delete`) is the infra equivalent of a regression test. Wire this check into any future IaC story's definition of done.
- **"200 is not enough" — always assert body content.** The SWA default holding page returns HTTP 200 before any content is deployed. The body-text assertion (`grep "The Markdown Web"`) became the standard proof-of-deploy check and should be used for any future live-site verification.
- **Separate provisioning from deployment token management.** Keeping `repositoryUrl`/`branch`/`repositoryToken` out of the Bicep template (manual-deploy mode) and wiring the deployment token as a GitHub secret kept IaC concerns and CI secrets cleanly separated. This boundary held through all subsequent epics — do not collapse it.
- **Document DNS steps as a handoff artifact.** The `1-4-dns-records-handoff.md` file with exact copy-pasteable records, GoDaddy-specific steps, and a parked-record removal warning was the difference between a smooth hand-off and a debugging session. For any future story that produces a human-gated out-of-band step, create a dedicated handoff doc.

---

## 6. Success Assessment

**Success — the walking skeleton is live end-to-end: push deploys, custom domain resolves over HTTPS with managed TLS, and the entire deployment spine was validated before any real content was built on it.**
