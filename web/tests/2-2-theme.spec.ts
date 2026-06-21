import { test, expect, type Page } from '@playwright/test';

/**
 * Story 2.2 — Apply the GitHub-style default theme. (TDD RED phase.)
 *
 * These specs are written FAILING-FIRST: they encode the DESIGN.md token
 * contract for the themed `/x` render before `web/src/styles/github.css`,
 * the `Page.astro` stylesheet wiring, and the Shiki `github-light` config
 * exist. They MUST fail on the current (unthemed, `github-dark`) build and
 * pass once Story 2.2 Step 5 implements the theme.
 *
 * They are purely additive to the existing 20 specs (ac1/ac2/ac3/ac5/ac6),
 * which must remain green — this file only READS computed styles and never
 * changes the asserted semantic shell.
 *
 * All assertions read RENDERED values via getComputedStyle so a regression
 * where the rendered color drifts from the token is caught — colors are not
 * inferred from the raw HTML hex.
 *
 * Token source of truth — DESIGN.md frontmatter (lines 10–41):
 *   surface #ffffff · fg #1f2328 · ink #0d1117 · muted #59636e ·
 *   border #d1d9e0 · link #0969da · code-bg #f6f8fa ·
 *   code{ keyword #cf222e, string #0a3069, comment #59636e,
 *         function #8250df, number #0550ae }
 *   sans -apple-system,… · mono ui-monospace,… · base 16px / line 1.6 ·
 *   measure 760px · page-x 24px · radius.code 6px ·
 *   scale h1 2.1em / h2 1.5em / code 85%
 */

// ---------------------------------------------------------------------------
// WCAG 2.1 §1.4.3 relative-luminance + contrast-ratio helpers (deterministic,
// in-test — no a11y dependency, so the gate fails loudly on a token regression).
// ---------------------------------------------------------------------------

/** Parse "rgb(r, g, b)" / "rgba(r, g, b, a)" -> [r,g,b] in 0..255. */
function parseRgb(color: string): [number, number, number] {
  const m = color.match(/rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)/i);
  if (!m) throw new Error(`Cannot parse color: "${color}"`);
  return [Number(m[1]), Number(m[2]), Number(m[3])];
}

/** WCAG 2.1 relative luminance of an sRGB color. */
function relativeLuminance([r, g, b]: [number, number, number]): number {
  const lin = (c: number) => {
    const s = c / 255;
    return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b);
}

/** WCAG 2.1 contrast ratio between two sRGB colors. */
function contrastRatio(fg: string, bg: string): number {
  const l1 = relativeLuminance(parseRgb(fg));
  const l2 = relativeLuminance(parseRgb(bg));
  const [hi, lo] = l1 >= l2 ? [l1, l2] : [l2, l1];
  return (hi + 0.05) / (lo + 0.05);
}

const AA_NORMAL = 4.5;

/** Resolve a computed style property for the first match of a selector. */
async function computed(page: Page, selector: string, prop: string): Promise<string> {
  return page.locator(selector).first().evaluate(
    (el, p) => getComputedStyle(el as Element).getPropertyValue(p as string),
    prop,
  );
}

