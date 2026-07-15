export interface AccountingJournalTab {
  code: string;
  label: string;
  icon: string;
  shortLabel: string;
}

export interface AccountingJournalTabChange {
  index: number;
  code: string;
}

export const ACCOUNTING_SUB_JOURNALS: readonly AccountingJournalTab[] = [
  { code: 'JV', label: 'Ventes', icon: 'pi pi-shopping-cart', shortLabel: 'JV' },
  { code: 'JA', label: 'Achats', icon: 'pi pi-shopping-bag', shortLabel: 'JA' },
  { code: 'JC', label: 'Caisse', icon: 'pi pi-wallet', shortLabel: 'JC' },
  { code: 'JB', label: 'Banque', icon: 'pi pi-building', shortLabel: 'JB' },
  { code: 'JOD', label: 'Opérations diverses', icon: 'pi pi-book', shortLabel: 'JOD' },
  { code: 'JIM', label: 'Immobilisations', icon: 'pi pi-box', shortLabel: 'JIM' }
];
