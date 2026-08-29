import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AppModule } from '@core/models/app-module';
import {
  ApiResponse,
  CreateTenantUserItem,
  ModuleCatalogDto,
  TenantUsersService,
  UserRole,
  UserModuleAccessItem
} from '@core/services/tenant-users.service';
import { ToastService } from '@core/services/toast.service';
import { TenantUsersBulkComponent } from './tenant-users-bulk.component';

/** Catalogue factice pilotant le rendu de la modale groupée (enums déjà en enum numérique côté test). */
function catalogFor(role: UserRole): ApiResponse<ModuleCatalogDto> {
  if (role === 'Auditor') {
    // Auditor : CRM grantable (extension lecture), Trésorerie NON grantable → sert au re-filtrage.
    return {
      success: true,
      data: {
        role,
        modules: [
          {
            module: AppModule.CRM,
            displayName: 'CRM Commercial',
            grantable: true,
            defaultEnabled: false,
            features: [
              {
                key: 'opportunities',
                basePermissions: [],
                allowedPermissions: ['crm:read'],
                defaultSelected: false,
                isExtension: true
              }
            ]
          },
          {
            module: AppModule.Treasury,
            displayName: 'Trésorerie (paiements)',
            grantable: false,
            defaultEnabled: false,
            features: []
          }
        ]
      },
      message: null,
      code: null,
      errors: []
    };
  }
  // Rôle par défaut (SalesRep, Accountant, etc.) : Trésorerie + Ventes grantables, Administration non grantable.
  return {
    success: true,
    data: {
      role,
      modules: [
        {
          module: AppModule.Treasury,
          displayName: 'Trésorerie (paiements)',
          grantable: true,
          defaultEnabled: true,
          features: [
            {
              key: 'read',
              basePermissions: ['payments:read'],
              allowedPermissions: ['payments:read'],
              defaultSelected: true,
              isExtension: false
            },
            {
              key: 'manage',
              basePermissions: [],
              allowedPermissions: ['payments:create', 'payments:update'],
              defaultSelected: false,
              isExtension: true
            },
            {
              // Feature sans aucune allowedPermission → doit être masquée.
              key: 'forecast_manage',
              basePermissions: [],
              allowedPermissions: [],
              defaultSelected: false,
              isExtension: false
            }
          ]
        },
        {
          module: AppModule.Sales,
          displayName: 'Ventes (factures)',
          grantable: true,
          defaultEnabled: true,
          features: [
            {
              key: 'invoices',
              basePermissions: ['invoices:create', 'invoices:read'],
              allowedPermissions: ['invoices:create', 'invoices:read', 'invoices:update', 'invoices:send'],
              defaultSelected: true,
              isExtension: false
            }
          ]
        },
        {
          // Module non grantable → masqué.
          module: AppModule.Administration,
          displayName: 'Paramètres et utilisateurs',
          grantable: false,
          defaultEnabled: false,
          features: []
        }
      ]
    },
    message: null,
    code: null,
    errors: []
  };
}

/** Forme structurelle d’une ligne du formulaire groupé (l’interface BulkRow n’est pas exportée). */
type BulkRowLike = {
  email: string;
  firstName: string;
  lastName: string;
  password: string;
  role: UserRole;
  moduleAccess: UserModuleAccessItem[];
};

function bulkRow(overrides: Partial<BulkRowLike> = {}): BulkRowLike {
  return {
    email: 'a@e.fr',
    firstName: 'Jean',
    lastName: 'Dupont',
    password: 'Secret123!',
    role: 'SalesRep',
    moduleAccess: [],
    ...overrides
  };
}

