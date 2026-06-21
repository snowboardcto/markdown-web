import { test, expect, type Page } from '@playwright/test';

/**
 * Story 2.5 — Browsable vault index. (TDD RED phase.)
 *
 * These specs are written FAILING-FIRST: they encode the contract for the
 * generated browsable index at `/` BEFORE `web/src/pages/index.astro` is
 * replaced (it is still the Story-1.1 "Coming soon" placeholder). They MUST
 * fail on the current build (no listing, placeholder copy present) and pass
 * once Story 2.5 Step 5 ships the generated index.
 *
 * They are purely additive to the existing 94 specs (ac1/ac2/ac3/ac5/ac6,
 * 2-2/2-3/2-4), which must remain green — this file only adds assertions for
 * the new `/` route and never changes the asserted content-page behaviour.
 *
 * Anti-tautology discipline (carried from 2.2/2.3/2.4): hrefs and labels are
 * read from the ACTUAL rendered HTML; link liveness is proven with a real
 * HTTP 200 (`page.request.get`), never re-derived from a slug fn in-test.
 *
 * The expected content-route set is the current content vault (every .md):
 *   /empty /gear-guide /h1-only /my-notes /my-notes-dir/page /no-h1 /readme
 *   /sub /sub/page /sub/page2 /sub/sibling /x
 * (12 routes — `content/sub/index.md` collapses to `/sub`, not `/sub/index`;
 *  no root `content/index.md`, so no `/` self-link.)
 */

// The authoritative content-route set the catch-all emits today (one per
// content/**/*.md, with sub/index.md index-collapsed to /sub). Pinned so a
// dropped or extra page fails loudly. Order here is the DOCUMENTED Decision-B
// sort: flat, by route id, via a plain code-unit comparison
// (`a.id < b.id ? -1 : a.id > b.id ? 1 : 0`) — ICU-independent, identical on
// dev and CI regardless of host locale data.
const EXPECTED_ROUTES_SORTED = [
  '/empty',
  '/gear-guide',
  '/h1-only',
  '/my-notes',
  '/my-notes-dir/page',
  '/no-h1',
  '/readme',
  '/sub',
  '/sub/page',
  '/sub/page2',
  '/sub/sibling',
  '/x',
] as const;

const EXPECTED_COUNT = EXPECTED_ROUTES_SORTED.length; // 12

/**
 * The slug-derived Title Case label (mirrors the destination page's
 * `slugToTitle`: last path segment, [-_]->space, Title-Case). Used ONLY to make
 * the label-honesty assertion robust to Decision D (full-precedence render vs
 * cheap data.title||slugToTitle) — the test accepts EITHER the destination's
 * actual <title> OR this slug-derived value, and never asserts a label the code
 * cannot produce. It is not used to derive hrefs.
 */
