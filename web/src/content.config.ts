import { defineCollection } from 'astro:content';
import { glob } from 'astro/loaders';
import { fileURLToPath } from 'node:url';

// Story 2.1 — source pages from the repo-root `content/` vault (single source of
// truth, AC4). The vault lives one level above `web/`; resolve an absolute path
// from this config file's location so the `glob()` base is robust both locally
// and in CI (where the build runs from `web/`). We never copy `.md` into `web/`.
const contentBase = fileURLToPath(new URL('../../content', import.meta.url));

const pages = defineCollection({
  loader: glob({ base: contentBase, pattern: '**/*.md' }),
});

export const collections = { pages };
