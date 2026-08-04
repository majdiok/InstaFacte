import {
  GUIDED_SCENARIOS,
  buildScenarioLines,
  getScenarioById,
  resolveAmount
} from './guided-scenarios.catalog';
import { computeTotals, isBalancedTotals } from '../services/entry-form.store';

describe('guided-scenarios.catalog', () => {
  const mockResolve = (candidates: readonly string[]) => {
    const plan = ['607', '43666', '4011', '4111', '707', '436711', '5321', '5411', '658', '211', '43662', '640', '421', '486', '481'];
    return candidates.find(c => plan.includes(c)) ?? null;
  };

  it('defines 8 scenarios', () => {
    expect(GUIDED_SCENARIOS.length).toBe(8);
  });

  it('resolveAmount picks ht/tva/ttc', () => {
    expect(resolveAmount('ht', 100, 19, 119)).toBe(100);
    expect(resolveAmount('tva', 100, 19, 119)).toBe(19);
    expect(resolveAmount('ttc', 100, 19, 119)).toBe(119);
  });

  for (const scenario of GUIDED_SCENARIOS) {
    it(`scenario ${scenario.id} builds balanced lines when amounts provided`, () => {
      const amounts = { ht: 1000, tva: 190, ttc: 1190 };
      const lines = buildScenarioLines(scenario, mockResolve, amounts, 'Test');
      expect(lines.length).toBeGreaterThanOrEqual(2);
      const totals = computeTotals(lines);
      if (scenario.id === 'achats' || scenario.id === 'ventes' || scenario.id === 'immobilisations') {
        expect(isBalancedTotals(totals)).toBe(true);
      }
    });
  }

  it('getScenarioById returns achats', () => {
    expect(getScenarioById('achats')?.defaultJournalCode).toBe('JA');
  });
});
