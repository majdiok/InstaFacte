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

function assertFullyInViewport(
  box: { x: number; y: number; width: number; height: number },
  viewport: { width: number; height: number }
): void {
  expect(box.width).toBeGreaterThan(0);
  expect(box.height).toBeGreaterThan(0);
  expect(box.x).toBeGreaterThanOrEqual(-1);
  expect(box.y).toBeGreaterThanOrEqual(-1);
  expect(box.x + box.width).toBeLessThanOrEqual(viewport.width + 1);
  expect(box.y + box.height).toBeLessThanOrEqual(viewport.height + 1);
}

/** Guards against ng-bootstrap static/navbar mode: menu must sit below the trigger. */
async function assertMenuBelowTrigger(
  trigger: import('@playwright/test').Locator,
  menu: import('@playwright/test').Locator
): Promise<void> {
  await expect(trigger).toHaveAttribute('aria-expanded', 'true');
  const triggerBox = await trigger.boundingBox();
  const menuBox = await menu.boundingBox();
  expect(triggerBox).toBeTruthy();
  expect(menuBox).toBeTruthy();
  expect(menuBox!.y).toBeGreaterThanOrEqual(triggerBox!.y + triggerBox!.height - 8);
}

test.describe('Header dropdowns', () => {
  test('notifications panel is fully visible and not clipped', async ({ page }) => {
    if (!(await ensureAuthenticated(page))) {
      test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
    }

    await page.setViewportSize({ width: 1280, height: 800 });
    await waitForAppReady(page);
    await page.goto('/dashboard');

    const bell = page.locator('#notificationsDropdown');
    await expect(bell).toBeVisible();
    await bell.click();

    const menu = page.locator('.notif-menu.dropdown-menu.show, .notif-menu.show, .notif-menu').first();
    await expect(menu).toBeVisible();
    await assertMenuBelowTrigger(bell, menu);

    const box = await menu.boundingBox();
    expect(box).toBeTruthy();
    expect(box!.width).toBeGreaterThanOrEqual(280);
    expect(box!.height).toBeGreaterThan(40);
    assertFullyInViewport(box!, { width: 1280, height: 800 });

    const emptyOrItems = menu.locator('.notif-menu__empty, .notif-item');
    await expect(emptyOrItems.first()).toBeVisible();

    const emptyText = menu.locator('.notif-menu__empty p');
    if ((await emptyText.count()) > 0) {
      await expect(emptyText).toHaveText('Aucune notification');
    }
  });

  test('profile menu is readable with expected actions', async ({ page }) => {
    if (!(await ensureAuthenticated(page))) {
      test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
    }

    await page.setViewportSize({ width: 1280, height: 800 });
    await waitForAppReady(page);
    await page.goto('/dashboard');

    const trigger = page.locator('#userDropdown');
    await expect(trigger).toBeVisible();
    await trigger.click();

    const menu = page.locator('.main-header__user-menu.dropdown-menu.show, .main-header__user-menu').first();
    await expect(menu).toBeVisible();
    await assertMenuBelowTrigger(trigger, menu);

    const box = await menu.boundingBox();
    expect(box).toBeTruthy();
    expect(box!.width).toBeGreaterThanOrEqual(160);
    assertFullyInViewport(box!, { width: 1280, height: 800 });

    await expect(menu.getByText('Mon profil', { exact: true })).toBeVisible();
    await expect(menu.getByText('Déconnexion', { exact: true })).toBeVisible();
  });

  test('quick access panel opens without clipping', async ({ page }) => {
    if (!(await ensureAuthenticated(page))) {
      test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
    }

    await page.setViewportSize({ width: 1280, height: 800 });
    await waitForAppReady(page);
    await page.goto('/dashboard');

    const trigger = page.locator('#quickAccessDropdown');
    if ((await trigger.count()) === 0) {
      test.skip(true, 'Quick access not available for this user');
    }

    await expect(trigger).toBeVisible();
    await trigger.click();

    const menu = page.locator('.quick-access__panel.dropdown-menu.show, .quick-access__panel').first();
    await expect(menu).toBeVisible();
    await assertMenuBelowTrigger(trigger, menu);

    const box = await menu.boundingBox();
    expect(box).toBeTruthy();
    expect(box!.width).toBeGreaterThanOrEqual(280);
    expect(box!.height).toBeGreaterThan(40);
    assertFullyInViewport(box!, { width: 1280, height: 800 });
  });
});
