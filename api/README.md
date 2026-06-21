# api/ — content negotiation (Azure Functions)

The only job of this component is **content negotiation** (FR-14):

- `Accept: text/html` (or any non-markdown / wildcard / absent Accept) → the HTML
  representation (the static Astro page served by the SWA static host)
- `Accept: text/markdown` → the raw `.md` source bytes (verbatim, byte-exact)
- **always** sets `Vary: Accept` (on every branch, so a shared/CDN cache keys on
  `Accept` and never cross-serves markdown to a browser, or vice-versa)

It negotiates **only** — it holds no content of its own. `content/**/*.md` at the
repo root is the single source of truth.

## How the raw `.md` is made available (Decision A — bundle with the API)

A build step (`scripts/build-content.mjs`) writes, under `negotiate/`:

- `negotiate/content/**` — a **verbatim byte-copy** of every `content/**/*.md`
  (`fs.copyFile`, never a read-as-text rewrite → no BOM injection, no CRLF/
  trailing-newline mutation → AC6 byte fidelity), and
- `negotiate/manifest.json` — a generated `{ "<slug>": "<relPath>" }` map whose
  slugs come from the **shared** `web/src/lib/slug.mjs` `pathToSlug` (so the
  markdown URL and the HTML URL can never drift → AC4), failing loud on a slug
  collision exactly as the web route does.

At runtime (`negotiate/vault.mjs`) the manifest + files load once into a **closed
`Map<slug, Buffer>`**. The request slug is a pure **lookup key** (`readMd(slug)`),
never concatenated into a filesystem path → an unknown/hostile key is a clean
404 by construction (AC5). The adapter (`negotiate/adapter.mjs`) additionally
decodes the slug **exactly once**, rejects `..`/`%2F`/`%5C`/`%2E`/`%00`/
double-encoded/absolute/UNC/drive-letter keys (and a post-decode literal `\0`),
then **normalises the decoded key with the same shared `pathToSlug`** (lowercase/
github-slug per segment) before the lookup — so a mixed-case URL like `/X`
resolves to the same `x` key the map was built from (no case drift, AC4).

The build also bundles a verbatim copy of `web/src/lib/slug.mjs` as
`negotiate/slug.mjs` so the runtime can normalise with the SAME rule the manifest
keys came from, without a `web/` dependency in the deployed package. All three
build artifacts (`negotiate/content/`, `negotiate/manifest.json`,
`negotiate/slug.mjs`) are git-ignored — they regenerate deterministically from
`content/` + `web/src/lib/slug.mjs` via `npm run build`.

## Module layout

- `negotiate/negotiate.mjs` — the **pure** handler `negotiate({ slug, acceptHeader, readMd }) → { status, headers, body }` (the CI-testable seam; RFC 9110 §12.5.1 Accept parsing; no Azure/FS/network).
- `negotiate/adapter.mjs` — request→response glue (slug sanitisation, HTML-branch redirect) with **no** `@azure/functions` dependency (also CI-testable).
- `negotiate/vault.mjs` — the bundled-vault `readMd` closed-map lookup.
- `negotiate/index.mjs` — the Azure Functions **v4** registration shim (`app.http(...)`).
- `scripts/build-content.mjs` — the Decision-A bundle/manifest build step.
- `host.json`, `package.json` — minimal Functions app scaffolding.

## Routing (SWA) — Option 2 (pragmatic; true same-URL deferred)

Azure Static Web Apps route rules **cannot branch on the request `Accept` header**
(a documented platform limitation — `rewrite`/`redirect` are path-only). So true
same-URL negotiation (markdown at the page URL `/<slug>`) is **deferred** to the
Epic 3 native-client consumer. Instead:

- the page URL `/<slug>` stays **pure static HTML** (fast static host, untouched);
- raw markdown is exposed **now** at the linked Function endpoint
  `/api/negotiate/<slug>` (`Content-Type: text/markdown; charset=utf-8` +
  `Vary: Accept`, byte-faithful). The native client (Story 3.2) fetches markdown
  from there.

`web/public/staticwebapp.config.json` (ships into `web/dist/`) advertises
`Vary: Accept` on the `/api/negotiate/*` route and carries only correct config (no
misleading page-URL→Function rewrite). The deploy workflow
(`.github/workflows/deploy-web.yml`) installs API deps with **`npm ci`** (committed
`api/package-lock.json`) and sets `api_location: 'api'` so the Function is deployed
alongside `web/dist`.

## Local development / running the tests

```bash
# 1. Bundle the vault for the Function (Decision A) — generates the manifest + copy
#    + a bundled copy of the shared slug.mjs.
cd api && npm ci && npm run build

# 2a. CI backstop — the pure-handler + runtime unit/integration tests (no emulator):
cd api && npm test            # node --test → all green

# 2b. Local fidelity — the gated endpoint test against a running emulator:
#     Start ONE emulator first:
cd api && func start                          # Functions Core Tools (API alone)
#     or, full SWA fidelity (static host + linked API + staticwebapp.config.json):
swa start ../web/dist --api-location ../api
#     Then, in another shell:
cd api && RUN_SWA_E2E=1 BASE_URL=http://localhost:4280 npm run test:e2e
```

The endpoint test (`tests/negotiate.e2e.test.mjs`) is **gated** behind
`RUN_SWA_E2E=1` and stays skipped in CI (the pure-handler test is the always-green
backstop). It requires an Azure emulator, which is impractical in this CI.
