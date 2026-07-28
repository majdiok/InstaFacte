import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { AuthService, User } from '@core/services/auth.service';
import { FirmContextService } from '@core/services/firm-context.service';
import { FirmAssignmentService } from '@core/services/firm-assignment.service';
import { AccountingFeatureFlagsService } from '@features/accounting/shared/accounting-feature-flags.service';
import { StudioNavService } from '@features/studio/studio-nav.service';
import { AppModule } from '@core/models/app-module';
import { SecondaryNavComponent } from './secondary-nav.component';

const ALL_MODULES = Object.values(AppModule).filter((v): v is number => typeof v === 'number');

const companyUser: User = {
  id: 'u-company',
  email: 'company@test.c',
  firstName: 'A',
  lastName: 'B',
  fullName: 'A B',
  role: 'Administrator',
  roleDisplay: 'Administrateur',
  tenantId: '00000000-0000-0000-0000-000000000001',
  companyName: 'Ste Test',
  tenantKind: 'Company',
  twoFactorEnabled: false,
  enabledModuleIds: ALL_MODULES,
  effectivePermissions: [
    'quotes:read',
    'delivery_notes:read',
    'invoices:read',
    'suppliers:read',
    'purchase_orders:read',
    'supplier_invoices:read',
    'clients:read',
    'products:read',
    'stock:read',
    'stock_transfers:read',
    'inventory:read',
    'payments:read',
    'reports:view',
    'withholding_tax:read',
    'withholding_tax:export',
    'payroll:read',
    'payroll:declare',
    'payroll:settings',
    'accounting:read'
  ]
};

function setUser(auth: AuthService, u: User | null): void {
  (auth as unknown as { userSignal: { set: (x: User | null) => void } }).userSignal.set(u);
}

function findTrigger(root: HTMLElement, label: string): HTMLButtonElement | undefined {
  return Array.from(root.querySelectorAll('.secondary-nav__trigger')).find(el =>
    el.textContent?.includes(label)
  ) as HTMLButtonElement | undefined;
}

