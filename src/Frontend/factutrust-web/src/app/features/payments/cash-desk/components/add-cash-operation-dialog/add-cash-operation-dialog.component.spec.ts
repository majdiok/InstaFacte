import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { CashDeskService, CashOperationType, CashOperationListItem } from '@core/services/cash-desk.service';
import { ToastService } from '@core/services/toast.service';
import { AddCashOperationDialogComponent } from './add-cash-operation-dialog.component';

describe('AddCashOperationDialogComponent', () => {
  let fixture: ComponentFixture<AddCashOperationDialogComponent>;
  let component: AddCashOperationDialogComponent;
  let createOperation: jasmine.Spy;
  let toastAdd: jasmine.Spy;

  const createdOperation: CashOperationListItem = {
    id: 'op-1',
    operationType: CashOperationType.Credit,
    operationTypeDisplay: 'Crédit',
    operationDate: '2026-07-30',
    method: 0,
    methodDisplay: 'Espèces',
    label: 'Test',
    category: null,
    categoryDisplay: null,
    revenueCategory: 0,
    revenueCategoryDisplay: 'Encaissement ventes au comptant',
    document: 'ENC-001',
    amount: 100,
    currency: 'TND',
    reference: null,
    notes: null,
    status: 0,
    origin: 0,
    sourceType: null,
    sourceId: null
  } as CashOperationListItem;

  beforeEach(async () => {
    createOperation = jasmine.createSpy('createOperation').and.returnValue(
      of({ success: true, data: createdOperation })
    );
    toastAdd = jasmine.createSpy('add');

    await TestBed.configureTestingModule({
      imports: [AddCashOperationDialogComponent],
      providers: [
        provideNoopAnimations(),
        { provide: CashDeskService, useValue: { createOperation } },
        { provide: ToastService, useValue: { add: toastAdd } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AddCashOperationDialogComponent);
    component = fixture.componentInstance;
  });

  function openDialog(): void {
    fixture.componentRef.setInput('visible', true);
    fixture.detectChanges();
  }

  it('renders title, type toggle, category, methods and footer fields when visible', () => {
    openDialog();

    const el = fixture.nativeElement as HTMLElement;
    const text = el.textContent ?? '';

    expect(text).toContain("Enregistrer l'opération");
    expect(text).toContain("Type d'opération");
    expect(text).toContain('Décaissement');
    expect(text).toContain('Encaissement');
    expect(text).toContain('Catégorie de revenu');
    expect(text).toContain('Méthode de paiement');
    expect(text).toContain('Espèces');
    expect(text).toContain('Virement bancaire');
    expect(text).toContain('Chèque');
    expect(text).toContain('Carte bancaire');
    expect(text).toContain('Paiement mobile');
    expect(text).toContain('Autre');
    expect(text).toContain('Montant');
    expect(text).toContain('Date de règlement');
    expect(text).toContain('Libellé');
    expect(text).toContain('Référence');
    expect(text).toContain('Note');
    expect(text).toContain('Annuler');
    expect(text).toContain('Valider');

    expect(el.querySelector('[role="radiogroup"][aria-label="Type d\'opération"]')).toBeTruthy();
    expect(el.querySelector('#revenueCategory') || el.querySelector('p-dropdown')).toBeTruthy();
    expect(el.querySelector('#label')).toBeTruthy();
    expect(el.querySelector('#reference')).toBeTruthy();
    expect(el.querySelector('#notes')).toBeTruthy();
  });

  it('switches category label and dropdown when toggling to Décaissement', () => {
    openDialog();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Catégorie de revenu');

    component.onTypeChange(CashOperationType.Debit);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Catégorie de dépense');
    expect(el.textContent).not.toContain('Catégorie de revenu');
    expect(el.querySelector('#expenseCategory') || el.querySelector('p-dropdown')).toBeTruthy();
  });

  it('does not call API when category is missing', () => {
    openDialog();
    component.amount = 50;
    component.label = 'Achat';
    component.selectedRevenueCategory = null;

    component.submit();

    expect(createOperation).not.toHaveBeenCalled();
    expect(component.errorMessage()).toContain('Catégorie de revenu');
  });

  it('submits correct credit payload when form is valid', () => {
    openDialog();
    component.operationType = CashOperationType.Credit;
    component.selectedRevenueCategory = 0;
    component.selectedMethod = 0;
    component.amount = 125.5;
    component.operationDate = new Date(2026, 6, 15);
    component.label = '  Vente comptant  ';
    component.reference = ' REF-1 ';
    component.notes = ' note ';

    const createdSpy = spyOn(component.operationCreated, 'emit');
    component.submit();

    expect(createOperation).toHaveBeenCalledWith(
      jasmine.objectContaining({
        operationType: CashOperationType.Credit,
        amount: 125.5,
        method: 0,
        label: 'Vente comptant',
        category: null,
        revenueCategory: 0,
        reference: 'REF-1',
        notes: 'note',
        operationDate: '2026-07-15'
      })
    );
    expect(createdSpy).toHaveBeenCalledWith(createdOperation);
    expect(toastAdd).toHaveBeenCalledWith(
      jasmine.objectContaining({
        severity: 'success',
        detail: 'Encaissement enregistré avec succès'
      })
    );
  });

  it('submits debit payload with expense category', () => {
    createOperation.and.returnValue(
      of({
        success: true,
        data: { ...createdOperation, operationType: CashOperationType.Debit }
      })
    );
    openDialog();
    component.operationType = CashOperationType.Debit;
    component.selectedExpenseCategory = 1;
    component.selectedMethod = 1;
    component.amount = 40;
    component.operationDate = new Date(2026, 6, 20);
    component.label = 'Loyer';

    component.submit();

    expect(createOperation).toHaveBeenCalledWith(
      jasmine.objectContaining({
        operationType: CashOperationType.Debit,
        category: 1,
        revenueCategory: null,
        label: 'Loyer',
        method: 1
      })
    );
    expect(toastAdd).toHaveBeenCalledWith(
      jasmine.objectContaining({
        detail: 'Décaissement enregistré avec succès'
      })
    );
  });

  it('resets form on open and on hide', () => {
    component.amount = 99;
    component.label = 'stale';
    component.reference = 'stale-ref';
    component.notes = 'stale-notes';
    component.selectedRevenueCategory = 2;
    component.operationType = CashOperationType.Debit;
    component.selectedMethod = 3;
    component.errorMessage.set('erreur');

    openDialog();

    expect(component.amount).toBe(0);
    expect(component.label).toBe('');
    expect(component.reference).toBe('');
    expect(component.notes).toBe('');
    expect(component.selectedRevenueCategory).toBeNull();
    expect(component.selectedExpenseCategory).toBeNull();
    expect(component.operationType as number).toBe(CashOperationType.Credit);
    expect(component.selectedMethod).toBe(0);
    expect(component.errorMessage()).toBe('');

    component.amount = 10;
    component.label = 'temp';
    const visibleSpy = spyOn(component.visibleChange, 'emit');
    component.onHide();

    expect(visibleSpy).toHaveBeenCalledWith(false);
    expect(component.amount).toBe(0);
    expect(component.label).toBe('');
  });

  it('shows API error message without closing', () => {
    createOperation.and.returnValue(throwError(() => ({ error: { message: 'Solde insuffisant' } })));
    openDialog();
    component.selectedRevenueCategory = 0;
    component.amount = 10;
    component.label = 'Test';

    component.submit();

    expect(component.errorMessage()).toBe('Solde insuffisant');
    expect(component.submitting()).toBeFalse();
  });
});
