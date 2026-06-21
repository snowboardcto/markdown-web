# web/ — Browser / HTML delivery path (Astro)

The web reader (FR-5–8). Astro renders the `content/` vault to static HTML at build time.

- **Now (Story 1.1):** a minimal Astro 5 project with a single placeholder page that builds
  (`astro build` → `dist/`). This is what Stories 1.3/1.4 deploy.
- **Later (Epic 2):** remark/rehype (GFM) + Shiki syntax highlighting + a GitHub-style stylesheet.

## Scripts

- `npm run dev` — local dev server
- `npm run build` — production build to `dist/`
- `npm run preview` — preview the production build

`node_modules/` and `dist/` are gitignored at the repo root.
