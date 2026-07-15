import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { PlatformPermissionsService } from '@core/services/platform-permissions.service';
import type { PlatformPermissionKey } from '@core/models/platform.models';

/**
 * Lot B1 — Garde de route exigeant une permission précise.
 *
 * Charge les permissions courantes si pas encore chargées, puis :
 * - autorise si la permission est présente
 * - redirige vers `/tenants` (page d'accueil par défaut) sinon
 *
 * Usage :
 * ```ts
 * { path: 'admins',
 *   canActivate: [platformPermissionGuard(PlatformPermission.AdminsRead)],
 *   loadComponent: () => import('...') }
 * ```
 */
export function platformPermissionGuard(required: PlatformPermissionKey): CanActivateFn {
  return async () => {
    const permissions = inject(PlatformPermissionsService);
    const router = inject(Router);

    if (!permissions.isLoaded()) {
      try {
        await firstValueFrom(permissions.load());
      } catch {
        // Si la requête échoue (token invalide etc.), on laisse l'auth guard de niveau supérieur gérer
        return router.createUrlTree(['/tenants']);
      }
    }

    return permissions.has(required) || router.createUrlTree(['/tenants']);
  };
}
