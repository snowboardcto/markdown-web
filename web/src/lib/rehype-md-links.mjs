// Story 2.3 — the web half of FR-2 (inter-file link resolution).
//
// A build-time rehype (HAST) transformer: for every `<a href>` whose href is a
// RELATIVE `.md` link, rewrite it to the resolved page route the catch-all
// route actually emits, so clicking it is a normal full-page `<a>` navigation
// (JS-free, crawler-followable, back/forward via the browser's native history)
// to `/route` instead of a dead literal `foo.md` URL. The native client (Epic 3
// Story 3.5) performs the same resolution at render time — keep this rule in
// sync with that path.
//
// Resolution rules (all in slug space, POSIX forward-slash):
//   - Pass through unchanged: empty hrefs; any scheme (`http:`, `https:`,
//     `mailto:`, `tel:`, …); protocol-relative `//host`; root-absolute `/route`;
//     pure same-page `#anchor`. (AC3 + AC2 same-page anchor.)
//   - Only rewrite when the path part (after splitting off `#fragment` on the
//     FIRST `#` and dropping any `?query`) ends in `.md` (case-insensitive).
//     Relative non-`.md` targets (`report.pdf`, `media/x.jpg`) are left as-is —
//     media/asset resolution is Story 2.4. (AC3.)
//   - decodeURIComponent the path part before slugging so `My%20Notes.md` and
//     `My Notes.md` both -> `/my-notes`; a malformed `%`-escape leaves the link
//     UNREWRITTEN (degrade, never throw). (AC2.)
//   - Resolve the relative path against the CURRENT PAGE'S directory slug
//     (derived from the VFile source path), via `path.posix` join+normalize, so
//     `./`, `..`, and nested dirs resolve deterministically on every OS. (AC2.)
//   - Slug-normalise the resolved path with the SAME `pathToSlug` the route uses
//     (github-slugger lower-cases each segment; `index.md` collapses to its
//     parent route). (AC1/AC2.)
//   - A `..` chain that escapes the vault root leaves the link UNREWRITTEN (do
//     NOT clamp to root / emit a broken `/../`). (AC2.)
//   - An empty resolved slug (a `..`-chain or `index.md` that resolves exactly
//     to the vault root) emits `'/' + fragment` (e.g. `/` or `/#frag`), never
//     `//` or a bare empty href. (AC2.)
//   - The plugin does NOT check whether the target file exists: a missing target
//     still rewrites to its would-be route and lands on the custom 404 page —
//     "clear not-found, never a crash". (AC4.)
//
// VFile path contract: Astro passes the source `.md`'s absolute path on the
// VFile. We resolve it via an explicit fallback chain (file.path ->
// file.history[0] -> Astro frontmatter seam); if NONE yields a path inside the
// vault `content/` dir, ALL links on that page are left unrewritten (degrade,
// never crash) and the page is recorded for visibility. A degraded page emits
// literal `foo.md` hrefs, which 404 onto the not-found page — still AC4-safe.
import { visit } from 'unist-util-visit';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { pathToSlug } from './slug.mjs';

// Repo-root vault dir, resolved identically to the route layer's contentDir.
const contentDir = fileURLToPath(new URL('../../../content', import.meta.url));

// Pages whose VFile path could not be resolved inside the vault (the link
// rewrite was skipped). Surfaced so a future Astro VFile-shape change is a
// visible no-op-with-a-trail, not a silent regression.
const unresolvedPages = new Set();

/** Resolve the current page's source `.md` absolute path from the VFile. */
function resolveSourcePath(file) {
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

export default function rehypeMdLinks() {
  return (tree, file) => {
    const sourcePath = resolveSourcePath(file);
    if (!sourcePath) {
      // No usable path -> leave EVERY link on this page unrewritten (degrade).
      const id = (file && (file.path || (file.history && file.history[0]))) || '<unknown>';
      unresolvedPages.add(String(id));
      return;
    }

    // The page's own slug, then its directory slug (slug minus last segment) —
    // relative links resolve against the page's location, not the site root.
    const relPosix = path.relative(contentDir, sourcePath).split(path.sep).join('/');
    const pageSlug = pathToSlug(relPosix);
    const segs = pageSlug.split('/');
    segs.pop();
    const pageDirSlug = segs.join('/');

    visit(tree, 'element', (node) => {
      if (node.tagName !== 'a' || !node.properties) return;
      const href = node.properties.href;
      if (typeof href !== 'string' || href === '') return;

      // Pass-throughs: scheme (http:/mailto:/tel:/…), protocol-relative,
      // root-absolute, pure same-page anchor.
      if (
        /^[a-z][a-z0-9+.-]*:/i.test(href) ||
        href.startsWith('//') ||
        href.startsWith('/') ||
        href.startsWith('#')
      ) {
        return;
      }

      // Split off the fragment on the FIRST `#` only; drop any `?query`.
      const hashAt = href.indexOf('#');
      const fragment = hashAt === -1 ? '' : href.slice(hashAt);
      const beforeHash = hashAt === -1 ? href : href.slice(0, hashAt);
      const pathNoQuery = beforeHash.split('?')[0];

      // Only relative `.md` targets are rewritten (non-.md assets are 2.4).
      if (!/\.md$/i.test(pathNoQuery)) return;

      // Decode before slugging; a malformed `%`-escape leaves the link as-is.
      let decoded;
      try {
        decoded = decodeURIComponent(pathNoQuery);
      } catch {
        return;
      }

      // Resolve against the page's directory in slug space (POSIX only).
      const joined = path.posix.normalize(
        path.posix.join(pageDirSlug, decoded),
      );

      // A `..` that escapes the vault root -> leave unrewritten (no clamp).
      if (joined === '..' || joined.startsWith('../')) return;

      // Strip a residual `./` that normalize can leave for same-dir targets.
      const cleaned = joined.replace(/^\.\//, '');
      const routeSlug = pathToSlug(cleaned);

      // Empty slug (vault root) -> `/` (+ fragment), never `//` or empty.
      node.properties.href = '/' + routeSlug + fragment;
    });
  };
}

/** Test/diagnostic accessor: pages whose VFile path was unusable. */
export function getUnresolvedPages() {
  return [...unresolvedPages];
}
