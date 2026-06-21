// Story 2.7 — Task 3: the Azure Functions v4 REGISTRATION entry (thin shim).
// Node v4 programming model: `app.http(...)` registration, no per-function
// `function.json`. All request/response adaptation lives in `adapter.mjs` (no
// Azure dependency, CI-testable) and all DECISION logic in `negotiate.mjs`
// (the pure handler) — this file only wires them to the Azure runtime.

import { app } from '@azure/functions';
import { handleNegotiate, rawSlugFromRequest } from './adapter.mjs';
import { readMd } from './vault.mjs';

/** The Azure HTTP handler: adapt request -> pure core -> HttpResponse. */
export async function negotiateHttp(request) {
  const result = handleNegotiate({
    acceptHeader: request.headers.get('accept'),
    rawSlug: rawSlugFromRequest(request),
    method: request.method,
    readMd,
  });
  return { status: result.status, headers: result.headers, body: result.body };
}

// Catch-all route so a SWA rewrite can forward any page path here. Anonymous
// auth (the static host fronts it); GET/HEAD only.
app.http('negotiate', {
  methods: ['GET', 'HEAD'],
  authLevel: 'anonymous',
  route: 'negotiate/{*slug}',
  handler: negotiateHttp,
});
