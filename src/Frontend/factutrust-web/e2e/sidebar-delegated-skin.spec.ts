import { test, expect } from '@playwright/test';
import { getDemoCredentials, loginAsDemoUser, waitForAppReady } from './helpers/partnership-demo.helpers';

async function ensureAuthenticated(page: import('@playwright/test').Page): Promise<boolean> {
  const creds = getDemoCredentials();
  if (!creds) {
    return false;
  }
  await loginAsDemoUser(page);
  return true;
}

test.describe('Sidebar delegated skin', () => {
  test('shows firm-delegated skin and footer when dossier context is active', async ({ page }) => {
    if (!(await ensureAuthenticated(page))) {
      test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
    }

    await page.setViewportSize({ width: 1280, height: 800 });
    await waitForAppReady(page);
    await page.goto('/dashboard');

    const hasDelegatedHeader = (await page.locator('.rail-label', { hasText: 'Dossier :' }).count()) > 0;
    if (!hasDelegatedHeader) {
      test.skip(true, 'Delegated accounting-firm context not active for this account');
    }

    const sidebar = page.locator('#sidebar');
    await expect(sidebar).toHaveClass(/sidebar--firm-delegated/);
    await expect(page.locator('.sidebar-delegated-footer')).toBeVisible();
    await expect(page.locator('.delegated-footer-link', { hasText: 'Contrôle & Audit' })).toBeVisible();
    await expect(page.locator('.rail-collapse-btn--header')).toBeVisible();
    await expect(page.locator('.sidebar-footer-collapse')).toHaveCount(0);
  });

  test('company mode does not apply delegated skin', async ({ page }) => {
    if (!(await ensureAuthenticated(page))) {
      test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
    }

    await page.setViewportSize({ width: 1280, height: 800 });
    await waitForAppReady(page);
    await page.goto('/dashboard');

    const hasDelegatedHeader = (await page.locator('.rail-label', { hasText: 'Dossier :' }).count()) > 0;
    if (hasDelegatedHeader) {
      test.skip(true, 'Delegated context active — cannot assert company skin on this account');
    }

    await expect(page.locator('#sidebar')).not.toHaveClass(/sidebar--firm-delegated/);
    await expect(page.locator('.sidebar-delegated-footer')).toHaveCount(0);
    await expect(page.locator('.sidebar-footer-collapse')).toBeVisible();
  });
});
