import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';
import { AppModule } from '@core/models/app-module';

/**
 * Paie interne cabinet : tenant natif, module Payroll activé (flag backend + re-login).
 */
export const firmInternalPayrollGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isAccountingFirm() || auth.isDelegatedMode()) {
    return router.createUrlTree(['/firm/dashboard']);
  }

  if (!auth.hasModule(AppModule.Payroll) || !auth.hasPermission('payroll:read')) {
    return router.createUrlTree(['/access-denied'], { queryParams: { returnUrl: state.url } });
  }

  return true;
};
