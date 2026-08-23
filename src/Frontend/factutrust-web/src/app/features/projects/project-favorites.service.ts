import { Injectable, signal } from '@angular/core';

const STORAGE_KEY = 'proj:favorites';

@Injectable({ providedIn: 'root' })
export class ProjectFavoritesService {
  private readonly ids = signal<string[]>(this.load());

  readonly favoriteIds = this.ids.asReadonly();

  isFavorite(id: string): boolean {
    return this.ids().includes(id);
  }

  toggle(id: string): boolean {
    const set = new Set(this.ids());
    const added = !set.has(id);
    if (added) set.add(id);
    else set.delete(id);
    const arr = [...set];
    this.ids.set(arr);
    this.persist(arr);
    return added;
  }

  private persist(arr: string[]): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(arr));
    } catch {
      /* ignore quota / privacy mode */
    }
  }

  private load(): string[] {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return [];
      const parsed = JSON.parse(raw);
      return Array.isArray(parsed) ? parsed.filter((x): x is string => typeof x === 'string') : [];
    } catch {
      return [];
    }
  }
}