describe('SecondaryNavComponent', () => {
  let fixture: ComponentFixture<SecondaryNavComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SecondaryNavComponent],
      providers: [
        provideRouter([
          { path: 'invoices', component: SecondaryNavComponent },
          { path: 'quotes', component: SecondaryNavComponent },
          { path: 'payroll/employees', component: SecondaryNavComponent },
          { path: 'accounting/journal', component: SecondaryNavComponent },
          { path: 'accounting/financial-statements', component: SecondaryNavComponent }
        ]),
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
    }).compileComponents();

    const auth = TestBed.inject(AuthService);
    setUser(auth, companyUser);
    TestBed.inject(FirmContextService).syncFromUser();

    fixture = TestBed.createComponent(SecondaryNavComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    document.querySelectorAll('.secondary-nav__dropdown').forEach(el => el.remove());
  });

  it('renders secondary section triggers including TEJ display label', () => {
    const labels = Array.from(
      fixture.nativeElement.querySelectorAll('.secondary-nav__label') as NodeListOf<HTMLElement>
    ).map(el => el.textContent?.trim());

    expect(labels).toContain('Ventes');
    expect(labels).toContain('Achats');
    expect(labels).toContain('Stock');
    expect(labels).toContain('Trésorerie');
    expect(labels).toContain('Fiches');
    expect(labels).toContain('RH & Paie');
    expect(labels).toContain('Comptabilité');
    expect(labels).toContain('TEJ');
    expect(labels).not.toContain('Fiscal / TEJ');
  });

  it('renders colored section icons on triggers', () => {
    const ventesBtn = findTrigger(fixture.nativeElement, 'Ventes');
    const achatsBtn = findTrigger(fixture.nativeElement, 'Achats');
    expect(ventesBtn?.querySelector('i.secondary-nav__icon.fa-bag-shopping')).toBeTruthy();
    expect(achatsBtn?.querySelector('i.secondary-nav__icon.fa-cart-shopping')).toBeTruthy();
  });

  it('renders colored icons inside dropdown submenu items', fakeAsync(() => {
    const ventesBtn = findTrigger(fixture.nativeElement, 'Ventes');
    ventesBtn!.click();
    fixture.detectChanges();
    tick();

    const menu = document.getElementById('secondary-nav-menu-Ventes');
    expect(menu).toBeTruthy();
    expect(menu!.querySelector('i.secondary-nav__icon.fa-file')).toBeTruthy();
  }));

  it('opens a NgbDropdown menu that is visible and marks the active child', fakeAsync(async () => {
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/invoices');
    fixture.detectChanges();

    const ventesBtn = findTrigger(fixture.nativeElement, 'Ventes');
    expect(ventesBtn).toBeTruthy();
    ventesBtn!.click();
    fixture.detectChanges();
    tick();

    const menu = document.getElementById('secondary-nav-menu-Ventes');
    expect(menu).toBeTruthy();
    expect(menu!.classList.contains('show')).toBeTrue();
    expect(menu!.getBoundingClientRect().height).toBeGreaterThan(40);

    const activeLink = menu!.querySelector('.secondary-nav__link.is-active') as HTMLElement | null;
    expect(activeLink?.textContent).toContain('Factures');
    expect(ventesBtn!.classList.contains('is-active')).toBeTrue();
    expect(ventesBtn!.getAttribute('aria-expanded')).toBe('true');
  }));

  it('navigates when clicking a dropdown child link', fakeAsync(async () => {
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigateByUrl').and.returnValue(Promise.resolve(true));

    const rhBtn = findTrigger(fixture.nativeElement, 'RH & Paie');
    expect(rhBtn).toBeTruthy();
    rhBtn!.click();
    fixture.detectChanges();
    tick();

    const menu = document.getElementById('secondary-nav-menu-RH-Paie');
    expect(menu).toBeTruthy();
    const salaries = Array.from(menu!.querySelectorAll('.secondary-nav__link')).find(el =>
      el.textContent?.includes('Salariés')
    ) as HTMLElement | undefined;
    expect(salaries).toBeTruthy();
    salaries!.click();
    fixture.detectChanges();
    tick();

    expect(navigateSpy).toHaveBeenCalledWith('/payroll/employees');
  }));

  it('closes the dropdown on navigation', fakeAsync(async () => {
    const ventesBtn = findTrigger(fixture.nativeElement, 'Ventes');
    ventesBtn!.click();
    fixture.detectChanges();
    tick();

    expect(document.getElementById('secondary-nav-menu-Ventes')?.classList.contains('show')).toBeTrue();

    const router = TestBed.inject(Router);
    await router.navigateByUrl('/quotes');
    fixture.detectChanges();
    tick();

    expect(document.getElementById('secondary-nav-menu-Ventes')?.classList.contains('show')).toBeFalsy();
  }));

  it('opens États comptables flyout and navigates to Journal', fakeAsync(() => {
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigateByUrl').and.returnValue(Promise.resolve(true));

    const comptaBtn = findTrigger(fixture.nativeElement, 'Comptabilité');
    expect(comptaBtn).toBeTruthy();
    comptaBtn!.click();
    fixture.detectChanges();
    tick();

    const menu = document.getElementById('secondary-nav-menu-Comptabilit');
    expect(menu).toBeTruthy();

    const etatsTrigger = Array.from(menu!.querySelectorAll('.secondary-nav__submenu-trigger')).find(el =>
      el.textContent?.includes('États comptables')
    ) as HTMLButtonElement | undefined;
    expect(etatsTrigger).toBeTruthy();
    etatsTrigger!.click();
    fixture.detectChanges();
    tick();

    const submenu = document.getElementById('secondary-nav-submenu-tats-comptables');
    expect(submenu).toBeTruthy();
    expect(submenu!.textContent).toContain('Tous les états');
    expect(submenu!.textContent).toContain('Journal');
    expect(submenu!.textContent).toContain('Grand livre');
    expect(submenu!.textContent).not.toContain("Journal d'audit");

    const journalExact = Array.from(submenu!.querySelectorAll('.secondary-nav__link')).find(el => {
      const span = el.querySelector('span');
      return span?.textContent?.trim() === 'Journal';
    }) as HTMLElement | undefined;
    expect(journalExact).toBeTruthy();
    journalExact!.click();
    fixture.detectChanges();
    tick();

    expect(navigateSpy).toHaveBeenCalledWith('/accounting/journal');
  }));
});
