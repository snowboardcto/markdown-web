import { test, expect } from '@playwright/test';

/**
 * Story 2.4 — Media embedding (TDD / RED phase).
 *
 * These specs are written FIRST, before the rehype media-rewrite plugin
 * (`web/src/lib/rehype-md-media.mjs`) and the build-time vault-media copy
 * integration (the `astro:build:done` hook) exist. They are EXPECTED TO FAIL
 * until Step 5 implements those.
 *
 * RED baseline (today, feature absent):
 *   - Relative media `src`/`poster` are emitted LITERALLY (`media/powder.jpg`,
 *     `../media/powder.jpg`, `diagram.png`), NOT rewritten to a served
 *     root-absolute `/media/powder.jpg`.
 *   - `content/media/**` is never copied into `dist`, so `/media/powder.jpg`
 *     404s (no asset served).
 *
 * The fixtures (`content/x.md`, `content/sub/page.md`) author the media as RAW
 * HTML `<img>`/`<video>`/`<audio>`/`<source>` rather than markdown `![]()`.
 * Rationale: the rewrite runs at the HAST (rehype) stage and visits media
 * ELEMENT nodes by `tagName` regardless of whether they came from `![]()` or
 * raw HTML, so raw HTML exercises the exact same code path; AND it keeps the
 * RED build from hard-crashing on the AC6 missing-asset reference (Astro's
 * default `astro:assets` pipeline throws `ImageNotFound` on a missing markdown
 * `![]()` image, which would take the whole build — and the existing 66 specs —
 * down for the wrong reason). The implementation step disables/bypasses
 * `astro:assets` for content media so markdown `![]()` flows through the same
 * plugin; these specs assert the served output either way.
 *
 * Everything is asserted against the BUILT/PREVIEW output (the harness runs
 * `npm run build && npm run preview`), so the assertions prove the build-time
 * rewrite + copy — no client JS is required. `src`/`poster` are read via
 * `getAttribute(...)` from the rendered HTML; serving is asserted via
 * `page.request.get('/media/…')` -> status + content-type.
 *
 * AC -> test mapping:
 *   AC1  same-dir `media/powder.jpg` on /x -> `/media/powder.jpg`, served 200
 *        with an image content-type, renders inline (naturalWidth > 0)
 *   AC2  relative resolution (nested `..`, sibling, `./`), filenames NOT slugged,
 *        + F1 `%2F`/leading-`%2F`/malformed-`%` smuggling left UNREWRITTEN
 *   AC3  pass-through unchanged: external https / protocol-relative / data: /
 *        root-absolute (not rewritten, not copied)
 *   AC4  alt / title / width / height survive; empty alt -> alt=""
 *   AC5  `<video src>`/`<video poster>`/`<audio src>`/`<source src>` rewritten +
 *        served
 *   AC6  missing referenced asset -> src rewrites to its would-be path -> 404 on
 *        the asset, build never crashes
 *   AC7  covered by the FULL suite (the existing 66 specs stay green)
 */

/** First media element matching a CSS selector on the current page, its attr. */
async function attrOf(
  page: import('@playwright/test').Page,
  selector: string,
  attr: string,
): Promise<string | null> {
  return page.locator(selector).first().getAttribute(attr);
}

