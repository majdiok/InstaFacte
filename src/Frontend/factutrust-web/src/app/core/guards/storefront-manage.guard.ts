import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { PERMISSIONS } from '../config/permission-keys';

/** Tenant admins / supervisors with `storefront:manage` (mirrors API `StorefrontTenantOwner`). */
export const storefrontManageGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    void router.navigate(['/auth/login'], { queryParams: { returnUrl: state.url } });
    return false;
  }

  if (auth.hasPermission(PERMISSIONS.storefront.manage)) {
    return true;
  }

  void router.navigate(['/settings']);
  return false;
};
