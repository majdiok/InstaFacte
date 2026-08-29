import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { AccountingService, InventoryBookDto } from '../services/accounting.service';
import { ToastService } from '@core/services/toast.service';
import { InventoryBookComponent } from './inventory-book.component';

function makeBook(overrides: Partial<InventoryBookDto> = {}): InventoryBookDto {
  return {
    fiscalYear: 2025,
    companyName: 'Société Test',
    closingBalance: [],
    detailedProvisions: [
      { code: '491000', label: 'Provision clients', amount: -50 },
      { code: '292000', label: 'Provision titres', amount: 120 }
    ],
    isYearLocked: false,
    lockedAt: null,
    ...overrides
  };
}

describe('InventoryBookComponent', () => {
  let fixture: ComponentFixture<InventoryBookComponent>;
  let accountingMock: { getInventoryBook: jasmine.Spy };
  let toastMock: { add: jasmine.Spy };

  beforeEach(async () => {
    accountingMock = {
      getInventoryBook: jasmine.createSpy('getInventoryBook').and.returnValue(of({ success: true, data: makeBook() }))
    };
    toastMock = { add: jasmine.createSpy('add') };

    await TestBed.configureTestingModule({
      imports: [InventoryBookComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AccountingService, useValue: accountingMock },
        { provide: ToastService, useValue: toastMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(InventoryBookComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  // BUG #013 : provisions négatives mises en évidence.
  it('applique la classe .ib-neg sur les lignes de provision au montant négatif', () => {
    const rows: HTMLTableRowElement[] = Array.from(fixture.nativeElement.querySelectorAll('tbody tr'));
    const negRow = rows.find(r => (r.textContent ?? '').includes('Provision clients'));
    const posRow = rows.find(r => (r.textContent ?? '').includes('Provision titres'));
    const negAmountCell = negRow?.querySelector('[data-label="Montant"]');
    const posAmountCell = posRow?.querySelector('[data-label="Montant"]');
    expect(negAmountCell?.classList.contains('ib-neg')).toBeTrue();
    expect(posAmountCell?.classList.contains('ib-neg')).toBeFalse();
  });

  // BUG #014 : notification lors du passage non verrouillé → verrouillé au reload.
  it('émet une notification si l’exercice passe de non verrouillé à verrouillé lors du rechargement', () => {
    expect(fixture.componentInstance.book()?.isYearLocked).toBeFalse();
    accountingMock.getInventoryBook.and.returnValue(of({ success: true, data: makeBook({ isYearLocked: true }) }));

    fixture.componentInstance.load();

    expect(toastMock.add).toHaveBeenCalled();
    const call = toastMock.add.calls.mostRecent().args[0];
    expect(call.severity).toBe('warn');
    expect(call.detail).toContain('verrouillé');
  });

  it('ne notifie pas si l’état de verrouillage ne change pas', () => {
    fixture.componentInstance.load();
    expect(toastMock.add).not.toHaveBeenCalled();
  });
});
