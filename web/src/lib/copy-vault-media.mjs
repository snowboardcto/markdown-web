// Story 2.4 — serve vault media: copy `content/`'s non-`.md` assets into the
// build output (`web/dist`) so a rewritten `<img src="/media/powder.jpg">`
// actually resolves to a served 200.
//
// An Astro integration that hooks `astro:build:done` (fires after `dist/` is
// written, exposes the `dir` output URL) and faithfully BINARY-copies every
// non-`.md`, non-dotfile, non-cruft file from the repo-root `content/**` tree
// into `dist/**` at the SAME relative path — so `content/media/powder.jpg` ->
// `dist/media/powder.jpg`, served at `/media/powder.jpg`.
//
// Why a build-time copy and NOT Astro `public/`: putting media under
// `web/public/media/` would duplicate vault content into `web/`, violating the
// architecture's hard rule "content/ is the single source of truth … No content
// in code" and desyncing from the vault. A symlink is fragile across OS/CI and
// Astro may not follow it. This copy keeps `content/` canonical and is
// deterministic on every OS/CI.
//
// Path-coherence invariant (cross-referenced in `rehype-md-media.mjs`): both the
// copy and the rewrite resolve to the asset's path relative to `content/`,
// verbatim (no slugging), root-absolutised. They MUST agree on the served path.
//
// Missing-asset policy (AC6): the copy copies only files that EXIST in
// `content/`. A referenced-but-missing asset simply has no file -> its rewritten
// `/media/missing.jpg` 404s (broken image), never a build crash. We additionally
// WARN (do not fail) listing referenced-but-missing assets — a typo is visible,
// not silent. Review fix #3: "referenced" is sourced by SCANNING the emitted
// `dist/**/*.html` for root-absolute media `src`/`poster` (something this hook
// can actually see) — the earlier approach diffed a rehype module-level Set that
// is empty in the integration's Vite module instance, so the warn was dead code.
// The non-negotiable floor: the copy NEVER throws mid-build on a missing/odd asset.
import { fileURLToPath } from 'node:url';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { contentDir } from './page-path.mjs';

// Files we must NOT copy into the served `dist`: pages (`.md`), dotfiles
// (`.gitkeep`/`.DS_Store`/`.gitignore`/…), README*, and editor/OS cruft.
function isExcluded(basename) {
  if (basename.startsWith('.')) return true; // all dotfiles
  if (/\.md$/i.test(basename)) return true; // pages, incl. README.md
  if (/^README(\.|$)/i.test(basename)) return true; // README, README.txt, …
  if (basename === 'Thumbs.db' || basename === 'desktop.ini') return true; // OS cruft
  if (/\.swp$/i.test(basename)) return true; // editor swap files
  return false;
}

// Cap on directory recursion depth — a backstop against a pathological tree or a
// symlink cycle the realpath-visited set somehow misses (review fix #6).
const MAX_WALK_DEPTH = 64;

/**
 * Recursively collect copyable asset paths (relative to `content/`, POSIX),
 * skipping excluded files, and NOT following symlinks out of the vault.
 *
 * Review fix #6: a cyclic directory-symlink chain wholly inside `content/`
 * (which passes the "escapes content/" guard) would otherwise recurse forever ->
 * `RangeError: Maximum call stack size`. We track VISITED real directory paths
 * and cap recursion depth so a cycle terminates (warn + skip) instead of
 * blowing the stack. The whole walk is wrapped by the caller in a try/catch
 * floor too, but this keeps the common case from ever throwing.
 *
 * @param {string} absDir an absolute directory inside `content/`
 * @param {string} relDir its path relative to `content/` (POSIX, '' at root)
 * @param {string[]} out accumulator of POSIX relative asset paths
 * @param {Set<string>} visited real directory paths already walked (cycle guard)
 * @param {number} depth current recursion depth
 */
