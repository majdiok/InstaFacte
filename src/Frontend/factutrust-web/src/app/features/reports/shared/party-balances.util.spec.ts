import {
  buildPartyBalanceSummaryMetrics,
  filterPartyBalanceRows,
  mapClientBalanceRows,
  mapSupplierBalanceRows
} from './party-balances.util';
import { PartyBalanceRow } from './party-balances-table.component';

describe('party-balances.util', () => {
  const rows: PartyBalanceRow[] = [
    {
      partyId: '1',
      partyName: 'Alpha SARL',
      totalInvoiced: 1000,
      totalPaid: 400,
      balance: 600,
      currency: 'TND'
    },
    {
      partyId: '2',
      partyName: 'Beta SA',
      totalInvoiced: 500,
      totalPaid: 500,
      balance: 0,
      currency: 'TND'
    },
    {
      partyId: '3',
      partyName: 'Gamma',
      totalInvoiced: 200,
      totalPaid: 50,
      balance: 150,
      currency: 'TND'
    }
  ];

  it('maps client balance rows', () => {
    const mapped = mapClientBalanceRows([
      {
        clientId: 'c1',
        clientName: 'Client A',
        totalInvoiced: 10,
        totalPaid: 4,
        balance: 6,
        currency: 'TND'
      }
    ]);
    expect(mapped).toEqual([
      {
        partyId: 'c1',
        partyName: 'Client A',
        totalInvoiced: 10,
        totalPaid: 4,
        balance: 6,
        currency: 'TND'
      }
    ]);
  });

  it('maps supplier balance rows', () => {
    const mapped = mapSupplierBalanceRows([
      {
        supplierId: 's1',
        supplierName: 'Fournisseur A',
        totalInvoiced: 20,
        totalPaid: 5,
        balance: 15,
        currency: 'TND'
      }
    ]);
    expect(mapped[0].partyId).toBe('s1');
    expect(mapped[0].partyName).toBe('Fournisseur A');
  });

  it('hides zero balances when requested', () => {
    const filtered = filterPartyBalanceRows(rows, '', true);
    expect(filtered.map((r) => r.partyId)).toEqual(['1', '3']);
  });

  it('filters by search (case-insensitive)', () => {
    const filtered = filterPartyBalanceRows(rows, 'beta', false);
    expect(filtered.length).toBe(1);
    expect(filtered[0].partyName).toBe('Beta SA');
  });

  it('combines search and hide-zero filters', () => {
    const filtered = filterPartyBalanceRows(rows, 'a', true);
    expect(filtered.map((r) => r.partyName)).toEqual(['Alpha SARL', 'Gamma']);
  });

  it('builds summary KPIs from filtered rows', () => {
    const metrics = buildPartyBalanceSummaryMetrics(rows.filter((r) => r.balance !== 0));
    expect(metrics.find((m) => m.label === 'Tiers')?.value).toBe(2);
    expect(metrics.find((m) => m.label === 'Total facturé')?.value).toBe(1200);
    expect(metrics.find((m) => m.label === 'Total payé')?.value).toBe(450);
    expect(metrics.find((m) => m.label === 'Solde total')?.value).toBe(750);
    expect(metrics.find((m) => m.label === 'Soldes > 0')?.value).toBe(2);
  });
});