test.describe('Story 2.2 AC1 — DESIGN tokens applied (typography, color, 760px measure)', () => {
  test.beforeEach(async ({ page }) => {
    const response = await page.goto('/x');
    expect(response, 'route /x should exist').not.toBeNull();
    expect(response!.status(), '/x should return 200').toBe(200);
  });

  test('body uses the DESIGN sans font stack', async ({ page }) => {
    const family = (await computed(page, 'body', 'font-family')).toLowerCase();
    // The sans stack leads with -apple-system / BlinkMacSystemFont / Segoe UI.
    expect(
      family.includes('-apple-system') ||
        family.includes('blinkmacsystemfont') ||
        family.includes('segoe ui'),
      `body font-family should be the DESIGN sans stack, got "${family}"`,
    ).toBeTruthy();
    // It must NOT be a bare/serif default (e.g. just "Times" / serif).
    expect(family).not.toMatch(/^times|^serif$/);
  });

  test('base font-size is 16px and line-height is ~1.6', async ({ page }) => {
    const fontSize = await computed(page, 'body', 'font-size');
    expect(fontSize, 'base font-size token is 16px').toBe('16px');

    const lh = await computed(page, 'body', 'line-height');
    // line-height 1.6 on 16px = 25.6px (browsers may report ~25.6px). Accept
    // unitless "1.6" or a px value close to 16 * 1.6.
    if (lh.endsWith('px')) {
      expect(Math.abs(parseFloat(lh) - 25.6)).toBeLessThanOrEqual(1.5);
    } else {
      expect(Math.abs(parseFloat(lh) - 1.6)).toBeLessThanOrEqual(0.05);
    }
  });

  test('content column is centered at the 760px reading measure', async ({ page }) => {
    // The measure may sit on <body>, <main>, or <article>; assert at least one
    // ancestor of the article content computes max-width: 760px.
    const maxWidths = await page.evaluate(() => {
      const out: Record<string, string> = {};
      for (const sel of ['body', 'main', 'article']) {
        const el = document.querySelector(sel);
        if (el) out[sel] = getComputedStyle(el).maxWidth;
      }
      return out;
    });
    const has760 = Object.values(maxWidths).some(
      (v) => v === '760px',
    );
    expect(
      has760,
      `expected one of body/main/article to have max-width 760px, got ${JSON.stringify(maxWidths)}`,
    ).toBeTruthy();
  });

  test('body text is colors.fg (#1f2328) on the colors.surface (#ffffff) background', async ({ page }) => {
    const color = await computed(page, 'body', 'color');
    expect(parseRgb(color)).toEqual([31, 35, 40]); // #1f2328

    const bg = await computed(page, 'body', 'background-color');
    expect(parseRgb(bg)).toEqual([255, 255, 255]); // #ffffff
  });

  test('links are colors.link (#0969da)', async ({ page }) => {
    // The fixture has the bare autolink to themarkdownweb.com.
    const linkColor = await computed(page, 'article a, main a, a', 'color');
    expect(parseRgb(linkColor)).toEqual([9, 105, 218]); // #0969da
  });

  test('h1 and h2 carry a bottom hairline border', async ({ page }) => {
    for (const tag of ['h1', 'h2']) {
      const width = await computed(page, tag, 'border-bottom-width');
      const style = await computed(page, tag, 'border-bottom-style');
      expect(
        parseFloat(width),
        `${tag} should have a bottom hairline border width`,
      ).toBeGreaterThan(0);
      expect(style, `${tag} bottom border should be a visible line`).not.toBe('none');
    }
  });
});

test.describe('Story 2.2 AC2 — code highlighted in the light GitHub palette (not github-dark)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/x');
  });

  test('<pre.astro-code> no longer carries the github-dark class', async ({ page }) => {
    const cls = await page.locator('pre.astro-code').first().getAttribute('class');
    expect(cls, 'a syntax-highlighted <pre.astro-code> should exist').toBeTruthy();
    expect(cls!).not.toContain('github-dark');
  });

  test('fenced <pre> background is the light code surface #f6f8fa (not dark)', async ({ page }) => {
    const bg = await computed(page, 'pre.astro-code', 'background-color');
    expect(parseRgb(bg), 'code surface should be colors.code-bg #f6f8fa').toEqual([
      246, 248, 250,
    ]);
  });

  test('fenced <pre> has the 6px code radius', async ({ page }) => {
    const radius = await computed(page, 'pre.astro-code', 'border-top-left-radius');
    expect(parseFloat(radius), 'code corners use rounded.code 6px').toBeCloseTo(6, 0);
  });

  test('token spans use light colors, not the dark github-dark palette', async ({ page }) => {
    // Collect every distinct inline token color inside the fenced block.
    const tokenColors = await page.locator('pre.astro-code code span').evaluateAll(
      (spans) =>
        Array.from(
          new Set(
            spans.map((s) => getComputedStyle(s as Element).color),
          ),
        ),
    );
    expect(tokenColors.length, 'fenced block should emit colored token spans').toBeGreaterThan(0);

    // The dark github-dark token palette (#F97583, #9ECBFF, #B392F0, #E1E4E8,
    // #FFAB70) must NOT be present.
    const darkPalette = new Set([
      'rgb(249, 117, 131)', // #F97583
      'rgb(158, 203, 255)', // #9ECBFF
      'rgb(179, 146, 240)', // #B392F0
      'rgb(225, 228, 232)', // #E1E4E8
      'rgb(255, 171, 112)', // #FFAB70
    ]);
    for (const c of tokenColors) {
      const [r, g, b] = parseRgb(c);
      expect(
        darkPalette.has(`rgb(${r}, ${g}, ${b})`),
        `dark github-dark token color ${c} must not be emitted by the light theme`,
      ).toBeFalsy();
    }
  });
});

