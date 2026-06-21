// Story 2.4 — the web half of FR-3 (inline media): rewrite relative media
// references to their served root-absolute URLs at build time.
//
// A build-time rehype (HAST) transformer: for every media element
// (`<img>`/`<video>`/`<audio>`/`<source>`) whose `src` (and a `<video>`'s
// `poster`) is a RELATIVE reference, rewrite it to the served `/…` path that the
// build-time vault-media copy (the `astro:build:done` hook in `astro.config.mjs`)
// places the asset at. The emitted HTML is plain static `<img src="/…">` /
// `<video>` — crawler-visible, JS-free. The native client (Epic 3 Story 3.5)
// performs the same resolution against the vault at render time — keep this rule
// in sync with that path.
//
// This is the exact analogue of Story 2.3's `<a href>` rewrite, with the SAME
// page-relative resolution and the SAME F1 per-segment-decode smuggling guards —
// reused from `page-path.mjs` so the two plugins can't drift. Three deliberate
// differences from the link plugin:
//   1. It visits media elements (`img`/`video`/`audio`/`source`), not `<a>`.
//   2. It rewrites `src` (all four) AND `poster` (on `<video>`), not `href`.
//   3. **Assets are NOT slugged.** A `.md` link target is `pathToSlug`'d because
//      it must match an emitted ROUTE; an asset must point at the literal
//      on-disk file, so only `./`/`..`/nested-dir resolution is applied — the
//      asset's own segments/filename are preserved BYTE-FOR-BYTE (case + all).
//      A slugged `src` would 404 (no file at the slugged name). We decode per
//      segment for path coherence and re-encode each segment on emit so the
//      served URL, when the browser decodes it, byte-matches the on-disk name.
//
// Path-coherence invariant (cross-referenced in the copy hook): both the rewrite
// and the copy resolve to the asset's path RELATIVE TO `content/`, verbatim,
// root-absolutised with a leading `/`. `content/media/powder.jpg` copies to
// `dist/media/powder.jpg` AND the rewrite emits `/media/powder.jpg`.
//
// Resolution rules (page-relative, POSIX forward-slash):
//   - Pass through unchanged (NOT rewritten, NOT copied): empty/missing/non-string
//     values; any scheme (`http:`/`https:`/`data:`/…); protocol-relative `//host`;
//     root-absolute `/served`. (AC3.)
//   - Only RELATIVE values are rewritten. A trailing `?query` is stripped from
//     the path used to resolve/copy (the served file is the path part). A value
//     bearing a `#fragment` is treated as not-a-clean-relative-asset and left
//     UNREWRITTEN (a fragment on a media `src` is non-standard; row 16 decision).
//   - decode PER SEGMENT (F1): split on `/` first, then decodeURIComponent each
//     segment; reject if a decoded segment manufactures a `/` or a leading-slash/
//     scheme (an encoded-separator smuggle), or if a `%`-escape is malformed —
//     leave UNREWRITTEN, never throw. (AC2 rows 12-14.)
//   - Resolve the decoded relative path against the page's VERBATIM directory
//     (relative to content/, NOT slugged — assets are files, not routes; review
//     fix #1) via `path.posix` join+normalize; a `..` that escapes the vault
//     root, or a leading `/`, leaves the value UNREWRITTEN (no clamp). A
//     directory/empty basename (trailing `/`) is not a servable asset ->
//     UNREWRITTEN. (rows 11/19.) Resolving against the verbatim (not slugged)
//     page dir is what keeps the emitted src in lock-step with the verbatim copy:
//     a page in `content/My Dir/` references `pic.png` -> `/My Dir/pic.png`, the
//     exact path the copy hook writes — a SLUGGED `my-dir` would 404.
//   - Emit `'/' + segments.map(encodeURIComponent).join('/')` — root-absolute,
//     with the verbatim (decoded) on-disk segments re-encoded URL-safely.
//   - Existence is NOT checked: a missing asset rewrites to its would-be path
//     and 404s (broken image), never a build crash. (AC6.)
import { visit } from 'unist-util-visit';
import path from 'node:path';
import { resolveSourcePath, pageDirFromSource } from './page-path.mjs';

// Media elements + the attribute(s) on each that carry a (possibly relative)
// served-asset reference. `<video>` carries BOTH `src` and `poster` (rewrite
// each independently). `<source>` (nested in `<video>`/`<audio>`) is visited as
// its own element node, so it is handled by this table, not by recursing.
// `srcset` (img/source) and `<track src>` (.vtt captions) are NOT handled in this
// story — see the Dev Agent Record "not-yet-handled" note.
const MEDIA_ATTRS = {
  img: ['src'],
  video: ['src', 'poster'],
  audio: ['src'],
  source: ['src'],
};

