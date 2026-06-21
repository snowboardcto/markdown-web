# Story 1.4: Bind custom domain themarkdownweb.com over HTTPS (FR-18)

Status: in-progress (phase-A done, verified live; phase-B BLOCKED-PENDING-DNS — user must add records)

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reader,
I want the site served at `themarkdownweb.com` over HTTPS (with `http://` redirecting to `https://`),
so that the product lives at its real, trusted address rather than a `*.azurestaticapps.net` hostname.

> **Critical reality — this is a MANUAL-DNS story.** The domain `themarkdownweb.com` is REGISTERED but its DNS is managed at an external registrar where automation is **not available** — records must be added **by hand by the user** (decision already made: "Registered, manual DNS"). Therefore this story **cannot fully auto-complete**. The automatable work (initiate the apex binding, capture the real validation token, emit the exact DNS records, capture the binding as IaC) is done NOW; final HTTPS/redirect verification is **BLOCKED until the user adds DNS and it propagates**, then closed as a documented follow-up gate.

## Acceptance Criteria

1. **Given** the live SWA `swa-markdown-web` in `rg-markdown-web` **When** the apex binding is initiated with `az staticwebapp hostname set -n swa-markdown-web -g rg-markdown-web --hostname themarkdownweb.com --validation-method dns-txt-token` **Then** the command returns a **real DNS-TXT validation token** and the custom domain is registered on the SWA in a `Validating`/pending state — and that exact token value is captured verbatim in the Dev Agent Record (not a placeholder). *(AC1 — apex binding initiated via dns-txt-token; real token captured)*

2. **Given** the validation token from AC1 **When** the dev emits the DNS records the user must add by hand **Then** the story output contains the **exact, copy-pasteable records**: (a) a **TXT** record at host `_dnsauth.themarkdownweb.com` with the captured token as its value, and (b) an **apex A** record for `themarkdownweb.com` → `40.67.153.174` (the SWA stable inbound IP) — *preferring* an **ALIAS/ANAME** record → the default hostname `purple-pond-09fadd20f.7.azurestaticapps.net` if the registrar supports apex aliasing, else the A record. *(AC2 — exact manual-DNS records emitted: `_dnsauth` TXT + apex A/ALIAS)*

3. **Given** the binding documents "configured via IaC where supported" (epic AC) **When** the dev captures the binding as code **Then** a `Microsoft.Web/staticSites/customDomains` child resource for `themarkdownweb.com` (validationMethod `dns-txt-token`) is added to `infra/main.bicep` **as the declarative record of the binding**, with an inline comment stating it must only be deployed once DNS is live (deploying it before DNS resolves blocks on validation) — and `az bicep build`/`bicep build` succeeds (the template still compiles). *(AC3 — binding captured as IaC; documented as apply-after-DNS)*

4. **Given** the user has NOT yet added the DNS records **When** the dev attempts final verification **Then** the story does NOT claim HTTPS success; instead it records a clear **BLOCKED-pending-DNS** gate with the exact post-DNS verification commands, and the SWA custom-domain status is reported as still-validating (e.g. `az staticwebapp hostname show ... --query status` → not `Ready`). *(AC4 — honest blocked-gate; no false HTTPS claim — BLOCKED-PENDING-DNS)*

5. **Given** the user has added the DNS records and they have propagated **When** the dev (or user) closes the follow-up gate **Then** Azure has auto-validated the domain and auto-issued a managed TLS cert, **And** `curl -sS -o /dev/null -w "%{http_code}" https://themarkdownweb.com/` returns **200** with a **valid certificate** (no `-k`), **And** the response body contains the placeholder text **"The Markdown Web"** (proving OUR page, not a parked/registrar page). *(AC5 — HTTPS loads with valid cert + our content — **BLOCKED-PENDING-DNS**)*

6. **Given** the domain has validated **When** a plain `http://themarkdownweb.com/` request is made **Then** it **redirects to `https://`** (HTTP 301/308, `Location: https://themarkdownweb.com/...`), as provided automatically by Azure SWA — verified with `curl -sS -o /dev/null -w "%{http_code} %{redirect_url}" http://themarkdownweb.com/`. *(AC6 — http→https redirect verified — **BLOCKED-PENDING-DNS**)*

