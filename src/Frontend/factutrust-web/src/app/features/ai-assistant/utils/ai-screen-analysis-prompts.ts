import { SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX } from './ai-volatile-analysis-payload.util';

const STANDARD_OUTPUT_HINT = `Structure ta réponse avec ces sections markdown :
## Synthèse exécutive
## Indicateurs clés
## Analyse détaillée
## Anomalies et risques (OK | Attention | Critique)
## Opportunités
## Actions recommandées
## Points à vérifier / limites des données

Appelle generate_dashboard_config UNE SEULE FOIS si des KPI sont identifiables dans le snapshot, puis rédige l'analyse complète dans les sections ci-dessus.`;

const FAMILY_PROMPTS: Record<string, string> = {
  'accounting-income-statement': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse le compte de résultat : calcule marge brute et nette, variations N/N-1, postes anormaux. ${STANDARD_OUTPUT_HINT}`,
  'accounting-balance-sheet': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse le bilan : structure actif/passif, équilibre, ratios de solvabilité et liquidité. ${STANDARD_OUTPUT_HINT}`,
  'accounting-balance': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse la balance : soldes débiteurs/créditeurs, comptes déséquilibrés, top variations. ${STANDARD_OUTPUT_HINT}`,
  'accounting-vat-declaration': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse la déclaration TVA : cohérence collectée/déductible, écarts et risques fiscaux. ${STANDARD_OUTPUT_HINT}`,
  'accounting-ledger': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse le grand livre : équilibre débit/crédit, écritures atypiques, évolution du solde. ${STANDARD_OUTPUT_HINT}`,
  'accounting-journal': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse le journal comptable : répartition par journal, pièces, montants significatifs. ${STANDARD_OUTPUT_HINT}`,
  'accounting-sub-journals': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse les journaux auxiliaires : flux clients/fournisseurs, encaissements et ajustements. ${STANDARD_OUTPUT_HINT}`,
  'accounting-manual-entry': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse la saisie manuelle : brouillons, équilibre des lignes, anomalies de saisie. ${STANDARD_OUTPUT_HINT}`,
  'cash-desk': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse la caisse : flux entrées/sorties, soldes par mode de paiement, opérations atypiques. ${STANDARD_OUTPUT_HINT}`,
  'accounting-aging': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse la balance âgée : créances par tranche, clients à risque, retards > 90 jours. ${STANDARD_OUTPUT_HINT}`,
  'accounting-lettering': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse le lettrage : écritures non lettrées, écarts, clients à rapprocher. ${STANDARD_OUTPUT_HINT}`,
  'invoice-list': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse la liste des factures : impayés, retards, concentration clients, statuts. ${STANDARD_OUTPUT_HINT}`,
  'credit-note-list': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse la liste des avoirs de vente : montants à rembourser, retards, concentration clients, statuts. ${STANDARD_OUTPUT_HINT}`,
  dashboard: `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse le tableau de bord : KPI commerciaux, alertes stock/livraison/facturation, priorités du jour. ${STANDARD_OUTPUT_HINT}`,
  'stock-simple': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse le stock : ruptures, surstock, rotation, produits critiques. ${STANDARD_OUTPUT_HINT}`,
  'forecasting-replenishment': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse les recommandations de réapprovisionnement : urgences, quantités, confiance. ${STANDARD_OUTPUT_HINT}`,
  'forecasting-revenue': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse les prévisions de revenus : tendances, saisonnalité, écarts vs historique. ${STANDARD_OUTPUT_HINT}`,
  'treasury-cash-forecast': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse la trésorerie prévisionnelle : mois de tension, concentration d'échéances, scénarios et marge de manœuvre. Les montants affichés font foi, ne les recalcule pas. ${STANDARD_OUTPUT_HINT}`,
  'forecasting-abc-xyz': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse la matrice ABC/XYZ : classification produits, priorités de gestion. ${STANDARD_OUTPUT_HINT}`,
  'forecasting-promotions': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse les promotions recommandées : impact estimé, risques, produits cibles. ${STANDARD_OUTPUT_HINT}`,
  'accounting-closing': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse la clôture comptable : avancement, contrôles manquants, écritures de clôture. ${STANDARD_OUTPUT_HINT}`,
  'accounting-chart': `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Analyse le plan comptable : couverture, comptes inutilisés, structure NCT 01 / SCE. ${STANDARD_OUTPUT_HINT}`
};

const DEFAULT_PROMPT = `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Signale anomalies, risques et opportunités. Propose des actions concrètes. ${STANDARD_OUTPUT_HINT}`;

/** Backend-only prompt for screen analysis, specialized per screen when available. */
export function resolveScreenAnalysisBackendPrompt(screenId: string): string {
  const trimmed = screenId.trim();
  return FAMILY_PROMPTS[trimmed] ?? DEFAULT_PROMPT;
}

export const SCREEN_ANALYSIS_REQUIRED_SECTIONS = [
  'Synthèse exécutive',
  'Indicateurs clés',
  'Analyse détaillée',
  'Anomalies et risques',
  'Actions recommandées'
] as const;