function slugTitle(route: string): string {
  const last = route.replace(/^\//, '').split('/').pop() ?? '';
  return last
    .replace(/[-_]+/g, ' ')
    .replace(/\b\w/g, (c) => c.toUpperCase())
    .trim();
}

/** Collect every root-absolute content link on `/` (DOM order), excluding the
 *  bare `/` self-link if any leaks in (asserted-against separately). */
async function indexContentHrefs(page: Page): Promise<string[]> {
  // Scope to the in-content listing (`<main>`). Story 2.6 adds site-header +
  // pitch-card chrome OUTSIDE <main> with its own `/get` and `/vision` stub
  // links, which must NOT be counted as vault-listing entries.
  const hrefs = await page.locator('main a[href^="/"]').evaluateAll((els) =>
    els.map((e) => (e as HTMLAnchorElement).getAttribute('href') ?? ''),
  );
  // Keep only real route links (drop empty, hash, and any asset/stylesheet
  // links that are not page routes). A page route is `/` followed by a slug
  // and never ends in a file extension like `.css`.
  return hrefs.filter((h) => h.startsWith('/') && !/\.[a-z0-9]+$/i.test(h));
}

test.describe('Story 2.5 AC1 — index lists EVERY vault page one-to-one', () => {
  test.beforeEach(async ({ page }) => {
    const response = await page.goto('/');
    expect(response, 'route / should exist').not.toBeNull();
    expect(response!.status(), '/ should return 200').toBe(200);
  });

  test('contains a link to every known content route', async ({ page }) => {
    for (const route of EXPECTED_ROUTES_SORTED) {
      await expect(
        page.locator(`a[href="${route}"]`),
        `index must link to ${route}`,
      ).toHaveCount(1);
    }
  });

  test('completeness invariant: content-link count EQUALS the collection entry count', async ({ page }) => {
    const hrefs = await indexContentHrefs(page);
    const routeLinks = hrefs.filter((h) => h !== '/');
    // No omissions AND no extras.
    expect(
      routeLinks.sort(),
      `expected exactly the ${EXPECTED_COUNT} content routes, got ${JSON.stringify(routeLinks)}`,
    ).toEqual([...EXPECTED_ROUTES_SORTED].sort());
    expect(routeLinks.length, 'exactly one link per collection entry').toBe(EXPECTED_COUNT);
  });

  test('no phantom /sub/index and no extra routes', async ({ page }) => {
    await expect(page.locator('a[href="/sub/index"]')).toHaveCount(0);
    const hrefs = await indexContentHrefs(page);
    const unexpected = hrefs.filter(
      (h) => h !== '/' && !(EXPECTED_ROUTES_SORTED as readonly string[]).includes(h),
    );
    expect(unexpected, `unexpected content links: ${JSON.stringify(unexpected)}`).toEqual([]);
  });

  test('every listed href resolves to a live 200 page', async ({ page }) => {
    const hrefs = (await indexContentHrefs(page)).filter((h) => h !== '/');
    expect(hrefs.length, 'there should be links to follow').toBeGreaterThan(0);
    for (const href of hrefs) {
      const res = await page.request.get(href);
      expect(res.status(), `${href} should resolve to a live page`).toBe(200);
    }
  });
});

test.describe('Story 2.5 AC2 — links are resolved routes with readable labels', () => {
  test.beforeEach(async ({ page }) => {
    const response = await page.goto('/');
    expect(response!.status()).toBe(200);
  });

  test('no href is a literal *.md path', async ({ page }) => {
    const hrefs = await page.locator('a[href]').evaluateAll((els) =>
      els.map((e) => (e as HTMLAnchorElement).getAttribute('href') ?? ''),
    );
    for (const h of hrefs) {
      expect(h, `index link "${h}" must be a resolved route, never a *.md path`).not.toMatch(/\.md($|[?#])/i);
    }
  });

  test('each content link href is root-absolute', async ({ page }) => {
    const hrefs = (await indexContentHrefs(page)).filter((h) => h !== '/');
    for (const h of hrefs) {
      expect(h.startsWith('/'), `${h} must be root-absolute`).toBeTruthy();
    }
  });

  test('label is a human-readable title (frontmatter title, else H1, else slug)', async ({ page }) => {
    // Read the actual destination <title> for each route, then assert the index
    // label equals EITHER the destination title (full-precedence Decision-D) OR
    // the slug-derived Title Case (cheap Decision-D) — the ACTUAL chosen
    // behaviour, never an idealised one. The label must also be non-empty.
    for (const route of EXPECTED_ROUTES_SORTED) {
      const label = (await page.locator(`a[href="${route}"]`).first().textContent())?.trim() ?? '';
      expect(label, `label for ${route} must not be empty`).not.toBe('');

      const dest = await page.request.get(route);
      const html = await dest.text();
      const destTitle = (html.match(/<title>([^<]*)<\/title>/i)?.[1] ?? '').trim();

      const acceptable = new Set([destTitle, slugTitle(route)].filter(Boolean));
      expect(
        acceptable.has(label),
        `label "${label}" for ${route} should match the destination <title> "${destTitle}" or slug title "${slugTitle(route)}"`,
      ).toBeTruthy();
    }
  });

  test('the H1-only page /no-h1 label is slug-derived "No H1" (precedence falls through)', async ({ page }) => {
    // /no-h1 has neither frontmatter title nor an H1, so BOTH Decision-D paths
    // collapse to slugToTitle('no-h1') === 'No H1'. Pinning this proves the
    // shared title precedence is actually exercised for the H1-divergence case.
    const label = (await page.locator('a[href="/no-h1"]').first().textContent())?.trim();
    expect(label, '/no-h1 label should be the slug-derived "No H1"').toBe('No H1');
  });

  test('Decision-D divergence is genuinely exercised: H1-only /h1-only label (slug) DIFFERS from destination <title> (H1)', async ({ page }) => {
    // `content/h1-only.md` is H1-only (no frontmatter title). The index uses the
    // cheap precedence `data.title || slugToTitle(entry.id)`, so its label is the
    // slug-derived Title Case "H1 Only", while the destination page's <title> is
    // the H1 "Distinct Heading Title". The two PROVABLY differ — this pins the
    // documented Decision-D divergence (unlike /no-h1, where they coincide).
    const label = (await page.locator('a[href="/h1-only"]').first().textContent())?.trim();
    expect(label, '/h1-only index label should be the slug-derived "H1 Only"').toBe('H1 Only');

    const dest = await page.request.get('/h1-only');
    const html = await dest.text();
    const destTitle = (html.match(/<title>([^<]*)<\/title>/i)?.[1] ?? '').trim();
    expect(destTitle, "destination <title> should be the page's H1").toBe('Distinct Heading Title');

    // The whole point: label and destination title are NOT equal here.
    expect(label, 'Decision-D: index label and destination title must diverge for an H1-only page').not.toBe(destTitle);
  });
});

test.describe('Story 2.5 AC3 — reachability from the index and from 404', () => {
  test('a page is reachable by clicking through from /', async ({ page }) => {
    const home = await page.goto('/');
    expect(home!.status()).toBe(200);
    await page.locator('a[href="/gear-guide"]').first().click();
    await expect(page).toHaveURL(/\/gear-guide\/?$/);
    await expect(page.locator('h1')).toHaveCount(1);
  });

  test("404 'Go back home' reaches the real index at /", async ({ page }) => {
    const notFound = await page.goto('/this-route-does-not-exist');
    expect(notFound!.status(), 'unknown route should 404').toBe(404);
    const home = page.locator('a[href="/"]').first();
    await expect(home, "404 should have a 'go home' link").toHaveCount(1);
    await home.click();
    await expect(page).toHaveURL(/\/$/);
    // It must land on the real index (the listing), not the retired placeholder.
    await expect(page.getByText('Coming soon')).toHaveCount(0);
    await expect(page.locator('a[href="/x"]')).toHaveCount(1);
  });
});

test.describe('Story 2.5 AC4 — deterministic, CI-stable order; nested included; no dup/self-link', () => {
  test.beforeEach(async ({ page }) => {
    const response = await page.goto('/');
    expect(response!.status()).toBe(200);
  });

  test('nested + index-collapsed routes are all present', async ({ page }) => {
    for (const route of ['/sub', '/sub/page', '/sub/page2', '/sub/sibling', '/my-notes-dir/page']) {
      await expect(page.locator(`a[href="${route}"]`), `nested route ${route} must appear`).toHaveCount(1);
    }
  });

  test('no /sub/index phantom and no / self-link in the listing', async ({ page }) => {
    await expect(page.locator('a[href="/sub/index"]')).toHaveCount(0);
    const hrefs = await indexContentHrefs(page);
    expect(hrefs, 'the index must not list a `/` self-link').not.toContain('/');
  });

  test('content links appear in the pinned, CI-stable sort-by-route order', async ({ page }) => {
    const hrefs = (await indexContentHrefs(page)).filter((h) => h !== '/');
    expect(
      hrefs,
      'DOM order of content links must equal the documented Decision-B sort (by route id, locale-pinned)',
    ).toEqual([...EXPECTED_ROUTES_SORTED]);
  });
});

test.describe('Story 2.5 AC5 — placeholder retired, / owned by the real index', () => {
  test.beforeEach(async ({ page }) => {
    const response = await page.goto('/');
    expect(response!.status()).toBe(200);
  });

  test("the Story-1.1 'Coming soon' placeholder is gone", async ({ page }) => {
    await expect(page.getByText('Coming soon')).toHaveCount(0);
  });

  test('/ is the real index (renders the listing)', async ({ page }) => {
    // The listing surface uses real <a> route links; at least the known set.
    await expect(page.locator('a[href="/x"]')).toHaveCount(1);
    await expect(page.locator('ul li a, ol li a').first()).toBeVisible();
  });

  test('/ does not appear as a content link in its own listing', async ({ page }) => {
    const hrefs = await indexContentHrefs(page);
    expect(hrefs.filter((h) => h === '/'), '/ must not self-list').toEqual([]);
  });
});

test.describe('Story 2.5 AC6 — themed, JS-free, crawlable, semantic shell, listing-only', () => {
  test.beforeEach(async ({ page }) => {
    const response = await page.goto('/');
    expect(response!.status()).toBe(200);
  });

  test('exactly one <h1> and a single semantic shell', async ({ page }) => {
    await expect(page.locator('html')).toHaveCount(1);
    const lang = await page.locator('html').getAttribute('lang');
    expect(lang, '<html> must declare lang').toBeTruthy();
    await expect(page.locator('head')).toHaveCount(1);
    await expect(page.locator('h1')).toHaveCount(1);
    await expect(page.locator('main, article').first()).toBeVisible();
  });

  test('serves a doctype with balanced structural tags', async ({ page }) => {
    const response = await page.goto('/');
    const html = (await response!.text()).toLowerCase();
    expect(html).toContain('<!doctype html>');
    const count = (s: string, sub: string) => s.split(sub).length - 1;
    expect(count(html, '<html')).toBe(1);
    expect(count(html, '</html>')).toBe(1);
    // `<head>` exactly — `<head` would also match the Story 2.6 `<header>` chrome.
    expect(count(html, '<head>')).toBe(1);
    expect(count(html, '</head>')).toBe(1);
    expect(count(html, '<body')).toBe(1);
    expect(count(html, '</body>')).toBe(1);
  });

  test('themed via the shared Page layout (github.css linked, light surface)', async ({ page }) => {
    // Mirror the 2.2 theme contract: the index renders through Page, so the body
    // computes the light DESIGN surface (#ffffff) and fg (#1f2328).
    const bg = await page.locator('body').evaluate((el) => getComputedStyle(el).backgroundColor);
    expect(bg, 'index body background should be the light theme surface').toBe('rgb(255, 255, 255)');
    const fg = await page.locator('body').evaluate((el) => getComputedStyle(el).color);
    expect(fg, 'index body text should be the DESIGN fg').toBe('rgb(31, 35, 40)');
    // A stylesheet must be linked (the themed surface comes from real CSS).
    await expect(page.locator('head link[rel="stylesheet"], head style')).not.toHaveCount(0);
  });

  test('no client island / runtime JS hydration directive', async ({ page }) => {
    const response = await page.goto('/');
    const html = await response!.text();
    expect(html, 'index must be JS-free: no astro-island').not.toContain('astro-island');
    expect(html, 'index must be JS-free: no client:* directive').not.toMatch(/client:(load|idle|visible|media|only)/);
  });

  test('the listing works with JavaScript disabled', async ({ browser }) => {
    const ctx = await browser.newContext({ javaScriptEnabled: false });
    const noJsPage = await ctx.newPage();
    const res = await noJsPage.goto('/');
    expect(res!.status()).toBe(200);
    // Links are present and followable with JS off (crawlable).
    await expect(noJsPage.locator('a[href="/x"]')).toHaveCount(1);
    const follow = await noJsPage.request.get('/x');
    expect(follow.status()).toBe(200);
    await ctx.close();
  });

  test('2.6 chrome present: site-header + "Get the client" on the index', async ({ page }) => {
    // RECONCILED in Story 2.6 (was the 2.5 "no chrome yet" guard). 2.6 layers the
    // wordmark / "the vision" / "Get the client" CTA onto ALL pages via the shared
    // Page.astro layout — so the index now carries the chrome too.
    await expect(page.getByText('Get the client', { exact: false })).toHaveCount(1);
    await expect(page.locator('header')).toHaveCount(1);
  });
});
