import { ReasonCodePipe } from './reason-code.pipe';

describe('ReasonCodePipe', () => {
  let pipe: ReasonCodePipe;

  beforeEach(() => { pipe = new ReasonCodePipe(); });

  it('returns empty string for null/undefined/empty input', () => {
    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
    expect(pipe.transform('')).toBe('');
  });

  it('translates fixed codes', () => {
    expect(pipe.transform('OutOfStock')).toBe('Rupture de stock');
    expect(pipe.transform('BelowSafetyStock')).toBe('Sous le stock de sécurité');
    expect(pipe.transform('AtOrBelowReorderPoint')).toBe('Au point de commande');
    expect(pipe.transform('BelowUserMinimumStock')).toBe('Sous le stock min. configuré');
    expect(pipe.transform('SeasonalUpcomingEvent')).toBe('Événement saisonnier à venir');
  });

  it('formats DailyDemand:X with French locale (comma)', () => {
    expect(pipe.transform('DailyDemand:0.56')).toBe('Demande quotidienne : 0,56 u/j');
    expect(pipe.transform('DailyDemand:12.5')).toBe('Demande quotidienne : 12,50 u/j');
  });

  it('falls back to ? when DailyDemand value is not a number', () => {
    expect(pipe.transform('DailyDemand:NaN')).toBe('Demande quotidienne : NaN u/j');
    expect(pipe.transform('DailyDemand:')).toBe('Demande quotidienne :  u/j');
  });

  it('returns unknown codes verbatim (forward-compat)', () => {
    expect(pipe.transform('SomethingNew')).toBe('SomethingNew');
    expect(pipe.transform('NewCode:42')).toBe('NewCode:42');
  });

  it('splits only on first colon', () => {
    expect(pipe.transform('UnknownKey:a:b:c')).toBe('UnknownKey:a:b:c');
  });
});
