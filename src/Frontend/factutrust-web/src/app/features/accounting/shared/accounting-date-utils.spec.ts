import {
  formatLocalDate,
  parseLocalDateString,
  validateDateRange
} from './accounting-date-utils';

describe('accounting-date-utils', () => {
  it('formatLocalDate uses calendar fields without UTC shift', () => {
    const d = new Date(2026, 3, 7);
    expect(formatLocalDate(d)).toBe('2026-04-07');
  });

  it('parseLocalDateString parses YYYY-MM-DD as local midnight', () => {
    const d = parseLocalDateString('2026-01-15');
    expect(d.getFullYear()).toBe(2026);
    expect(d.getMonth()).toBe(0);
    expect(d.getDate()).toBe(15);
  });

  it('validateDateRange rejects inverted range', () => {
    const from = new Date(2026, 0, 10);
    const to = new Date(2026, 0, 1);
    expect(validateDateRange(from, to).valid).toBe(false);
  });

  it('validateDateRange accepts same day', () => {
    const d = new Date(2026, 0, 5);
    expect(validateDateRange(d, d).valid).toBe(true);
  });
});
