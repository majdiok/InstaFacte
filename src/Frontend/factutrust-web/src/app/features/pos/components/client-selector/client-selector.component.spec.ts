import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ClientSelectorComponent } from './client-selector.component';
import { ClientService } from '@core/services/client.service';
import { PosStateService } from '../../services/pos-state.service';

describe('ClientSelectorComponent outstanding', () => {
  let fixture: ComponentFixture<ClientSelectorComponent>;
  let posState: {
    client: jasmine.Spy;
    clientOutstanding: jasmine.Spy;
    formatAmount: jasmine.Spy;
    selectClient: jasmine.Spy;
    setWalkInClient: jasmine.Spy;
    setClientOutstanding: jasmine.Spy;
    applyFirstPurchaseDiscountIfEligible: jasmine.Spy;
  };

  beforeEach(async () => {
    posState = {
      client: jasmine.createSpy('client').and.returnValue({
        id: 'client-1',
        name: 'Acme SARL',
        nif: '1234567A'
      }),
      clientOutstanding: jasmine.createSpy('clientOutstanding').and.returnValue({
        totalOutstanding: 1234.567,
        creditLimit: 5000,
        isOverLimit: false
      }),
      formatAmount: jasmine.createSpy('formatAmount').and.callFake((amount: number) =>
        amount.toLocaleString('fr-TN', { minimumFractionDigits: 3, maximumFractionDigits: 3 })
      ),
      selectClient: jasmine.createSpy('selectClient'),
      setWalkInClient: jasmine.createSpy('setWalkInClient'),
      setClientOutstanding: jasmine.createSpy('setClientOutstanding'),
      applyFirstPurchaseDiscountIfEligible: jasmine.createSpy('applyFirstPurchaseDiscountIfEligible')
    };

    await TestBed.configureTestingModule({
      imports: [ClientSelectorComponent],
      providers: [
        { provide: PosStateService, useValue: posState },
        {
          provide: ClientService,
          useValue: {
            getClients: () => of({ success: true, data: { items: [] } }),
            getClientOutstanding: () => of({ success: true, data: null })
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ClientSelectorComponent);
    fixture.detectChanges();
  });

  it('renders formatted outstanding amounts without DecimalPipe locale errors', () => {
    const outstanding = fixture.nativeElement.querySelector('.client-selector__outstanding');
    expect(outstanding).toBeTruthy();
    const text = outstanding.textContent.replace(/\u202f/g, ' ');
    expect(text).toContain('Encours');
    expect(text).toContain('1 234,567 TND');
    expect(text).toContain('/ plafond 5 000,000');
    expect(posState.formatAmount).toHaveBeenCalledWith(1234.567);
    expect(posState.formatAmount).toHaveBeenCalledWith(5000);
  });
});
