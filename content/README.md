# content/ — the Vault

Single source of truth for the product. Plain `.md` files plus media live here and are
consumed by **both** delivery paths:

- `web/` reads this content at **build time** (Astro → static HTML).
- the native Windows client reads it at **runtime** via `api/` (raw `.md`).

No content ever lives in code. Covers FR-1–4. Media goes under `content/media/`.
