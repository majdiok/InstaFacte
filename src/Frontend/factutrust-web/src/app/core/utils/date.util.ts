/**
 * Formate une Date en YYYY-MM-DD selon le calendrier LOCAL.
 *
 * À utiliser pour sérialiser une date sélectionnée (p-calendar, input date) vers une
 * query string / API, à la place de `toISOString().split('T')[0]` qui convertit d'abord
 * en UTC et décale donc d'un jour pour les fuseaux positifs (ex. Tunisie UTC+1 :
 * minuit local du 18/06 → `2026-06-17T23:00:00Z` → "2026-06-17").
 */
export function formatLocalDate(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}
