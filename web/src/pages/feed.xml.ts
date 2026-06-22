/**
 * Story 5.2 — Follow / Feed: static endpoint emitting /feed.xml at build time.
 *
 * An Astro static endpoint (`feed.xml.ts`) that calls getCollection('pages'),
 * feeds entries to the pure builder in `web/src/lib/feed.mjs`, and returns
 * a Response with Content-Type: application/rss+xml.
 *
 * This file name (`feed.xml.ts`) causes Astro to emit `dist/feed.xml` as a
 * normal static asset on every build — no SSR, no client:*, no new dependency.
 *
 * Design decisions (AC5 / Dev Agent Record):
 *   - Content-Type: application/rss+xml (the IANA-registered media type for RSS).
 *   - Uses context.site (= Astro.site, the production origin from astro.config.mjs).
 *   - Feed builder is the pure `buildFeed` from feed.mjs — no @astrojs/rss dep.
 *   - The feed is generated from getCollection('pages'), the SAME source as the
 *     2.5 index, so the feed set and the index set can never drift.
 */

import { getCollection } from 'astro:content';
import { buildFeed } from '../lib/feed.mjs';

export async function GET(context: { site?: URL }): Promise<Response> {
  const entries = await getCollection('pages');

  // context.site is a URL object (from Astro.site); buildFeed accepts string or URL.
  // Pass the URL object directly — feed.mjs normalises it via toString/href internally.
  const siteArg: unknown = context.site ?? 'https://themarkdownweb.com';
  const xml = buildFeed(entries, {
    title: 'The Markdown Web',
    description: 'Every page in this vault — follow along as the author adds new content.',
    site: siteArg as string,
  });

  return new Response(xml, {
    headers: {
      'Content-Type': 'application/rss+xml',
    },
  });
}
