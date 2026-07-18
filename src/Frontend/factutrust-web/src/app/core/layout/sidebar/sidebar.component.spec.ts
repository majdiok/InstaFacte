import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService, User } from '@core/services/auth.service';
import { FirmContextService } from '@core/services/firm-context.service';
import { FirmAssignmentService } from '@core/services/firm-assignment.service';
import { AccountingFeatureFlagsService } from '@features/accounting/shared/accounting-feature-flags.service';
import { StudioNavService } from '@features/studio/studio-nav.service';
import { SidebarComponent } from './sidebar.component';

const firmUser: User = {
  id: 'u1',
  email: 'firm@test.c',
  firstName: 'O',
  lastName: 'G',
  fullName: 'O G',
  role: 'FirmManager',
  roleDisplay: 'Responsable cabinet',
  tenantId: '00000000-0000-0000-0000-000000000002',
  companyName: 'Cabinet Test',
  tenantKind: 'AccountingFirm',
  twoFactorEnabled: false,
  // Requis depuis le fail-closed : hasModule refuse quand le champ est absent.
  enabledModuleIds: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
  effectivePermissions: ['firm:manage', 'accounting:read']
};

const delegatedUser: User = {
  ...firmUser,
  accessMode: 'delegated',
  contextTenantId: '00000000-0000-0000-0000-000000000003',
  contextCompanyName: 'Ste Bouzgarou',
  effectivePermissions: ['accounting:read', 'invoices:read']
};

function setUser(auth: AuthService, u: User | null): void {
  (auth as unknown as { userSignal: { set: (x: User | null) => void } }).userSignal.set(u);
}

