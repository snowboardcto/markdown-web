# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Git workflow

- **Commit and push directly to `main`.** Do not create feature branches or pull
  requests for routine work unless explicitly asked. Make changes, commit with a
  clear message, and push to `main`.

## Project

The Markdown Web — a monorepo publishing a vault of `.md` files beautifully to the
web and reading them natively on Windows. See `README.md` for the full layout and
component boundaries.

### Layout
- `content/` — the Vault (source `.md` + media)
- `web/` — Astro site (browser/HTML path); tests are Playwright
- `api/` — Azure Function for content negotiation
- `clients/windows/` — .NET 10 WPF native client (App / Rendering / Agent)
- `infra/` — Bicep IaC
- `.github/workflows/` — `deploy-web.yml`, `build-windows.yml`

## Verification

- Web tests: `cd web && npx playwright test`
- Web typecheck: `cd web && npx astro check`
- Web build: `cd web && npm run build`
- API tests: `cd api && node --test`
- Native (Windows-only, runs in `build-windows.yml` CI): `cd clients/windows && dotnet test`
