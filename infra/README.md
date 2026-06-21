# infra/ — Infrastructure as Code (Bicep)

Azure resources defined as code so the environment is reproducible, not hand-clicked:

- Azure Static Web App (hosting)
- custom domain `themarkdownweb.com` + TLS (FR-18)

Templates are parameterized — no hard-coded secrets or subscription IDs. Bicep templates
land in Story 1.2; this folder is their home.
