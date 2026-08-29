import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AppModule } from '@core/models/app-module';
import {
  ApiResponse,
  ModuleCatalogDto,
  ModuleCatalogModuleDto,
  TenantUserListItem,
  TenantUsersService,
  UserRole
} from '@core/services/tenant-users.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';
import { TenantUsersListComponent } from './tenant-users-list.component';

const REVOCATION_MSG =
  'Utilisateur mis à jour ; ses accès effectifs ont changé, ses sessions en cours sont révoquées.';

/** Catalogue factice pilotant le rendu de la modale (enums déjà en enum numérique côté test). */
function catalogFor(role: UserRole): ApiResponse<ModuleCatalogDto> {
  if (role === 'Auditor') {
    // Auditor : CRM grantable (extension lecture), Trésorerie NON grantable → sert au test de re-filtrage.
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
  // Rôle par défaut (SalesRep, etc.) : Trésorerie + Ventes grantables, Administration non grantable.
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
              key: 'forecast_read',
              basePermissions: [],
              allowedPermissions: ['treasury_forecast:view'],
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

function makeUser(overrides: Partial<TenantUserListItem> = {}): TenantUserListItem {
  return {
    id: 'u-1',
    email: 'user@example.fr',
    firstName: 'Jean',
    lastName: 'Dupont',
    role: 'SalesRep',
    roleDisplay: 'Commercial',
    isActive: true,
    lastLoginAt: null,
    enabledModuleIds: [],
    ...overrides
  };
}

describe('TenantUsersListComponent', () => {
  let fixture: ComponentFixture<TenantUsersListComponent>;
  let component: TenantUsersListComponent;
  let tenantUsers: jasmine.SpyObj<TenantUsersService>;
  let toast: { add: jasmine.Spy };
  let users: TenantUserListItem[];

  beforeEach(async () => {
    users = [
      makeUser({ id: 'u-no-grants', email: 'a@e.fr', moduleFeatures: undefined }),
      makeUser({ id: 'u-null-keys', email: 'b@e.fr', moduleFeatures: [{ module: AppModule.Treasury, enabled: true, featureKeys: null }] }),
      makeUser({ id: 'u-explicit', email: 'c@e.fr', moduleFeatures: [{ module: AppModule.Treasury, enabled: true, featureKeys: ['read'] }] }),
      makeUser({
        id: 'u-two',
        email: 'd@e.fr',
        moduleFeatures: [
          { module: AppModule.Treasury, enabled: true, featureKeys: ['read'] },
          { module: AppModule.Sales, enabled: true, featureKeys: null }
        ]
      }),
      makeUser({ id: 'u-auditor', email: 'e@e.fr', role: 'Auditor' })
    ];

    tenantUsers = jasmine.createSpyObj<TenantUsersService>('TenantUsersService', [
      'list',
      'getModuleCatalog',
      'update',
      'create',
      'batchCreate'
    ]);
    tenantUsers.list.and.returnValue(of({ success: true, data: users, message: null, errors: [] } as unknown as ApiResponse<TenantUserListItem[]>));
    tenantUsers.getModuleCatalog.and.callFake((role: UserRole) => of(catalogFor(role)));
    tenantUsers.update.and.returnValue(of({ success: true, data: null, message: REVOCATION_MSG, errors: [] } as ApiResponse<unknown>));
    tenantUsers.create.and.returnValue(of({ success: true, data: null, message: 'Utilisateur créé', errors: [] } as ApiResponse<unknown>));

    toast = { add: jasmine.createSpy('add') };

    await TestBed.configureTestingModule({
      imports: [TenantUsersListComponent],
      providers: [
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        { provide: TenantUsersService, useValue: tenantUsers },
        { provide: ToastService, useValue: toast },
        { provide: AuthService, useValue: { user: () => ({ id: 'admin-1' }) } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TenantUsersListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges(); // déclenche ngOnInit -> load()
  });

  function findUser(id: string): TenantUserListItem {
    const u = component.users().find(x => x.id === id);
    if (!u) throw new Error(`user ${id} not loaded`);
    return u;
  }

  function saveAndCapturePayload(): Array<{ module: AppModule; enabled: boolean; enabledFeatureKeys?: string[] | null }> {
    component.saveEdit();
    expect(tenantUsers.update).toHaveBeenCalled();
    const body = tenantUsers.update.calls.mostRecent().args[1] as { moduleAccess: Array<{ module: AppModule; enabled: boolean; enabledFeatureKeys?: string[] | null }> };
    return body.moduleAccess ?? [];
  }

  function findInPayload(payload: ReturnType<typeof saveAndCapturePayload>, module: AppModule) {
    return payload.find(p => p.module === module);
  }

  // ---------------------------------------------------------------- 7.3.1 — Round-trip no-op

  describe('§7.3.1 round-trip no-op (sans toucher aucun module)', () => {
    it('(a) aucun grant → payload moduleAccess vide (absent reste absent)', () => {
      component.openEdit(findUser('u-no-grants'));
      const payload = saveAndCapturePayload();
      expect(payload.length).toBe(0);
    });

    it('(b) grant EnabledFeatureKeys=null → payload reproduit null (champ featureKeys omis)', () => {
      component.openEdit(findUser('u-null-keys'));
      const payload = saveAndCapturePayload();
      expect(payload.length).toBe(1);
      const row = findInPayload(payload, AppModule.Treasury);
      expect(row).toBeDefined();
      expect(row!.enabled).toBeTrue();
      expect(row!.enabledFeatureKeys).toBeUndefined(); // null → omis, jamais normalisé en liste
    });

    it('(c) grant avec liste explicite → payload reproduit la liste à l’identique', () => {
      component.openEdit(findUser('u-explicit'));
      const payload = saveAndCapturePayload();
      expect(payload.length).toBe(1);
      const row = findInPayload(payload, AppModule.Treasury);
      expect(row).toBeDefined();
      expect(row!.enabled).toBeTrue();
      expect(row!.enabledFeatureKeys).toEqual(['read']);
    });

    it('toucher un seul module → seul ce module est normalisé, les autres reproduisent l’état stocké', () => {
      component.openEdit(findUser('u-two'));
      // Avant touche : Treasury ['read'], Sales null, aucun dirty.
      expect(component.moduleDraft().find(m => m.module === AppModule.Treasury)?.dirty).toBeFalse();

      // On touche UNIQUEMENT Trésorerie (coche la feature extension « manage »).
      component.onEditSubFeatureChange(AppModule.Treasury, 'manage', true);

      const payload = saveAndCapturePayload();
      // Trésorerie : touchée → normalisée (la sélection visible remplace l’état stocké).
      const treasury = findInPayload(payload, AppModule.Treasury);
      expect(treasury).toBeDefined();
      expect(treasury!.enabledFeatureKeys).toEqual(['read', 'manage']);
      // Ventes : non touchée → reproduit exactement l’état stocké (featureKeys null → champ omis).
      const sales = findInPayload(payload, AppModule.Sales);
      expect(sales).toBeDefined();
      expect(sales!.enabled).toBeTrue();
      expect(sales!.enabledFeatureKeys).toBeUndefined();
    });

    it('Treasury keys=null + décocher manage → payload sans forecast_manage (feature masquée)', () => {
      // Commercial : forecast_manage a allowedPermissions vide → masquée. Un grant null
      // (« toutes les features ») ne doit pas réintroduire cette clé au PATCH.
      component.openEdit(findUser('u-null-keys'));
      component.onEditSubFeatureChange(AppModule.Treasury, 'manage', false);

      const payload = saveAndCapturePayload();
      const treasury = findInPayload(payload, AppModule.Treasury);
      expect(treasury).toBeDefined();
      expect(treasury!.enabled).toBeTrue();
      expect(treasury!.enabledFeatureKeys).toEqual(['read', 'forecast_read']);
      expect(treasury!.enabledFeatureKeys).not.toContain('forecast_manage');
    });

    it('affiche le message de révocation renvoyé par l’API en toast après une modification réussie', () => {
      component.openEdit(findUser('u-explicit'));
      component.onEditSubFeatureChange(AppModule.Treasury, 'manage', true);
      component.saveEdit();
      expect(toast.add).toHaveBeenCalled();
      const last = toast.add.calls.mostRecent().args[0] as { detail?: string };
      expect(last.detail).toBe(REVOCATION_MSG);
    });
  });

  // ---------------------------------------------------------------- 7.3.2 — Catalogue et rendu

  describe('§7.3.2 catalogue et rendu de la modale', () => {
    it('module grantable=false est masqué (absent du draft)', () => {
      component.openEdit(findUser('u-no-grants'));
      expect(component.moduleDraft().find(m => m.module === AppModule.Administration)).toBeUndefined();
      expect(component.moduleDraft().find(m => m.module === AppModule.Treasury)).toBeDefined();
    });

    it('feature sans allowedPermissions est masquée (catalogVisibleFeatures)', () => {
      component.openEdit(findUser('u-no-grants'));
      const cm = component.editCatalogModule(AppModule.Treasury) as ModuleCatalogModuleDto;
      const keys = component.catalogVisibleFeatures(cm).map(f => f.key);
      expect(keys).toContain('read');
      expect(keys).toContain('manage');
      expect(keys).not.toContain('forecast_manage'); // allowedPermissions vide → masquée
    });

    it('badge « Inclus par défaut » sur un module defaultEnabled', () => {
      component.openEdit(findUser('u-no-grants'));
      expect(component.isModuleDefaultIncluded(component.editCatalogModule(AppModule.Treasury))).toBeTrue();
    });

    it('feature isExtension est marquée « Extension » et cochable', () => {
      component.openEdit(findUser('u-no-grants'));
      const cm = component.editCatalogModule(AppModule.Treasury) as ModuleCatalogModuleDto;
      const manage = component.catalogVisibleFeatures(cm).find(f => f.key === 'manage');
      expect(manage?.isExtension).toBeTrue();
    });

    it('échec du chargement catalogue → état d’erreur (fail-closed) et bouton Enregistrer désactivé', () => {
      tenantUsers.getModuleCatalog.and.callFake(() => throwError(() => new Error('network')));
      component.openEdit(findUser('u-no-grants'));
      expect(component.editCatalogError()).toBeTrue();
      expect(component.moduleDraft().length).toBe(0);

      fixture.detectChanges();
      const native = fixture.nativeElement as HTMLElement;
      const saveBtn = Array.from(native.querySelectorAll<HTMLButtonElement>('button')).find(
        b => (b.textContent ?? '').includes('Enregistrer')
      );
      expect(saveBtn).withContext('le bouton Enregistrer est rendu dans la modale d’édition').toBeTruthy();
      expect(saveBtn!.disabled).toBeTrue();
    });

    it('changement de rôle → catalogue rechargé et draft re-filtré (modules non grantables pour le nouveau rôle masqués)', () => {
      component.openEdit(findUser('u-two')); // rôle SalesRep
      expect(component.moduleDraft().find(m => m.module === AppModule.Treasury)).toBeDefined();

      component.onEditRoleChange('Auditor');
      expect(tenantUsers.getModuleCatalog).toHaveBeenCalledWith('Auditor');
      // Auditor : Trésorerie non grantable → masquée ; CRM grantable → présente.
      expect(component.moduleDraft().find(m => m.module === AppModule.Treasury)).toBeUndefined();
      expect(component.moduleDraft().find(m => m.module === AppModule.CRM)).toBeDefined();
      // Changement de rôle → tous les modules visibles sont marqués touchés (normalisés à l’enregistrement).
      expect(component.moduleDraft().every(m => m.dirty)).toBeTrue();
    });
  });
});
