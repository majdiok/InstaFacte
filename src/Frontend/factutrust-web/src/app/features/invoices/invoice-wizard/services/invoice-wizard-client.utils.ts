import { ClientInfo } from '../models/invoice-wizard.models';

/**
 * Normalise un identifiant client (API camelCase / PascalCase, chaînes vides).
 */
export function normalizeClientId(raw: unknown): string | null {
  if (raw == null) return null;
  if (typeof raw === 'string') {
    const t = raw.trim();
    return t.length > 0 ? t : null;
  }
  if (typeof raw === 'number' && !Number.isNaN(raw)) {
    return String(raw);
  }
  return null;
}

/** Extrait l'id depuis un enregistrement client API (id ou Id). */
export function resolveClientRecordId(source: Record<string, unknown> | null | undefined): string | null {
  if (!source || typeof source !== 'object') return null;
  const fromCamel = normalizeClientId(source['id']);
  if (fromCamel) return fromCamel;
  return normalizeClientId(source['Id']);
}

/**
 * Canonise ClientInfo : id fiable, isNewClient cohérent avec la présence d'un id.
 */
export function normalizeClientInfo(client: ClientInfo): ClientInfo {
  const id = resolveClientRecordId(client as unknown as Record<string, unknown>);
  const isNewClient = id != null ? false : client.isNewClient === true;
  return { ...client, id, isNewClient };
}

/** Client prêt pour l'émission : client existant (id) ou création explicite (isNewClient). */
export function isClientReadyForSubmission(client: ClientInfo | null): boolean {
  if (!client) return false;
  const n = normalizeClientInfo(client);
  const hasId = n.id != null && n.id !== '';
  return hasId || n.isNewClient === true;
}