// Pages whose VFile path could not be resolved inside the vault (media rewrite
// skipped). Surfaced so a future Astro VFile-shape change is a visible no-op.
const unresolvedPages = new Set();

// Pages where the astro:assets bypass seam (`file.data.astro`) was absent or
// shape-changed (review fix #4): if this seam disappears, Astro's internal
// `rehypeImages` stays live and its unconditional `decodeURI`/`getImage` would
// re-introduce the AC6 build crash on a malformed/missing reference. We warn
// once per page so a silent re-crash regression is visible, and the plugin
// no-ops the bypass safely rather than throwing.
const bypassSeamMissing = new Set();

// The set of relative asset paths (relative to content/, verbatim) the rewrite
// emitted a served URL for, deduped across pages. The `astro:build:done` copy
// hook can diff this against what actually exists in `content/` to warn (once)
// about referenced-but-missing assets (AC6 visibility nicety) — never throws.
const referencedAssets = new Set();

/**
 * Resolve one authored relative media reference to its served root-absolute path,
 * or return null to leave it UNREWRITTEN (pass-through / smuggle / escape).
 *
 * @param {string} value the authored attribute value
 * @param {string} pageDir the current page's VERBATIM directory (relative to
 *   content/, NOT slugged — assets are files, not routes; review fix #1)
 * @returns {string|null} the served `/…` path, or null to leave as-is
 */
