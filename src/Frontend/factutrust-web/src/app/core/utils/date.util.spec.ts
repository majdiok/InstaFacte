import { formatLocalDate } from './date.util';

describe('formatLocalDate', () => {
  it('formate une date locale en YYYY-MM-DD sans décalage UTC', () => {
    // 18/06/2026 (mois 0-indexé : 5 = juin) — le bug toISOString reculait au 17/06 en UTC+1.
    expect(formatLocalDate(new Date(2026, 5, 18))).toBe('2026-06-18');
  });

  it('applique le zero-padding au mois et au jour', () => {
    expect(formatLocalDate(new Date(2026, 0, 5))).toBe('2026-01-05');
  });

  it('gère le dernier jour de l’année', () => {
    expect(formatLocalDate(new Date(2026, 11, 31))).toBe('2026-12-31');
  });

  it('utilise les champs calendaires locaux même avec une heure proche de minuit', () => {
    // Une heure tardive ne doit pas faire basculer le jour vers la veille (contrairement à toISOString).
    expect(formatLocalDate(new Date(2026, 5, 18, 23, 59, 59))).toBe('2026-06-18');
  });
});