function collectAssets(absDir, relDir, out, visited, depth) {
  if (depth > MAX_WALK_DEPTH) {
    console.warn(
      `[copy-vault-media] Skipping ${relDir || '.'}: max directory depth (${MAX_WALK_DEPTH}) ` +
        'exceeded (possible symlink cycle).',
    );
    return;
  }
  // Cycle guard: never re-walk a real directory path we've already entered.
  let realDir;
  try {
    realDir = fs.realpathSync(absDir);
  } catch {
    realDir = absDir;
  }
  if (visited.has(realDir)) {
    console.warn(
      `[copy-vault-media] Skipping ${relDir || '.'}: directory already visited ` +
        '(symlink cycle guard).',
    );
    return;
  }
  visited.add(realDir);

  let entries;
  try {
    entries = fs.readdirSync(absDir, { withFileTypes: true });
  } catch {
    return; // missing/unreadable dir (e.g. no content/media yet) -> clean no-op.
  }
  for (const entry of entries) {
    const name = entry.name;
    if (isExcluded(name)) continue;
    const abs = path.join(absDir, name);
    const rel = relDir === '' ? name : relDir + '/' + name;
    if (entry.isSymbolicLink()) {
      // Defensive: do NOT follow a symlink out of the vault (path-escape /
      // arbitrary-file-read). Copy only if the resolved target is inside content/.
      let real;
      try {
        real = fs.realpathSync(abs);
      } catch {
        continue;
      }
      const dirWithSep = contentDir.endsWith(path.sep) ? contentDir : contentDir + path.sep;
      if (!real.startsWith(dirWithSep)) {
        console.warn(`[copy-vault-media] Skipping symlink escaping content/: ${rel}`);
        continue;
      }
      let stat;
      try {
        stat = fs.statSync(abs);
      } catch {
        continue;
      }
      if (stat.isDirectory()) collectAssets(abs, rel, out, visited, depth + 1);
      else if (stat.isFile()) out.push(rel);
    } else if (entry.isDirectory()) {
      collectAssets(abs, rel, out, visited, depth + 1);
    } else if (entry.isFile()) {
      out.push(rel);
    }
  }
}

/** Byte-identical check via SHA-256 (review fix #5: hash, not size, decides "is
 * this my own prior copy?" before any overwrite). */
function filesAreIdentical(a, b) {
  try {
    const sa = fs.statSync(a);
    const sb = fs.statSync(b);
    if (sa.size !== sb.size) return false; // fast reject before hashing
    const ha = crypto.createHash('sha256').update(fs.readFileSync(a)).digest('hex');
    const hb = crypto.createHash('sha256').update(fs.readFileSync(b)).digest('hex');
    return ha === hb;
  } catch {
    return false;
  }
}

// Media-element attributes whose value is a (possibly served) URL. Used by the
// dist-HTML scan that powers the real missing-asset warning (review fix #3).
const SRC_ATTR_RE = /\b(?:src|poster)\s*=\s*(?:"([^"]*)"|'([^']*)')/gi;

/**
 * Scan an emitted `dist/**.html` tree for root-absolute media `src`/`poster`
 * references (review fix #3). The previous design diffed a module-level Set
 * populated by the rehype plugin, but Astro runs markdown in a SEPARATE Vite
 * module graph from the integration, so that Set is EMPTY at `astro:build:done`
 * -> the warning was dead code. Reading the actually-emitted HTML is something
 * the hook CAN see, so the missing-asset warning becomes real.
 *
 * @param {string} distDir absolute dist output dir
 * @returns {Set<string>} decoded content/-relative asset paths referenced as
 *   root-absolute `/…` media src/poster in the emitted HTML
 */
function scanReferencedAssets(distDir) {
  const referenced = new Set();
  const stack = [distDir];
  const visited = new Set();
  let guard = 0;
  while (stack.length > 0) {
    if (++guard > 200000) break; // hard backstop, never spin
    const dir = stack.pop();
    let entries;
    try {
      entries = fs.readdirSync(dir, { withFileTypes: true });
    } catch {
      continue;
    }
    for (const entry of entries) {
      const abs = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        let real;
        try {
          real = fs.realpathSync(abs);
        } catch {
          real = abs;
        }
        if (visited.has(real)) continue;
        visited.add(real);
        stack.push(abs);
      } else if (entry.isFile() && /\.html?$/i.test(entry.name)) {
        let html;
        try {
          html = fs.readFileSync(abs, 'utf8');
        } catch {
          continue;
        }
        let m;
        SRC_ATTR_RE.lastIndex = 0;
        while ((m = SRC_ATTR_RE.exec(html)) !== null) {
          const raw = m[1] !== undefined ? m[1] : m[2];
          // Only ROOT-ABSOLUTE refs (our rewrite output). Skip external/scheme/
          // protocol-relative/_astro and the empty value.
          if (!raw || !raw.startsWith('/') || raw.startsWith('//')) continue;
          if (raw.startsWith('/_astro/')) continue;
          // Decode per segment to recover the on-disk content/-relative path.
          const segs = raw.replace(/^\//, '').split('/');
          let decodedOk = true;
          const decoded = segs.map((s) => {
            try {
              return decodeURIComponent(s);
            } catch {
              decodedOk = false;
              return s;
            }
          });
          if (!decodedOk) continue;
          const rel = decoded.join('/');
          if (rel !== '') referenced.add(rel);
        }
      }
    }
  }
  return referenced;
}

