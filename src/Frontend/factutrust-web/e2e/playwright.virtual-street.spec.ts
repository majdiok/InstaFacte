import { test, expect } from '@playwright/test';

test.describe('Visite virtuelle — rue publique', () => {
  test.describe.configure({ mode: 'serial', timeout: 60_000 });
  test("la page d'accueil propose un lien vers la visite virtuelle", async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('link', { name: /visite virtuelle/i }).first()).toBeVisible();
  });

  test('la route /visite-virtuelle affiche la Rue InstaFact', async ({ page }) => {
    await page.goto('/visite-virtuelle');
    await expect(page.getByRole('heading', { name: 'Rue InstaFact' })).toBeVisible();
    await expect(page.getByRole('link', { name: /version accessible/i })).toBeVisible();
  });

  test('canvas 3D : tabindex clavier si la scène est rendue (pas mouvement réduit)', async ({ page }) => {
    await page.goto('/visite-virtuelle');
    const reduced = await page.evaluate(() => matchMedia('(prefers-reduced-motion: reduce)').matches);
    if (reduced) return;
    const canvas = page.locator('canvas.street-canvas');
    if ((await canvas.count()) > 0) {
      await expect(canvas).toBeVisible();
      await expect(canvas).toHaveAttribute('tabindex', '0');
    }
  });

  test('la liste accessible est joignable', async ({ page }) => {
    await page.goto('/visite-virtuelle/liste');
    await expect(page.locator('body')).toContainText(/vitrine|liste|rue/i);
  });

  test('les GLB de façades statiques sont servis (même origine)', async ({ request }) => {
    const files = ['classic.glb', 'modern.glb', 'vintage.glb', 'minimal.glb', 'artisan.glb'];
    for (const name of files) {
      const res = await request.get(`/assets/virtual-street/facades/${name}`);
      expect(res.status(), name).toBe(200);
      const ct = (res.headers()['content-type'] ?? '').toLowerCase();
      if (ct.length > 0) {
        expect(ct).toMatch(/octet-stream|model\/gltf-binary|application\/octet-stream/);
      }
    }
  });

  test('slug vitrine inconnu : état d’erreur explicite et navigation', async ({ page }) => {
    const randomSlug = `zz-no-such-store-${Date.now()}`;
    await page.goto(`/visite-virtuelle/${randomSlug}`, { waitUntil: 'domcontentloaded' });
    // Avec API locale : 404 → message « pas disponible publiquement ». Sans backend : message réseau.
    await expect(page.getByRole('alert')).toContainText(
      /pas disponible publiquement|Impossible de joindre le service des vitrines publiques/i
    );
    await expect(page.getByRole('navigation', { name: 'Navigation visite virtuelle' })).toBeVisible();
  });
});
