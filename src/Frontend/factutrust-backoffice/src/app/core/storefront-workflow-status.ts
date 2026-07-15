/**
 * Copie fonctionnelle de `factutrust-web/.../storefront-workflow-status.ts` : les deux apps Angular
 * ne partagent pas de package commun ; toute évolution du contrat API doit être reflétée ici aussi.
 * @see `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)` sur l’API InstaFact ; PascalCase toléré pour anciens déploiements.
 */
export type StorefrontWorkflowStatus = 'draft' | 'pendingReview' | 'published' | 'suspended';

const STRING_TO_STATUS: Record<string, StorefrontWorkflowStatus> = {
  draft: 'draft',
  pendingReview: 'pendingReview',
  published: 'published',
  suspended: 'suspended'
};

const INT_TO_STATUS: Record<number, StorefrontWorkflowStatus> = {
  0: 'draft',
  1: 'pendingReview',
  2: 'published',
  3: 'suspended'
};

export type ParseStorefrontWorkflowStatusResult =
  | { ok: true; value: StorefrontWorkflowStatus }
  | { ok: false };

export function parseStorefrontWorkflowStatus(raw: unknown): ParseStorefrontWorkflowStatusResult {
  if (typeof raw === 'string') {
    const trimmed = raw.trim();
    if (!trimmed) {
      return { ok: false };
    }
    const direct = STRING_TO_STATUS[trimmed];
    if (direct) {
      return { ok: true, value: direct };
    }
    const fromPascal = trimmed.charAt(0).toLowerCase() + trimmed.slice(1);
    const v = STRING_TO_STATUS[fromPascal];
    return v ? { ok: true, value: v } : { ok: false };
  }
  if (typeof raw === 'number' && Number.isInteger(raw) && raw >= 0 && raw <= 3) {
    const v = INT_TO_STATUS[raw];
    return v ? { ok: true, value: v } : { ok: false };
  }
  return { ok: false };
}

export function workflowStatusLabel(value: StorefrontWorkflowStatus): string {
  switch (value) {
    case 'draft':
      return 'Brouillon';
    case 'pendingReview':
      return 'En attente de validation';
    case 'published':
      return 'Publiée';
    case 'suspended':
      return 'Suspendue';
    default: {
      const _exhaustive: never = value;
      return _exhaustive;
    }
  }
}

export function workflowStatusTagLabel(value: StorefrontWorkflowStatus): string {
  return value === 'pendingReview' ? 'En attente' : workflowStatusLabel(value);
}
