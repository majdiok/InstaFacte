import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { WarehouseContextService } from '../services/warehouse-context.service';

/**
 * Runs after `authGuard`. Ensures a warehouse is selected when the tenant has more than one active warehouse.
 */
export const warehouseSelectedGuard: CanActivateFn = (_route, state) => {
  if (inject(AuthService).isClientPortal()) {
    return true;
  }
  const warehouseContext = inject(WarehouseContextService);
  return warehouseContext.resolveForAuthenticatedRoute(state.url);
};
