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

## Future (later stories)

- **Story 1.3** deploys the built Astro site to this SWA via GitHub Actions using the SWA
  **deployment token** (enabled by manual-deploy mode).
- **Story 1.4** binds the custom domain `themarkdownweb.com` + TLS (FR-18) — DNS records are managed
  externally, so that story emits the records to add by hand.
