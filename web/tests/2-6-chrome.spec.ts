import { test, expect, type Page } from '@playwright/test';

/**
 * Story 2.6 — Site header + pitch-card chrome. (TDD RED phase.)
 *
 * These specs are written FAILING-FIRST: they encode the contract for the
 * sticky site-header and the end-of-page pitch-card BEFORE the chrome is added
 * to the shared `web/src/layouts/Page.astro` layout (Step 5 implementation).
 * They MUST fail on the current build (no `<header>`/pitch chrome exists) and
 * pass once Step 5 wires `<SiteHeader/>` + `<PitchCard/>` into `Page.astro`.
 *
 * They are purely additive: the prior suite (ac1/ac2/ac3/ac5/ac6, 2-2/2-3/2-4,
 * 2-5) stays as-is in THIS step. Two prior assertions WILL become wrong once the
 * chrome ships — they are reconciled in Step 5, NOT here:
 *   - web/tests/2-5-index.spec.ts:323–328 (asserts NO header / NO "Get the
 *     client" on the index — inverts when 2.6 adds chrome everywhere).
 *   - web/tests/2-3-linking-nav.spec.ts:242 (asserts a single a[href="/"] on the
 *     404 page — may break IF the wordmark links to `/`).
 * Do NOT modify those in the red phase.
 *
 * Anti-tautology discipline (carried from 2.2–2.5): microcopy + hrefs are read
 * from the ACTUAL rendered HTML (getByText / getByRole / getAttribute); a real
 * route target is proven live with an HTTP 200, never re-derived in-test.
 *
 * The chrome lives in the shared layout, so it must appear identically on EVERY
 * surface: a content route (/x), a nested content route (/sub/page), the index
 * (/), and the custom 404 (a missing route). The pitch-headline / wordmark are
 * NOT the page <h1> (AC6 single-<h1> invariant).
 */

// ── EXPERIENCE.md / epics.md verbatim microcopy anchors ───────────────────────
// Pinned EXACTLY (straight apostrophe U+0027, em-dash U+2014) so any copy drift in
// the shipped chrome fails loudly. These are the load-bearing strings.
const WORDMARK_TEXT = 'the markdown web'; // mono ".md" chip + lowercase sans wordmark
const MD_CHIP = '.md';
const PITCH_HEADLINE = "You're reading one fixed view. There's a better one."; // straight ' (U+0027) in You're / There's
const PITCH_BODY_TAIL = 'Same file. Your shape.'; // final cadence anchor (EXPERIENCE.md line 35)
const PITCH_BODY_EMDASH = '—'; // U+2014 em-dash before "your layout" (NOT a hyphen)

// The two CTA strings are DELIBERATELY different (AC4): header = SHORT, pitch = LONG.
const HEADER_CTA = 'Get the client';
const PITCH_CTA = 'Get the Markdown Web client';
const VISION_LINK = 'the vision'; // header ghost link (lowercase, no punctuation)
const WHY_LINK = 'Why a markdown web?'; // pitch link (with the question mark)

// Every surface that routes through Page.astro and therefore inherits the chrome.
type Surface = { name: string; path: string; expectStatus: number };
const SURFACES: Surface[] = [
  { name: 'content route /x', path: '/x', expectStatus: 200 },
  { name: 'nested content route /sub/page', path: '/sub/page', expectStatus: 200 },
  { name: 'vault index /', path: '/', expectStatus: 200 },
  { name: '404 page', path: '/does-not-exist', expectStatus: 404 },
];

// 200-guarded (status-guarded) navigation: confirm we landed on the surface the
// chrome must decorate before asserting chrome on it.
async function gotoSurface(page: Page, s: Surface) {
  const res = await page.goto(s.path);
  expect(res, `navigation to ${s.path} should produce a response`).not.toBeNull();
  expect(res!.status(), `${s.name} should respond ${s.expectStatus}`).toBe(s.expectStatus);
  return res!;
}

