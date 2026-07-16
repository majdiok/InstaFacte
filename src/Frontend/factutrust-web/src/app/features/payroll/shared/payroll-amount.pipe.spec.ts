import { PayrollAmountPipe, formatPayrollAmount } from './payroll-amount.pipe';

describe('PayrollAmountPipe', () => {
  const pipe = new PayrollAmountPipe();

  it('formate un montant avec 3 décimales et le suffixe TND', () => {
    const result = pipe.transform(1234.5);
    expect(result).toContain('234,500');
    expect(result.endsWith(' TND')).toBeTrue();
  });

  it('formate zéro', () => {
    expect(pipe.transform(0)).toBe('0,000 TND');
  });

  it('omet le suffixe TND quand showCurrency est false', () => {
    const result = pipe.transform(1547, false);
    expect(result).toContain('547,000');
    expect(result).not.toContain('TND');
  });

  it('affiche un tiret pour null, undefined et NaN', () => {
    expect(pipe.transform(null)).toBe('—');
    expect(pipe.transform(undefined)).toBe('—');
    expect(pipe.transform(Number.NaN)).toBe('—');
  });

  it('ne jette pas même sans locale fr-TN enregistrée côté Angular', () => {
    expect(() => pipe.transform(159.871)).not.toThrow();
  });

  it('expose formatPayrollAmount pour un usage hors template', () => {
    expect(formatPayrollAmount(142.015)).toContain('142,015');
    expect(formatPayrollAmount(null)).toBe('—');
  });
});
