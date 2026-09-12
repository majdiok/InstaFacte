import { STUDIO_AI_LABELS, formatLabel } from '../studio-ai-labels';

/** Sévérités `p-tag` PrimeNG 19 utilisées par l'historique. */
export type StudioAiTagSeverity = 'success' | 'info' | 'warn' | 'danger' | 'secondary';

/** Terminé `success`, En cours `info`, À valider `warn`, Échec `danger`, Annulé/Expiré `secondary`. */
export function planStatusSeverity(status: string | null | undefined): StudioAiTagSeverity {
  switch (status) {
    case 'Completed': return 'success';
    case 'Executing': return 'info';
    case 'Pending': return 'warn';
    case 'Failed': return 'danger';
    default: return 'secondary';
  }
}

export function planStatusLabel(status: string | null | undefined): string {
  const labels = STUDIO_AI_LABELS.planStatus as Record<string, string>;
  return (status && labels[status]) || status || '';
}

export function planKindLabel(kind: string | null | undefined): string {
  return (kind && STUDIO_AI_LABELS.planKind[kind]) || kind || '';
}

/**
 * Date relative FR courte (« à l’instant », « il y a 5 min », « il y a 3 h », « il y a 2 j »).
 * `now` est injectable pour les tests ; une date illisible renvoie une chaîne vide.
 */
export function relativeTime(iso: string | null | undefined, now: Date = new Date()): string {
  if (!iso) return '';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  const rail = STUDIO_AI_LABELS.rail;
  const seconds = Math.max(0, Math.round((now.getTime() - date.getTime()) / 1000));
  if (seconds < 60) return rail.justNow;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return formatLabel(rail.minutesAgo, { count: minutes });
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return formatLabel(rail.hoursAgo, { count: hours });
  return formatLabel(rail.daysAgo, { count: Math.floor(hours / 24) });
}