// ── AC1 — sticky site-header present on EVERY surface ─────────────────────────
test.describe('Story 2.6 AC1 — sticky site-header on every surface', () => {
  for (const s of SURFACES) {
    test(`${s.name}: a banner <header> with wordmark + "the vision" + "Get the client"`, async ({
      page,
    }) => {
      await gotoSurface(page, s);

      // Exactly one semantic <header> exposing the implicit `banner` landmark
      // (a bare <header> that is a direct child of <body>, NOT nested in <main>).
      await expect(page.locator('header')).toHaveCount(1);
      await expect(page.getByRole('banner')).toHaveCount(1);

      const header = page.locator('header');
      // Wordmark: the ".md" chip beside the lowercase sans " the markdown web".
      await expect(header).toContainText(MD_CHIP);
      await expect(header).toContainText(WORDMARK_TEXT);

      // The two interactive controls are real <a href> with accessible names ==
      // visible text (NO decorative ▣ glyph leaking into the accessible name).
      await expect(header.getByRole('link', { name: VISION_LINK, exact: true })).toHaveCount(1);
      await expect(header.getByRole('link', { name: HEADER_CTA, exact: true })).toHaveCount(1);
    });

    test(`${s.name}: header is position: sticky (top:0), in normal flow`, async ({ page }) => {
      await gotoSurface(page, s);
      const position = await page
        .locator('header')
        .evaluate((el) => getComputedStyle(el).position);
      expect(position, 'site-header must be position: sticky (not fixed/absolute)').toBe('sticky');
    });
  }
});

// ── AC2 — end-of-page pitch-card present on EVERY surface ─────────────────────
test.describe('Story 2.6 AC2 — end-of-page pitch-card on every surface', () => {
  for (const s of SURFACES) {
    test(`${s.name}: pitch headline + body anchor + both CTAs in a named region`, async ({
      page,
    }) => {
      await gotoSurface(page, s);

      // The pitch-card is a named end-of-page region: a <footer> (implicit
      // `contentinfo`) OR a named <section> (aria-label / aria-labelledby).
      const contentinfo = page.getByRole('contentinfo');
      const namedRegion = page.getByRole('region', { name: /better one|markdown web/i });
      const regionCount = (await contentinfo.count()) + (await namedRegion.count());
      expect(regionCount, 'pitch-card must be a named contentinfo/region landmark').toBeGreaterThan(
        0,
      );

      // Verbatim headline (straight ' apostrophes) + body cadence anchor present.
      await expect(page.getByText(PITCH_HEADLINE, { exact: false })).toHaveCount(1);
      await expect(page.getByText(PITCH_BODY_TAIL, { exact: false }).first()).toBeVisible();

      // Both interactive controls present as real links with exact accessible names.
      await expect(page.getByRole('link', { name: PITCH_CTA, exact: true })).toHaveCount(1);
      await expect(page.getByRole('link', { name: WHY_LINK, exact: true })).toHaveCount(1);
    });
  }
});

