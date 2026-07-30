/**
 * Sections affichées dans la 2e barre horizontale (desktop).
 * Les labels DOIVENT correspondre exactement à ceux de ALL_NAV_ITEMS /
 * DELEGATED_SECTION_LABELS pour préserver le filtrage existant.
 */
export const SECONDARY_NAV_SECTION_ORDER = [
  'Ventes',
  'Achats',
  'Stock',
  'Trésorerie',
  'Fiches',
  'RH & Paie',
  'Comptabilité',
  'Fiscal / TEJ'
] as const;

export type SecondaryNavSectionLabel = (typeof SECONDARY_NAV_SECTION_ORDER)[number];

/** Sections à masquer dans la 2e barre pour le cabinet comptable. */
export const ACCOUNTING_FIRM_SECONDARY_EXCLUDED_LABELS: ReadonlySet<string> = new Set([
  'Ventes',
  'Achats',
  'Trésorerie',
  'RH & Paie'
]);

/** Libellés courts d’affichage (source de vérité des labels = registry). */
export const SECONDARY_NAV_DISPLAY_LABELS: Readonly<Partial<Record<SecondaryNavSectionLabel, string>>> = {
  'Fiscal / TEJ': 'TEJ'
};

export function secondaryNavDisplayLabel(label: string): string {
  return SECONDARY_NAV_DISPLAY_LABELS[label as SecondaryNavSectionLabel] ?? label;
}
