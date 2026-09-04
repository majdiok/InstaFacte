import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ContractFormComponent } from './contract-form.component';

const VALID_CLIENT_ID = '550e8400-e29b-41d4-a716-446655440000';
const VALID_METRIC_ID = '550e8400-e29b-41d4-a716-446655440042';
const VALID_PRODUCT_ID = '550e8400-e29b-41d4-a716-446655440041';

describe('ContractFormComponent canSave (validation du bouton Enregistrer)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ContractFormComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    });
  });

  /** Instancie le composant sans déclencher ngOnInit (pas de detectChanges). */
  function createComponent(): ContractFormComponent {
    return TestBed.createComponent(ContractFormComponent).componentInstance;
  }

  /** Remplit le formulaire avec des valeurs valides minimales. */
  function fillValidForm(cmp: ContractFormComponent): void {
    cmp.clientId = VALID_CLIENT_ID;
    cmp.billingDayOfMonth = 1;
    cmp.startDate = '2026-01-01';
    cmp.endDate = '';
    cmp.lines = [{
      id: null,
      lineType: 'FixedRecurring',
      productId: VALID_PRODUCT_ID,
      description: 'Abonnement mensuel',
      quantity: 1,
      unitPriceHT: 100,
      vatRate: 19,
      usageMetricId: null,
      includedQuantity: null,
      overageUnitPriceHT: null,
      sortOrder: 0
    }];
  }

  it('canSave est false quand aucun client n\'est sélectionné', () => {
    const cmp = createComponent();
    fillValidForm(cmp);
    cmp.clientId = '';
    expect(cmp.canSave).toBeFalse();
  });

  it('canSave est false quand le jour de facturation est hors bornes [1..31]', () => {
    const cmp = createComponent();
    fillValidForm(cmp);
    cmp.billingDayOfMonth = 0;
    expect(cmp.canSave).toBeFalse();
    cmp.billingDayOfMonth = 32;
    expect(cmp.canSave).toBeFalse();
  });

  it('canSave est false quand le jour de facturation est vidé (input numérique → null)', () => {
    const cmp = createComponent();
    fillValidForm(cmp);
    cmp.billingDayOfMonth = null as unknown as number;
    expect(cmp.canSave).toBeFalse();
  });

  it('canSave est false quand la date de début est vide', () => {
    const cmp = createComponent();
    fillValidForm(cmp);
    cmp.startDate = '';
    expect(cmp.canSave).toBeFalse();
  });

  it('canSave est false quand la date de fin est antérieure à la date de début', () => {
    const cmp = createComponent();
    fillValidForm(cmp);
    cmp.startDate = '2026-06-01';
    cmp.endDate = '2026-05-31';
    expect(cmp.canSave).toBeFalse();
  });

  it('canSave est true quand la date de fin égale la date de début', () => {
    const cmp = createComponent();
    fillValidForm(cmp);
    cmp.startDate = '2026-06-01';
    cmp.endDate = '2026-06-01';
    expect(cmp.canSave).toBeTrue();
  });

  it('canSave est false sans aucune ligne', () => {
    const cmp = createComponent();
    fillValidForm(cmp);
    cmp.lines = [];
    expect(cmp.canSave).toBeFalse();
  });

  it('canSave est true quand une ligne n\'a pas de description', () => {
    const cmp = createComponent();
    fillValidForm(cmp);
    cmp.lines[0].description = '   ';
    expect(cmp.canSave).toBeTrue();
  });

  it('canSave est true avec une description vide', () => {
    const cmp = createComponent();
    fillValidForm(cmp);
    cmp.lines[0].description = '';
    expect(cmp.canSave).toBeTrue();
  });

  it('canSave est false pour une ligne récurrente fixe sans produit', () => {
    const cmp = createComponent();
    fillValidForm(cmp);
    cmp.lines[0].productId = null;
    expect(cmp.canSave).toBeFalse();
  });

  it('canSave est true pour une ligne frais d\'installation sans produit', () => {
    const cmp = createComponent();
    fillValidForm(cmp);
    cmp.lines[0].lineType = 'OneTimeSetup';
    cmp.lines[0].productId = null;
    expect(cmp.canSave).toBeTrue();
  });

  it('canSave est false pour une ligne à la consommation sans produit même avec métrique', () => {
    const cmp = createComponent();
    fillValidForm(cmp);
    cmp.lines[0].lineType = 'UsageMetered';
    cmp.lines[0].productId = null;
    cmp.lines[0].usageMetricId = VALID_METRIC_ID;
    expect(cmp.canSave).toBeFalse();
  });

  it('canSave est false pour une ligne à la consommation sans métrique', () => {
    const cmp = createComponent();
    fillValidForm(cmp);
    cmp.lines[0].lineType = 'UsageMetered';
    cmp.lines[0].usageMetricId = null;
    expect(cmp.canSave).toBeFalse();
  });

  it('canSave est true pour une ligne à la consommation avec métrique et produit', () => {
    const cmp = createComponent();
    fillValidForm(cmp);
    cmp.lines[0].lineType = 'UsageMetered';
    cmp.lines[0].productId = VALID_PRODUCT_ID;
    cmp.lines[0].usageMetricId = VALID_METRIC_ID;
    expect(cmp.canSave).toBeTrue();
  });

  it('canSave est true quand tous les champs obligatoires sont valides', () => {
    const cmp = createComponent();
    fillValidForm(cmp);
    expect(cmp.canSave).toBeTrue();
  });

  it('canSave redevient false quand un champ obligatoire est invalidé après coup', () => {
    const cmp = createComponent();
    fillValidForm(cmp);
    expect(cmp.canSave).toBeTrue();
    cmp.clientId = '';
    expect(cmp.canSave).toBeFalse();
  });

  it('l\'état initial du formulaire (aucun client) désactive l\'enregistrement', () => {
    const cmp = createComponent();
    expect(cmp.canSave).toBeFalse();
  });
});
