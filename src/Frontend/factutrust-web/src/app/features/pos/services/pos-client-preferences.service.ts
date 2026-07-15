import { Injectable } from '@angular/core';

const STORAGE_KEY = 'factutrust_pos_client_preferences';

export interface PosClientPreference {
  preferReceiptByEmail: boolean;
}

interface ClientPreferencesRecord {
  [clientId: string]: PosClientPreference;
}

@Injectable({
  providedIn: 'root'
})
export class PosClientPreferencesService {
  private getStorage(): ClientPreferencesRecord {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as ClientPreferencesRecord) : {};
    } catch {
      return {};
    }
  }

  private setStorage(data: ClientPreferencesRecord): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
    } catch {
      // ignore
    }
  }

  getPreference(clientId: string | null): PosClientPreference | null {
    if (!clientId) return null;
    return this.getStorage()[clientId] ?? null;
  }

  getPreferReceiptByEmail(clientId: string | null): boolean {
    return this.getPreference(clientId)?.preferReceiptByEmail ?? false;
  }

  setPreferReceiptByEmail(clientId: string, value: boolean): void {
    const store = this.getStorage();
    store[clientId] = { ...store[clientId], preferReceiptByEmail: value };
    this.setStorage(store);
  }

  setPreference(clientId: string, pref: Partial<PosClientPreference>): void {
    const store = this.getStorage();
    store[clientId] = { ...store[clientId], ...pref };
    this.setStorage(store);
  }
}
