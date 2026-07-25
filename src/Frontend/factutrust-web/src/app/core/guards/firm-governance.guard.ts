import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { FirmFeatureFlagsService } from '@core/services/firm-feature-flags.service';

export const firmGovernanceFeatureGuard: CanActivateFn = () => {
  const flags = inject(FirmFeatureFlagsService);
  if (flags.isEnabled('firmGovernance')) {
    return true;
  }
  return inject(Router).createUrlTree(['/firm/dashboard']);
};