function resolveMediaRef(value, pageDir) {
  // Pass-throughs: scheme (http:/https:/data:/…), protocol-relative, root-absolute.
  if (
    /^[a-z][a-z0-9+.-]*:/i.test(value) ||
    value.startsWith('//') ||
    value.startsWith('/')
  ) {
    return null;
  }

  // A `#fragment` on a media src is non-standard/N/A — leave UNREWRITTEN (row 16).
  if (value.includes('#')) return null;

  // Strip a trailing `?query` (a cache-buster on the URL, not the on-disk name).
  const pathNoQuery = value.split('?')[0];
  if (pathNoQuery === '') return null; // e.g. `?v=2` with no path -> not servable.

  // A value resolving to a directory (trailing slash, empty basename) is not a
  // servable asset (row 19).
  if (pathNoQuery.endsWith('/')) return null;

  // F1: decode PER SEGMENT so an encoded separator/leading-slash cannot
  // manufacture a new path part. `a%2Fb.jpg` -> a segment decoding to `a/b.jpg`
  // (a smuggled separator); `%2Fetc/x.png` -> a leading `/` (would emit `//`).
  // Either case -> leave UNREWRITTEN. A malformed `%`-escape throws -> degrade.
  let decodedSegments;
  try {
    const encodedSegments = pathNoQuery.split('/');
    decodedSegments = encodedSegments.map((s) => decodeURIComponent(s));
    if (decodedSegments.some((s) => s.includes('/'))) return null;
  } catch {
    return null;
  }
  const decoded = decodedSegments.join('/');

  // Belt-and-suspenders: a decoded value that looks absolute or scheme-like means
  // a smuggled separator/scheme slipped past the still-encoded pass-through guards.
  if (decoded.startsWith('/') || /^[a-z][a-z0-9+.-]*:/i.test(decoded)) return null;

  // Resolve against the page's VERBATIM directory (POSIX). Assets are NOT slugged
  // and the page dir is NOT slugged either (review fix #1): the copy step writes
  // assets to dist/ at their verbatim content/-relative path, so the served `src`
  // must use the same verbatim directory or it 404s. Only `./`/`..`/nested-dir is
  // resolved; the segments stay byte-for-byte.
  const joined = path.posix.normalize(path.posix.join(pageDir, decoded));

  // A `..` that escapes the vault root -> leave unrewritten (no clamp).
  if (joined === '..' || joined.startsWith('../')) return null;
  // A leading `/` after join (from an absolute decoded path) -> unrewritten.
  if (joined.startsWith('/')) return null;

  // Strip a residual `./` that normalize can leave for same-dir targets.
  const cleaned = joined.replace(/^\.\//, '');
  if (cleaned === '' || cleaned === '.') return null; // not a servable asset.

  // Record the verbatim relative path (for the copy hook's missing-asset diff).
  referencedAssets.add(cleaned);

  // Re-encode each segment URL-safely so the served `src`, when the browser
  // decodes it, byte-matches the on-disk name (round-trips spaces/non-ASCII).
  const servedPath = cleaned
    .split('/')
    .map((s) => encodeURIComponent(s))
    .join('/');
  return '/' + servedPath;
}

export default function rehypeMdMedia() {
  return (tree, file) => {
    // astro:assets BYPASS. Astro's internal `rehypeImages` (which runs AFTER
    // this plugin) rewrites any markdown `<img>` whose `src` is in
    // `vfile.data.astro.localImagePaths` into an optimised `/_astro/*.webp`
    // (via `getImage`), and it unconditionally `decodeURI`s every `<img src>` —
    // which THROWS on a malformed `%`-escape (`bad%zz.jpg`) and CRASHES on a
    // missing asset. We rewrite ALL relative media `src`/`poster` ourselves
    // (verbatim served copy, not optimised), so astro:assets is fully redundant
    // here. Clear the collected image-path sets so `rehypeImages` early-returns:
    // markdown `![](media/x.jpg)` then flows through our rewrite to a plain
    // served `<img src="/media/x.jpg">` (AC1 verbatim), and a missing/malformed
    // reference never takes the build down via the assets pipeline (AC6). This is
    // the cleanest Astro-5 bypass — no `<Image>`/image-service override needed.
    //
    // Resilience (review fix #4): this seam is undocumented Astro internals. If
    // `file.data.astro` is absent or its shape changes, the bypass silently
    // no-ops while `rehypeImages` stays live -> the exact AC6 build crash could
    // return with no warning. Guard so we NEVER throw here, and warn once per
    // page if the seam is missing so a re-crash regression is visible rather than
    // silent. (We can only no-op safely; we cannot synthesise the seam.)
    try {
      const astroData = file && file.data && file.data.astro;
      if (astroData && typeof astroData === 'object') {
        astroData.localImagePaths = [];
        astroData.remoteImagePaths = [];
      } else {
        const seamId =
          (file && (file.path || (file.history && file.history[0]))) || '<unknown>';
        if (!bypassSeamMissing.has(String(seamId))) {
          bypassSeamMissing.add(String(seamId));
          console.warn(
            `[rehype-md-media] astro:assets bypass seam (file.data.astro) was absent for ` +
              `"${String(seamId)}". The media src rewrite still runs, but Astro's internal ` +
              'rehypeImages was NOT neutralised — if the Astro VFile contract changed, a ' +
              'malformed/missing markdown image reference could crash the build (AC6 regression).',
          );
        }
      }
    } catch (err) {
      // Never let the bypass itself take the build down.
      console.warn(
        `[rehype-md-media] astro:assets bypass step threw and was ignored: ${err && err.message}`,
      );
    }

    const sourcePath = resolveSourcePath(file);
    if (!sourcePath) {
      // No usable path -> leave EVERY media ref on this page unrewritten (degrade).
      const id = (file && (file.path || (file.history && file.history[0]))) || '<unknown>';
      if (!unresolvedPages.has(String(id))) {
        unresolvedPages.add(String(id));
        // Surface the degrade loudly (mirrors rehype-md-links.mjs F4): a future
        // Astro VFile-shape change would otherwise silently disable ALL media
        // rewriting site-wide on a green build.
        console.warn(
          `[rehype-md-media] Could not resolve a vault source path for "${String(id)}"; ` +
            'all relative media src/poster on this page were left UNREWRITTEN. ' +
            'If this is unexpected, the Astro VFile contract may have changed.',
        );
      }
      return;
    }

    // Verbatim page directory (NOT slugged) — assets are files, not routes
    // (review fix #1). The copy step preserves on-disk dir names; the rewrite
    // must too, or the served `src` won't match the copied path.
    const pageDir = pageDirFromSource(sourcePath);

    visit(tree, 'element', (node) => {
      if (!node.properties) return;
      const attrs = MEDIA_ATTRS[node.tagName];
      if (!attrs) return;
      for (const attr of attrs) {
        const value = node.properties[attr];
        // Attribute-value-shape defensiveness: HAST can carry non-string/array
        // property values; only process a non-empty string.
        if (typeof value !== 'string' || value === '') continue;
        const resolved = resolveMediaRef(value, pageDir);
        if (resolved !== null) node.properties[attr] = resolved;
      }
    });
  };
}

/** Test/diagnostic accessor: pages whose VFile path was unusable. */
export function getUnresolvedPages() {
  return [...unresolvedPages];
}

/** The set of relative asset paths (relative to content/) the rewrite emitted. */
export function getReferencedAssets() {
  return [...referencedAssets];
}
