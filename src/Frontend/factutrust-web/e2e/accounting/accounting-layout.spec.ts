import { test, expect } from '@playwright/test';

test.describe('Accounting UI layout', () => {
  test('ledger toolbar buttons do not overlap at 1280px', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/accounting/ledger');
    if (page.url().includes('/auth/login')) {
      test.skip(true, 'Authentication required');
    }

    const afficher = page.getByRole('button', { name: /Afficher/i });
    const ia = page.getByRole('button', { name: /Analyser/i });
    await expect(afficher).toBeVisible();
    await expect(ia).toBeVisible();

    const a = await afficher.boundingBox();
    const b = await ia.boundingBox();
    expect(a).toBeTruthy();
    expect(b).toBeTruthy();
    if (a && b) {
      const overlap =
        a.x < b.x + b.width &&
        a.x + a.width > b.x &&
        a.y < b.y + b.height &&
        a.y + a.height > b.y;
      expect(overlap).toBe(false);
    }
  });
});
