# infra/ — Infrastructure as Code (Bicep)

Azure hosting defined as code so the environment is reproducible, not hand-clicked.

## What's here now (Story 1.2)

- **`main.bicep`** — provisions one **Azure Static Web App** (`Microsoft.Web/staticSites`), SKU
  **Free**, resource-group scoped, in **manual-deploy mode** (no GitHub repo linked, so no PAT/secret
  lives in the template). Emits the `defaultHostname` as an output.
- **`main.bicepparam`** — declarative, secret-free parameter values (`appName`, `location`, `sku`).

The template is fully parameterized — no hard-coded subscription IDs or secrets.

## Deploy

Resource-group scoped (the CI deploy identity is Contributor on `rg-markdown-web` only):

```sh
az deployment group create \
  -g rg-markdown-web \
  -f infra/main.bicep \
  -p infra/main.bicepparam
```

Re-running is idempotent: stable `appName`, no duplicate resource, identical hostname. (On re-run,
`what-if` reports a single benign `Modify` on the read-only, server-computed `stableInboundIP` field —
this cannot be set in any template and does not represent real drift.)

`infra/main.json` (compiled ARM) is a build artifact and is gitignored — never commit it; recompile
from `main.bicep`.

## Story 1.4 — custom domain (FR-18)

`main.bicep` also captures the apex custom-domain binding as IaC, behind a guard flag:

- `param enableCustomDomain bool = false` — **leave false** until DNS is in place. The live binding was
  initiated out-of-band via `az staticwebapp hostname set ... --validation-method dns-txt-token` to
  surface the validation token for the manual DNS step.
- `param customDomain = 'themarkdownweb.com'` — the apex domain.
- The guarded `Microsoft.Web/staticSites/customDomains` resource is dormant while the flag is false
  (flag-off deploy adds no resource — idempotency preserved). Apply it **after** the `_dnsauth` TXT and
  apex A/ALIAS records propagate and the domain shows `Ready`:
  `az deployment group create -g rg-markdown-web -f infra/main.bicep -p infra/main.bicepparam -p enableCustomDomain=true`

The exact DNS records to add by hand are in
`_bmad-output/implementation-artifacts/1-4-dns-records-handoff.md`. Azure issues a managed TLS cert and
redirects http→https automatically once the domain validates.

## Related downstream

- **Story 1.3** deploys the built Astro site to this SWA via GitHub Actions using the SWA
  **deployment token** (enabled by manual-deploy mode). See `.github/workflows/deploy-web.yml`.
