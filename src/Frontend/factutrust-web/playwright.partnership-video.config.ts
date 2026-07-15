import { defineConfig, devices } from '@playwright/test';
import * as path from 'path';

/**
 * Playwright config for expert-comptable partnership screencast.
 * Records one video per chapter test → docs/partnership/expert-comptable/raw/
 */
export default defineConfig({
  testDir: './e2e',
  testMatch: 'partnership-expert-comptable-video.spec.ts',
  timeout: 300 * 1000,
  expect: { timeout: 10000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list']],
  use: {
    baseURL: 'http://localhost:4200',
    ignoreHTTPSErrors: true,
    viewport: { width: 1920, height: 1080 },
    contextOptions: {
      recordVideo: {
        dir: path.join(__dirname, '..', '..', '..', 'docs', 'partnership', 'expert-comptable', 'raw'),
        size: { width: 1920, height: 1080 }
      }
    },
    launchOptions: {
      slowMo: 150
    },
    trace: 'off',
    screenshot: 'off'
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] }
    }
  ],
  outputDir: path.join(__dirname, 'test-results-partnership'),
  globalSetup: require.resolve('./e2e/partnership-global-setup.ts'),
  webServer: {
    command: 'npm start',
    url: 'http://localhost:4200',
    reuseExistingServer: !process.env.CI,
    timeout: 120 * 1000
  }
});
