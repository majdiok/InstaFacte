import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';
import { isCompanyAccountingRestricted } from '@core/config/company-accounting-nav.config';
import { AccountingFeatureFlagsService } from './accounting-feature-flags.service';

export const fixedAssetsFeatureGuard: CanActivateFn = () => {
  const flags = inject(AccountingFeatureFlagsService);
  const auth = inject(AuthService);
  const router = inject(Router);
  if (flags.isEnabled('fixedAssetsEnabled')) {
    return true;
  }
  const fallback = isCompanyAccountingRestricted(auth)
    ? '/accounting/financial-statements'
    : '/accounting/home';
  return router.createUrlTree([fallback]);
};