7. *(Optional / secondary)* **Given** apex is bound **When** `www` coverage is desired **Then** `www.themarkdownweb.com` is optionally bound via `az staticwebapp hostname set ... --hostname www.themarkdownweb.com` with a **CNAME** `www → purple-pond-09fadd20f.7.azurestaticapps.net` emitted for the user (CNAME validation; no TXT needed) — captured as an optional second `customDomains` child in Bicep. If deferred, it is explicitly recorded as deferred, not silently dropped. *(AC7 — optional `www` binding (CNAME) — secondary; explicitly do-or-defer)*

> Source: [_bmad-output/planning-artifacts/epics.md#Story 1.4: Bind custom domain themarkdownweb.com over HTTPS (FR-18)] (lines 166–177). Pipeline decision: **Registered, manual DNS** — `themarkdownweb.com` is registered but DNS is managed externally with NO automation; records are added BY HAND by the user. SWA stable inbound IP for apex A-records: `40.67.153.174`. SWA default hostname: `purple-pond-09fadd20f.7.azurestaticapps.net`. Custom domains + managed TLS are supported on SWA **Free**.

## Tasks / Subtasks

- [x] **Task 1 — Initiate the apex custom-domain binding and capture the REAL validation token** (AC: 1)
  - [ ] Confirm `az` context: `az account show` targets subscription `1522535a-d614-4009-93a3-09294fbdd6e0`; SWA `swa-markdown-web` exists in `rg-markdown-web` (`az staticwebapp show -n swa-markdown-web -g rg-markdown-web --query "defaultHostname" -o tsv` → `purple-pond-09fadd20f.7.azurestaticapps.net`).
  - [ ] Run: `az staticwebapp hostname set -n swa-markdown-web -g rg-markdown-web --hostname themarkdownweb.com --validation-method dns-txt-token`. This **registers** the apex domain on the SWA and returns a **TXT validation token**. (Apex/root domains CANNOT use CNAME validation — they MUST use `dns-txt-token`; this is exactly why the flow needs a `_dnsauth` TXT record + an apex A/ALIAS, not a single CNAME.)
  - [ ] Capture the token value **verbatim** into the Dev Agent Record. Re-read it if needed: `az staticwebapp hostname show -n swa-markdown-web -g rg-markdown-web --hostname themarkdownweb.com --query "validationToken" -o tsv`. (edge: the token is stable for the pending binding; if the binding is deleted and re-created the token changes — emit the records from the SAME binding the user will validate against.)
  - [ ] Confirm the domain now shows a pending/validating status: `az staticwebapp hostname show -n swa-markdown-web -g rg-markdown-web --hostname themarkdownweb.com --query "status" -o tsv` (expect a non-`Ready` state such as `Validating`). (This is the AC4 evidence that the binding is initiated but not yet live.)
- [x] **Task 2 — Emit the EXACT manual-DNS records for the user to add by hand** (AC: 2)
  - [ ] Emit, copy-pasteable, the **TXT** record: host/name `_dnsauth` (FQDN `_dnsauth.themarkdownweb.com`), type `TXT`, value = the AC1 token (exact string, quoted as the registrar requires). (This is the domain-ownership proof Azure polls.)
  - [ ] Emit the **apex A** record: host/name `@` (FQDN `themarkdownweb.com`), type `A`, value `40.67.153.174` (the SWA **stable inbound IP**). **Prefer** an **ALIAS/ANAME** at the apex → `purple-pond-09fadd20f.7.azurestaticapps.net` **if and only if** the external registrar supports apex aliasing (many do not — apex `CNAME` is invalid per RFC); otherwise use the A record to `40.67.153.174`. State both options explicitly so the user picks the one their registrar supports. (inversion: a flat `CNAME` at the apex is the classic broken setup — never emit an apex CNAME.)
  - [ ] State the order/expectation clearly: the user adds BOTH records (TXT for validation + A/ALIAS for traffic), then waits for DNS propagation (minutes to hours depending on the registrar TTL). Note that the TXT only needs to exist long enough for Azure to validate, but leaving it in place is harmless. (edge: if the registrar auto-appends the zone, the host is `_dnsauth` not the full FQDN — call this out to avoid a doubled domain like `_dnsauth.themarkdownweb.com.themarkdownweb.com`.)
- [x] **Task 3 — Capture the binding as IaC (declarative `customDomains` child in Bicep)** (AC: 3)
  - [ ] Add a `Microsoft.Web/staticSites/customDomains@2024-04-01` child resource (parent `staticWebApp`) named `themarkdownweb.com` with `properties: { validationMethod: 'dns-txt-token' }` to `infra/main.bicep`.
  - [ ] Add an inline comment making the deploy-ordering hazard explicit: this resource **must not be deployed until the `_dnsauth` TXT + apex A/ALIAS records are live and propagated** — deploying it against missing DNS makes the deployment **block on validation and eventually fail/time out**. The live binding is therefore initiated out-of-band via `az` (Task 1); this Bicep resource is the **declarative record** to converge to once DNS is in place. (Consider gating it behind a `param bindCustomDomain bool = false` so a default `az deployment group create` does NOT attempt the binding — document whichever approach is chosen.)
  - [ ] Verify the template still compiles: `az bicep build --file infra/main.bicep` (or `bicep build infra/main.bicep`) succeeds with no errors. Do NOT run `az deployment group create` with the binding enabled while DNS is absent (that is the thing the comment warns against).
  - [ ] (Optional, AC7) Add a second `customDomains` child for `www.themarkdownweb.com` (validationMethod `cname-delegation`/`dns-txt-token` as appropriate) if `www` is bound; otherwise omit and record `www` as deferred.
- [x] **Task 4 — Record the BLOCKED-pending-DNS gate honestly (no false HTTPS claim)** (AC: 4)
  - [ ] In the Dev Agent Record, state plainly: binding initiated + records emitted + IaC captured; **HTTPS/redirect verification is BLOCKED** until the user adds the `_dnsauth` TXT and apex A/ALIAS records and they propagate. Do NOT mark AC5/AC6 satisfied yet.
  - [ ] Record the current `az staticwebapp hostname show ... --query status` value as evidence the domain is not yet `Ready`.
  - [ ] Write the exact **post-DNS verification commands** (the AC5/AC6 curls below) into the record so closing the gate later is mechanical. (pre-mortem: the worst outcome is declaring "done" on a green-looking `az` call while `https://themarkdownweb.com` actually serves the registrar's parked page or NXDOMAIN — the gate prevents that.)
- [ ] **Task 5 — (AFTER user adds DNS + propagation) Verify HTTPS + redirect, close the gate** (AC: 5, 6)
  - [ ] Confirm DNS resolves: TXT present (`nslookup -type=TXT _dnsauth.themarkdownweb.com` or `dig +short TXT _dnsauth.themarkdownweb.com` → the token) and apex resolves (`nslookup themarkdownweb.com` / `dig +short themarkdownweb.com` → `40.67.153.174` or the ALIAS target).
  - [ ] Confirm Azure validated + issued TLS: `az staticwebapp hostname show -n swa-markdown-web -g rg-markdown-web --hostname themarkdownweb.com --query "status" -o tsv` → `Ready`.
  - [ ] AC5: `curl -sS -o /dev/null -w "%{http_code}\n" https://themarkdownweb.com/` → **200** (no `-k`, so a cert error fails the check), AND `curl -sS https://themarkdownweb.com/ | grep -q "The Markdown Web"` → match (proves OUR page, not a parked page).
  - [ ] AC6: `curl -sS -o /dev/null -w "%{http_code} %{redirect_url}\n" http://themarkdownweb.com/` → **301/308** with `redirect_url` = `https://themarkdownweb.com/...`. (Redirect + cert are platform behaviors Azure SWA provides automatically once validated — VERIFY, don't configure.)
  - [ ] Update the story: flip AC5/AC6 to satisfied, record the cert issuer/expiry and curl outputs in the Dev Agent Record.
- [ ] **Task 6 — Final verification against ACs (Definition of Done)** (AC: 1, 2, 3, 4, 5, 6, 7)
  - [ ] AC1: `az ... hostname set --validation-method dns-txt-token` ran; real token captured verbatim; domain in validating state.
  - [ ] AC2: exact `_dnsauth` TXT + apex A (`40.67.153.174`) / ALIAS records emitted; no apex CNAME; both registrar options stated.
  - [ ] AC3: `customDomains` child in `infra/main.bicep` with apply-after-DNS comment; `bicep build` passes; binding NOT deployed against missing DNS.
  - [ ] AC4: BLOCKED-pending-DNS gate recorded; status shown non-`Ready`; post-DNS commands captured. (This is the legitimate stopping point if the user has not yet added DNS.)
  - [ ] AC5/AC6: after DNS — `https://themarkdownweb.com` → 200 + valid cert + body "The Markdown Web"; `http://` → 301/308 → https. (Closed when DNS is live.)
  - [ ] AC7: `www` bound (CNAME emitted + IaC) OR explicitly recorded as deferred.
  - [ ] **Scope discipline:** this story adds ONLY the custom-domain binding (`az` initiation + emitted DNS records + `customDomains` in `infra/main.bicep`, optional `www`). Do NOT add real markdown content (→ Epic 2), `staticwebapp.config.json` routing, the Windows client, or any change to `deploy-web.yml`. Do NOT hand-click the domain in the portal — bindings go through `az` + IaC. Do NOT fabricate DNS success.

## Dev Notes

### What this story delivers (and explicitly does not)

This story binds the registered apex domain `themarkdownweb.com` to the live SWA and lands managed HTTPS (FR-18), completing the walking-skeleton spine: the placeholder will be reachable at its real address over TLS with `http→https` redirect. It is the **domain/TLS** piece; Story 1.2 provisioned the SWA, Story 1.3 wired publish-on-push. Real content rendering is **Epic 2**. Keep that out. [Source: epics.md#Epic 1, Story 1.4, lines 166–177; #FR Coverage Map FR-18, line 94]

**The defining constraint is MANUAL DNS.** Because the registrar offers no automation, the story is structurally a *two-phase* job: phase A (fully automatable, done now) = initiate the binding, capture the real token, emit exact records, capture IaC; phase B (human-gated) = the user adds DNS by hand, it propagates, Azure auto-validates + auto-issues TLS, then HTTPS/redirect are verified. ACs 5 and 6 are honestly marked **BLOCKED-PENDING-DNS** and closed as a follow-up gate — they are NOT a license to claim HTTPS works before DNS exists. [Source: Pipeline Context — "Registered, manual DNS"; KEY TECHNICAL REALITY]

### Live environment (already provisioned — do not re-create)

- **SWA:** `swa-markdown-web` in `rg-markdown-web`, default hostname `purple-pond-09fadd20f.7.azurestaticapps.net`, Free tier (custom domains + managed TLS supported on Free). [Source: 1-2-...; infra/main.bicep]
- **SWA stable inbound IP (for apex A-record):** `40.67.153.174`. [Source: Pipeline Context]
- **Domain:** `themarkdownweb.com` REGISTERED; DNS managed externally, **no automation** — user adds records by hand. [Source: Pipeline Context]
- **`az` auth:** control plane via OIDC / `az login`; subscription `1522535a-d614-4009-93a3-09294fbdd6e0`. `az account show` before Task 1.

### Apex domains need TXT validation, not CNAME (why `_dnsauth` + A/ALIAS)

A root/apex domain (`themarkdownweb.com`, no subdomain label) **cannot** be validated or pointed with a flat `CNAME` — RFC forbids a CNAME coexisting with the apex SOA/NS records. So SWA's apex flow is:
1. **Ownership proof** via a **TXT** record at `_dnsauth.themarkdownweb.com` carrying the validation token (`--validation-method dns-txt-token`).
2. **Traffic** via an **apex A** record → the SWA stable inbound IP `40.67.153.174`, OR an **ALIAS/ANAME** record → `purple-pond-09fadd20f.7.azurestaticapps.net` if the registrar supports apex aliasing (preferred — survives IP changes; but not all registrars offer it).

A subdomain like `www` is different: it CAN use a single `CNAME → purple-pond-09fadd20f.7.azurestaticapps.net` (and `cname-delegation` validation), which is why the optional `www` binding (AC7) is simpler. [Source: Azure SWA custom-domain docs; Pipeline Context — KEY TECHNICAL REALITY]

### TLS + redirect are platform behaviors — VERIFY, don't configure

Once the domain validates, Azure SWA **automatically** (a) issues and renews a managed TLS certificate and (b) serves `http→https` redirects. There is nothing to configure for these — the story's job is to VERIFY them after propagation (the AC5/AC6 curls), not to author cert/redirect config. Do not add a `staticwebapp.config.json` for this (routing config is a later concern; the architecture lists `infra/staticwebapp.config.json` as future). [Source: epics.md#Story 1.4 ACs; architecture.md line 135]

### The IaC-vs-DNS ordering hazard (AC3)

The epic says the domain is "configured via IaC where supported." A `Microsoft.Web/staticSites/customDomains` child resource IS the declarative representation — but **deploying it before DNS is live will block on validation and fail/time out**, because ARM waits for Azure to validate the domain, which it can't do until `_dnsauth` TXT resolves. Resolution: initiate the **live** binding via `az` now (so we get the real token to hand the user), and add the `customDomains` resource to Bicep as the **declarative record to converge to once DNS is live** — guarded (e.g. `param bindCustomDomain bool = false`) and/or clearly commented so a routine `az deployment group create` does not attempt the binding prematurely. Compile-check with `bicep build`; do not deploy-enable it against missing DNS. [Source: epics.md#Story 1.4; infra/main.bicep header comment — "Story 1.4 binds the custom domain … keeping CI/domain out of the template keeps provisioning idempotent"]

### Honest completion — the BLOCKED-PENDING-DNS gate (AC4)

Because the human DNS step sits in the middle, the legitimate "done for now" state is: binding initiated, real token captured, exact records emitted, IaC captured, status shown non-`Ready`, and post-DNS commands written down. Marking AC5/AC6 "passed" before the user has added DNS would be a lie (and would likely be serving a registrar parked page or NXDOMAIN). The gate is the integrity mechanism. When the user reports DNS added + propagated, Task 5 closes the gate mechanically. [Source: Advanced Elicitation — Pre-mortem + Inversion, see Advanced Elicitation Record]

### Verification commands (the "tests" for this domain/TLS story)

No unit-test framework applies (greenfield infra story; first unit tests are the WPF `Rendering/` lib in Epic 3). The gates ARE the tests:
1. `az staticwebapp hostname set ... --validation-method dns-txt-token` → returns a token (AC1).
2. Emitted records reviewed for correctness: `_dnsauth` TXT = token; apex A = `40.67.153.174` (or ALIAS); no apex CNAME (AC2).
3. `az bicep build --file infra/main.bicep` → success with the `customDomains` child present (AC3).
4. `az staticwebapp hostname show ... --query status` → non-`Ready` now (AC4); `Ready` after DNS (AC5).
5. After DNS: `curl https://themarkdownweb.com/` → 200 + valid cert + body "The Markdown Web" (AC5); `curl http://themarkdownweb.com/` → 301/308 → https (AC6).

Windows note: dev host is Windows 11; the Bash tool resets cwd between calls and `curl`/`dig` may differ from Linux. Prefer `curl.exe` and `nslookup` (or `Resolve-DnsName` in PowerShell) on Windows; invoke `az` with the repo at a known path; ensure `az account show` is authenticated before Task 1.

### Previous story intelligence (Story 1.3)

- 1.3 proved the live site at `purple-pond-09fadd20f.7.azurestaticapps.net` serves OUR placeholder (body "The Markdown Web"); this story reuses that exact body-text assertion to prove the **custom domain** serves our page, not a parked page (AC5). [Source: 1-3-...#The "200 is not enough" trap]
- 1.3 established the **"200 is necessary but not sufficient"** discipline — carried into AC5 (200 + valid cert + body text). [Source: 1-3-...#AC5]
- 1.3 reinforced **scope containment** (don't pull in Epic 2 content / other workflows) and **never hand-click in the portal** (use `az` + IaC) — both carried here. [Source: 1-3-...#Scope discipline]
- 1.2's `infra/main.bicep` header already anticipates this story ("Story 1.4 binds the custom domain") and deliberately kept the domain OUT of the provisioning template for idempotency — this story adds the `customDomains` child as the guarded/commented declarative record, consistent with that intent. [Source: infra/main.bicep lines 1–9, 57]

### Project Structure Notes

- Modifies: `infra/main.bicep` (add `Microsoft.Web/staticSites/customDomains` child; guarded/commented apply-after-DNS). No new top-level directories.
- Live binding initiated out-of-band via `az` (control-plane data is in Azure, not the repo) — the repo's IaC is the declarative mirror.
- No conflict with existing structure; the architecture maps FR-18 to SWA custom-domain + free SSL and reserves `infra/staticwebapp.config.json` for later routing (NOT needed here). [Source: architecture.md#FR → component map, line 154; line 135]

### Testing standards summary

No automated test framework applies to this infra story (greenfield; first unit tests are the WPF `Rendering/` lib in Epic 3 — [Source: 1-1-scaffold-the-monorepo.md#Testing standards summary]). "Passing" = AC1–AC4 satisfied now (binding initiated, records emitted, IaC captured + `bicep build` green, blocked-gate recorded) and AC5/AC6 closed after the user's manual DNS propagates.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.4: Bind custom domain themarkdownweb.com over HTTPS (FR-18)] — user story + ACs (lines 166–177)
- [Source: _bmad-output/planning-artifacts/epics.md#FR Coverage Map] — FR-18 → Epic 1 (line 94); #Additional Requirements — IaC custom domain + free SSL (lines 59–60)
- [Source: _bmad-output/planning-artifacts/architecture.md] — domain themarkdownweb.com owned (line 57); `infra/staticwebapp.config.json` FR-18 (line 135); FR → component map (line 154)
- [Source: c:/Users/snowboardcto/Documents/GitHub/markdown-web/infra/main.bicep] — live SWA `swa-markdown-web`, default hostname, Free tier; header comment reserving the custom-domain binding for Story 1.4 (lines 1–9, 37–58)
- [Source: _bmad-output/implementation-artifacts/1-3-deploy-a-placeholder-page-via-github-actions-on-push.md] — "200 is not enough" body-text proof; scope/portal discipline; live hostname
- [Source: Pipeline Context] — "Registered, manual DNS"; SWA stable inbound IP `40.67.153.174`; default hostname `purple-pond-09fadd20f.7.azurestaticapps.net`; dns-txt-token flow; TLS/redirect auto-provided; KEY TECHNICAL REALITY

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m] (coordinator-driven story creation, enhanced-automated-sprint pipeline)

### Debug Log References

- `az staticwebapp hostname set -n swa-markdown-web -g rg-markdown-web --hostname themarkdownweb.com --validation-method dns-txt-token` → binding registered.
- `az staticwebapp hostname show ... --query "{status,validationToken}"` → status `Validating`; **validation token `_0eikqrixy05gpyeji5phm1667xvioua`** (real, verbatim).
- Stable inbound IP (apex A fallback): `40.67.153.174` (via ARM REST `properties.stableInboundIP`).
- Emitted records → `_bmad-output/implementation-artifacts/1-4-dns-records-handoff.md`: TXT `_dnsauth`=token; apex A→40.67.153.174 / ALIAS→default hostname; optional www CNAME; parked-record-removal warning.
- `az bicep build infra/main.bicep` → clean (warning-free) with the guarded `customDomains` resource. Flag-OFF what-if adds no customDomains resource (idempotency preserved).
- **Phase-B (BLOCKED-PENDING-DNS):** `https://themarkdownweb.com` + http→https redirect cannot be verified until the user adds the records and they propagate; Azure then auto-validates and issues managed TLS. Verify commands are pre-written in the handoff doc.

### Completion Notes List

- Ultimate context engine analysis completed — comprehensive developer guide created.
- Advanced elicitation applied (Pre-mortem #57, Inversion #31, Boundary & Edge Case Sweep #69 — risk/inversion/edge bias) — hardening deltas folded into ACs/Tasks/Dev Notes (see Advanced Elicitation Record).
- MANUAL-DNS reality baked in: ACs 5/6 explicitly BLOCKED-PENDING-DNS; phase-A (binding initiated, real token captured, exact records emitted, IaC captured) done + verified live; phase-B (HTTPS/redirect verification) is a documented follow-up gate the user closes after DNS.
- **AC7 (www binding): DEFERRED** — apex-only for now; the www CNAME + `cname-delegation` command are documented in the handoff doc for optional later use.
- Code review: phase-A **PASS** (0 critical, 5 low). Fixes applied: handoff "only after Ready" caution; infra/README Story-1.4 subsection; this Dev Agent Record updated; www-deferred recorded.

### Review Follow-ups (AI)

- [x] (low) Handoff: caution that `enableCustomDomain=true` deploy must run only after status `Ready`. Done.
- [x] (low) infra/README: add Story 1.4 custom-domain subsection (guard flag + apply-after-DNS). Done.
- [x] (low) Story Dev Agent Record: status + token + snapshot reflect verified live state. Done.
- [x] (low) Record AC7 www binding as explicitly DEFERRED. Done.
- (info) Re-applying flag-on deploy is idempotent against the already-live az-initiated binding (same name/method).

### File List

Modified:
- `infra/main.bicep` (added guarded `apexCustomDomain` customDomains resource + `enableCustomDomain`/`customDomain` params)
- `infra/README.md` (Story 1.4 subsection)

Created:
- `_bmad-output/implementation-artifacts/1-4-dns-records-handoff.md` (exact manual-DNS records + verify steps)

Out-of-band (not in repo): live `az` apex custom-domain binding on `swa-markdown-web` (status Validating, awaiting DNS).

## Advanced Elicitation Record

Auto-selected 3 methods most relevant to a manual-DNS custom-domain/TLS story (risk / inversion / edge bias, non-scope-expanding):

- **#57 Pre-mortem Analysis (risk):** Imagined this story declared "done" and themarkdownweb.com still broken in production. Surfaced: (a) **false-success on a parked/NXDOMAIN page** — a green `az hostname set` only *registers* the pending binding; if we "verified" against `https://themarkdownweb.com` before DNS existed we'd be testing the registrar's parked page or a resolution failure → made AC4 a hard **BLOCKED-PENDING-DNS** gate and AC5 require **200 + valid cert (no `-k`) + body "The Markdown Web"**; (b) **IaC deploy hangs** — deploying the `customDomains` Bicep child against missing DNS blocks on validation and times out → AC3 captures the binding as code but mandates an apply-after-DNS comment/guard and only a `bicep build` compile-check now, with the LIVE binding initiated via `az` to obtain the real token.
- **#31 Inversion Analysis (core):** Asked "what would guarantee this story fails its ACs (or causes harm)?" → (a) **emitting an apex CNAME** (the classic broken apex setup — RFC-invalid alongside SOA/NS) → AC2/Task 2 forbid an apex CNAME and require a `_dnsauth` TXT + apex **A** (`40.67.153.174`) or **ALIAS**, with both registrar options stated; (b) **handing the user a placeholder/stale token** → AC1 requires capturing the **real** token verbatim from the SAME binding the user validates against, and warns that deleting/re-creating the binding rotates the token; (c) **claiming HTTPS works without DNS** → inverted into the explicit blocked-gate (AC4) so "done now" means phase-A only.
- **#69 Boundary & Edge Case Sweep (technical):** Walked the apex-vs-subdomain, registrar-capability, and propagation boundaries → (a) **apex vs `www`** — apex MUST use `dns-txt-token` + A/ALIAS, but `www` CAN use a single CNAME → captured `www` as the simpler **optional AC7** (CNAME → default hostname) so it's do-or-explicitly-defer, not silently dropped; (b) **registrar zone auto-append** — host `_dnsauth` vs FQDN `_dnsauth.themarkdownweb.com` can double the domain → Task 2 calls this out; (c) **ALIAS availability** — not all external registrars support apex ALIAS/ANAME → emitted A-record fallback to `40.67.153.174` as the always-works path while preferring ALIAS where supported.

**Top 3 hardening deltas vs the raw epic ACs:**
1. Split the epic's single "visit https://… → loads with valid cert; http→https redirects" AC into an **honest two-phase contract**: phase-A automatable NOW (binding initiated + real token captured + exact records emitted + IaC captured) vs phase-B **BLOCKED-PENDING-DNS** (AC5/AC6 closed after the user's manual DNS propagates) — so the story can't lie about HTTPS before DNS exists.
2. Made the **manual-DNS records exact and apex-correct** (AC2): `_dnsauth` TXT = real token + apex **A `40.67.153.174`** (prefer ALIAS → default hostname where the registrar supports it), and explicitly **never an apex CNAME** — plus the registrar zone-append caveat.
3. Captured the binding as **guarded IaC** (AC3): a `customDomains` child in `infra/main.bicep` documented as **apply-after-DNS** (deploying against missing DNS blocks on validation), compile-checked with `bicep build` and NOT deploy-enabled prematurely — reconciling "configured via IaC where supported" with the live `az`-initiated binding that yields the real token.
