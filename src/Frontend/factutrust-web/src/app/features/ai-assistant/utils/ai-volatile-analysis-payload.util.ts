/** Client-side bound; server applies MaxAnalysisSummaryChars again.
 *  Réduit à 8192 pour limiter le coût de prefill LLM sur CPU (cohérent avec ScreenAnalysis.MaxAnalysisSummaryChars côté serveur).
 *  Valeur historique : 14000 — observée comme rarement atteinte en pratique (médiane mesurée ~3000). */
export const VOLATILE_ANALYSIS_MAX_JSON_CHARS = 8192;

export const VOLATILE_ANALYSIS_SCHEMA_VERSION = '2';

/** Prefix used to detect persisted screen-analysis user messages in the UI sanitizer. */
export const SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX =
  'Analyse les données du contexte écran joint à cette requête (bloc CONTEXTE ÉCRAN)';

/** @deprecated Use resolveScreenAnalysisBackendPrompt(screenId) */
export const SCREEN_ANALYSIS_BACKEND_PROMPT = `${SCREEN_ANALYSIS_BACKEND_PROMPT_PREFIX}. Signale anomalies, risques et opportunités. Propose des actions concrètes (achats, ventes, relances, corrections d'anomalies). Si une information manque ou doit être vérifiée dans le système, indique-le et suggère d'utiliser les outils ou écrans appropriés pour les montants officiels.`;

/** @deprecated Use {@link SCREEN_ANALYSIS_BACKEND_PROMPT} — kept for backward compatibility in tests. */
export const DEFAULT_AI_ANALYSIS_USER_PROMPT = SCREEN_ANALYSIS_BACKEND_PROMPT;

/** Serializes a screen-specific payload for the volatile UI context block. */
export function buildVolatileAnalysisJson(screenId: string, payload: unknown): string {
  const obj = {
    schemaVersion: VOLATILE_ANALYSIS_SCHEMA_VERSION,
    screenId,
    payload
  };
  let s = JSON.stringify(obj);
  if (s.length <= VOLATILE_ANALYSIS_MAX_JSON_CHARS) {
    return s;
  }
  return `${s.slice(0, VOLATILE_ANALYSIS_MAX_JSON_CHARS)}\n... [tronqué côté client]`;
}

/** Backend-only block appended to the user message when deduplication is disabled. */
export function buildVolatileScreenContextBlock(screenId: string, analysisSummary: string): string {
  return `[CONTEXTE ÉCRAN — ${screenId} — non contractuel]\n${analysisSummary}\n[FIN CONTEXTE ÉCRAN]`;
}

/** Appends the volatile screen snapshot block to the message sent to the API when needed. */
export function appendVolatileScreenBlock(
  message: string,
  screenId: string,
  analysisSummary: string,
  deduplicate = false
): string {
  if (deduplicate) {
    return message;
  }
  const block = buildVolatileScreenContextBlock(screenId, analysisSummary);
  return message ? `${message}\n\n${block}` : block;
}
