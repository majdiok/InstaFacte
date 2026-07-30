/** Feature flag local pour l'UX feuille de temps fidélité maximale. Off par défaut. */
const STORAGE_KEY = 'timesheetRichUi';

export function isTimesheetRichUiEnabled(): boolean {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw === null) return false;
    return raw === '1' || raw.toLowerCase() === 'true';
  } catch {
    return false;
  }
}

export function setTimesheetRichUiEnabled(enabled: boolean): void {
  try {
    localStorage.setItem(STORAGE_KEY, enabled ? '1' : '0');
  } catch {
    /* ignore quota / private mode */
  }
}
