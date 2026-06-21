// Story 2.7 — Task 1 (Decision A): make the raw `.md` available to the Azure
// Function, KEYED BY SLUG, by BUNDLING a verbatim copy of the repo-root vault
// with the API + a generated slug -> file manifest.
//
// WHY Decision A (bundle-with-API) over B (emit into dist/) or C (fetch at
// runtime): the Function owns its own data — no network hop, no cross-origin
// coupling, no runtime dependency on the static origin being up; byte-fidelity
// (AC6) is trivial because the copy is a verbatim `fs.copyFile` (never a
// read-as-text rewrite that could inject a BOM or rewrite CRLF); and the closed
// bundled file set yields a closed slug -> file lookup map (AC5) by construction.
// The one cost — a copy step must run before the API is packaged — is mitigated
// by this script (the existing `copy-vault-media.mjs` is the precedent).
//
// Output (under api/negotiate/, both git-ignored build artifacts):
//   - api/negotiate/content/**          verbatim byte-copy of every content/*.md
//   - api/negotiate/manifest.json       { "<slug>": "<relPosixPath>", ... }
//
// The slug derivation REUSES the web build's single source of truth
// (`web/src/lib/slug.mjs` `pathToSlug`) so the markdown URL and the HTML URL can
// never drift (AC4), and HONORS the build's fail-loud collision contract
// (`[...slug].astro` throws on two files -> one slug) — this script throws too.
//
// Run: `node scripts/build-content.mjs` from `api/` (or `npm run build` in api/).

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { pathToSlug } from '../../web/src/lib/slug.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(__dirname, '../..');
const CONTENT_ROOT = path.join(REPO_ROOT, 'content');
const OUT_CONTENT = path.resolve(__dirname, '../negotiate/content');
const OUT_MANIFEST = path.resolve(__dirname, '../negotiate/manifest.json');
// The web build's slug source of truth, BUNDLED with the Function so the runtime
// can normalise a request slug (lowercase/github-slug each segment) with the SAME
// rule the map keys were built from — no drift (AC4), and no `web/` dependency in
// the deployed package (only `api/` ships).
const WEB_SLUG_SRC = path.resolve(__dirname, '../../web/src/lib/slug.mjs');
const OUT_SLUG = path.resolve(__dirname, '../negotiate/slug.mjs');

/** Recursively list every `.md` file under `dir` (absolute paths). */
function listMarkdownFiles(dir) {
  const out = [];
  for (const dirent of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, dirent.name);
    if (dirent.isDirectory()) out.push(...listMarkdownFiles(full));
    else if (dirent.isFile() && path.extname(dirent.name).toLowerCase() === '.md')
      out.push(full);
  }
  return out;
}

function relPosix(absPath) {
  return path.relative(CONTENT_ROOT, absPath).split(path.sep).join('/');
}

function main() {
  const files = listMarkdownFiles(CONTENT_ROOT);

  // Build the slug -> file map with the SHARED pathToSlug, failing loud on a
  // collision exactly as the web route does (no silent drop of a colliding pair).
  const manifest = {};
  const seen = new Map();
  for (const abs of files) {
    const rel = relPosix(abs);
    const slug = pathToSlug(rel);
    const prior = seen.get(slug);
    if (prior !== undefined) {
      throw new Error(
        `Duplicate page slug "${slug}" from content files "${prior}" and "${rel}". ` +
          'Two .md files normalise to the same route; rename one so each page has a unique slug.',
      );
    }
    seen.set(slug, rel);
    manifest[slug] = rel;
  }

  // Fresh output dir (idempotent), then VERBATIM byte-copy each .md.
  fs.rmSync(OUT_CONTENT, { recursive: true, force: true });
  fs.mkdirSync(OUT_CONTENT, { recursive: true });
  for (const abs of files) {
    const rel = relPosix(abs);
    const dest = path.join(OUT_CONTENT, ...rel.split('/'));
    fs.mkdirSync(path.dirname(dest), { recursive: true });
    // Faithful binary copy (no encoding transform, no BOM injection) — AC6.
    fs.copyFileSync(abs, dest);
  }

  fs.writeFileSync(OUT_MANIFEST, JSON.stringify(manifest, null, 2) + '\n');

  // Bundle the shared slug logic verbatim so the runtime adapter normalises a
  // request slug with the SAME `pathToSlug` the manifest keys came from.
  fs.copyFileSync(WEB_SLUG_SRC, OUT_SLUG);

  console.log(
    `[build-content] Bundled ${files.length} .md file(s) -> api/negotiate/content/, ` +
      `wrote ${Object.keys(manifest).length} slug(s) -> api/negotiate/manifest.json, ` +
      `and bundled slug.mjs -> api/negotiate/slug.mjs.`,
  );
}

main();
