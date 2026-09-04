import { Injectable } from '@angular/core';

/**
 * Brouillon d'inscription (lot 4) — permet de reprendre le wizard après un rechargement de page.
 *
 * Sur un parcours en 4 étapes, un F5 à l'étape 4 faisait jusqu'ici tout ressaisir : c'est le
 * premier facteur d'abandon du tunnel.
 *
 * Trois décisions de sécurité, volontairement structurelles plutôt que déclaratives :
 *
 *  1. **Liste blanche explicite** (`PERSISTED_FIELDS`). Le brouillon n'est jamais construit en
 *     retirant des champs d'un objet complet — il est construit en n'y mettant QUE les champs
 *     listés. Un nouveau champ de formulaire n'est donc jamais persisté par accident : il faut
 *     l'ajouter ici sciemment. `password` / `confirmPassword` sont hors liste par construction,
 *     et `registration-draft.service.spec.ts` échoue si un secret apparaît dans la charge.
 *
 *  2. **`sessionStorage`, pas `localStorage`.** Le brouillon meurt avec l'onglet. Sur un poste
 *     partagé, les coordonnées d'une inscription abandonnée ne survivent pas à la fermeture du
 *     navigateur.
 *
 *  3. **`acceptTerms` n'est jamais restauré.** L'acceptation des CGU doit être un geste explicite
 *     à chaque tentative — la restaurer reviendrait à consentir à la place de l'utilisateur.
 */
export const PERSISTED_FIELDS = [
  // Étape 1
  'companySegment',
  'businessDomain',
  // Étape 2 (identité et société — jamais les mots de passe)
  'firstName',
  'lastName',
  'email',
  'companyName',
  'nif',
  'taxRegime',
  'companyEmail',
  'phone',
  'website',
  'warehouseName',
  // Étape 3
  'enabledModules',
  'hasPhysicalStock',
  'sellsToConsumers',
  'headcountBand',
  'accountingDelegatedToFirm',
  // Étape 4 (hors acceptTerms)
  'street',
  'streetLine2',
  'city',
  'postalCode',
  'governorate'
] as const;

export type PersistedField = (typeof PERSISTED_FIELDS)[number];

/** Champs dont la présence dans un brouillon est un défaut de sécurité, pas une régression mineure. */
export const FORBIDDEN_DRAFT_FIELDS: readonly string[] = ['password', 'confirmPassword', 'acceptTerms'];

interface DraftEnvelope {
  version: number;
  savedAt: number;
  step: number;
  values: Record<string, unknown>;
}

export interface RegistrationDraft {
  step: number;
  values: Record<string, unknown>;
}

@Injectable({ providedIn: 'root' })
export class RegistrationDraftService {
  private static readonly STORAGE_KEY = 'ft_register_draft';

  /** Incrémenter invalide tous les brouillons existants (changement de forme du formulaire). */
  private static readonly VERSION = 1;

  /** Au-delà, le brouillon est considéré périmé et purgé au chargement. */
  private static readonly MAX_AGE_MS = 24 * 60 * 60 * 1000;

  /**
   * N'enregistre QUE les champs de `PERSISTED_FIELDS` présents dans `formValue`. Les valeurs
   * vides/nulles sont omises pour ne pas écraser un formulaire vierge à la restauration.
   * Tout accès au stockage est protégé : navigation privée ou stockage désactivé ne doit jamais
   * casser l'inscription.
   */
  save(formValue: Record<string, unknown>, step: number): void {
    const values: Record<string, unknown> = {};
    for (const field of PERSISTED_FIELDS) {
      const value = formValue[field];
      if (value === null || value === undefined || value === '') {
        continue;
      }
      values[field] = value;
    }

    if (Object.keys(values).length === 0) {
      return;
    }

    const envelope: DraftEnvelope = {
      version: RegistrationDraftService.VERSION,
      savedAt: Date.now(),
      step,
      values
    };

    try {
      sessionStorage.setItem(RegistrationDraftService.STORAGE_KEY, JSON.stringify(envelope));
    } catch {
      // Stockage plein, désactivé ou navigation privée : le brouillon est un confort, jamais un dû.
    }
  }

  /** Brouillon exploitable, ou `null` (absent, illisible, version obsolète, expiré). */
  load(): RegistrationDraft | null {
    let raw: string | null = null;
    try {
      raw = sessionStorage.getItem(RegistrationDraftService.STORAGE_KEY);
    } catch {
      return null;
    }
    if (!raw) {
      return null;
    }

    let envelope: DraftEnvelope;
    try {
      envelope = JSON.parse(raw) as DraftEnvelope;
    } catch {
      this.clear();
      return null;
    }

    if (
      !envelope ||
      envelope.version !== RegistrationDraftService.VERSION ||
      typeof envelope.savedAt !== 'number' ||
      Date.now() - envelope.savedAt > RegistrationDraftService.MAX_AGE_MS ||
      !envelope.values ||
      typeof envelope.values !== 'object'
    ) {
      this.clear();
      return null;
    }

    // Filtre de défense en profondeur : même si un brouillon forgé arrivait dans le stockage,
    // seuls les champs de la liste blanche sont rendus au formulaire.
    const allowed = new Set<string>(PERSISTED_FIELDS);
    const values: Record<string, unknown> = {};
    for (const [key, value] of Object.entries(envelope.values)) {
      if (allowed.has(key)) {
        values[key] = value;
      }
    }

    if (Object.keys(values).length === 0) {
      return null;
    }

    const step = Number.isInteger(envelope.step) ? Math.min(Math.max(envelope.step, 0), 3) : 0;
    return { step, values };
  }

  clear(): void {
    try {
      sessionStorage.removeItem(RegistrationDraftService.STORAGE_KEY);
    } catch {
      // idem save() : jamais bloquant.
    }
  }
}
