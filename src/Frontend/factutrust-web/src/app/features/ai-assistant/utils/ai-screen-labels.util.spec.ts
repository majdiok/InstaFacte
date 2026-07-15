import {
  buildScreenAnalysisDisplayLabel,
  resolveScreenLabel
} from './ai-screen-labels.util';

describe('ai-screen-labels.util', () => {
  it('maps known screen ids to French labels', () => {
    expect(resolveScreenLabel('accounting-ledger')).toBe('Grand livre');
    expect(resolveScreenLabel('invoice-list')).toBe('Liste des factures');
    expect(resolveScreenLabel('dashboard')).toBe('Tableau de bord');
  });

  it('humanizes unknown screen ids', () => {
    expect(resolveScreenLabel('custom-report')).toBe('Custom Report');
  });

  it('buildScreenAnalysisDisplayLabel wraps the resolved label', () => {
    expect(buildScreenAnalysisDisplayLabel('accounting-ledger')).toBe(
      "Analyse de l'écran : Grand livre"
    );
  });

  it('falls back to écran for empty screen id', () => {
    expect(buildScreenAnalysisDisplayLabel('')).toBe("Analyse de l'écran : écran");
  });
});