test.describe('Story 2.2 AC3 — every text/surface pairing meets WCAG 2.1 AA (>= 4.5:1)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/x');
  });

  test('the WCAG helper reproduces the DESIGN-computed reference ratios', () => {
    // Sanity-pin the in-test math against the story's pre-computed ratios.
    expect(contrastRatio('rgb(31, 35, 40)', 'rgb(255, 255, 255)')).toBeCloseTo(15.8, 1); // fg
    expect(contrastRatio('rgb(13, 17, 23)', 'rgb(255, 255, 255)')).toBeCloseTo(18.93, 1); // ink
    expect(contrastRatio('rgb(9, 105, 218)', 'rgb(255, 255, 255)')).toBeCloseTo(5.19, 1); // link
    expect(contrastRatio('rgb(89, 99, 110)', 'rgb(255, 255, 255)')).toBeCloseTo(6.11, 1); // muted
    expect(contrastRatio('rgb(130, 80, 223)', 'rgb(246, 248, 250)')).toBeCloseTo(4.74, 1); // function on code-bg
    expect(contrastRatio('rgb(207, 34, 46)', 'rgb(246, 248, 250)')).toBeCloseTo(5.03, 1); // keyword on code-bg
  });

  test('body fg, headings ink, and links all clear 4.5:1 on the surface', async ({ page }) => {
    const surface = await computed(page, 'body', 'background-color');

    const fg = await computed(page, 'body', 'color');
    expect(contrastRatio(fg, surface)).toBeGreaterThanOrEqual(AA_NORMAL);

    const ink = await computed(page, 'h1', 'color');
    expect(contrastRatio(ink, surface)).toBeGreaterThanOrEqual(AA_NORMAL);

    const link = await computed(page, 'article a, main a, a', 'color');
    expect(contrastRatio(link, surface)).toBeGreaterThanOrEqual(AA_NORMAL);
  });

  test('every emitted Shiki token clears 4.5:1 on the code-bg surface', async ({ page }) => {
    const preBg = await computed(page, 'pre.astro-code', 'background-color');
    const tokenColors = await page.locator('pre.astro-code code span').evaluateAll((spans) =>
      Array.from(
        new Set(spans.map((s) => getComputedStyle(s as Element).color)),
      ),
    );
    expect(tokenColors.length).toBeGreaterThan(0);
    for (const c of tokenColors) {
      const ratio = contrastRatio(c, preBg);
      expect(
        ratio,
        `Shiki token ${c} on ${preBg} = ${ratio.toFixed(2)}:1 must clear AA 4.5:1`,
      ).toBeGreaterThanOrEqual(AA_NORMAL);
    }
  });

  test('the tight function (#8250df) and keyword (#cf222e) tokens are pinned on #f6f8fa', async ({ page }) => {
    // These are the tightest pairings (4.74:1 / 5.03:1). If github-light emits a
    // LIGHTER purple/red than DESIGN, the rendered ratio dips below AA — assert
    // against the RENDERED pre background so the gate catches that drift.
    const preBg = await computed(page, 'pre.astro-code', 'background-color');

    // DESIGN target tokens must themselves clear AA on the code surface.
    expect(
      contrastRatio('rgb(130, 80, 223)', preBg), // function #8250df
      'DESIGN function token #8250df must clear AA on the rendered code-bg',
    ).toBeGreaterThanOrEqual(AA_NORMAL);
    expect(
      contrastRatio('rgb(207, 34, 46)', preBg), // keyword #cf222e
      'DESIGN keyword token #cf222e must clear AA on the rendered code-bg',
    ).toBeGreaterThanOrEqual(AA_NORMAL);

    // And the code surface itself must be the light #f6f8fa these ratios assume.
    expect(parseRgb(preBg)).toEqual([246, 248, 250]);
  });
});