describe('SidebarComponent — firm navigation', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: FirmAssignmentService,
          useValue: {
            getActiveClients: () => of({ success: true, data: [] }),
            getIncomingInvitations: () => of({ success: true, data: [] })
          }
        },
        {
          provide: AccountingFeatureFlagsService,
          useValue: { flags: () => ({ fixedAssetsEnabled: true }) }
        },
        {
          provide: StudioNavService,
          useValue: { items: () => [] }
        }
      ]
    });
  });

  it('shows firm native sidemenu labels when not in delegated mode', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, firmUser);
    TestBed.inject(FirmContextService).syncFromUser();

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const labels = fixture.componentInstance.navItems().map(i => i.label);
    expect(labels).toContain('Mes dossiers clients');
    expect(labels).not.toContain('Ventes');
    expect(labels).not.toContain('Documentation');
  });

  it('does not show Echeancier fiscal in firm native sidemenu', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, firmUser);
    TestBed.inject(FirmContextService).syncFromUser();

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const labels = fixture.componentInstance.navItems().map(i => i.label);
    expect(labels).not.toContain('Echeancier fiscal');
  });

  it('shows pending invitations badge on Invitations, not elsewhere', () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: FirmAssignmentService,
          useValue: {
            getActiveClients: () => of({ success: true, data: [] }),
            getIncomingInvitations: () =>
              of({ success: true, data: [{ id: 'inv-1' }] })
          }
        },
        {
          provide: AccountingFeatureFlagsService,
          useValue: { flags: () => ({ fixedAssetsEnabled: true }) }
        },
        {
          provide: StudioNavService,
          useValue: { items: () => [] }
        }
      ]
    });

    const auth = TestBed.inject(AuthService);
    setUser(auth, firmUser);
    TestBed.inject(FirmContextService).syncFromUser();

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const items = fixture.componentInstance.navItems();
    const invitations = items.find(i => i.label === 'Invitations');
    expect(invitations?.badge).toBe(1);
    expect(items.find(i => i.label === 'Echeancier fiscal')).toBeUndefined();
  });

  it('shows delegated sidemenu sections when in delegated mode', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedUser);
    TestBed.inject(FirmContextService).syncFromUser();

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const labels = fixture.componentInstance.navItems().map(i => i.label);
    expect(labels.some(l => l.startsWith('Dossier :'))).toBe(true);
    expect(labels).toContain('Ventes');
    expect(labels).not.toContain('Mes dossiers clients');
    expect(labels).not.toContain('Documentation');
  });

  it('uses action items for return and change dossier in delegated mode', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedUser);
    TestBed.inject(FirmContextService).syncFromUser();

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const dossierSection = fixture.componentInstance.navItems().find(i => i.label.startsWith('Dossier :'));
    expect(dossierSection?.children?.find(c => c.label === 'Retour au cabinet')?.action).toBe('returnToFirm');
    expect(dossierSection?.children?.find(c => c.label === 'Changer de dossier')?.action).toBe('changeDossier');
  });

  it('shows only three ventes and achats submenus in delegated mode', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, {
      ...delegatedUser,
      effectivePermissions: [
        'invoices:read',
        'quotes:read',
        'delivery_notes:read',
        'reports:view',
        'supplier_invoices:read',
        'suppliers:read',
        'purchase_orders:read'
      ]
    });
    TestBed.inject(FirmContextService).syncFromUser();

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const ventes = fixture.componentInstance.navItems().find(i => i.label === 'Ventes');
    expect(ventes?.children?.map(c => c.label)).toEqual([
      'Factures',
      'Factures impayées',
      'Rapports'
    ]);

    const achats = fixture.componentInstance.navItems().find(i => i.label === 'Achats');
    expect(achats?.children?.map(c => c.label)).toEqual([
      'Factures fournisseurs',
      'Factures impayées',
      'Rapports'
    ]);
  });

  it('splits accounting into module menus in delegated mode', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedUser);
    TestBed.inject(FirmContextService).syncFromUser();

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const items = fixture.componentInstance.navItems();
    const labels = items.map(i => i.label);

    // Les modules comptables visibles sont des menus de premier niveau.
    expect(labels).not.toContain('Configuration');
    expect(labels).not.toContain('Traitements');
    expect(labels).not.toContain('États');
    expect(labels).not.toContain('Liasse fiscale');
    expect(labels).toContain('Budgétaire');
    expect(labels).toContain('Declarations');
    expect(labels).toContain('Gestion immobilisations');

    // « Comptabilité » devient un accès direct au hub (route, sans sous-menu).
    const compta = items.find(i => i.label === 'Comptabilité');
    expect(compta?.route).toBe('/accounting/home');
    expect(compta?.children).toBeUndefined();

    const declarations = items.find(i => i.label === 'Declarations');
    const declarationLabels = declarations?.children?.map(c => c.label) ?? [];
    expect(declarationLabels).toContain('Declaration mensuelle');
  });

  it('shows four accounting submenus for company users with AI access', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, {
      ...firmUser,
      tenantKind: 'Company',
      role: 'Admin',
      roleDisplay: 'Administrateur',
      effectivePermissions: ['accounting:read', 'accounting:create', 'ai:chat']
    });
    TestBed.inject(FirmContextService).syncFromUser();

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const items = fixture.componentInstance.navItems();
    const compta = items.find(i => i.label === 'Comptabilité');
    expect(compta?.children?.map(c => c.label)).toEqual([
      'Assistant Comptabilité',
      'États comptables',
      'Déclaration mensuelle',
      'Liste des immobilisations'
    ]);
    expect(items.map(i => i.label)).not.toContain('Traitements');
    expect(items.map(i => i.label)).not.toContain('Documentation');
  });

  it('hides accounting assistant submenu when company user lacks ai:chat', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, {
      ...firmUser,
      tenantKind: 'Company',
      role: 'Admin',
      roleDisplay: 'Administrateur',
      effectivePermissions: ['accounting:read']
    });
    TestBed.inject(FirmContextService).syncFromUser();

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const compta = fixture.componentInstance.navItems().find(i => i.label === 'Comptabilité');
    expect(compta?.children?.map(c => c.label)).toEqual([
      'États comptables',
      'Déclaration mensuelle',
      'Liste des immobilisations'
    ]);
  });

  it('hides fixed assets submenu for company when feature flag is disabled', () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: FirmAssignmentService,
          useValue: {
            getActiveClients: () => of({ success: true, data: [] }),
            getIncomingInvitations: () => of({ success: true, data: [] })
          }
        },
        {
          provide: AccountingFeatureFlagsService,
          useValue: { flags: () => ({ fixedAssetsEnabled: false }) }
        },
        {
          provide: StudioNavService,
          useValue: { items: () => [] }
        }
      ]
    });

    const auth = TestBed.inject(AuthService);
    setUser(auth, {
      ...firmUser,
      tenantKind: 'Company',
      role: 'Admin',
      roleDisplay: 'Administrateur',
      effectivePermissions: ['accounting:read']
    });
    TestBed.inject(FirmContextService).syncFromUser();

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();

    const compta = fixture.componentInstance.navItems().find(i => i.label === 'Comptabilité');
    expect(compta?.children?.map(c => c.label)).toEqual([
      'États comptables',
      'Déclaration mensuelle'
    ]);
  });
});

