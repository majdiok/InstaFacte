import { Injectable, signal } from '@angular/core';
import { createClientUuid } from '@core/utils/safe-random-uuid.util';

export interface AiPromptFavorite {
  id: string;
  text: string;
  createdAtUtc: string;
}

const STORAGE_KEY = 'ft_ai_prompt_favorites_v1';
const MAX_FAVORITES = 30;

@Injectable({ providedIn: 'root' })
export class AiPromptFavoritesService {
  /**
   * Espace de favoris actif : '' = assistant global (clé historique inchangée),
   * sinon suffixe ':<slug>' par expert de module (ex. ft_ai_prompt_favorites_v1:ventes).
   */
  private scopeSuffix = '';

  readonly favorites = signal<AiPromptFavorite[]>(this.readFromStorage());

  /** Bascule l'espace de favoris (null = global). Recharge la liste depuis le stockage. */
  setScope(slug: string | null): void {
    const suffix = slug ? `:${slug}` : '';
    if (suffix === this.scopeSuffix) {
      return;
    }
    this.scopeSuffix = suffix;
    this.favorites.set(this.readFromStorage());
  }

  add(text: string): void {
    const t = text.trim();
    if (t.length === 0 || t.length > 500) {
      return;
    }
    const list = this.favorites();
    if (list.some(f => f.text === t)) {
      return;
    }
    const next: AiPromptFavorite[] = [
      {
        id: createClientUuid(),
        text: t,
        createdAtUtc: new Date().toISOString()
      },
      ...list
    ].slice(0, MAX_FAVORITES);
    this.favorites.set(next);
    this.persist(next);
  }

  remove(id: string): void {
    const next = this.favorites().filter(f => f.id !== id);
    this.favorites.set(next);
    this.persist(next);
  }

  private get storageKey(): string {
    return STORAGE_KEY + this.scopeSuffix;
  }

  private readFromStorage(): AiPromptFavorite[] {
    try {
      const raw = localStorage.getItem(this.storageKey);
      if (!raw) {
        return [];
      }
      const parsed = JSON.parse(raw) as unknown;
      if (!Array.isArray(parsed)) {
        return [];
      }
      return parsed.filter(
        (x): x is AiPromptFavorite =>
          !!x &&
          typeof x === 'object' &&
          typeof (x as AiPromptFavorite).id === 'string' &&
          typeof (x as AiPromptFavorite).text === 'string'
      );
    } catch {
      return [];
    }
  }

  private persist(list: AiPromptFavorite[]): void {
    try {
      localStorage.setItem(this.storageKey, JSON.stringify(list));
    } catch {
      /* ignore quota */
    }
  }
}
