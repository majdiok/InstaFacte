import {
  InvalidFiscalYearStartMonth,
  buildFiscalYearOptions,
  fiscalYearEndDateTime,
  fiscalYearKey,
  fiscalYearLabel,
  fiscalYearStartDateTime
} from './fiscal-year.util';

// Miroir frontend du canonique backend `FiscalYearMath` (plan « Exercices décalés », décision D2).
// Les cas ci-dessous reproduisent `FiscalYearResolverTests` côté backend afin de garantir la
// parité d'affichage client/serveur.
describe('fiscal-year.util (FiscalYearMath mirror)', () => {
  describe('fiscalYearKey', () => {
    it('returns the calendar year for a civil exercise (startMonth = 1)', () => {
      expect(fiscalYearKey(new Date(2026, 0, 1), 1)).toBe(2026);
      expect(fiscalYearKey(new Date(2026, 5, 30), 1)).toBe(2026);
      expect(fiscalYearKey(new Date(2026, 11, 31), 1)).toBe(2026);
    });

    it('assigns a date on/after the start month to the current year (july start)', () => {
      expect(fiscalYearKey(new Date(2026, 6, 1), 7)).toBe(2026); // 01/07/2026
      expect(fiscalYearKey(new Date(2026, 11, 31), 7)).toBe(2026); // 31/12/2026
      expect(fiscalYearKey(new Date(2027, 5, 30), 7)).toBe(2026); // 30/06/2027
    });

    it('assigns a date before the start month to the previous year (july start)', () => {
      expect(fiscalYearKey(new Date(2026, 5, 30), 7)).toBe(2025); // 30/06/2026 -> FY 2025
      expect(fiscalYearKey(new Date(2026, 0, 1), 7)).toBe(2025); // 01/01/2026 -> FY 2025
    });

    it('handles the exact boundary day (first day of the start month)', () => {
      expect(fiscalYearKey(new Date(2026, 6, 1), 7)).toBe(2026);
      expect(fiscalYearKey(new Date(2026, 5, 30), 7)).toBe(2025);
    });

    it('rejects an out-of-range start month', () => {
      expect(() => fiscalYearKey(new Date(2026, 0, 1), 0)).toThrowMatching(
        e => e instanceof InvalidFiscalYearStartMonth
      );
      expect(() => fiscalYearKey(new Date(2026, 0, 1), 13)).toThrowMatching(
        e => e instanceof InvalidFiscalYearStartMonth
      );
    });
  });

  describe('fiscalYearLabel', () => {
    it('returns the bare year for a civil exercise regardless of format', () => {
      expect(fiscalYearLabel(2026, 1, 'N/N+1')).toBe('2026');
      expect(fiscalYearLabel(2026, 1, 'N')).toBe('2026');
    });

    it('returns "N/N+1" for an offset exercise with the default format', () => {
      expect(fiscalYearLabel(2026, 7, 'N/N+1')).toBe('2026/2027');
    });

    it('returns the bare start year when format is "N" for an offset exercise', () => {
      expect(fiscalYearLabel(2026, 7, 'N')).toBe('2026');
    });

    it('treats any unknown format as "N/N+1" for an offset exercise', () => {
      expect(fiscalYearLabel(2026, 4, '')).toBe('2026/2027');
      expect(fiscalYearLabel(2026, 4, 'weird')).toBe('2026/2027');
    });
  });

  describe('fiscalYearStartDateTime / fiscalYearEndDateTime', () => {
    it('returns 01/01..31/12 for a civil exercise', () => {
      expect(fiscalYearStartDateTime(2026, 1)).toEqual(new Date(2026, 0, 1));
      expect(fiscalYearEndDateTime(2026, 1)).toEqual(new Date(2026, 11, 31));
    });

    it('returns 01/07..30/06 for a july offset exercise', () => {
      expect(fiscalYearStartDateTime(2026, 7)).toEqual(new Date(2026, 6, 1));
      expect(fiscalYearEndDateTime(2026, 7)).toEqual(new Date(2027, 5, 30));
    });

    it('handles leap years for a march offset exercise (ends 28/02 of the next year)', () => {
      // FY 2023 starting in March ends 29/02/2024 (leap year).
      expect(fiscalYearEndDateTime(2023, 3)).toEqual(new Date(2024, 1, 29));
      // FY 2022 starting in March ends 28/02/2023 (non-leap).
      expect(fiscalYearEndDateTime(2022, 3)).toEqual(new Date(2023, 1, 28));
    });
  });

  describe('buildFiscalYearOptions', () => {
    it('builds a centered window of exercise options with N/N+1 labels for an offset exercise', () => {
      const opts = buildFiscalYearOptions(2026, 7, 'N/N+1', 2, 1);
      expect(opts.map(o => o.key)).toEqual([2024, 2025, 2026, 2027]);
      expect(opts.map(o => o.label)).toEqual(['2024/2025', '2025/2026', '2026/2027', '2027/2028']);
    });

    it('uses bare-year labels for a civil exercise', () => {
      const opts = buildFiscalYearOptions(2026, 1, 'N/N+1', 1, 0);
      expect(opts.map(o => o.key)).toEqual([2025, 2026]);
      expect(opts.map(o => o.label)).toEqual(['2025', '2026']);
    });

    it('defaults to a 4-before / 1-after window', () => {
      const opts = buildFiscalYearOptions(2026, 1, 'N');
      expect(opts.length).toBe(6);
      expect(opts[0].key).toBe(2022);
      expect(opts[opts.length - 1].key).toBe(2027);
    });
  });
});