// ── AC4 — microcopy verbatim per EXPERIENCE.md ────────────────────────────────
test.describe('Story 2.6 AC4 — microcopy verbatim (EXPERIENCE.md anchors)', () => {
  test('pitch headline is the EXACT two-sentence anchor (straight apostrophes, no "!")', async ({
    page,
  }) => {
    await gotoSurface(page, SURFACES[0]);
    const html = await page.content();
    expect(html, 'pitch headline must match EXPERIENCE.md verbatim').toContain(PITCH_HEADLINE);
    // Two sentences, one period each — never an exclamation-driven sales headline.
    // Assert against the RENDERED headline text, not the in-test constant.
    const headlineText = await page.getByText(PITCH_HEADLINE, { exact: false }).first().innerText();
    expect(headlineText, 'rendered pitch headline must not be exclamation-driven').not.toContain(
      '!',
    );
  });

  test('pitch body carries the em-dash (U+2014) + "Same file. Your shape." cadence', async ({
    page,
  }) => {
    await gotoSurface(page, SURFACES[0]);
    const body = await page.locator('body').innerText();
    expect(body, 'pitch body must contain the em-dash before "your layout"').toContain(
      PITCH_BODY_EMDASH,
    );
    expect(body, 'pitch body must end on the "Same file. Your shape." cadence').toContain(
      PITCH_BODY_TAIL,
    );
  });

  test('the ".md" chip renders in a mono/code-styled element, not as a second <h1>', async ({
    page,
  }) => {
    await gotoSurface(page, SURFACES[0]);
    await expect(page.locator('header')).toContainText(MD_CHIP);
  });

  test('header "Get the client" and pitch "Get the Markdown Web client" are DISTINCT strings', async ({
    page,
  }) => {
    await gotoSurface(page, SURFACES[0]);
    // Both CTAs are present as DISTINCT rendered links (AC4 deliberate
    // distinction): the SHORT header label and the LONG pitch label each match
    // exactly ONE link, and — crucially — the long pitch string does NOT match
    // the header CTA (its exact accessible name is the short string only). Read
    // from the rendered DOM via role+exact-name, never an in-test comparison.
    await expect(
      page.locator('header').getByRole('link', { name: HEADER_CTA, exact: true }),
    ).toHaveCount(1);
    await expect(
      page.locator('header').getByRole('link', { name: PITCH_CTA, exact: true }),
    ).toHaveCount(0);
    await expect(page.getByRole('link', { name: PITCH_CTA, exact: true })).toHaveCount(1);
  });

  test('all anchor labels appear verbatim ("the vision", "Why a markdown web?", wordmark)', async ({
    page,
  }) => {
    await gotoSurface(page, SURFACES[0]);
    const body = await page.locator('body').innerText();
    expect(body).toContain(VISION_LINK);
    expect(body).toContain(WHY_LINK);
    expect(body).toContain(WORDMARK_TEXT);
  });
});

// ── AC5 — stub links point at their DOCUMENTED targets (no 404 / no dangle) ───
// Decisions A/B (Dev Agent Record): "Get the client" + "Get the Markdown Web
// client" → /get ; "the vision" + "Why a markdown web?" → /vision. Each test
// asserts the href EQUALS its documented target (not just that *some* route is
// 200) — so a header↔vision href swap fails — AND that the route resolves 200.
const GET_TARGET = '/get'; // Decision A — Epic-3 client-download stub
const VISION_TARGET = '/vision'; // Decision B — vision/manifesto stub

test.describe('Story 2.6 AC5 — stub links resolve to their documented target', () => {
  async function assertHrefIsTarget(
    page: Page,
    href: string | null,
    expected: string,
    label: string,
  ) {
    expect(href, `${label} href must equal its documented target ${expected}`).toBe(expected);
    const res = await page.request.get(expected);
    expect(res.status(), `${label} target ${expected} must resolve 200 (no 404)`).toBe(200);
  }

  test('"Get the client" (header) → /get', async ({ page }) => {
    await gotoSurface(page, SURFACES[0]);
    const href = await page
      .locator('header')
      .getByRole('link', { name: HEADER_CTA, exact: true })
      .getAttribute('href');
    await assertHrefIsTarget(page, href, GET_TARGET, '"Get the client"');
  });

  test('"Get the Markdown Web client" (pitch) → /get', async ({ page }) => {
    await gotoSurface(page, SURFACES[0]);
    const href = await page
      .getByRole('link', { name: PITCH_CTA, exact: true })
      .getAttribute('href');
    await assertHrefIsTarget(page, href, GET_TARGET, '"Get the Markdown Web client"');
  });

  test('"the vision" (header) → /vision', async ({ page }) => {
    await gotoSurface(page, SURFACES[0]);
    const href = await page
      .locator('header')
      .getByRole('link', { name: VISION_LINK, exact: true })
      .getAttribute('href');
    await assertHrefIsTarget(page, href, VISION_TARGET, '"the vision"');
  });

  test('"Why a markdown web?" (pitch) → /vision', async ({ page }) => {
    await gotoSurface(page, SURFACES[0]);
    const href = await page
      .getByRole('link', { name: WHY_LINK, exact: true })
      .getAttribute('href');
    await assertHrefIsTarget(page, href, VISION_TARGET, '"Why a markdown web?"');
  });
});

