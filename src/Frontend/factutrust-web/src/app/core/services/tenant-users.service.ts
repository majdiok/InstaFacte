import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '@environments/environment';
import { AppModule } from '@core/models/app-module';

/**
 * L'API sérialise `AppModule` en chaîne (`JsonStringEnumConverter`), alors que le front utilise l'enum numérique.
 * Sans cette normalisation, `moduleFeatures[].module === AppModule.Stock` échoue silencieusement.
 */
export function parseAppModule(raw: unknown): AppModule {
  if (typeof raw === 'number' && Number.isInteger(raw)) {
    return raw as AppModule;
  }
  if (typeof raw === 'string') {
    const byName: Record<string, AppModule> = {
      Clients: AppModule.Clients,
      Products: AppModule.Products,
      Sales: AppModule.Sales,
      Treasury: AppModule.Treasury,
      Reports: AppModule.Reports,
      Administration: AppModule.Administration,
      Purchases: AppModule.Purchases,
      Stock: AppModule.Stock,
      Accounting: AppModule.Accounting,
      CRM: AppModule.CRM,
      Fiscal: AppModule.Fiscal,
      AI: AppModule.AI
    };
    const v = byName[raw];
    if (v !== undefined) return v;
  }
  return AppModule.Clients;
}

export type UserRole = 'Administrator' | 'Accountant' | 'Client'
  | 'SalesRep' | 'SalesManager' | 'Warehouse' | 'Purchaser'
  | 'Cashier' | 'Auditor' | 'Supervisor' | 'Developer';

export interface UserModuleAccessItem {
  module: AppModule;
  enabled: boolean;
  /** null = toutes les sous-fonctions ; [] = aucune ; sinon sous-ensemble explicite. */
  enabledFeatureKeys?: string[] | null;
}

export interface TenantUserModuleFeaturesRow {
  /** Après `list()`, toujours un `AppModule` numérique ; le JSON brut peut être une chaîne d'enum. */
  module: AppModule;
  enabled: boolean;
  /** null = toutes les sous-fonctions (JSON null en base). */
  featureKeys: string[] | null;
}

/**
 * Miroir TypeScript du DTO backend `ModuleCatalogFeatureDto` (plan §6 Phase 2.1 / §5.2).
 * `allowedPermissions` vide => la feature n'est pas cochable pour ce rôle (masquée dans la modale).
 */
export interface ModuleCatalogFeatureDto {
  key: string;
  basePermissions: string[];
  allowedPermissions: string[];
  defaultSelected: boolean;
  isExtension: boolean;
}

/**
 * Miroir TypeScript du DTO backend `ModuleCatalogModuleDto`.
 * `grantable` false (rôle exclu ou plafond vide) => module masqué dans la modale.
 * `defaultEnabled` true => module inclus dans la base du rôle (badge « Inclus par défaut »).
 */
export interface ModuleCatalogModuleDto {
  module: AppModule;
  displayName: string;
  grantable: boolean;
  defaultEnabled: boolean;
  features: ModuleCatalogFeatureDto[];
}

/** Miroir TypeScript du DTO backend `ModuleCatalogDto` (réponse de `GET /api/tenant-users/module-catalog`). */
export interface ModuleCatalogDto {
  role: UserRole;
  modules: ModuleCatalogModuleDto[];
}

export { AppModule, APP_MODULE_OPTIONS } from '@core/models/app-module';

export interface TenantUserListItem {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: UserRole;
  roleDisplay: string;
  isActive: boolean;
  lastLoginAt: string | null;
  enabledModuleIds: number[];
  /** Présent lorsque des droits modules sont stockés en base (ligne par module). */
  moduleFeatures?: TenantUserModuleFeaturesRow[];
}

export interface CreateTenantUserItem {
  email: string;
  firstName: string;
  lastName: string;
  password: string;
  role: UserRole;
  phoneNumber?: string;
  moduleAccess?: UserModuleAccessItem[];
}

export interface ApiResponse<T> {
  success: boolean;
  data: T;
  message: string | null;
  code?: string | null;
  errors: string[];
}

@Injectable({ providedIn: 'root' })
export class TenantUsersService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/tenant-users`;

  /**
   * Catalogue normalisé par rôle (plan §4.a) — mémorisé pour éviter un rechargement HTTP à chaque
   * changement de rôle aller-retour. Seules les réponses en succès sont cachées ; un échec HTTP ou
   * `success=false` n'est pas mis en cache afin de permettre une retry (fail-closed côté composant).
   */
  private readonly catalogCache = new Map<UserRole, ModuleCatalogDto>();

  list(): Observable<ApiResponse<TenantUserListItem[]>> {
    return this.http.get<ApiResponse<TenantUserListItem[]>>(this.base).pipe(
      map(res => {
        if (!res.data) return res;
        return {
          ...res,
          data: res.data.map(u => ({
            ...u,
            moduleFeatures: u.moduleFeatures?.map(mf => ({
              ...mf,
              module: parseAppModule(mf.module as unknown)
            }))
          }))
        };
      })
    );
  }

  create(user: CreateTenantUserItem): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(this.base, user);
  }

  batchCreate(users: CreateTenantUserItem[]): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/batch`, { users });
  }

  update(
    id: string,
    body: {
      firstName?: string;
      lastName?: string;
      role?: UserRole;
      isActive?: boolean;
      phoneNumber?: string | null;
      newPassword?: string;
      moduleAccess?: Array<{
        module: AppModule;
        enabled: boolean;
        enabledFeatureKeys?: string[] | null;
      }>;
    }
  ): Observable<ApiResponse<unknown>> {
    return this.http.patch<ApiResponse<unknown>>(`${this.base}/${id}`, body);
  }

  /**
   * `GET /api/tenant-users/module-catalog?role={role}` (plan §6 Phase 2.1).
   * Normalise les valeurs d'enum `AppModule` (sérialisées en chaînes PascalCase par l'API) via
   * `parseAppModule`. Mémorisé par rôle en succès (cf. `catalogCache`).
   */
  getModuleCatalog(role: UserRole): Observable<ApiResponse<ModuleCatalogDto>> {
    const cached = this.catalogCache.get(role);
    if (cached) {
      return of({ success: true, data: cached, message: null, code: null, errors: [] });
    }
    return this.http
      .get<ApiResponse<ModuleCatalogDto>>(`${this.base}/module-catalog`, { params: { role } })
      .pipe(
        map(res => {
          if (res && res.success && res.data) {
            const normalized = this.normalizeCatalog(res.data);
            this.catalogCache.set(role, normalized);
            return { ...res, data: normalized };
          }
          return res;
        })
      );
  }

  private normalizeCatalog(dto: ModuleCatalogDto): ModuleCatalogDto {
    return {
      ...dto,
      modules: (dto.modules ?? []).map(m => ({
        ...m,
        module: parseAppModule(m.module as unknown),
        features: (m.features ?? []).map(f => ({ ...f }))
      }))
    };
  }
}
