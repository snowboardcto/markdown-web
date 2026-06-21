# .github/workflows/ — GitHub Actions

CI/CD for the project (FR-17, publish-on-push):

- **`deploy-web.yml`** — build Astro (`web/`) → deploy to Azure Static Web Apps.
- **`build-windows.yml`** — build/test the WPF native client.

Workflow YAML lands in Stories 1.3/1.4; this folder is their home.