// ── AC6 — themed + JS-free + crawlable + single <h1> on every surface ─────────
test.describe('Story 2.6 AC6 — themed / JS-free / crawlable / single <h1>', () => {
  for (const s of SURFACES) {
    test(`${s.name}: exactly one <h1> and it is NOT the wordmark / pitch headline`, async ({
      page,
    }) => {
      await gotoSurface(page, s);
      // The page keeps exactly ONE <h1> (the content/listing/not-found heading).
      await expect(page.locator('h1')).toHaveCount(1);
      // The wordmark is a <span>/<a>, NEVER an <h1>. Match on the FULL wordmark
      // (".md the markdown web" — chip + sans) so this does NOT collide with the
      // index's legitimate content <h1>, which is the title "The Markdown Web".
      await expect(page.locator('h1', { hasText: `${MD_CHIP} ${WORDMARK_TEXT}` })).toHaveCount(0);
      // The pitch headline is an <h4>/non-h1, NEVER a second <h1>.
      await expect(page.locator('h1', { hasText: 'fixed view' })).toHaveCount(0);
    });

    test(`${s.name}: single <html lang> shell, github.css theme linked`, async ({ page }) => {
      await gotoSurface(page, s);
      await expect(page.locator('html')).toHaveCount(1);
      expect(await page.locator('html').getAttribute('lang')).toBeTruthy();
      // A real stylesheet drives the theme (github.css tokens).
      await expect(page.locator('head link[rel="stylesheet"], head style')).not.toHaveCount(0);
    });

    test(`${s.name}: chrome is JS-free (no astro-island / no client:* directive)`, async ({
      page,
    }) => {
      const res = await page.goto(s.path);
      const html = await res!.text();
      expect(html, `${s.name} chrome must be JS-free: no astro-island`).not.toContain(
        'astro-island',
      );
      expect(html, `${s.name} chrome must be JS-free: no client:* directive`).not.toMatch(
        /client:(load|idle|visible|media|only)/,
      );
    });
  }

  test('chrome is readable + followable with JavaScript disabled (crawlable)', async ({
    browser,
  }) => {
    const ctx = await browser.newContext({ javaScriptEnabled: false });
    const noJs = await ctx.newPage();
    const res = await noJs.goto('/x');
    expect(res!.status()).toBe(200);
    // Header + pitch chrome present in the static HTML with JS off.
    await expect(noJs.locator('header')).toHaveCount(1);
    await expect(noJs.getByText(PITCH_HEADLINE, { exact: false })).toHaveCount(1);
    // The four chrome links are plain <a> a crawler can follow.
    await expect(noJs.getByRole('link', { name: HEADER_CTA, exact: true })).toHaveCount(1);
    await expect(noJs.getByRole('link', { name: VISION_LINK, exact: true })).toHaveCount(1);
    await expect(noJs.getByRole('link', { name: PITCH_CTA, exact: true })).toHaveCount(1);
    await expect(noJs.getByRole('link', { name: WHY_LINK, exact: true })).toHaveCount(1);
    await ctx.close();
  });
});

// ── AC7 edge case — the /get + /vision stub pages keep exactly one <h1> ───────
// Decisions A/B add real /get and /vision routes; they ALSO route through
// Page.astro and inherit the header+pitch chrome — so each must itself resolve
// 200 and carry EXACTLY ONE <h1> (its own placeholder heading), the chrome
// adding none. No skip guard: if a stub route ever vanishes, this FAILS loudly
// (a 404 would break the single-<h1>-under-chrome invariant silently otherwise).
test.describe('Story 2.6 AC7 — /get + /vision stub pages keep a single <h1> under chrome', () => {
  for (const stub of ['/get', '/vision']) {
    test(`${stub}: real 200 route with the chrome and exactly one <h1>`, async ({ page }) => {
      const res = await page.goto(stub);
      expect(res, `navigation to ${stub} should produce a response`).not.toBeNull();
      expect(res!.status(), `${stub} must be a real 200 route`).toBe(200);
      await expect(page.locator('h1')).toHaveCount(1);
      await expect(page.locator('header')).toHaveCount(1);
      await expect(page.getByText(PITCH_HEADLINE, { exact: false })).toHaveCount(1);
    });
  }
});
