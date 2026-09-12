import { AccountingAmountPipe, decimalsForCurrency, formatAccountingAmount } from './accounting-amount.pipe';

describe('formatAccountingAmount', () => {
  it('formate le dinar au millime', () => {
    expect(formatAccountingAmount(3314.2)).toContain('3');
    expect(formatAccountingAmount(3314.2)).toContain('200');
    expect(formatAccountingAmount(3314.2)).toContain('TND');
  });

  it('formate une devise étrangère à deux décimales', () => {
    const result = formatAccountingAmount(1000, 'EUR');

    // Contrôle sur la partie décimale seule : « 1 000,00 » contient « 000 » par son millier.
    expect(result).toContain('EUR');
    expect(result).toMatch(/,\d{2} EUR$/);
    expect(result).not.toMatch(/,\d{3}/);
  });

  it('formate le dinar à trois décimales, contrairement à l’euro', () => {
    expect(formatAccountingAmount(1000, 'TND')).toMatch(/,\d{3} TND$/);
  });

  it('peut omettre le code devise', () => {
    expect(formatAccountingAmount(1000, 'EUR', false)).not.toContain('EUR');
  });

  it('accepte un nombre de décimales imposé', () => {
    expect(formatAccountingAmount(3.3142, 'EUR', false, 5)).toContain('31420');
  });

  it('rend un tiret plutôt qu’un zéro pour une valeur absente', () => {
    // « 0,000 » se confondrait avec un solde nul, qui est une information différente.
    expect(formatAccountingAmount(null)).toBe('—');
    expect(formatAccountingAmount(undefined)).toBe('—');
    expect(formatAccountingAmount(Number.NaN)).toBe('—');
  });

  it('formate bien un zéro réel', () => {
    expect(formatAccountingAmount(0)).not.toBe('—');
  });

  it('retombe sur la devise de tenue si la devise est vide', () => {
    expect(formatAccountingAmount(10, '')).toContain('TND');
    expect(formatAccountingAmount(10, null)).toContain('TND');
  });
});

describe('decimalsForCurrency', () => {
  it('donne 3 décimales au dinar', () => {
    expect(decimalsForCurrency('TND')).toBe(3);
    expect(decimalsForCurrency(null)).toBe(3);
    expect(decimalsForCurrency(undefined)).toBe(3);
  });

  it('donne 2 décimales aux autres devises', () => {
    expect(decimalsForCurrency('EUR')).toBe(2);
    expect(decimalsForCurrency('USD')).toBe(2);
  });
});

describe('AccountingAmountPipe', () => {
  const pipe = new AccountingAmountPipe();

  it('délègue au formateur', () => {
    expect(pipe.transform(1000, 'EUR')).toBe(formatAccountingAmount(1000, 'EUR'));
    expect(pipe.transform(null)).toBe('—');
  });
});
