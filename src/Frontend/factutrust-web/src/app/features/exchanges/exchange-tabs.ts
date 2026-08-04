/**
 * In-page tabs for the Échanges workspace.
 * Query param `?tab=` is the source of truth (except conversation = default, no param).
 */

export type ExchangeTabKey = 'conversation' | 'documents' | 'demandes' | 'taches' | 'historique';

export interface ExchangeTabDef {
  key: ExchangeTabKey;
  label: string;
}

export const EXCHANGE_TABS: readonly ExchangeTabDef[] = [
  { key: 'conversation', label: 'Conversation' },
  { key: 'documents', label: 'Documents' },
  { key: 'demandes', label: 'Demandes' },
  { key: 'taches', label: 'Tâches' },
  { key: 'historique', label: 'Historique' }
] as const;

const TAB_KEYS = new Set<string>(EXCHANGE_TABS.map(t => t.key));

/** Legacy query aliases → canonical tab keys. */
const TAB_ALIASES: Record<string, ExchangeTabKey> = {
  partage: 'documents',
  messagerie: 'conversation',
  notifications: 'conversation'
};

export function isExchangeTabKey(raw: string): raw is ExchangeTabKey {
  return TAB_KEYS.has(raw);
}

/**
 * Parse `?tab=` value. Unknown / null → conversation (default).
 * Legacy aliases (partage, messagerie, notifications) are normalized.
 */
export function parseExchangeTab(raw: string | null | undefined): ExchangeTabKey {
  if (!raw) return 'conversation';
  const key = raw.trim().toLowerCase();
  if (!key) return 'conversation';
  if (isExchangeTabKey(key)) return key;
  return TAB_ALIASES[key] ?? 'conversation';
}
