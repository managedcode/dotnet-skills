# Playwright Visual Testing Patterns

Use this reference when implementing screenshot baselines, Pixelmatch comparison, or GitHub Actions wiring.

Sources reviewed:

- Pradap Pandiyan, "Pixel-by-Pixel Visual Testing Using Playwright with GitHub Actions": https://pradappandiyan.medium.com/pixel-by-pixel-visual-testing-using-playwright-with-github-actions-f47700f8a50e
- Example repository from the article: https://github.com/pradapjackie/playwright-visual-testing
- Playwright visual comparisons: https://playwright.dev/docs/test-snapshots
- Playwright CI setup: https://playwright.dev/docs/ci
- Playwright GitHub Actions setup: https://playwright.dev/docs/ci-intro

The official CI and visual-comparison pages were re-reviewed in July 2026. Their core operational contract is unchanged: install the exact browser/dependency set in CI, keep the rendering environment stable, and update snapshot baselines only through an explicit reviewed command.

## Preferred Built-In Snapshot Path

Use this path when the repo already uses Playwright Test or can add it cleanly. Playwright's `toHaveScreenshot` integrates snapshot storage, pixel comparison, retry-until-stable behavior, traces, and the HTML report.

```ts
import { expect, test } from '@playwright/test';

test('home page visual baseline', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto('/');
  await page.getByRole('main').waitFor();

  await expect(page).toHaveScreenshot('home-page.png', {
    fullPage: true,
    maxDiffPixelRatio: 0.005,
    animations: 'disabled'
  });
});
```

Project-level defaults keep threshold choices reviewable:

```ts
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  workers: process.env.CI ? 1 : undefined,
  use: {
    baseURL: process.env.APP_BASE_URL ?? 'http://127.0.0.1:5000',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    viewport: { width: 1280, height: 800 },
    deviceScaleFactor: 1,
    colorScheme: 'light'
  },
  expect: {
    toHaveScreenshot: {
      maxDiffPixelRatio: 0.005,
      animations: 'disabled',
      stylePath: './tests/visual-stability.css'
    }
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } }
  ]
});
```

Use a stability stylesheet for volatile areas:

```css
[data-visual-hide],
iframe,
.timestamp,
.toast,
.skeleton,
.spinner {
  visibility: hidden !important;
}

* {
  caret-color: transparent !important;
}
```

## Standalone Pixelmatch Path

Use this path only when the repo needs a custom compare script, central screenshot folders, or comparison of images not produced by Playwright Test snapshots.

Install locally:

```bash
npm install --save-dev @playwright/test playwright pixelmatch pngjs fs-extra
npx playwright install
```

Capture screenshots:

```ts
import { test } from '@playwright/test';
import fsExtra from 'fs-extra';

test('capture home page screenshot', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto(process.env.APP_BASE_URL ?? 'https://example.com');
  await page.getByRole('main').waitFor();

  await fsExtra.ensureDir('screenshots/actual');
  await page.screenshot({
    path: 'screenshots/actual/home-page.png',
    fullPage: true,
    animations: 'disabled'
  });
});
```

Compare images:

```js
import fs from 'node:fs';
import fsExtra from 'fs-extra';
import { PNG } from 'pngjs';
import pixelmatch from 'pixelmatch';

const cases = ['home-page'];
const allowedMismatchRatio = 0.005;

await fsExtra.ensureDir('screenshots/diff');

let failed = false;

for (const name of cases) {
  const baselinePath = `screenshots/baseline/${name}.png`;
  const actualPath = `screenshots/actual/${name}.png`;
  const diffPath = `screenshots/diff/${name}.png`;

  if (!fs.existsSync(baselinePath)) {
    throw new Error(`Missing baseline: ${baselinePath}`);
  }

  const baseline = PNG.sync.read(fs.readFileSync(baselinePath));
  const actual = PNG.sync.read(fs.readFileSync(actualPath));

  if (baseline.width !== actual.width || baseline.height !== actual.height) {
    throw new Error(`${name}: image dimensions differ`);
  }

  const diff = new PNG({ width: baseline.width, height: baseline.height });
  const mismatchedPixels = pixelmatch(
    baseline.data,
    actual.data,
    diff.data,
    baseline.width,
    baseline.height,
    {
      threshold: 0.15,
      includeAA: false,
      alpha: 0.8
    }
  );

  fs.writeFileSync(diffPath, PNG.sync.write(diff));

  const mismatchRatio = mismatchedPixels / (baseline.width * baseline.height);
  console.log(`${name}: ${mismatchedPixels} pixels, ${(mismatchRatio * 100).toFixed(2)}% mismatch`);

  if (mismatchRatio > allowedMismatchRatio) {
    failed = true;
  }
}

if (failed) {
  process.exitCode = 1;
}
```

For custom Pixelmatch flows, do not create baselines inside CI. Fail with a clear missing-baseline error so reviewers intentionally add baseline images.

## GitHub Actions

Prefer current maintained actions and upload artifacts even on failures:

```yaml
name: Playwright Visual Tests

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  visual:
    timeout-minutes: 60
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v5

      - uses: actions/setup-node@v6
        with:
          node-version: lts/*
          cache: npm

      - name: Install dependencies
        run: npm ci

      - name: Install Playwright browsers
        run: npx playwright install --with-deps

      - name: Run visual tests
        run: npx playwright test --project=chromium

      - name: Upload Playwright report
        if: ${{ !cancelled() }}
        uses: actions/upload-artifact@v5
        with:
          name: playwright-report
          path: playwright-report/
          retention-days: 30

      - name: Upload test results
        if: ${{ !cancelled() }}
        uses: actions/upload-artifact@v5
        with:
          name: playwright-test-results
          path: test-results/
          retention-days: 30
```

For pull requests, a preliminary `--only-changed` run can surface likely failures sooner, but it must not replace the complete run:

```yaml
      - name: Run likely affected tests first
        if: github.event_name == 'pull_request'
        run: npx playwright test --only-changed=origin/$GITHUB_BASE_REF

      - name: Run the full Playwright suite
        run: npx playwright test
```

Use `fetch-depth: 0` on `actions/checkout` when the job needs the base ref for `--only-changed`. Treat the result as a heuristic only. Visual baselines must still be generated and compared in the same rendering environment; pin an official Playwright container image when runner-level browser, font, or OS drift remains noisy.

For a standalone Pixelmatch script, run the compare step after screenshot capture and upload image folders:

```yaml
      - name: Compare screenshots
        run: npm run compare

      - name: Upload visual diffs
        if: ${{ !cancelled() }}
        uses: actions/upload-artifact@v5
        with:
          name: visual-screenshots
          path: |
            screenshots/baseline/
            screenshots/actual/
            screenshots/diff/
          retention-days: 30
```

## Triage Checklist

- Expected changed and actual is correct: update baselines intentionally and mention the product change.
- Actual changed unexpectedly: fix the UI or data setup before updating baselines.
- Diff is font rendering or antialiasing noise: align OS/browser/font setup, then tune threshold narrowly.
- Diff is clock, user data, animation, ad, or network content: freeze or mask the volatile region.
- Diff is one browser only: keep browser-specific baselines or narrow the visual gate to the browser that reflects the product requirement.