test.describe('Story 2.2 AC5 — robustness: overflow, inline-vs-block, nested blockquote', () => {
  test.beforeEach(async ({ page }) => {
    // Pin a deterministic desktop viewport so the 760px measure is meaningful.
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.goto('/x');
  });

  test('a long unbreakable code line scrolls inside <pre> (overflow-x), not the body', async ({ page }) => {
    const overflowX = await computed(page, 'pre.astro-code', 'overflow-x');
    expect(['auto', 'scroll'], `pre overflow-x was "${overflowX}"`).toContain(overflowX);

    // The long line must actually overflow the pre's own box (proving scroll is
    // needed) but NOT the document.
    const preScrolls = await page.locator('pre.astro-code').first().evaluate(
      (el) => (el as HTMLElement).scrollWidth > (el as HTMLElement).clientWidth + 1,
    );
    expect(preScrolls, 'the long line should overflow the <pre> content box').toBeTruthy();

    const bodyOverflows = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
    );
    expect(bodyOverflows, 'the document must NOT scroll horizontally').toBeFalsy();
  });

  test('the 760px reading measure is not blown out by the wide code/table', async ({ page }) => {
    const measure = await page.evaluate(() => {
      const el =
        document.querySelector('article') ??
        document.querySelector('main') ??
        document.body;
      return el.getBoundingClientRect().width;
    });
    // The content column stays at (or under) the 760px measure + page padding,
    // not stretched to the wide code line.
    expect(measure, `content width ${measure}px should respect the 760px measure`).toBeLessThanOrEqual(
      760 + 2 * 24 + 1,
    );
  });

  test('inline <code> gets the code-bg chip but block code does not double-apply it', async ({ page }) => {
    // Inline code: a <code> whose parent is NOT <pre>.
    const inlineBg = await page.locator(':not(pre) > code').first().evaluate(
      (el) => getComputedStyle(el as Element).backgroundColor,
    );
    expect(parseRgb(inlineBg), 'inline code should carry the code-bg chip fill #f6f8fa').toEqual([
      246, 248, 250,
    ]);

    // Inline code has no block padding/scroll treatment — distinct from <pre>.
    const inlineOverflowX = await page.locator(':not(pre) > code').first().evaluate(
      (el) => getComputedStyle(el as Element).overflowX,
    );
    expect(inlineOverflowX, 'inline code must not get the block scroll treatment').toBe('visible');

    // The <code> INSIDE <pre> must not re-apply its own chip on top of the pre
    // surface (it should be transparent / inherit the pre background).
    const preCodeBg = await page.locator('pre code').first().evaluate(
      (el) => getComputedStyle(el as Element).backgroundColor,
    );
    const [r, g, b, a] = (() => {
      const m = preCodeBg.match(/rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)(?:\s*,\s*([\d.]+))?/i);
      return m ? [Number(m[1]), Number(m[2]), Number(m[3]), m[4] === undefined ? 1 : Number(m[4])] : [0, 0, 0, 0];
    })();
    const transparent = a === 0;
    const sameAsSurface = r === 246 && g === 248 && b === 250;
    expect(
      transparent || sameAsSurface,
      `pre > code should not double-apply a chip (got ${preCodeBg})`,
    ).toBeTruthy();
  });

  test('a nested blockquote keeps a visible left-rule at each level', async ({ page }) => {
    const outer = page.locator('blockquote').first();
    await expect(outer).toBeVisible();
    const outerBorder = await outer.evaluate(
      (el) => parseFloat(getComputedStyle(el as Element).borderLeftWidth),
    );
    expect(outerBorder, 'outer blockquote should have a left rule').toBeGreaterThan(0);

    const inner = page.locator('blockquote blockquote').first();
    await expect(inner, 'fixture has a nested blockquote (AC5)').toHaveCount(1);
    const innerBorder = await inner.evaluate(
      (el) => parseFloat(getComputedStyle(el as Element).borderLeftWidth),
    );
    expect(
      innerBorder,
      'nested blockquote must keep its own left rule (not collapse/merge)',
    ).toBeGreaterThan(0);
  });
});
