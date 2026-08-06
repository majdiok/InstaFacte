/** Options et libellés partagés par les états de contrôle paie. */

export const MONTH_LABELS = [
  '',
  'Janvier', 'Février', 'Mars', 'Avril', 'Mai', 'Juin',
  'Juillet', 'Août', 'Septembre', 'Octobre', 'Novembre', 'Décembre'
];

export const MONTH_OPTIONS = MONTH_LABELS
  .slice(1)
  .map((label, index) => ({ value: index + 1, label }));

/** Exercices proposés : deux ans en arrière, deux ans en avant (même règle que la DTS). */
export function yearOptions(): { value: number; label: string }[] {
  const current = new Date().getFullYear();
  return Array.from({ length: 5 }, (_, i) => {
    const y = current - 2 + i;
    return { value: y, label: String(y) };
  });
}

export function formatMonthList(months: number[]): string {
  return months.map(m => MONTH_LABELS[m] ?? `M${m}`).join(', ');
}
