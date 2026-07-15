import { FORECASTING_ROUTES } from './forecasting.routes';

/**
 * Verifies the routing topology of the Forecasting module. After the 2026-05-13 V1 cutover
 * a single canonical URL `/forecasting/replenishment` directly loads the V2 board — no
 * fallback alias, no feature-flag-driven loader.
 */
describe('FORECASTING_ROUTES', () => {
  const root = FORECASTING_ROUTES[0];
  const children = root.children ?? [];

  it('declares the hub component as the root with permission guard', () => {
    expect(root.path).toBe('');
    expect(root.canActivate).toBeDefined();
    expect(root.data?.['permissions']).toEqual(['forecasting:view']);
  });

  it('exposes a single "replenishment" child at the canonical URL', () => {
    const repl = children.find(c => c.path === 'replenishment');
    expect(repl).toBeDefined();
    expect(repl?.canActivate).toBeDefined();
    expect(repl?.loadComponent).toBeDefined();
  });

  it('no longer ships V1/V2 alias routes (cutover 2026-05-13)', () => {
    expect(children.find(c => c.path === 'replenishment-v1')).toBeUndefined();
    expect(children.find(c => c.path === 'replenishment-v2')).toBeUndefined();
  });

  it('the "replenishment" route loads the V2 board component directly', async () => {
    const repl = children.find(c => c.path === 'replenishment')!;
    const cmp = await (repl.loadComponent as () => Promise<unknown>)();
    expect(typeof cmp).toBe('function');
    // The class itself is still named ReplenishmentBoardComponent until Phase C renames it.
    expect((cmp as Function).name).toMatch(/^Replenishment(Board|BoardV2)Component$/);
  });
});
