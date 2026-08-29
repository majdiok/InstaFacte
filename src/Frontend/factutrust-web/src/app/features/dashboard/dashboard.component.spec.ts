import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService, User } from '@core/services/auth.service';
import { WarehouseContextService } from '@core/services/warehouse-context.service';
import { InvoiceService } from '@core/services/invoice.service';
import { StockService } from '@core/services/stock.service';
import { DeliveryNoteService } from '@features/delivery-notes/services/delivery-note.service';
import { DashboardService } from './services/dashboard.service';
import { AccountingService } from '@features/accounting/services/accounting.service';
import { CrmService } from '@features/crm/services/crm.service';
import { AppModule } from '@core/models/app-module';
import { DashboardComponent } from './dashboard.component';

/**
 * §7.3.3 — Tableau de bord en permissions mixtes.
 *
 * On garde AuthService réel (piloté par `effectivePermissions`/`enabledModuleIds` d'un `User`
 * mocké) pour que tous les computed de gating (canReadInvoices, showStockUrgent, hasAccountingModule…)
 * se comportent exactement comme en production. Les autres services métier sont des spies pour
 * observer précisément quels appels partent.
 */

const ALL_MODULES = Object.values(AppModule).filter((v): v is number => typeof v === 'number');

function makeUser(effectivePermissions: string[], enabledModuleIds: number[] = ALL_MODULES): User {
  return {
    id: 'u-1',
    email: 'u@test.c',
    firstName: 'A',
    lastName: 'B',
    fullName: 'A B',
    role: 'Custom',
    roleDisplay: 'Personnalisé',
    tenantId: '00000000-0000-0000-0000-000000000001',
    companyName: 'Ste Test',
    tenantKind: 'Company',
    twoFactorEnabled: false,
    enabledModuleIds,
    effectivePermissions
  };
}

function setUser(auth: AuthService, u: User): void {
  (auth as unknown as { userSignal: { set: (x: User | null) => void } }).userSignal.set(u);
}

const emptyApiOk = of({ success: true, data: { items: [] }, message: null, errors: [] as string[] });

