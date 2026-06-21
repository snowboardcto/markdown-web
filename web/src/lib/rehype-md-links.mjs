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
import path from 'node:path';
import { pathToSlug } from './slug.mjs';
// Story 2.4 — the VFile->source-path + page-dir-slug derivation was extracted
// into the shared `page-path.mjs` so this link plugin and the media plugin
// resolve relative references against the page's location identically (anti-drift).
import { resolveSourcePath, pageDirSlugFromSource } from './page-path.mjs';

// Pages whose VFile path could not be resolved inside the vault (the link
// rewrite was skipped). Surfaced so a future Astro VFile-shape change is a
// visible no-op-with-a-trail, not a silent regression.
const unresolvedPages = new Set();

export default function rehypeMdLinks() {
  return (tree, file) => {
    const sourcePath = resolveSourcePath(file);
    if (!sourcePath) {
      // No usable path -> leave EVERY link on this page unrewritten (degrade).
      const id = (file && (file.path || (file.history && file.history[0]))) || '<unknown>';
      unresolvedPages.add(String(id));
      // F4: surface the degrade loudly at build time. Without this, a future
      // Astro VFile-shape change that yields no usable source path would
      // silently disable ALL link rewriting site-wide on a green build. Warn
      // once per newly-degraded page so the no-op-with-a-trail is visible.
      console.warn(
        `[rehype-md-links] Could not resolve a vault source path for "${String(id)}"; ` +
          'all relative .md links on this page were left UNREWRITTEN. ' +
          'If this is unexpected, the Astro VFile contract may have changed.',
      );
      return;
    }

    // The page's directory slug — relative links resolve against the page's
    // location, not the site root (the F2 derivation now lives in page-path.mjs,
    // shared with the media plugin so both resolve the page dir identically).
    const pageDirSlug = pageDirSlugFromSource(sourcePath);

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

      // F1: decode PER SEGMENT (split on the *encoded* path's `/` first, then
      // decode each piece individually) so an encoded separator cannot introduce
      // a NEW path separator. A decoded segment that *itself* contains a `/`
      // means the author smuggled a `%2F` past the pass-through guards (which ran
      // on the still-encoded href): `%2Ffoo.md` -> a segment decoding to `/foo.md`
      // (a leading slash -> a protocol-relative `//foo` off-site href), and
      // `a%2Fb.md` -> a segment decoding to `a/b.md` (one filename split into two
      // route segments). In either case leave the link UNREWRITTEN rather than
      // emit a mis-routed / off-site href. A malformed `%`-escape (`bad%zz.md`)
      // throws in decodeURIComponent and likewise leaves the link as-is (degrade,
      // never throw).
      let decoded;
      try {
        const encodedSegments = pathNoQuery.split('/');
        const decodedSegments = encodedSegments.map((s) => decodeURIComponent(s));
        // Reject if decoding manufactured a new separator inside any segment.
        if (decodedSegments.some((s) => s.includes('/'))) return;
        decoded = decodedSegments.join('/');
      } catch {
        return;
      }

      // F1 belt-and-suspenders: a *single* encoded segment must not, after
      // decoding, look like an absolute path or a scheme — that means an encoded
      // char (`%2F`, `%3A`) smuggled a separator/scheme past the pass-through
      // guards (which ran on the still-encoded href). A legitimately-authored
      // `../foo.md` is NOT rejected here — the `..`-escape is handled *after*
      // `posix.join` below, so real parent-relative links still resolve.
      if (decoded.startsWith('/') || /^[a-z][a-z0-9+.-]*:/i.test(decoded)) {
        return;
      }

      // Resolve against the page's directory in slug space (POSIX only).
      const joined = path.posix.normalize(
        path.posix.join(pageDirSlug, decoded),
      );

      // A `..` that escapes the vault root -> leave unrewritten (no clamp).
      if (joined === '..' || joined.startsWith('../')) return;

      // F1: a leading `/` after join (from an absolute decoded path) would
      // produce a protocol-relative `//...` href — leave unrewritten.
      if (joined.startsWith('/')) return;

      // Strip a residual `./` that normalize can leave for same-dir targets.
      const cleaned = joined.replace(/^\.\//, '');
      const routeSlug = pathToSlug(cleaned);

      // F3: a degenerate `.md`-only basename (`.md`, `./.md`, `...md`) slugs to
      // an empty route *without* having resolved to the vault root via a real
      // `index`/`..` chain — it would silently rewrite garbage to `/` (or
      // `/sub/`). Only emit the empty-slug vault-root href when the resolved
      // path actually collapses to the root (the joined path is empty or an
      // `index` route); otherwise leave the link UNREWRITTEN.
      if (routeSlug === '') {
        const collapsesToRoot =
          cleaned === '' ||
          cleaned === '.' ||
          /(^|\/)index$/i.test(cleaned.replace(/\.md$/i, ''));
        if (!collapsesToRoot) return;
      }

      // Empty slug (vault root) -> `/` (+ fragment), never `//` or empty.
      node.properties.href = '/' + routeSlug + fragment;
    });
  };
}

/** Test/diagnostic accessor: pages whose VFile path was unusable. */
export function getUnresolvedPages() {
  return [...unresolvedPages];
}
