import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { AuthService } from '@core/services/auth.service';
import { BankAccountService } from '@core/services/bank-account.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { BankAccountsComponent } from './bank-accounts.component';

describe('BankAccountsComponent — firm delegated readonly', () => {
  let fixture: ComponentFixture<BankAccountsComponent>;
  let authReadonly: boolean;

  const sampleAccount = {
    id: 'a1',
    designation: 'Compte principal',
    bankName: 'STB',
    bankCode: '10',
    iban: 'TN5900100000000000000000',
    rib: '10 000 0000000000 00',
    isDefault: false
  };

  beforeEach(async () => {
    authReadonly = false;
    await TestBed.configureTestingModule({
      imports: [BankAccountsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: BankAccountService,
          useValue: {
            list: () => of({ success: true, data: [sampleAccount] })
          }
        },
        {
          provide: ConfirmationService,
          useValue: { confirm: jasmine.createSpy('confirm') }
        },
        {
          provide: AuthService,
          useValue: {
            isFirmDelegatedReadonly: () => authReadonly
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BankAccountsComponent);
  });

  it('shows CRUD buttons for non-readonly users', () => {
    authReadonly = false;
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent ?? '';
    expect(text).toContain('Ajouter un compte');
    expect(text).toContain('Modifier');
    expect(text).toContain('Supprimer');
  });

  it('hides CRUD buttons in firm delegated readonly mode', () => {
    authReadonly = true;
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent ?? '';
    expect(text).not.toContain('Ajouter un compte');
    expect(text).not.toContain('Modifier');
    expect(text).not.toContain('Supprimer');
    expect(text).not.toContain('Défaut');
  });

  it('does not open create dialog when readonly', () => {
    authReadonly = true;
    fixture.detectChanges();
    fixture.componentInstance.openCreate();
    expect(fixture.componentInstance.dialogVisible).toBe(false);
  });
});
