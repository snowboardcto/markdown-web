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
// WARN (do not fail) listing referenced-but-missing assets, mirroring 2.3's
// `unresolvedPages` warn — a typo is visible, not silent. The non-negotiable
// floor: the copy NEVER throws mid-build on a missing/odd asset.
import { fileURLToPath } from 'node:url';
import fs from 'node:fs';
import path from 'node:path';
import { contentDir } from './page-path.mjs';
import { getReferencedAssets } from './rehype-md-media.mjs';

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

/**
 * Recursively collect copyable asset paths (relative to `content/`, POSIX),
 * skipping excluded files, and NOT following symlinks out of the vault.
 *
 * @param {string} absDir an absolute directory inside `content/`
 * @param {string} relDir its path relative to `content/` (POSIX, '' at root)
 * @param {string[]} out accumulator of POSIX relative asset paths
 */
function collectAssets(absDir, relDir, out) {
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
      if (stat.isDirectory()) collectAssets(abs, rel, out);
      else if (stat.isFile()) out.push(rel);
    } else if (entry.isDirectory()) {
      collectAssets(abs, rel, out);
    } else if (entry.isFile()) {
      out.push(rel);
    }
  }
}

export default function copyVaultMedia() {
  return {
    name: 'copy-vault-media',
    hooks: {
      'astro:build:done': ({ dir, logger }) => {
        const log = logger || console;
        let distDir;
        try {
          distDir = fileURLToPath(dir);
        } catch {
          // Fall back to the conventional dist location relative to this module.
          distDir = fileURLToPath(new URL('../../dist', import.meta.url));
        }

        const assets = [];
        collectAssets(contentDir, '', assets);

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
              // It is ours only if we are the one who would write it; treat any
              // pre-existing file at a media path conservatively: if it differs
              // in size, warn + skip to avoid clobbering Astro output.
              const srcStat = fs.statSync(srcPath);
              const destStat = fs.statSync(destPath);
              if (destStat.isDirectory()) {
                (log.warn || log.warn === undefined ? console.warn : log.warn)(
                  `[copy-vault-media] Skipping ${rel}: a directory already exists at the dist path.`,
                );
                continue;
              }
              if (srcStat.size !== destStat.size) {
                console.warn(
                  `[copy-vault-media] Skipping ${rel}: a different file already exists at the ` +
                    'dist path (would clobber Astro HTML/public output).',
                );
                continue;
              }
              // Same size — assume our own prior copy; overwrite identical bytes.
            }
            fs.mkdirSync(path.dirname(destPath), { recursive: true });
            // Faithful BINARY copy (cpSync byte copy; no encoding transform).
            fs.copyFileSync(srcPath, destPath);
            copied++;
          } catch (err) {
            // Floor: never throw mid-build on one odd asset — warn and continue.
            console.warn(`[copy-vault-media] Failed to copy ${rel}: ${err && err.message}`);
          }
        }

        // Missing-asset visibility (AC6): warn (do NOT fail) on referenced-but-
        // missing assets. Never throws on an empty/again-missing set.
        try {
          const present = new Set(assets);
          const missing = getReferencedAssets().filter((a) => !present.has(a));
          if (missing.length > 0) {
            console.warn(
              `[copy-vault-media] ${missing.length} referenced media asset(s) are missing from ` +
                `content/ (they will 404 as broken images, never a crash): ${missing.join(', ')}`,
            );
          }
        } catch {
          /* visibility nicety only — never let it break the build */
        }

        const msg = `Copied ${copied} vault media asset(s) into dist/.`;
        if (log && typeof log.info === 'function') log.info(msg);
        else console.log(`[copy-vault-media] ${msg}`);
      },
    },
  };
}
