import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ConfirmationService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { AuthService } from '@core/services/auth.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { ToastService } from '@core/services/toast.service';
import { AccountingService, ChartOfAccountDto } from '../services/accounting.service';
import { ChartOfAccountsComponent, SCE_ACCOUNT_NUMBER_PATTERN } from './chart-of-accounts.component';

describe('ChartOfAccountsComponent', () => {
  function createComponent(overrides: {
    createSubAccount?: ReturnType<AccountingService['createSubAccount']>;
    toggleAccountActive?: ReturnType<AccountingService['toggleAccountActive']>;
    extractErrorMessage?: string;
  } = {}): {
    component: ChartOfAccountsComponent;
    extractErrorMessage: jasmine.Spy;
  } {
    const extractErrorMessage = jasmine.createSpy('extractErrorMessage')
      .and.returnValue(overrides.extractErrorMessage ?? 'Le numéro de compte doit être un numéro SCE (chiffres, points autorisés).');

    TestBed.configureTestingModule({
      providers: [
        {
          provide: AccountingService,
          useValue: {
            getChartOfAccounts: () => of({ success: true, data: [] }),
            createSubAccount: jasmine.createSpy('createSubAccount').and.returnValue(
              overrides.createSubAccount ?? of({ success: true, data: 'id' })
            ),
            toggleAccountActive: jasmine.createSpy('toggleAccountActive').and.returnValue(
              overrides.toggleAccountActive ?? of({ success: true, data: true })
            )
          }
        },
        { provide: AuthService, useValue: { hasPermission: () => true } },
        { provide: ToastService, useValue: { add: jasmine.createSpy('add') } },
        { provide: ConfirmationService, useValue: { confirm: (opts: { accept: () => void }) => opts.accept() } },
        { provide: ErrorHandlerService, useValue: { extractErrorMessage } }
      ]
    });

    return {
      component: TestBed.runInInjectionContext(() => new ChartOfAccountsComponent()),
      extractErrorMessage
    };
  }

  function account(partial: Partial<ChartOfAccountDto> = {}): ChartOfAccountDto {
    return {
      id: 'acc-1',
      accountNumber: '428.1',
      label: 'Personnel — mutuelles et caisses complémentaires',
      accountClass: 4,
      natureType: 1,
      isSystem: true,
      isActive: true,
      level: 5,
      ...partial
    };
  }

  it('accepte les numéros SCE pointés et les auxiliaires numériques', () => {
    expect(SCE_ACCOUNT_NUMBER_PATTERN.test('428.3')).toBeTrue();
    expect(SCE_ACCOUNT_NUMBER_PATTERN.test('425.1')).toBeTrue();
    expect(SCE_ACCOUNT_NUMBER_PATTERN.test('41100001')).toBeTrue();
    expect(SCE_ACCOUNT_NUMBER_PATTERN.test('421.1')).toBeTrue();

    const { component } = createComponent();
    component.form.label = 'Personnel — mutuelle';
    component.form.accountNumber = '428.3';
    expect(component.canSubmit()).toBeTrue();
    component.form.accountNumber = '41100001';
    expect(component.canSubmit()).toBeTrue();
  });

  it('refuse les numéros SCE mal formés', () => {
    const { component } = createComponent();
    component.form.label = 'Personnel — mutuelle';
    for (const num of ['428.', '.428', '42..1', 'A28']) {
      component.form.accountNumber = num;
      expect(component.canSubmit()).withContext(num).toBeFalse();
    }
  });

  it('rejette un numéro déjà présent dans le plan sans appeler l’API', () => {
    const { component } = createComponent();
    const api = TestBed.inject(AccountingService) as unknown as { createSubAccount: jasmine.Spy };
    component.rows.set([
      account({ accountNumber: '436712', label: 'TVA collectée sur encaissements' })
    ]);
    component.form.accountNumber = '436712';
    component.form.label = 'tv 10%';

    component.create();

    expect(component.createError()).toBe(
      'Le compte 436712 existe déjà (TVA collectée sur encaissements).'
    );
    expect(component.error()).toBeNull();
    expect(api.createSubAccount).not.toHaveBeenCalled();
  });

  it('propose le prochain numéro libre sous 43671', () => {
    const { component } = createComponent();
    component.rows.set([
      account({ accountNumber: '43671', label: 'TVA collectée' }),
      account({ accountNumber: '436711', label: 'TVA collectée sur débits' }),
      account({ accountNumber: '436712', label: 'TVA collectée sur encaissements' })
    ]);
    component.form.accountNumber = '';
    component.lastSuggestedNumber = '';

    component.onParentChange('43671');

    expect(component.form.accountNumber).toBe('436713');
    expect(component.lastSuggestedNumber).toBe('436713');
  });

  it('n’écrase pas un numéro déjà saisi par l’utilisateur', () => {
    const { component } = createComponent();
    component.rows.set([
      account({ accountNumber: '43671', label: 'TVA collectée' }),
      account({ accountNumber: '436711', label: 'TVA collectée sur débits' }),
      account({ accountNumber: '436712', label: 'TVA collectée sur encaissements' })
    ]);
    component.form.accountNumber = '436799';
    component.lastSuggestedNumber = '';

    component.onParentChange('43671');

    expect(component.form.accountNumber).toBe('436799');
  });

  it('affiche le message API sur HTTP 400 à la création', () => {
    const { component, extractErrorMessage } = createComponent({
      createSubAccount: throwError(() => new HttpErrorResponse({
        status: 400,
        error: {
          code: 'VALIDATION_FAILED',
          message: 'La validation a échoué.',
          fieldErrors: {
            'Request.AccountNumber': [{ message: 'Le numéro de compte doit être un numéro SCE (chiffres, points autorisés).' }]
          }
        }
      })),
      extractErrorMessage: 'Le numéro de compte doit être un numéro SCE (chiffres, points autorisés).'
    });

    component.form.accountNumber = '428.3';
    component.form.label = 'Personnel — mutuelle complémentaire';
    component.create();

    expect(extractErrorMessage).toHaveBeenCalled();
    expect(component.createError()).toBe('Le numéro de compte doit être un numéro SCE (chiffres, points autorisés).');
  });

  it('garde le message réseau uniquement si status === 0', () => {
    const { component, extractErrorMessage } = createComponent({
      createSubAccount: throwError(() => new HttpErrorResponse({ status: 0 }))
    });

    component.form.accountNumber = '428.3';
    component.form.label = 'Personnel — mutuelle complémentaire';
    component.create();

    expect(extractErrorMessage).not.toHaveBeenCalled();
    expect(component.createError()).toBe('Erreur réseau lors de la création du compte.');
  });

  it('httpFailureMessage extrait le message API et réserve le repli réseau au status 0', () => {
    const { component, extractErrorMessage } = createComponent({
      extractErrorMessage: 'Le compte 428.1 existe déjà (Personnel — mutuelles et caisses complémentaires).'
    });

    const apiErr = new HttpErrorResponse({ status: 400, error: { error: 'Le compte 428.1 existe déjà.' } });
    expect(component.httpFailureMessage(apiErr, 'Erreur réseau lors de la création du compte.'))
      .toBe('Le compte 428.1 existe déjà (Personnel — mutuelles et caisses complémentaires).');
    expect(extractErrorMessage).toHaveBeenCalledWith(apiErr);

    expect(component.httpFailureMessage(new HttpErrorResponse({ status: 0 }), 'Erreur réseau lors de la création du compte.'))
      .toBe('Erreur réseau lors de la création du compte.');
  });

  it('affiche le message API sur HTTP 400 au changement de statut', () => {
    const { component, extractErrorMessage } = createComponent({
      toggleAccountActive: throwError(() => new HttpErrorResponse({
        status: 400,
        error: { error: 'Un compte système ne peut pas être désactivé.' }
      })),
      extractErrorMessage: 'Un compte système ne peut pas être désactivé.'
    });

    component.confirmToggleActive(account({ isSystem: false, isActive: true }));

    expect(extractErrorMessage).toHaveBeenCalled();
    expect(component.error()).toBe('Un compte système ne peut pas être désactivé.');
  });
});
