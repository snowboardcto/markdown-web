// Story 2.4 — the single source of truth for resolving a markdown page's vault
// source path and its directory slug from the unified VFile, shared by BOTH
// rehype plugins (`rehype-md-links.mjs` for `<a href>` and `rehype-md-media.mjs`
// for media `src`/`poster`) so the two relative-path resolvers can never drift.
//
// Extracted verbatim from Story 2.3's `rehype-md-links.mjs` (the
// `resolveSourcePath` fallback chain + the F2 page-dir-slug derivation): both
// plugins must resolve a relative reference against the CURRENT PAGE'S directory
// identically, so the link route and the media served-path agree on what "the
// page's location" is.
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { pathToSlug } from './slug.mjs';

// Repo-root vault dir, resolved identically to the route layer's contentDir
// (`web/src/content.config.ts` resolves the same `content/` from its own
// location). From `web/src/lib/page-path.mjs`, three levels up is the repo root.
export const contentDir = fileURLToPath(new URL('../../../content', import.meta.url));

/**
 * Resolve the current page's source `.md` absolute path from the VFile.
 *
 * Astro passes the source `.md`'s absolute path on the VFile; resolve it via an
 * explicit fallback chain (file.path -> file.history[0] -> the Astro frontmatter
 * seam). If NONE yields a path inside the vault `content/` dir, return null so
 * the caller can degrade (leave all references on that page unrewritten) rather
 * than crash. This makes a future Astro VFile-shape change a visible no-op.
 *
 * @param {any} file the unified VFile
 * @returns {string|null} the absolute source path inside `content/`, or null
 */
export function resolveSourcePath(file) {
  const candidate =
    (file && typeof file.path === 'string' && file.path) ||
    (file && Array.isArray(file.history) && file.history[0]) ||
    (file && file.data && file.data.astro && file.data.astro.frontmatter
      ? file.data.astro.frontmatter.__sourcePath
      : undefined) ||
    '';
  if (!candidate) return null;
  // Must be inside the vault content/ dir, else treat as "no usable path".
  const normalized = path.normalize(candidate);
  const dirWithSep = contentDir.endsWith(path.sep) ? contentDir : contentDir + path.sep;
  if (!normalized.startsWith(dirWithSep)) return null;
  return normalized;
}

/**
 * Derive the current page's DIRECTORY slug from its absolute source path.
 *
 * F2: derive the directory from the SOURCE FILE PATH *before* any index-collapse.
 * Slugging the page path first and popping the last segment is wrong for
 * `content/<dir>/index.md`, whose page slug index-collapses to `<dir>` so the pop
 * yields `''` (root) instead of `<dir>`. Take the file's directory (drop the
 * basename) and slug THAT, so `sub/index.md` and `sub/page.md` both yield a page
 * dir slug of `sub`. Relative references resolve against THIS, not the site root.
 *
 * @param {string} sourcePath an absolute path inside `content/`
 * @returns {string} the page's directory slug (no leading/trailing `/`), `''` at root
 */
export function pageDirSlugFromSource(sourcePath) {
  const relPosix = path.relative(contentDir, sourcePath).split(path.sep).join('/');
  const lastSlash = relPosix.lastIndexOf('/');
  const relDirPosix = lastSlash === -1 ? '' : relPosix.slice(0, lastSlash);
  return relDirPosix === '' ? '' : pathToSlug(relDirPosix);
}

/**
 * Derive the current page's VERBATIM directory (relative to `content/`) from its
 * absolute source path — the on-disk directory name(s), NOT slugged.
 *
 * Story 2.4 review fix #1 (CRITICAL): media assets are NOT routes, so a media
 * reference must resolve against the page's REAL on-disk directory, not its
 * slugged route directory. The copy step (`copy-vault-media.mjs`) walks
 * `content/` and writes assets to `dist/` VERBATIM (e.g. `content/My Dir/pic.png`
 * -> `dist/My Dir/pic.png`). If the media rewrite resolved against the SLUGGED
 * dir (`my-dir`) it would emit `src="/my-dir/pic.png"` while the file lives at
 * `dist/My Dir/pic.png` -> a guaranteed 404. Resolving against this verbatim dir
 * keeps the rewritten `src` and the copied file path in lock-step.
 *
 * Unlike `pageDirSlugFromSource`, this is for the MEDIA plugin only — `.md`
 * links DO slug (they target routes), media does not (it targets files).
 *
 * @param {string} sourcePath an absolute path inside `content/`
 * @returns {string} the page's verbatim directory (POSIX, no leading/trailing
 *   `/`), `''` at the vault root
 */
export function pageDirFromSource(sourcePath) {
  const relPosix = path.relative(contentDir, sourcePath).split(path.sep).join('/');
  const lastSlash = relPosix.lastIndexOf('/');
  return lastSlash === -1 ? '' : relPosix.slice(0, lastSlash);
}
