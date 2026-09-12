import { ACCOUNTING_ROUTES } from './accounting.routes';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

describe('accounting.routes — gardes de la liasse fiscale (T18)', () => {
  const liasseRoutePaths = ['nct-statements', 'inventory-book', 'fiscal-result', 'fiscal-parameters'];

  for (const path of liasseRoutePaths) {
    it(`route "${path}" exige la permission accounting:read via permissionGuard`, () => {
      const route = ACCOUNTING_ROUTES.find(r => r.path === path);
      expect(route).toBeTruthy();
      expect(route?.canActivate).toEqual([permissionGuard]);
      expect(route?.data?.['permissions']).toEqual([PERMISSIONS.accounting.read]);
      // Ne doit jamais exiger accounting:create au niveau route (exclurait la lecture seule).
      expect(route?.data?.['permissions']).not.toContain(PERMISSIONS.accounting.create);
    });
  }

  for (const path of ['fiscal-result', 'fiscal-parameters']) {
    it(`route "${path}" a une garde canDeactivate (T23)`, () => {
      const route = ACCOUNTING_ROUTES.find(r => r.path === path);
      expect(route?.canDeactivate?.length).toBe(1);
    });
  }
});

describe('accounting.routes — gardes des écrans de devises', () => {
  // Ces routes n'avaient AUCUN canActivate : l'écran d'édition des taux, qui est un écran
  // d'écriture pure, s'ouvrait sans le droit correspondant.

  it('route "currencies" exige accounting:read', () => {
    const route = ACCOUNTING_ROUTES.find(r => r.path === 'currencies');
    expect(route).toBeTruthy();
    expect(route?.canActivate).toEqual([permissionGuard]);
    expect(route?.data?.['permissions']).toEqual([PERMISSIONS.accounting.read]);
  });

  it('route "currencies/:id" exige accounting:currencies_manage', () => {
    const route = ACCOUNTING_ROUTES.find(r => r.path === 'currencies/:id');
    expect(route).toBeTruthy();
    expect(route?.canActivate).toEqual([permissionGuard]);
    expect(route?.data?.['permissions']).toEqual([PERMISSIONS.accounting.currenciesManage]);
  });

  it('route "currencies/:id" conserve sa garde de sortie', () => {
    // Le canDeactivate confirme la perte des taux non enregistrés : l'ajout du canActivate
    // ne doit pas l'avoir évincé.
    const route = ACCOUNTING_ROUTES.find(r => r.path === 'currencies/:id');
    expect(route?.canDeactivate?.length).toBe(1);
  });

  it('le segment paramétré est déclaré APRÈS la liste', () => {
    // L'ordre est significatif : déclaré avant, "currencies/:id" capterait "currencies".
    const list = ACCOUNTING_ROUTES.findIndex(r => r.path === 'currencies');
    const detail = ACCOUNTING_ROUTES.findIndex(r => r.path === 'currencies/:id');
    expect(list).toBeGreaterThanOrEqual(0);
    expect(detail).toBeGreaterThan(list);
  });
});
