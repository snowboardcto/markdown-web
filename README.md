# The Markdown Web

A vault of `.md` files, published beautifully to the web and read natively on Windows.
`themarkdownweb.com` is the live home of the project.

This repository is a **monorepo**: every component has a documented home, and the boundaries
between components are deliberate and enforced.

## Monorepo layout

```
themarkdownweb/  (repo root)
├── content/                    # the Vault (FR-1–4): seed .md + media; single source of truth
│   └── media/
├── web/                        # Browser/HTML path (FR-5–8): Astro + remark/rehype (GFM) + Shiki
│   └── src/pages/index.astro   # minimal placeholder page (this story)
├── api/                        # Content negotiation (FR-14): Azure Function — Accept → HTML | raw .md
│   └── negotiate/
├── clients/
│   └── windows/                # Native client (FR-9–13): .NET 10 + WPF
│       ├── App/                # shell, window, navigation, fetch raw .md
│       ├── Rendering/          # BEDROCK: Markdig AST → FlowDocument (pure, no net, no AI)
│       └── Agent/              # AI-personality transform (later; isolated)
├── infra/                      # IaC: Bicep (Azure SWA, custom domain, TLS) (FR-18)
└── .github/workflows/          # deploy-web.yml + build-windows.yml (FR-17)
```

## Component boundaries

These are not suggestions — later work depends on them holding:

- **`content/` is the single source of truth.** Both `web/` (at build time) and the native
  client (at runtime, via `api/`) consume the same `.md`. No content ever lives in code.
- **`Rendering/` is isolated and pure.** Markdig AST → WPF `FlowDocument`, with **no networking
  and no AI**. It is independently testable. `App/` and `Agent/` depend on `Rendering/` —
  **never the reverse**.
- **`api/` only negotiates.** Browsers get static HTML (Astro/SWA); clients get raw `.md`. It
  holds no content of its own.
- **No Chromium (NFR-1, hard).** The native Windows client renders native WPF UI only —
  never an embedded browser, webview, or Chromium. This constraint is non-negotiable.

## FR → component map

| FRs | Lives in |
|---|---|
| 1–4 Vault | `content/` (consumed by `web/` & `api/`) |
| 5–8 HTML client | `web/` (Astro) |
| 9–13 Native client | `clients/windows/` (App + Rendering + Agent) |
| 14 Content negotiation | `api/` |
| 17–18 Publish / host | `.github/workflows/` + `infra/` + Azure SWA |

## Scope & platform

- **Windows-first.** The native client targets Windows (.NET 10 + WPF).
- This scaffold (Story 1.1) creates **directory homes + the minimal Astro project only**.
  Bicep (1.2), the deploy workflow (1.3), the custom domain (1.4), and the .NET solution
  (Epic 3) land in their own stories.

## Getting started (web)

```sh
cd web
npm install
npm run build   # → web/dist/
```
