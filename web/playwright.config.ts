import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright E2E harness for the Astro web app (Story 2.1).
 *
 * The ACs are about the DEPLOYED static HTML, so we build the site and serve
 * `web/dist` via `astro preview`, then run assertions against that output.
 *
 * `webServer` runs `npm run build && npm run preview`; the build is part of the
 * server command so `npx playwright test` is a single, CI-friendly entrypoint.
 * Astro preview defaults to port 4321.
 */
const PREVIEW_PORT = 4321;

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: process.env.CI ? 'list' : [['list']],
  use: {
    baseURL: `http://localhost:${PREVIEW_PORT}`,
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: {
    command: 'npm run build && npm run preview',
    url: `http://localhost:${PREVIEW_PORT}/`,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