test.describe('Story 2.4 AC1 — relative image embed renders inline + asset served', () => {
  test.beforeEach(async ({ page }) => {
    const response = await page.goto('/x');
    expect(response, 'route /x should exist').not.toBeNull();
    expect(response!.status(), '/x should return 200').toBe(200);
  });

  test('`media/powder.jpg` on /x emits <img src="/media/powder.jpg"> (build-time, not literal)', async ({
    page,
  }) => {
    const src = await attrOf(page, 'img[alt="a skier in deep powder"]', 'src');
    expect(src, 'relative media src must be rewritten to its served root-absolute path').toBe(
      '/media/powder.jpg',
    );
    expect(src, 'must NOT be the literal relative src (would 404 against /x route)').not.toBe(
      'media/powder.jpg',
    );
  });

  test('the asset is actually served: GET /media/powder.jpg -> 200 + image content-type', async ({
    page,
  }) => {
    const res = await page.request.get('/media/powder.jpg');
    expect(res.status(), '/media/powder.jpg must be copied into dist and served').toBe(200);
    expect(res.headers()['content-type'], 'must be served as an image').toMatch(/^image\//);
    const body = await res.body();
    expect(body.length, 'served image must be non-empty (the real JPEG bytes)').toBeGreaterThan(0);
    // Valid JPEG magic bytes (SOI marker) — proves the byte-faithful copy.
    expect(body[0], 'JPEG SOI byte 0').toBe(0xff);
    expect(body[1], 'JPEG SOI byte 1').toBe(0xd8);
  });

  test('the embedded image actually decodes/renders inline (naturalWidth > 0)', async ({ page }) => {
    const img = page.locator('img[alt="a skier in deep powder"]').first();
    await expect(img).toBeVisible();
    // Wait for the browser to load+decode the real JPEG bytes.
    await img.evaluate((el: HTMLImageElement) =>
      el.complete ? Promise.resolve() : el.decode().catch(() => undefined),
    );
    const naturalWidth = await img.evaluate((el: HTMLImageElement) => el.naturalWidth);
    expect(naturalWidth, 'a served+decoded image has a non-zero intrinsic width').toBeGreaterThan(0);
  });

  test('the emitted <img> is plain static HTML — no client:* island, no runtime JS', async ({
    page,
  }) => {
    const response = await page.goto('/x');
    const raw = await response!.text();
    expect(raw, 'rewritten <img> must be plain static HTML in the served document').toContain(
      'src="/media/powder.jpg"',
    );
    expect(raw, 'no Astro island hydration directive on media').not.toMatch(/astro-island/);
  });

  // AC1 verbatim: the epic example is LITERAL markdown `![](media/powder.jpg)`.
  // This proves the markdown image syntax (not just raw HTML `<img>`) is rewritten
  // to a served `/media/powder.jpg` AND bypasses Astro's astro:assets pipeline
  // (no `/_astro/*.webp` optimisation, no ImageNotFound crash) — the same rehype
  // rewrite handles both authoring forms because it runs at the HAST stage before
  // Astro's internal rehypeImages.
  test('LITERAL markdown `![markdown embed](media/powder.jpg)` -> served plain <img src="/media/powder.jpg"> (AC1 verbatim)', async ({
    page,
  }) => {
    const img = page.locator('img[alt="markdown embed"]');
    await expect(img, 'the markdown image must render as a single <img>').toHaveCount(1);
    const src = await img.getAttribute('src');
    expect(src, 'markdown ![]() src must be rewritten to the served root-absolute path').toBe(
      '/media/powder.jpg',
    );
    // NOT an astro:assets-optimised /_astro/*.webp — a verbatim served copy.
    expect(src, 'must NOT be processed by astro:assets (no /_astro/ optimisation)').not.toMatch(
      /^\/_astro\//,
    );
    // The asset it points at is actually served (200, real JPEG bytes).
    const res = await page.request.get(src!);
    expect(res.status(), 'the markdown-embedded asset must be served 200').toBe(200);
    expect(res.headers()['content-type']).toMatch(/^image\//);
  });
});

test.describe('Story 2.4 AC2 — relative media resolution (assert emitted src), filenames not slugged', () => {
  test('same-dir + leading `./` collapse from /x', async ({ page }) => {
    const res = await page.goto('/x');
    expect(res!.status()).toBe(200);
    // same-dir (row 1)
    expect(await attrOf(page, 'img[alt="a skier in deep powder"]', 'src')).toBe(
      '/media/powder.jpg',
    );
    // leading ./ collapse (row 4)
    expect(
      await attrOf(page, 'img[alt="dot slash"]', 'src'),
      './media/powder.jpg from /x -> /media/powder.jpg',
    ).toBe('/media/powder.jpg');
  });

  test('parent `..` resolves against the page dir from /sub/page (row 2)', async ({ page }) => {
    const res = await page.goto('/sub/page');
    expect(res!.status(), '/sub/page should return 200').toBe(200);
    expect(
      await attrOf(page, 'img[alt="from sub"]', 'src'),
      '../media/powder.jpg from /sub/page -> /media/powder.jpg',
    ).toBe('/media/powder.jpg');
  });

  test('sibling nested-dir asset resolves to the page dir from /sub/page (row 3) + served', async ({
    page,
  }) => {
    const res = await page.goto('/sub/page');
    expect(res!.status()).toBe(200);
    expect(
      await attrOf(page, 'img[alt="sibling diagram"]', 'src'),
      'diagram.png from /sub/page -> /sub/diagram.png',
    ).toBe('/sub/diagram.png');
    // The nested asset is copied at its nested path and served.
    const asset = await page.request.get('/sub/diagram.png');
    expect(asset.status(), '/sub/diagram.png must be served from the nested vault path').toBe(200);
    expect(asset.headers()['content-type']).toMatch(/^image\//);
  });

  test('asset filenames are preserved byte-exact (the served file matches the rewritten src)', async ({
    page,
  }) => {
    await page.goto('/x');
    const src = await attrOf(page, 'img[alt="a skier in deep powder"]', 'src');
    // No github-slugging of the filename: it is the literal on-disk name, only
    // root-absolutised. A slugged src (e.g. lower-cased/dashed) would 404.
    expect(src).toBe('/media/powder.jpg');
    const served = await page.request.get(src!);
    expect(served.status(), 'the verbatim-named asset must exist on disk at the rewritten path').toBe(
      200,
    );
  });

  // F1 smuggling guards — the per-segment-decode guard copied from
  // rehype-md-links.mjs must be active on the media path too: an encoded
  // separator/leading-slash/malformed-escape value is left UNREWRITTEN.
  test('F1: encoded interior separator `media%2Fpowder.jpg` left unrewritten (row 13)', async ({
    page,
  }) => {
    await page.goto('/x');
    const src = await attrOf(page, 'img[alt="encoded separator smuggle"]', 'src');
    expect(src, '%2F must not be split into a new path part').toBe('media%2Fpowder.jpg');
    expect(src, 'must not manufacture a rewritten /media/powder.jpg from a smuggled separator').not.toBe(
      '/media/powder.jpg',
    );
  });

  test('F1: encoded leading slash `%2Fetc/passwd.png` left unrewritten (row 14)', async ({
    page,
  }) => {
    await page.goto('/x');
    const src = await attrOf(page, 'img[alt="encoded leading slash smuggle"]', 'src');
    expect(src, 'a decoded leading / must not produce a protocol-relative //host src').toBe(
      '%2Fetc/passwd.png',
    );
    expect(src, 'must never emit a protocol-relative off-site src').not.toMatch(/^\/\//);
  });

  test('F1: malformed `%`-escape `bad%zz.jpg` left unrewritten (row 12, decode throws -> degrade)', async ({
    page,
  }) => {
    await page.goto('/x');
    expect(
      await attrOf(page, 'img[alt="malformed percent escape"]', 'src'),
      'a malformed %-escape degrades to UNREWRITTEN, never throws',
    ).toBe('bad%zz.jpg');
  });
});

test.describe('Story 2.4 AC3 — non-relative / external media passes through unchanged', () => {
  test.beforeEach(async ({ page }) => {
    const res = await page.goto('/x');
    expect(res!.status()).toBe(200);
  });

  test('external https image keeps its absolute src (not rewritten, not copied)', async ({
    page,
  }) => {
    expect(await attrOf(page, 'img[alt="remote https"]', 'src')).toBe('https://example.com/a.png');
  });

  test('protocol-relative `//host` image is untouched', async ({ page }) => {
    expect(await attrOf(page, 'img[alt="protocol relative"]', 'src')).toBe(
      '//cdn.example.com/b.png',
    );
  });

  test('data: URI image is untouched', async ({ page }) => {
    const src = await attrOf(page, 'img[alt="data uri"]', 'src');
    expect(src, 'a data: URI must pass through verbatim').toMatch(/^data:image\/gif;base64,/);
  });

  test('root-absolute image is untouched (already a served path)', async ({ page }) => {
    expect(await attrOf(page, 'img[alt="already served"]', 'src')).toBe('/already/served.png');
  });
});

test.describe('Story 2.4 AC4 — alt / title / width / height survive the rewrite', () => {
  test.beforeEach(async ({ page }) => {
    const res = await page.goto('/x');
    expect(res!.status()).toBe(200);
  });

  test('author alt text passes through untouched alongside the src rewrite', async ({ page }) => {
    const img = page.locator('img[src="/media/powder.jpg"][alt="a skier in deep powder"]');
    await expect(img, 'the rewrite must not drop or alter alt').toHaveCount(1);
  });

  test('empty-alt decorative embed emits alt="" (and still rewrites the src)', async ({ page }) => {
    // The empty-alt image: alt="" AND src rewritten to /media/powder.jpg.
    const img = page.locator('img[alt=""]');
    await expect(img, 'a decorative empty-alt embed must survive as alt=""').not.toHaveCount(0);
    const src = await img.first().getAttribute('src');
    expect(src, 'empty-alt embed src is still rewritten').toBe('/media/powder.jpg');
  });

  test('title / width / height authored attributes survive the rewrite', async ({ page }) => {
    const img = page.locator('img[alt="titled"]');
    await expect(img).toHaveCount(1);
    expect(await img.getAttribute('src'), 'src rewritten').toBe('/media/powder.jpg');
    expect(await img.getAttribute('title'), 'title preserved').toBe('Powder Day');
    expect(await img.getAttribute('width'), 'width preserved').toBe('320');
    expect(await img.getAttribute('height'), 'height preserved').toBe('240');
  });
});

test.describe('Story 2.4 AC5 — non-image media (video/audio/source/poster) rewritten + served', () => {
  test.beforeEach(async ({ page }) => {
    const res = await page.goto('/x');
    expect(res!.status()).toBe(200);
  });

  test('<source src> inside <video> is rewritten to its served path', async ({ page }) => {
    expect(
      await attrOf(page, 'video source[type="video/mp4"]', 'src'),
      'media/clip.mp4 -> /media/clip.mp4',
    ).toBe('/media/clip.mp4');
  });

  test('<video poster> is rewritten independently of src (distinct attribute)', async ({ page }) => {
    expect(
      await attrOf(page, 'video[poster]', 'poster'),
      'a relative video poster is a served image too',
    ).toBe('/media/powder.jpg');
  });

  test('<audio src> relative reference is rewritten', async ({ page }) => {
    expect(await attrOf(page, 'audio[src]', 'src'), 'media/clip.mp3 -> /media/clip.mp3').toBe(
      '/media/clip.mp3',
    );
  });

  test('the video poster asset (an existing image) is actually served', async ({ page }) => {
    // The poster reuses the real powder.jpg fixture, so it must serve 200 once
    // the copy step runs. (The mp4/mp3 source assets are intentionally absent —
    // their serving parity with images is covered by AC1's served-asset check;
    // here we prove the poster image — a real fixture — is copied + served.)
    const res = await page.request.get('/media/powder.jpg');
    expect(res.status()).toBe(200);
    expect(res.headers()['content-type']).toMatch(/^image\//);
  });
});

test.describe('Story 2.4 AC6 — missing referenced asset -> 404, never a build crash', () => {
  test('the missing embed still rewrites to its would-be served path', async ({ page }) => {
    await page.goto('/x');
    expect(
      await attrOf(page, 'img[alt="broken"]', 'src'),
      'a missing asset still rewrites (existence-agnostic) to /media/missing.jpg',
    ).toBe('/media/missing.jpg');
  });

  test('the missing asset 404s (broken image), it is not served as a 0-byte/200', async ({
    page,
  }) => {
    const res = await page.request.get('/media/missing.jpg');
    expect(res.status(), 'no file copied for a missing reference -> normal 404').toBe(404);
  });

  test('the build did not crash on the missing reference (the whole suite built + /x renders)', async ({
    page,
  }) => {
    // If the missing-asset reference had crashed the build, the preview server
    // would not be up and /x would not render its fixture content at all.
    const res = await page.goto('/x');
    expect(res!.status(), '/x renders -> the build with a missing-asset ref did NOT crash').toBe(
      200,
    );
    await expect(page.locator('h1', { hasText: 'Heading One' })).toHaveCount(1);
  });
});
