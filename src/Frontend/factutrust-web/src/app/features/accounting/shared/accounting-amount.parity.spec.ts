import { TestBed } from '@angular/core/testing';
import { DecimalPipe, registerLocaleData } from '@angular/common';
import localeFrTN from '@angular/common/locales/fr-TN';
import localeFrTNExtra from '@angular/common/locales/extra/fr-TN';
import { LOCALE_ID } from '@angular/core';
import { formatAccountingAmount } from './accounting-amount.pipe';

// main.ts n'est pas chargé sous Karma : sans cet enregistrement, DecimalPipe n'a pas les données
// de locale fr-TN et lève NG0701 au lieu de formater.
registerLocaleData(localeFrTN, 'fr-TN', localeFrTNExtra);

/**
 * Prérequis de la migration des `| number : '1.3-3'` vers `accountingAmount`.
 *
 * <p>
 * Les deux formateurs ne partagent pas leur implémentation : `DecimalPipe` s'appuie sur les données
 * de locale Angular (`registerLocaleData(localeFrTN)`), `formatAccountingAmount` sur
 * `Intl.NumberFormat`. Un séparateur de milliers différent — espace fine insécable contre espace
 * insécable, par exemple — suffirait à modifier l'affichage de tous les montants comptables et à
 * casser les specs qui assertent sur le texte.
 * </p>
 *
 * <p>
 * Ce test tranche la question par la mesure plutôt que par la prudence. S'il passe, la migration
 * est un remplacement sûr ; s'il échoue, il faut faire reposer le pipe sur `DecimalPipe`.
 * </p>
 */
describe('accountingAmount — parité avec DecimalPipe', () => {
  let decimal: DecimalPipe;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [{ provide: LOCALE_ID, useValue: 'fr-TN' }, DecimalPipe]
    });
    decimal = TestBed.inject(DecimalPipe);
  });

  const values = [
    0, 1, 12.5, 100, 1000, 1234.567, 3314.2, 12345.678,
    999999.999, 1234567.891, -1234.5, -0.001, 0.001
  ];

  for (const value of values) {
    it(`produit la même chaîne que DecimalPipe pour ${value}`, () => {
      const viaDecimalPipe = decimal.transform(value, '1.3-3');
      const viaFormatter = formatAccountingAmount(value, 'TND', false);

      expect(viaFormatter).toBe(viaDecimalPipe as string);
    });
  }

  it('les séparateurs de milliers sont identiques', () => {
    const viaDecimalPipe = decimal.transform(1234567.891, '1.3-3') as string;
    const viaFormatter = formatAccountingAmount(1234567.891, 'TND', false);

    // Comparaison des points de code : une espace fine insécable (U+202F) et une espace
    // insécable (U+00A0) sont visuellement proches et se confondraient à l'œil.
    expect([...viaFormatter].map(c => c.codePointAt(0)))
      .toEqual([...viaDecimalPipe].map(c => c.codePointAt(0)));
  });
});
