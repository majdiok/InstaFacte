import {
  fanOutWarning,
  formatImpact,
  neverScannedHint,
  riskLabelOf,
  riskLevelOf,
  severityLabel,
  sortByPriority,
  sortNoteItems
} from './firm-revision.view-model';
import { FirmRevisionDossierRow, FirmRevisionNoteItem, FirmRevisionOverview } from './firm-revision.service';

function row(overrides: Partial<FirmRevisionDossierRow> = {}): FirmRevisionDossierRow {
  return {
    companyTenantId: 'a',
    companyName: 'Dossier',
    riskScore: 0,
    blockingCount: 0,
    warningCount: 0,
    infoCount: 0,
    totalAnomalies: 0,
    impactAmount: 0,
    readFailed: false,
    neverScanned: false,
    hasRevisionNote: false,
    ...overrides
  };
}

function noteItem(overrides: Partial<FirmRevisionNoteItem> = {}): FirmRevisionNoteItem {
  return {
    anomalyId: 'x',
    ruleCode: 'r',
    moduleCode: 'm',
    severity: 0,
    title: 'Anomalie',
    workingNote: '',
    action: 'aucune',
    actionLabel: 'Aucune action requise',
    ...overrides
  };
}

describe('firm-revision view model', () => {
  describe('niveau de risque', () => {
    it('classe en critique dès le premier bloquant, quel que soit le score', () => {
      // Un bloquant empêche la clôture : il ne doit jamais être noyé sous un score global faible.
      expect(riskLevelOf(row({ blockingCount: 1, riskScore: 1 }))).toBe('critical');
    });

    it('classe un dossier sans anomalie en conforme', () => {
      expect(riskLevelOf(row())).toBe('clean');
      expect(riskLabelOf(row())).toBe('Conforme');
    });

    it('distingue élevé et modéré par le score', () => {
      expect(riskLevelOf(row({ riskScore: 6 }))).toBe('high');
      expect(riskLevelOf(row({ riskScore: 2 }))).toBe('moderate');
    });

    it('signale un dossier illisible plutôt que de le présenter comme conforme', () => {
      const unreadable = row({ readFailed: true });
      expect(riskLevelOf(unreadable)).toBe('unknown');
      expect(riskLabelOf(unreadable)).toBe('Dossier illisible');
    });

    it('signale un dossier jamais contrôlé', () => {
      expect(riskLabelOf(row({ neverScanned: true, riskScore: 50 }))).toBe('Jamais contrôlé');
    });
  });

  describe('impact chiffré', () => {
    it('affiche « non chiffrable » plutôt que zéro quand la règle ne produit pas de montant', () => {
      // Afficher « 0,000 TND » laisserait croire à un enjeu nul alors qu'il est inconnu.
      expect(formatImpact(null)).toBe('Non chiffrable');
      expect(formatImpact(undefined)).toBe('Non chiffrable');
    });

    it('formate au millime, la précision du dinar', () => {
      expect(formatImpact(1234.5)).toContain('1');
      expect(formatImpact(1234.5)).toContain('500');
      expect(formatImpact(1234.5)).toContain('TND');
    });

    it('distingue un montant nul réel de l’absence de montant', () => {
      expect(formatImpact(0)).not.toBe('Non chiffrable');
    });
  });

  describe('tri', () => {
    it('classe les dossiers par risque puis par impact', () => {
      const sorted = sortByPriority([
        row({ companyName: 'B', riskScore: 5, impactAmount: 100 }),
        row({ companyName: 'A', riskScore: 20, impactAmount: 10 }),
        row({ companyName: 'C', riskScore: 5, impactAmount: 900 })
      ]);

      expect(sorted.map(r => r.companyName)).toEqual(['A', 'C', 'B']);
    });

    it('ne modifie pas le tableau reçu', () => {
      const source = [row({ companyName: 'B', riskScore: 1 }), row({ companyName: 'A', riskScore: 9 })];
      sortByPriority(source);
      expect(source[0].companyName).toBe('B');
    });

    it('place les entrées bloquantes de la note en tête', () => {
      const sorted = sortNoteItems([
        noteItem({ title: 'info', severity: 0 }),
        noteItem({ title: 'bloquant', severity: 2 }),
        noteItem({ title: 'avertissement', severity: 1 })
      ]);

      expect(sorted.map(i => i.title)).toEqual(['bloquant', 'avertissement', 'info']);
    });
  });

  describe('bandeaux', () => {
    const overview = (o: Partial<FirmRevisionOverview>): FirmRevisionOverview => ({
      fiscalYear: 2026,
      dossiersCount: 0,
      dossiersWithAnomaliesCount: 0,
      dossiersNeverScannedCount: 0,
      blockingCount: 0,
      warningCount: 0,
      infoCount: 0,
      totalAnomalies: 0,
      totalImpactAmount: 0,
      currency: 'TND',
      generatedAt: '',
      dossiers: [],
      byFamily: [],
      fanOut: { dossiersRead: 0, dossiersFailed: 0, isPartial: false },
      ...o
    });

    it('avertit quand des dossiers n’ont pas pu être lus', () => {
      const warning = fanOutWarning(overview({
        fanOut: { dossiersRead: 8, dossiersFailed: 2, isPartial: true }
      }));
      expect(warning).toContain('2');
      expect(warning).toContain('partiels');
    });

    it('reste silencieux quand le balayage est complet', () => {
      expect(fanOutWarning(overview({}))).toBeNull();
      expect(fanOutWarning(null)).toBeNull();
    });

    it('rappelle que l’absence de contrôle ne vaut pas conformité', () => {
      expect(neverScannedHint(overview({ dossiersNeverScannedCount: 3 }))).toContain('ne vaut pas conformité');
      expect(neverScannedHint(overview({}))).toBeNull();
    });
  });

  it('libelle les sévérités', () => {
    expect(severityLabel(2)).toBe('Bloquant');
    expect(severityLabel(1)).toBe('Avertissement');
    expect(severityLabel(0)).toBe('Information');
    expect(severityLabel(99)).toBe('—');
  });
});
