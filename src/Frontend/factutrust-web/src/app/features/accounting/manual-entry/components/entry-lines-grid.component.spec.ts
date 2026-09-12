import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { EntryLinesGridComponent } from './entry-lines-grid.component';
import { EntryFormStore } from '../services/entry-form.store';
import { EntryReferenceStore } from '../services/entry-reference.store';
import { VatAssistService } from '../services/vat-assist.service';
import { createEmptyLine } from '../models/entry-form.model';

describe('EntryLinesGridComponent', () => {
  let fixture: ComponentFixture<EntryLinesGridComponent>;
  let store: EntryFormStore;

  const refsMock = {
    vatRates: signal([{ id: '1', percent: 19, label: '19%' }]),
    thirdPartySuggestions: signal([]),
    filterAccounts: () => [],
    searchThirdParties: jasmine.createSpy('searchThirdParties'),
    accounts: () => []
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EntryLinesGridComponent],
      providers: [
        provideNoopAnimations(),
        EntryFormStore,
        { provide: EntryReferenceStore, useValue: refsMock },
        { provide: VatAssistService, useValue: { applyVatToLine: jasmine.createSpy('applyVatToLine') } }
      ]
    }).compileComponents();

    store = TestBed.inject(EntryFormStore);
    fixture = TestBed.createComponent(EntryLinesGridComponent);
    fixture.detectChanges();
  });

  it('renders lines grid title and scrollable table', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain("Lignes d'écriture");
    expect(el.querySelector('.entry-lines-table')).toBeTruthy();
    expect(el.querySelector('.p-datatable-scrollable')).toBeTruthy();
  });

  it('renders autocomplete fields', () => {
    const autocompletes = fixture.nativeElement.querySelectorAll('p-autocomplete');
    expect(autocompletes.length).toBeGreaterThanOrEqual(2);
  });

  it('renders accounting amount inputs for debit and credit', () => {
    const amountInputs = fixture.nativeElement.querySelectorAll('app-accounting-amount-input');
    expect(amountInputs.length).toBeGreaterThanOrEqual(4);
  });

  it('shows TVA column by default', () => {
    expect(fixture.nativeElement.textContent).toContain('TVA');
  });

  it('hides TVA column when disabled in store', () => {
    store.columnVisibility.update(v => ({ ...v, vat: false }));
    fixture.detectChanges();
    const headers = fixture.nativeElement.querySelector('.p-datatable-scrollable-header, .p-datatable-thead');
    expect(headers?.textContent ?? '').not.toContain('TVA');
  });

  it('shows custom title when title input is set', () => {
    fixture.componentRef.setInput('title', 'Écriture proposée — journal JA');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Écriture proposée — journal JA');
  });

  it('uses compact scroll height when compactMode is true', () => {
    fixture.componentRef.setInput('compactMode', true);
    fixture.detectChanges();
    expect(fixture.componentInstance.compactMode()).toBe(true);
  });

  /**
   * La puce « Solde » affichait le montant saisi avec le libellé de la devise de tenue :
   * « Solde 600,000 TND » pour un solde de 600,00 EUR.
   */
  describe('devise de l’écriture', () => {
    function fill(): void {
      store.lines.set([
        { ...createEmptyLine(), accountNumber: '164', debit: 1000, credit: null },
        { ...createEmptyLine(), accountNumber: '452', debit: null, credit: 400 }
      ]);
    }

    function chipText(): string {
      const el = fixture.nativeElement as HTMLElement;
      return el.querySelector('.lines-grid__balance-chip')?.textContent ?? '';
    }

    function headerText(): string {
      const el = fixture.nativeElement as HTMLElement;
      return el.querySelector('.p-datatable-thead')?.textContent ?? '';
    }

    it('libelle le solde en dinar en devise de tenue', () => {
      fill();
      fixture.detectChanges();

      expect(chipText()).toContain('TND');
    });

    it('libelle le solde dans la devise de saisie, jamais en dinar', () => {
      store.currency.set('EUR');
      store.currencyDecimals.set(2);
      store.exchangeRate.set(3.29);
      fill();
      fixture.detectChanges();

      expect(chipText()).toContain('EUR');
      expect(chipText()).not.toContain('TND');
    });

    it('distingue les colonnes saisies des colonnes de contre-valeur', () => {
      store.currency.set('EUR');
      store.currencyDecimals.set(2);
      store.exchangeRate.set(3.29);
      fill();
      fixture.detectChanges();

      // Sans suffixe, deux colonnes « Débit » se suivaient sans rien qui les sépare.
      expect(headerText()).toContain('Débit (EUR)');
      expect(headerText()).toContain('Débit (TND)');
    });
  });
});
