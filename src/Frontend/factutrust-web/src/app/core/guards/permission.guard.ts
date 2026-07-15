import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';
import { AppModule } from '@core/models/app-module';

export interface PermissionRouteData {
  /** Every permission required when mode is `all` (default). */
  permissions?: readonly string[];
  permissionMode?: 'all' | 'any';
  /** Every module must be enabled (AND). */
  modules?: readonly AppModule[];
}

function pathWithoutQuery(url: string): string {
  const q = url.indexOf('?');
  return q >= 0 ? url.slice(0, q) : url;
}

/**
 * Declarative guard: optional {@link PermissionRouteData} on the route.
 * Redirects to `/access-denied` like {@link moduleGuard}.
 */
export const permissionGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const data = route.data as PermissionRouteData;

  const modules = data.modules;
  if (modules?.length) {
    for (const m of modules) {
      if (!auth.hasModule(m)) {
        router.navigate(['/access-denied'], { queryParams: { returnUrl: pathWithoutQuery(state.url) } });
        return false;
      }
    }
  }

  const perms = data.permissions;
  if (perms?.length) {
    const mode = data.permissionMode ?? 'all';
    const ok = mode === 'any' ? auth.hasAnyPermission(perms) : auth.hasAllPermissions(perms);
    if (!ok) {
      router.navigate(['/access-denied'], { queryParams: { returnUrl: pathWithoutQuery(state.url) } });
      return false;
    }
  }

  return true;
};
