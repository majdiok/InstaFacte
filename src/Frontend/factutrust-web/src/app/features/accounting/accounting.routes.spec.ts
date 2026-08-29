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
