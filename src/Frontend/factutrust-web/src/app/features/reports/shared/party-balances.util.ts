import { PartyBalanceRow } from './party-balances-table.component';
import { ClientBalanceReportRow, SupplierBalanceReportRow } from '@core/services/reports-api.service';
import { TotalMetric } from '@shared/components/table-totals-bar/table-totals-bar.component';

export function mapClientBalanceRows(rows: ClientBalanceReportRow[]): PartyBalanceRow[] {
  return rows.map((r) => ({
    partyId: r.clientId,
    partyName: r.clientName,
    totalInvoiced: r.totalInvoiced,
    totalPaid: r.totalPaid,
    balance: r.balance,
    currency: r.currency
  }));
}

export function mapSupplierBalanceRows(rows: SupplierBalanceReportRow[]): PartyBalanceRow[] {
  return rows.map((r) => ({
    partyId: r.supplierId,
    partyName: r.supplierName,
    totalInvoiced: r.totalInvoiced,
    totalPaid: r.totalPaid,
    balance: r.balance,
    currency: r.currency
  }));
}

export function filterPartyBalanceRows(
  rows: PartyBalanceRow[],
  search: string,
  hideZeroBalances: boolean
): PartyBalanceRow[] {
  const q = search.trim().toLowerCase();
  return rows.filter((r) => {
    if (hideZeroBalances && r.balance === 0) {
      return false;
    }
    if (q && !r.partyName.toLowerCase().includes(q)) {
      return false;
    }
    return true;
  });
}

export function buildPartyBalanceSummaryMetrics(rows: PartyBalanceRow[]): TotalMetric[] {
  const currency = rows[0]?.currency ?? 'TND';
  const totalInvoiced = rows.reduce((sum, r) => sum + r.totalInvoiced, 0);
  const totalPaid = rows.reduce((sum, r) => sum + r.totalPaid, 0);
  const totalBalance = rows.reduce((sum, r) => sum + r.balance, 0);
  const positiveCount = rows.filter((r) => r.balance > 0).length;

  return [
    { label: 'Tiers', value: rows.length, format: 'number', icon: 'pi-users', tone: 'primary' },
    { label: 'Total facturé', value: totalInvoiced, format: 'currency', currency, icon: 'pi-file', tone: 'cyan' },
    { label: 'Total payé', value: totalPaid, format: 'currency', currency, icon: 'pi-check', tone: 'emerald' },
    { label: 'Solde total', value: totalBalance, format: 'currency', currency, icon: 'pi-wallet', tone: 'amber' },
    { label: 'Soldes > 0', value: positiveCount, format: 'number', icon: 'pi-exclamation-circle', tone: 'rose' }
  ];
}

export function exportPartyBalancesCsv(
  rows: PartyBalanceRow[],
  partyLabel: string,
  filenamePrefix: string
): void {
  const escape = (v: string | number) => `"${String(v).replace(/"/g, '""')}"`;
  const header = [partyLabel, 'Total facturé', 'Total payé', 'Solde', 'Devise'];
  const lines = [
    header.map(escape).join(';'),
    ...rows.map((r) =>
      [r.partyName, r.totalInvoiced.toFixed(3), r.totalPaid.toFixed(3), r.balance.toFixed(3), r.currency]
        .map(escape)
        .join(';')
    )
  ];
  const blob = new Blob(['\ufeff' + lines.join('\n')], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  const date = new Date();
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  link.download = `${filenamePrefix}-${y}-${m}-${d}.csv`;
  link.click();
  URL.revokeObjectURL(url);
}
