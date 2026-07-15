import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PlatformAuthService } from '@core/services/platform-auth.service';

/** Redirects to /tenants if already signed in as platform operator. */
export const platformGuestGuard: CanActivateFn = () => {
  const auth = inject(PlatformAuthService);
  const router = inject(Router);
  if (auth.isAuthenticated()) {
    return router.createUrlTree(['/tenants']);
  }
  return true;
};
