import { AuthService } from '../services/auth.service';
import { AppModule } from '../models/app-module';

export interface NavVisibilityContext {
  modules?: AppModule[];
  permissionsAll?: string[];
  platformSettingsOnly?: boolean;
}

export function canSeeNavEntry(auth: AuthService, entry: NavVisibilityContext): boolean {
  if (entry.platformSettingsOnly && !auth.canAccessPlatformSettings()) {
    return false;
  }
  if (entry.modules?.length && !auth.hasAllModules(entry.modules)) {
    return false;
  }
  if (entry.permissionsAll?.length && !auth.hasAllPermissions(entry.permissionsAll)) {
    return false;
  }
  return true;
}
