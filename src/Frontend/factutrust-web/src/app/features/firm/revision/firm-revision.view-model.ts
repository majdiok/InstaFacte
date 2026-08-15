import { FirmRevisionDossierRow, FirmRevisionNoteItem, FirmRevisionOverview } from './firm-revision.service';

/** Niveau de risque d'un dossier, pour la pastille de la liste. */
export type RiskLevel = 'critical' | 'high' | 'moderate' | 'clean' | 'unknown';

/**
 * Seuils du niveau de risque. Alignés sur le calcul serveur
 * (`bloquant × 10 + avertissement × 3 + info`) : un seul bloquant suffit à passer en « critique »,
 * parce qu'un bloquant empêche la clôture.
 */
const RISK_CRITICAL = 10;
const RISK_HIGH = 5;

export function riskLevelOf(row: FirmRevisionDossierRow): RiskLevel {
  if (row.readFailed) return 'unknown';
  if (row.blockingCount > 0 || row.riskScore >= RISK_CRITICAL) return 'critical';
  if (row.riskScore >= RISK_HIGH) return 'high';
  if (row.riskScore > 0) return 'moderate';
  return 'clean';
}

export function riskLabelOf(row: FirmRevisionDossierRow): string {
  if (row.readFailed) return 'Dossier illisible';
  if (row.neverScanned) return 'Jamais contrôlé';
  switch (riskLevelOf(row)) {
    case 'critical': return 'Critique';
    case 'high': return 'Élevé';
    case 'moderate': return 'Modéré';
    default: return 'Conforme';
  }
}

const SEVERITY_LABELS: Record<number, string> = {
  0: 'Information',
  1: 'Avertissement',
  2: 'Bloquant'
};

export function severityLabel(severity: number): string {
  return SEVERITY_LABELS[severity] ?? '—';
}

export function severityClass(severity: number): string {
  if (severity === 2) return 'fr-badge--blocking';
  if (severity === 1) return 'fr-badge--warning';
  return 'fr-badge--info';
}

/**
 * Montant en dinars, ou la mention explicite « non chiffrable ».
 *
 * Toutes les règles ne produisent pas un impact financier : une écriture en brouillard ou un trou
 * de numérotation n'ont pas de montant. Afficher « 0,000 TND » laisserait croire à un enjeu nul
 * alors qu'il est simplement inconnu — la nuance compte dans une note de travail.
 */
export function formatImpact(amount: number | null | undefined): string {
  if (amount === null || amount === undefined) return 'Non chiffrable';
  return `${amount.toLocaleString('fr-TN', {
    minimumFractionDigits: 3,
    maximumFractionDigits: 3
  })} TND`;
}

/** Dossiers du plus risqué au moins risqué ; à impact égal, le plus gros montant d'abord. */
export function sortByPriority(rows: readonly FirmRevisionDossierRow[]): FirmRevisionDossierRow[] {
  return [...rows].sort((a, b) =>
    b.riskScore - a.riskScore ||
    b.impactAmount - a.impactAmount ||
    a.companyName.localeCompare(b.companyName, 'fr'));
}

/** Entrées de la note, bloquants d'abord : c'est l'ordre de traitement du réviseur. */
export function sortNoteItems(items: readonly FirmRevisionNoteItem[]): FirmRevisionNoteItem[] {
  return [...items].sort((a, b) =>
    b.severity - a.severity ||
    (b.impactAmount ?? 0) - (a.impactAmount ?? 0));
}

/**
 * Bandeau d'avertissement quand le balayage est incomplet. Renvoie null quand tout va bien :
 * le composant n'affiche rien plutôt qu'un bandeau vide.
 */
export function fanOutWarning(overview: FirmRevisionOverview | null): string | null {
  if (!overview?.fanOut?.isPartial) return null;
  const failed = overview.fanOut.dossiersFailed;
  return `${failed} dossier(s) n'ont pas pu être lus : les compteurs ci-dessous sont partiels.`;
}

/** Rappel affiché quand des dossiers n'ont jamais été contrôlés. */
export function neverScannedHint(overview: FirmRevisionOverview | null): string | null {
  const count = overview?.dossiersNeverScannedCount ?? 0;
  if (count === 0) return null;
  return `${count} dossier(s) n'ont jamais été contrôlés : leur absence d'anomalie ne vaut pas conformité.`;
}
