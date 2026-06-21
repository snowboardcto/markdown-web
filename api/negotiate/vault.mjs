// Story 2.7 — Task 2: the runtime slug -> raw `.md` Buffer lookup the Azure
// adapter hands to the pure `negotiate(...)` handler.
//
// Decision A (bundle-with-API): the build step (`scripts/build-content.mjs`)
// emits a verbatim copy of `content/**/*.md` into `./content/` plus a
// `manifest.json` mapping each slug (derived by the SHARED `pathToSlug`) to its
// relative file. At cold start we load the manifest and read each file as a raw
// Buffer (no `'utf8'` flag -> no re-encode, no BOM/CRLF mutation, AC6) into a
// CLOSED `Map`. The request slug is then a pure LOOKUP KEY (AC5): an unknown or
// hostile key is a clean miss by construction — the slug is NEVER concatenated
// into a filesystem path.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const CONTENT_DIR = path.join(__dirname, 'content');
const MANIFEST_PATH = path.join(__dirname, 'manifest.json');

let cachedMap = null;

/**
 * Build (once) the closed slug -> Buffer map from the bundled manifest + files.
 * If the bundle is missing (build step not run) it returns an EMPTY map so the
 * Function degrades to a uniform 404 rather than throwing at cold start.
 *
 * @returns {Map<string, Buffer>}
 */
export function loadVaultMap() {
  if (cachedMap) return cachedMap;
  const map = new Map();
  let manifest;
  try {
    manifest = JSON.parse(fs.readFileSync(MANIFEST_PATH, 'utf8'));
  } catch {
    // Cold-start health signal (#6): a MISSING/unreadable manifest means the
    // build step (`npm run build`, Decision A) did not run or was not packaged —
    // every markdown request would 404. Warn loudly rather than degrade silently.
    console.warn(
      `[negotiate] WARNING: manifest not found/readable at ${MANIFEST_PATH}; ` +
        'the content bundle is missing (build step did not run / was stripped from ' +
        'the deploy package) — ALL markdown requests will 404. Run `npm run build`.',
    );
    cachedMap = map; // no manifest -> empty closed map (all slugs miss -> 404).
    return cachedMap;
  }
  for (const [slug, rel] of Object.entries(manifest)) {
    if (typeof rel !== 'string') continue;
    // `rel` comes from OUR build manifest, not the request — but resolve+contain
    // anyway (belt-and-braces, AC5(c)): assert the file stays inside content/.
    const abs = path.resolve(CONTENT_DIR, rel);
    const root = CONTENT_DIR.endsWith(path.sep) ? CONTENT_DIR : CONTENT_DIR + path.sep;
    if (abs !== CONTENT_DIR && !abs.startsWith(root)) continue;
    try {
      map.set(slug, fs.readFileSync(abs)); // raw Buffer, no 'utf8' -> byte-exact
    } catch {
      // A manifest entry whose file is absent is simply skipped (miss -> 404).
    }
  }
  // A manifest that parsed but yielded no usable entries (e.g. the bundled
  // `content/**` was stripped from the package, Critical #2) is also a deploy
  // footgun — surface it instead of silently serving an all-404 Function.
  if (map.size === 0) {
    console.warn(
      `[negotiate] WARNING: manifest at ${MANIFEST_PATH} produced an EMPTY ` +
        'slug->content map; the bundled content/**/*.md is missing from the package ' +
        '(check `.funcignore` is not stripping negotiate/content) — ALL markdown ' +
        'requests will 404.',
    );
  }
  cachedMap = map;
  return cachedMap;
}

/**
 * The closed-map `readMd(slug)` the handler expects: returns the raw Buffer for
 * a KNOWN slug key, or `null` for an unknown/hostile key.
 *
 * @param {string} slug the request slug (a lookup key, never a path)
 * @returns {Buffer|null}
 */
export function readMd(slug) {
  const map = loadVaultMap();
  return typeof slug === 'string' && map.has(slug) ? map.get(slug) : null;
}