describe('TenantUsersBulkComponent', () => {
  let fixture: ComponentFixture<TenantUsersBulkComponent>;
  let component: TenantUsersBulkComponent;
  let tenantUsers: jasmine.SpyObj<TenantUsersService>;
  let toast: { add: jasmine.Spy };

  beforeEach(async () => {
    tenantUsers = jasmine.createSpyObj<TenantUsersService>('TenantUsersService', [
      'getModuleCatalog',
      'batchCreate'
    ]);
    tenantUsers.getModuleCatalog.and.callFake((role: UserRole) => of(catalogFor(role)));
    tenantUsers.batchCreate.and.returnValue(
      of({ success: true, data: null, message: 'Utilisateurs créés', errors: [] } as unknown as ApiResponse<unknown>)
    );

    toast = { add: jasmine.createSpy('add') };

    await TestBed.configureTestingModule({
      imports: [TenantUsersBulkComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: TenantUsersService, useValue: tenantUsers },
        { provide: ToastService, useValue: toast }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TenantUsersBulkComponent);
    component = fixture.componentInstance;
    // Pas de detectChanges : on teste la logique/les signaux directement (robuste face au DOM primeng).
  });

  // ---------------------------------------------------------------- catalogue par ligne

  describe('catalogue par ligne', () => {
    it('openModules charge le catalogue du rôle de la ligne et construit le brouillon sur les défauts du rôle', () => {
      component.rows.set([bulkRow({ role: 'SalesRep' })]);
      component.openModules(0);

      expect(component.configRowIndex).toBe(0);
      expect(component.modulesVisible).toBeTrue();
      expect(component.bulkCatalogLoading()).toBeFalse();
      expect(component.bulkCatalogError()).toBeFalse();
      expect(tenantUsers.getModuleCatalog).toHaveBeenCalledWith('SalesRep');

      // Catalogue chargé (toutes les entrées, y compris non grantables, servent de table de référence).
      expect(component.bulkCatalog().map(m => m.module)).toContain(AppModule.Treasury);

      // Brouillon = modules grantables uniquement (Administration masqué), avec défauts du rôle.
      const working = component.workingModules();
      expect(working.map(m => m.module)).toContain(AppModule.Treasury);
      expect(working.map(m => m.module)).toContain(AppModule.Sales);
      expect(working.find(m => m.module === AppModule.Administration)).toBeUndefined();
      const treasury = working.find(m => m.module === AppModule.Treasury)!;
      expect(treasury.enabled).toBeTrue();
      expect(treasury.enabledFeatureKeys).toEqual(['read']);
    });

    it('openModules préserve l’état déjà configuré de la ligne quand il existe', () => {
      component.rows.set([
        bulkRow({
          role: 'SalesRep',
          moduleAccess: [{ module: AppModule.Treasury, enabled: false, enabledFeatureKeys: null }]
        })
      ]);
      component.openModules(0);

      const treasury = component.workingModules().find(m => m.module === AppModule.Treasury)!;
      // L’état existant (désactivé) l’emporte sur le défaut du rôle (activé).
      expect(treasury.enabled).toBeFalse();
    });
  });

  // ---------------------------------------------------------------- onRowRoleChange

  describe('onRowRoleChange', () => {
    it('recharge le catalogue et réinitialise moduleAccess sur les défauts du nouveau rôle', () => {
      component.rows.set([
        bulkRow({
          role: 'Accountant',
          moduleAccess: [{ module: AppModule.Administration, enabled: true, enabledFeatureKeys: null }]
        })
      ]);
      component.onRowRoleChange(0, 'SalesRep');

      expect(component.rows()[0].role).toBe('SalesRep');
      expect(tenantUsers.getModuleCatalog).toHaveBeenCalledWith('SalesRep');

      const access = component.rows()[0].moduleAccess;
      // Défauts du rôle SalesRep : Trésorerie + Ventes grantables, défauts cochés ; Administration écarté.
      expect(access.map(a => a.module)).toContain(AppModule.Treasury);
      expect(access.map(a => a.module)).toContain(AppModule.Sales);
      expect(access.find(a => a.module === AppModule.Administration)).toBeUndefined();
      const treasury = access.find(a => a.module === AppModule.Treasury)!;
      expect(treasury.enabled).toBeTrue();
      expect(treasury.enabledFeatureKeys).toEqual(['read']);
    });
  });

  // ---------------------------------------------------------------- copyModulesFromFirst

  describe('copyModulesFromFirst (re-filtrage par rôle cible)', () => {
    it('écarte les modules non grantables pour les rôles cibles et avertit (toast warn)', () => {
      component.rows.set([
        bulkRow({
          role: 'SalesRep',
          moduleAccess: [
            { module: AppModule.Treasury, enabled: true, enabledFeatureKeys: ['read'] },
            { module: AppModule.Sales, enabled: true, enabledFeatureKeys: null }
          ]
        }),
        bulkRow({ role: 'Auditor' })
      ]);
      component.copyModulesFromFirst();

      // Auditor : ni Trésorerie ni Ventes ne sont grantables → tout écarté.
      expect(component.rows()[1].moduleAccess).toEqual([]);
      expect(toast.add).toHaveBeenCalled();
      const last = toast.add.calls.mostRecent().args[0] as { severity: string; detail: string };
      expect(last.severity).toBe('warn');
      expect(last.detail).toContain('écartés');
    });

    it('sans module écarté → toast info et configuration appliquée', () => {
      component.rows.set([
        bulkRow({
          role: 'SalesRep',
          moduleAccess: [
            { module: AppModule.Treasury, enabled: true, enabledFeatureKeys: ['read'] },
            { module: AppModule.Sales, enabled: true, enabledFeatureKeys: null }
          ]
        }),
        bulkRow({ role: 'Accountant' })
      ]);
      component.copyModulesFromFirst();

      // Accountant (défaut) : Trésorerie + Ventes grantables → rien écarté, configuration appliquée.
      const applied = component.rows()[1].moduleAccess.map(a => a.module);
      expect(applied).toContain(AppModule.Treasury);
      expect(applied).toContain(AppModule.Sales);
      const last = toast.add.calls.mostRecent().args[0] as { severity: string; detail: string };
      expect(last.severity).toBe('info');
    });
  });

  // ---------------------------------------------------------------- submit (re-filtrage anti-400)

  describe('submit (re-filtrage par catalogue avant batchCreate)', () => {
    it('re-filtre chaque ligne par le catalogue de son rôle avant l’envoi (anti-400)', () => {
      component.rows.set([
        bulkRow({
          email: 'a@e.fr',
          firstName: 'Jean',
          lastName: 'Dupont',
          password: 'Secret123!',
          role: 'SalesRep',
          moduleAccess: [
            { module: AppModule.Treasury, enabled: true, enabledFeatureKeys: ['read'] },
            { module: AppModule.Administration, enabled: true, enabledFeatureKeys: null }
          ]
        }),
        bulkRow({
          email: 'b@e.fr',
          firstName: 'Anne',
          lastName: 'Bert',
          password: 'Secret456!',
          role: 'Auditor',
          moduleAccess: [{ module: AppModule.Treasury, enabled: true, enabledFeatureKeys: null }]
        })
      ]);
      component.submit();

      expect(tenantUsers.getModuleCatalog).toHaveBeenCalledWith('SalesRep');
      expect(tenantUsers.getModuleCatalog).toHaveBeenCalledWith('Auditor');
      expect(tenantUsers.batchCreate).toHaveBeenCalledTimes(1);

      const items = tenantUsers.batchCreate.calls.mostRecent().args[0] as CreateTenantUserItem[];
      expect(items.length).toBe(2);

      // Ligne 0 (SalesRep) : Administration non grantable → écarté, Trésorerie conservée.
      const r0 = items[0].moduleAccess ?? [];
      expect(r0.map(a => a.module)).toEqual([AppModule.Treasury]);
      expect(r0.find(a => a.module === AppModule.Treasury)?.enabledFeatureKeys).toEqual(['read']);

      // Ligne 1 (Auditor) : Trésorerie non grantable → écarté, accès vide.
      const r1 = items[1].moduleAccess ?? [];
      expect(r1).toEqual([]);
    });
  });

  // ---------------------------------------------------------------- fail-closed sur erreur catalogue

  describe('fail-closed sur échec catalogue', () => {
    it('échec du chargement → état d’erreur, brouillon vidé, et la fermeture n’écrase pas la ligne', () => {
      tenantUsers.getModuleCatalog.and.callFake(() => throwError(() => new Error('network')));
      const initial: UserModuleAccessItem[] = [
        { module: AppModule.Treasury, enabled: true, enabledFeatureKeys: ['read'] }
      ];
      component.rows.set([bulkRow({ role: 'SalesRep', moduleAccess: [...initial] })]);

      component.openModules(0);
      expect(component.bulkCatalogError()).toBeTrue();
      expect(component.bulkCatalogLoading()).toBeFalse();
      expect(component.workingModules()).toEqual([]);

      // La fermeture (onHide) ne doit pas persister un brouillon vide sur erreur (fail-closed) :
      // la ligne conserve sa configuration existante.
      component.onModulesDialogHide();
      expect(component.rows()[0].moduleAccess).toEqual(initial);
    });

    it('sans erreur, la fermeture persiste le brouillon dans la ligne configurée', () => {
      component.rows.set([bulkRow({ role: 'SalesRep' })]);
      component.openModules(0);
      // On touche une feature (active l’extension « manage » sur Trésorerie).
      component.onWorkingSubFeatureChange(AppModule.Treasury, 'manage', true);
      component.onModulesDialogHide();

      const treasury = component.rows()[0].moduleAccess.find(a => a.module === AppModule.Treasury)!;
      expect(treasury.enabledFeatureKeys).toEqual(['read', 'manage']);
    });
  });
});
