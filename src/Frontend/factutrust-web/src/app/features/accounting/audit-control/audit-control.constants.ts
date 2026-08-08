export const ANOMALY_STATUS_LABELS: Record<number, string> = {
  0: 'Non corrigé',
  1: 'En cours',
  2: 'À surveiller',
  3: 'Corrigé',
  4: 'Ignoré'
};

export const SEVERITY_LABELS: Record<number, string> = {
  2: 'Bloquant',
  1: 'Avertissement',
  0: 'Information'
};

export const CATEGORY_LABELS: Record<number, string> = {
  0: 'Intégrité',
  1: 'Écritures',
  2: 'Comptes',
  3: 'Lettrage',
  4: 'Rapprochement',
  5: 'TVA',
  6: 'Analytique',
  7: 'Fiscalité',
  8: 'Immobilisations',
  9: 'Budgétaire',
  10: 'Liasse',
  11: 'Banque',
  12: 'Achats',
  13: 'Ventes',
  14: 'Paie',
  15: 'Trésorerie',
  16: 'Documents'
};

export type SeverityTab = 'all' | 'blocking' | 'warning' | 'info' | 'ignored';

export const SEVERITY_TAB_TO_FILTER: Record<SeverityTab, { severity?: number; status?: number }> = {
  all: {},
  blocking: { severity: 2 },
  warning: { severity: 1 },
  info: { severity: 0 },
  ignored: { status: 4 }
};