describe('SidebarComponent — collapse', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: FirmAssignmentService,
          useValue: {
            getActiveClients: () => of({ success: true, data: [] }),
            getIncomingInvitations: () => of({ success: true, data: [] })
          }
        },
        {
          provide: AccountingFeatureFlagsService,
          useValue: { flags: () => ({ fixedAssetsEnabled: true }) }
        },
        {
          provide: StudioNavService,
          useValue: { items: () => [] }
        }
      ]
    });
  });

  it('applies sidebar-collapsed class when collapsed input is true', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedUser);
    TestBed.inject(FirmContextService).syncFromUser();

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.componentRef.setInput('collapsed', true);
    fixture.detectChanges();

    const sidebar = fixture.nativeElement.querySelector('#sidebar') as HTMLElement;
    expect(sidebar.classList.contains('sidebar-collapsed')).toBe(true);
  });

  it('hides rail labels in DOM when collapsed', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedUser);
    TestBed.inject(FirmContextService).syncFromUser();

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.componentRef.setInput('collapsed', true);
    fixture.detectChanges();

    const label = fixture.nativeElement.querySelector('.rail-label') as HTMLElement;
    expect(getComputedStyle(label).display).toBe('none');
  });

  it('uses collapsed rail width token on sidebar element', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedUser);
    TestBed.inject(FirmContextService).syncFromUser();

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.componentRef.setInput('collapsed', true);
    fixture.detectChanges();

    const sidebar = fixture.nativeElement.querySelector('#sidebar') as HTMLElement;
    expect(getComputedStyle(sidebar).getPropertyValue('--sidebar-rail-width').trim()).toBe('72px');
  });

  it('clears expandedParentLabel when collapsed becomes true', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedUser);
    TestBed.inject(FirmContextService).syncFromUser();

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.componentRef.setInput('collapsed', false);
    fixture.detectChanges();

    const ventes = fixture.componentInstance.navItems().find(i => i.label === 'Ventes');
    expect(ventes).toBeTruthy();
    fixture.componentInstance.toggleSubmenu(ventes!);
    expect(fixture.componentInstance.expandedParentLabel).toBe('Ventes');

    fixture.componentRef.setInput('collapsed', true);
    fixture.detectChanges();
    expect(fixture.componentInstance.expandedParentLabel).toBeNull();
  });

  it('emits requestExpand and opens section when parent clicked in collapsed mode', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedUser);
    TestBed.inject(FirmContextService).syncFromUser();

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.componentRef.setInput('collapsed', true);
    fixture.detectChanges();

    const expandSpy = jasmine.createSpy('requestExpand');
    fixture.componentInstance.requestExpand.subscribe(expandSpy);

    const ventes = fixture.componentInstance.navItems().find(i => i.label === 'Ventes');
    expect(ventes).toBeTruthy();
    fixture.componentInstance.toggleSubmenu(ventes!);

    expect(expandSpy).toHaveBeenCalled();
    expect(fixture.componentInstance.expandedParentLabel).toBe('Ventes');
  });

  it('toggles submenu section when expanded and parent clicked twice', () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedUser);
    TestBed.inject(FirmContextService).syncFromUser();

    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.componentRef.setInput('collapsed', false);
    fixture.detectChanges();

    const ventes = fixture.componentInstance.navItems().find(i => i.label === 'Ventes');
    fixture.componentInstance.toggleSubmenu(ventes!);
    expect(fixture.componentInstance.expandedParentLabel).toBe('Ventes');
    fixture.componentInstance.toggleSubmenu(ventes!);
    expect(fixture.componentInstance.expandedParentLabel).toBeNull();
  });
});
