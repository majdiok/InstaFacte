import { IrppRegularizationMonth, IrppRegularizationPreview } from '@core/services/payroll.service';
import {
  buildDetailRows,
  buildSummaryRows,
  featureDisabledNotice,
  isWorthSaving,
  monthLabel,
  outcomeLabel,
  outcomeSeverity,
  partialYearNotice,
  resolveOutcome
} from './irpp-regularization.view-model';

function month(m: number, netTaxable: number, irpp: number, css: number, isSettled = true): IrppRegularizationMonth {
  return { month: m, monthLabel: monthLabel(m), monthlyNetTaxable: netTaxable, irpp, css, isSettled };
}

function preview(overrides: Partial<IrppRegularizationPreview> = {}): IrppRegularizationPreview {
  return {
    employeeId: 'emp-1',
    employeeName: 'BEN ALI Karim',
    employeeNumber: 'EMP-001',
    year: 2026,
    month: 12,
    reason: 0,
    reasonLabel: 'Régularisation annuelle (décembre)',
    monthsCounted: 12,
    cumulNetTaxable: 19796.796,
    cumulIrppWithheld: 3199.2,
    cumulCssWithheld: 98.988,
    irppDue: 3199.199,
    cssDue: 98.984,
    irppDelta: -0.001,
    cssDelta: -0.004,
    totalDelta: -0.005,
    isAdditionalWithholding: false,
    isFeatureDisabled: false,
    isPartialYear: false,
    months: [],
    ...overrides
  };
}

describe('irpp-regularization.view-model', () => {
  describe('resolveOutcome', () => {
    it('classe un écart positif en rappel', () => {
      expect(resolveOutcome(150)).toBe('rappel');
      expect(outcomeLabel(resolveOutcome(150))).toBe('Rappel');
      expect(outcomeSeverity(resolveOutcome(150))).toBe('warn');
    });

    it('classe un écart négatif en restitution', () => {
      expect(resolveOutcome(-220)).toBe('restitution');
      expect(outcomeLabel(resolveOutcome(-220))).toBe('Restitution');
      expect(outcomeSeverity(resolveOutcome(-220))).toBe('success');
    });

    it('classe un écart nul en neutre', () => {
      expect(resolveOutcome(0)).toBe('neutre');
      expect(outcomeSeverity(resolveOutcome(0))).toBe('info');
    });
  });

  describe('buildDetailRows', () => {
    it('ajoute une ligne de total qui somme chaque colonne', () => {
      const rows = buildDetailRows([
        month(1, 1000, 100, 5),
        month(2, 2000, 200, 10)
      ]);

      expect(rows.length).toBe(3);
      const total = rows[rows.length - 1];
      expect(total.isTotal).toBeTrue();
      expect(total.label).toBe('Total');
      expect(total.netTaxable).toBe(3000);
      expect(total.irpp).toBe(300);
      expect(total.css).toBe(15);
    });

    it('marque le mois non arrêté comme en cours', () => {
      const rows = buildDetailRows([month(11, 1000, 100, 5), month(12, 1000, 100, 5, false)]);

      expect(rows[0].isPending).toBeFalsy();
      expect(rows[1].isPending).toBeTrue();
    });

    it('produit uniquement la ligne de total quand il n’y a aucun mois', () => {
      const rows = buildDetailRows([]);

      expect(rows.length).toBe(1);
      expect(rows[0].isTotal).toBeTrue();
      expect(rows[0].netTaxable).toBe(0);
    });

    it('arrondit la somme au millime', () => {
      const rows = buildDetailRows([month(1, 1649.733, 266.6, 8.249), month(2, 1649.733, 266.6, 8.249)]);
      const total = rows[rows.length - 1];

      expect(total.netTaxable).toBe(3299.466);
      expect(total.css).toBe(16.498);
    });
  });

  describe('buildSummaryRows', () => {
    it('met en avant la régularisation à porter', () => {
      const rows = buildSummaryRows(preview({ totalDelta: -125.5 }));
      const highlighted = rows.filter(r => r.highlight);

      expect(highlighted.length).toBe(1);
      expect(highlighted[0].label).toBe('Régularisation à porter');
      expect(highlighted[0].value).toBe(-125.5);
    });

    it('expose le cumul et l’impôt dû', () => {
      const rows = buildSummaryRows(preview());

      expect(rows.find(r => r.label === 'Cumul net imposable')?.value).toBe(19796.796);
      expect(rows.find(r => r.label === 'IRPP dû sur le cumul')?.value).toBe(3199.199);
      expect(rows.find(r => r.label === 'IRPP déjà retenu')?.value).toBe(3199.2);
    });
  });

  describe('partialYearNotice', () => {
    it('explique une année incomplète', () => {
      const message = partialYearNotice(preview({ isPartialYear: true, monthsCounted: 3, month: 12 }));

      expect(message).toContain('3 bulletins arrêtés sur 12');
      expect(message).toContain('revenu réellement perçu');
    });

    it('accorde le singulier', () => {
      const message = partialYearNotice(preview({ isPartialYear: true, monthsCounted: 1, month: 12 }));

      expect(message).toContain('1 bulletin arrêté');
    });

    it('ne dit rien sur une année complète', () => {
      expect(partialYearNotice(preview({ isPartialYear: false }))).toBeNull();
    });
  });

  describe('featureDisabledNotice', () => {
    it('avertit quand l’exercice n’a pas activé la régularisation', () => {
      const message = featureDisabledNotice(preview({ isFeatureDisabled: true }));

      expect(message).toContain('indicatif');
      expect(message).toContain('Paramètres paie');
    });

    it('ne dit rien quand l’option est active', () => {
      expect(featureDisabledNotice(preview({ isFeatureDisabled: false }))).toBeNull();
    });
  });

  describe('isWorthSaving', () => {
    it('refuse un écart inférieur au millime', () => {
      expect(isWorthSaving(preview({ totalDelta: 0 }))).toBeFalse();
      expect(isWorthSaving(preview({ totalDelta: 0.0004 }))).toBeFalse();
    });

    it('accepte un écart d’au moins un millime, dans les deux sens', () => {
      expect(isWorthSaving(preview({ totalDelta: 0.001 }))).toBeTrue();
      expect(isWorthSaving(preview({ totalDelta: -12.5 }))).toBeTrue();
    });

    it('refuse l’absence de calcul', () => {
      expect(isWorthSaving(null)).toBeFalse();
    });
  });

  describe('monthLabel', () => {
    it('nomme les mois en français', () => {
      expect(monthLabel(1)).toBe('Janvier');
      expect(monthLabel(12)).toBe('Décembre');
    });

    it('retombe sur le numéro hors bornes', () => {
      expect(monthLabel(0)).toBe('0');
      expect(monthLabel(13)).toBe('13');
    });
  });
});
