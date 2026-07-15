import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '@core/services/auth.service';
import { CashDeskService } from '@core/services/cash-desk.service';
import { CashDeskComponent } from './cash-desk.component';

describe('CashDeskComponent — firm delegated readonly', () => {
  let authReadonly: boolean;

  function createComponent(): CashDeskComponent {
    const fixture = TestBed.createComponent(CashDeskComponent);
    return fixture.componentInstance;
  }

  beforeEach(async () => {
    authReadonly = false;
    await TestBed.configureTestingModule({
      imports: [CashDeskComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: CashDeskService,
          useValue: {
            invalidateCachesAfterCashLedgerMutation: () => undefined,
            getBalances: () => of({ success: true, data: null }),
            getOperations: () => of({ success: true, data: { items: [], totalCount: 0 } })
          }
        },
        {
          provide: AuthService,
          useValue: {
            isFirmDelegatedReadonly: () => authReadonly
          }
        }
      ]
    }).compileComponents();
  });

  it('does not open add dialog when readonly', () => {
    authReadonly = true;
    const component = createComponent();
    component.openAddDialog();
    expect(component.addDialogVisible).toBe(false);
  });

  it('does not open bank deposit wizard when readonly', () => {
    authReadonly = true;
    const component = createComponent();
    component.openBankDepositWizard();
    expect(component.bankDepositWizardVisible).toBe(false);
  });

  it('opens add dialog when not readonly', () => {
    authReadonly = false;
    const component = createComponent();
    component.openAddDialog();
    expect(component.addDialogVisible).toBe(true);
  });
});