describe('DashboardComponent — gating par permission (§7.3.3)', () => {
  let getInvoices: jasmine.Spy;
  let getStockAlerts: jasmine.Spy;
  let getDeliveryNotes: jasmine.Spy;
  let getAccountingDashboard: jasmine.Spy;
  let getMyReminders: jasmine.Spy;
  let getOpportunities: jasmine.Spy;
  let loadDashboardData: jasmine.Spy;
  let selectedWarehouseId: jasmine.Spy;

  function configure() {
    getInvoices = jasmine.createSpy('getInvoices').and.returnValue(emptyApiOk);
    getStockAlerts = jasmine.createSpy('getStockAlerts').and.returnValue(
      of({ success: true, data: { items: [], totalCount: 0 }, message: null, errors: [] })
    );
    getDeliveryNotes = jasmine.createSpy('getDeliveryNotes').and.returnValue(
      of({ success: true, data: { items: [] }, message: null, errors: [] })
    );
    getAccountingDashboard = jasmine.createSpy('getAccountingDashboard').and.returnValue(
      of({ success: true, data: {}, message: null, errors: [] })
    );
    getMyReminders = jasmine.createSpy('getMyReminders').and.returnValue(
      of({ success: true, data: [], message: null, errors: [] })
    );
    getOpportunities = jasmine.createSpy('getOpportunities').and.returnValue(
      of({ success: true, data: [], message: null, errors: [] })
    );
    loadDashboardData = jasmine.createSpy('loadDashboardData').and.returnValue(
      of({
        allInvoices: [],
        recentInvoices: [],
        allQuotes: [],
        recentQuotes: [],
        topClients: [],
        monthlyRevenue: [],
        recentActivity: [],
        kpiTrends: { revenueChange: undefined, salesTodayChange: undefined, pendingChange: undefined },
        kpiSparklines: { revenue: [], salesToday: [], currentMonth: [], pending: [] },
        activeQuotesCount: 0
      })
    );
    selectedWarehouseId = jasmine.createSpy('selectedWarehouseId').and.returnValue(null);

    TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: InvoiceService, useValue: { getInvoices } },
        { provide: StockService, useValue: { getStockAlerts } },
        { provide: DeliveryNoteService, useValue: { getDeliveryNotes } },
        { provide: DashboardService, useValue: { loadDashboardData } },
        { provide: AccountingService, useValue: { getDashboard: getAccountingDashboard } },
        { provide: CrmService, useValue: { getMyReminders, getOpportunities } },
        { provide: WarehouseContextService, useValue: { selectedWarehouseId } }
      ]
    });
  }

  it('profil « Commercial étendu » (invoices ✗, quotes ✓, clients ✓, stock ✗, comptabilité ✗, CRM ✗): ' +
    'aucun appel invoices/stock/comptabilité/CRM, blocs CA/impayées absents, bloc Devis présent', () => {
    configure();
    const auth = TestBed.inject(AuthService);
    setUser(auth, makeUser(['quotes:read', 'clients:read'], []));

    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    // (i) aucune requête invoices n'est émise par le composant lui-même
    // (le service dashboard est mocké et vérifié séparément dans dashboard.service.spec.ts —
    // ici on vérifie les 3 chemins additionnels propres au composant).
    expect(getStockAlerts).not.toHaveBeenCalled();
    expect(getDeliveryNotes).not.toHaveBeenCalled();
    expect(getAccountingDashboard).not.toHaveBeenCalled();
    expect(getMyReminders).not.toHaveBeenCalled();
    expect(getOpportunities).not.toHaveBeenCalled();

    // (ii) gating des computed exposés au template
    expect(component.canReadInvoices()).toBeFalse();
    expect(component.canReadQuotes()).toBeTrue();
    expect(component.canReadClients()).toBeTrue();
    expect(component.showStockUrgent()).toBeFalse();
    expect(component.showDeliveryUrgent()).toBeFalse();
    expect(component.hasAccountingModule()).toBeFalse();
    expect(component.hasCrmModule()).toBeFalse();

    // (iii) blocs DOM : KPI CA / Factures impayées absents, Devis récents présent
    const html: string = fixture.nativeElement.textContent ?? '';
    expect(html).not.toContain("Chiffre d'affaires");
    expect(html).not.toContain('Factures impayées');
    expect(html).toContain('Devis récents');
  });

  it('loadStockAlerts() ne fait aucun appel sans showStockUrgent()', () => {
    configure();
    const auth = TestBed.inject(AuthService);
    setUser(auth, makeUser(['invoices:read'], []));

    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.showStockUrgent()).toBeFalse();
    expect(getStockAlerts).not.toHaveBeenCalled();
  });

  it('loadPendingDeliveries() ne fait aucun appel sans showDeliveryUrgent()', () => {
    configure();
    const auth = TestBed.inject(AuthService);
    setUser(auth, makeUser(['invoices:read'], ALL_MODULES));

    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.showDeliveryUrgent()).toBeFalse();
    expect(getDeliveryNotes).not.toHaveBeenCalled();
  });

  it('loadAccountingKpis() a un early-return sans hasAccountingModule (pas d\'appel API)', () => {
    configure();
    const auth = TestBed.inject(AuthService);
    // Module Accounting non activé -> hasAccountingModule() est false même avec la permission.
    setUser(auth, makeUser(['accounting:read'], []));

    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.hasAccountingModule()).toBeFalse();
    expect(getAccountingDashboard).not.toHaveBeenCalled();
  });

  it('loadCrmKpis() a un early-return sans hasCrmModule (pas d\'appel API)', () => {
    configure();
    const auth = TestBed.inject(AuthService);
    setUser(auth, makeUser(['crm:read'], []));

    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.hasCrmModule()).toBeFalse();
    expect(getMyReminders).not.toHaveBeenCalled();
    expect(getOpportunities).not.toHaveBeenCalled();
  });

  it('profil « Auditeur » (toutes les permissions en lecture) : tous les blocs rendus, tous les appels partent', () => {
    configure();
    const auth = TestBed.inject(AuthService);
    setUser(
      auth,
      makeUser(
        [
          'invoices:read',
          'quotes:read',
          'clients:read',
          'stock:read',
          'delivery_notes:read',
          'accounting:read',
          'crm:read'
        ],
        ALL_MODULES
      )
    );

    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    expect(component.canReadInvoices()).toBeTrue();
    expect(component.canReadQuotes()).toBeTrue();
    expect(component.canReadClients()).toBeTrue();
    expect(component.showStockUrgent()).toBeTrue();
    expect(component.showDeliveryUrgent()).toBeTrue();
    expect(component.hasAccountingModule()).toBeTrue();
    expect(component.hasCrmModule()).toBeTrue();

    expect(getStockAlerts).toHaveBeenCalled();
    expect(getDeliveryNotes).toHaveBeenCalled();
    expect(getAccountingDashboard).toHaveBeenCalled();
    expect(getMyReminders).toHaveBeenCalled();
    expect(getOpportunities).toHaveBeenCalled();

    const html: string = fixture.nativeElement.textContent ?? '';
    expect(html).toContain("Chiffre d'affaires");
    expect(html).toContain('Factures impayées');
    expect(html).toContain('Devis récents');
  });
});