export default function copyVaultMedia() {
  return {
    name: 'copy-vault-media',
    hooks: {
      'astro:build:done': ({ dir, logger }) => {
        const log = logger || console;
        // Review fix #2: never-throwing logger selectors. The previous inverted
        // ternary returned `console.warn` for a real logger and CALLED a falsy
        // `.warn` (TypeError). These resolve to a guaranteed-callable function.
        const warn =
          log && typeof log.warn === 'function' ? log.warn.bind(log) : console.warn;
        const info =
          log && typeof log.info === 'function' ? log.info.bind(log) : console.log;

        let distDir;
        try {
          distDir = fileURLToPath(dir);
        } catch {
          // Fall back to the conventional dist location relative to this module.
          distDir = fileURLToPath(new URL('../../dist', import.meta.url));
        }

        // Review fix #6: wrap the walk in the never-throw floor — a symlink cycle
        // or odd FS error in collectAssets must not take the build down.
        const assets = [];
        try {
          collectAssets(contentDir, '', assets, new Set(), 0);
        } catch (err) {
          warn(`[copy-vault-media] Asset walk aborted (continuing): ${err && err.message}`);
        }

        let copied = 0;
        for (const rel of assets) {
          const srcPath = path.join(contentDir, ...rel.split('/'));
          const destPath = path.join(distDir, ...rel.split('/'));
          try {
            // Never CLOBBER a file Astro already emitted (HTML/public output): a
            // vault asset whose path collides with a route/public path is an
            // authoring error -> warn + skip, not a crash. (Idempotent re-runs of
            // the SAME asset to its own dist path are fine — that path is ours.)
            if (fs.existsSync(destPath)) {
              const destStat = fs.statSync(destPath);
              if (destStat.isDirectory()) {
                warn(
                  `[copy-vault-media] Skipping ${rel}: a directory already exists at the dist path.`,
                );
                continue;
              }
              // Review fix #5: decide "is this my own prior copy?" by byte-identity
              // (SHA-256), NOT size. Size-equality could match a genuine Astro/
              // public output that merely happens to be the same length -> silent
              // clobber. Only overwrite when the existing file is byte-identical to
              // the vault asset (a re-run of our own copy); otherwise warn + skip,
              // never overwriting non-identical existing output.
              if (!filesAreIdentical(srcPath, destPath)) {
                warn(
                  `[copy-vault-media] Skipping ${rel}: a different file already exists at the ` +
                    'dist path (would clobber Astro HTML/public output).',
                );
                continue;
              }
              // Byte-identical — our own prior copy; the copy below is a no-op.
            }
            fs.mkdirSync(path.dirname(destPath), { recursive: true });
            // Faithful BINARY copy (copyFileSync byte copy; no encoding transform).
            fs.copyFileSync(srcPath, destPath);
            copied++;
          } catch (err) {
            // Floor: never throw mid-build on one odd asset — warn and continue.
            warn(`[copy-vault-media] Failed to copy ${rel}: ${err && err.message}`);
          }
        }

        // Missing-asset visibility (AC6) — review fix #3. "Referenced" is now
        // sourced from the EMITTED dist HTML (root-absolute media src/poster the
        // rewrite produced), which the hook can actually see — the old approach
        // diffed a rehype module-level Set that is empty in this Vite module
        // instance, so the warning never fired. Diff referenced-but-not-copied
        // and warn once. Never throws.
        try {
          const present = new Set(assets);
          const referenced = scanReferencedAssets(distDir);
          const missing = [...referenced].filter((a) => !present.has(a)).sort();
          if (missing.length > 0) {
            warn(
              `[copy-vault-media] ${missing.length} referenced media asset(s) are missing from ` +
                `content/ (they will 404 as broken images, never a crash): ${missing.join(', ')}`,
            );
          }
        } catch (err) {
          // Visibility nicety only — never let it break the build.
          warn(`[copy-vault-media] missing-asset scan skipped: ${err && err.message}`);
        }

        info(`[copy-vault-media] Copied ${copied} vault media asset(s) into dist/.`);
      },
    },
  };
}
