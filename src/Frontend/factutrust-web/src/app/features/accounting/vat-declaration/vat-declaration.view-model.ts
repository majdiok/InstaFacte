import { VatDeclarationDto } from '../services/accounting.service';
import { Company } from '@core/services/company.service';
import { User } from '@core/services/auth.service';

export type VatExtrasKey = 'fodec' | 'droitTimbre' | 'tcl' | 'tfp' | 'foprolos' | 'withholdingTax' | 'acomptes';

export interface CompanyDisplayInfo {
  companyName: string;
  nif: string | null;
  taxRegimeDisplay: string | null;
  tradeName: string | null;
}

export interface VatDeclarationExtras {
  fodec: number;
  droitTimbre: number;
  tcl: number;
  tfp: number;
  foprolos: number;
  withholdingTax: number;
  acomptes: number;
}

export interface VatTaxTableRow {
  taxLabel: string;
  taxableBase: number | null;
  ratePercent: number | null;
  amountToPay: number | null;
  deductibleAmount: number | null;
  netAmount: number | null;
  highlight?: boolean;
  bold?: boolean;
  editableKey?: VatExtrasKey;
  /** Valeur calculée Base × Taux, proposée pour les lignes éditables assises sur un taux (FODEC/TCL). */
  suggestedAmount?: number | null;
}

export interface VatDocLinkRow {
  label: string;
  route: string;
  queryParams?: Record<string, string>;
  status: 'ok' | 'neutral' | 'warning';
  statusLabel: string;
}

export interface VatChartSegment {
  label: string;
  value: number;
}

const MONTH_NAMES_FR = [
  '', 'Janvier', 'Février', 'Mars', 'Avril', 'Mai', 'Juin',
  'Juillet', 'Août', 'Septembre', 'Octobre', 'Novembre', 'Décembre'
];

export function formatPeriodLabel(year: number, month: number): string {
  const name = month >= 1 && month <= 12 ? MONTH_NAMES_FR[month] : String(month);
  return `${name} ${year}`;
}

export function formatDateFr(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleDateString('fr-FR', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

export function formatDateTimeFr(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString('fr-FR', {
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit'
  });
}

export function periodDateRange(year: number, month: number): { from: string; to: string } {
  const start = new Date(year, month - 1, 1);
  const end = new Date(year, month, 0);
  const fmt = (d: Date) =>
    `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  return { from: fmt(start), to: fmt(end) };
}

export function computeTotalToPay(d: VatDeclarationDto, extras: VatDeclarationExtras): number {
  const total = d.vatDue + extras.fodec + extras.droitTimbre + extras.tcl + extras.tfp
    + extras.foprolos + extras.withholdingTax - extras.acomptes;
  return Math.max(0, total);
}

export function computeTotalCollected(d: VatDeclarationDto): number {
  return d.collectedVat19 + d.collectedVat13 + d.collectedVat7;
}

export function computeTotalDeductible(d: VatDeclarationDto): number {
  return d.deductibleVatGoods + d.deductibleVatAssets;
}

export function statusLabel(status: number): string {
  switch (status) {
    case 1: return 'Soumise';
    case 2: return 'Verrouillée';
    default: return 'Brouillon';
  }
}

/**
 * Une déclaration n'est soumettable que si elle est au statut Brouillon (0). Une déclaration
 * déjà soumise (1) ou verrouillée (2) se corrige via « Rectificative », pas par re-soumission.
 */
export function canSubmitDeclaration(status: number): boolean {
  return status === 0;
}

/** Statuts visibles pour la société (Soumise / Verrouillée). */
export function isSubmittedOrLocked(status: number): boolean {
  return status === 1 || status === 2;
}

export function statusClass(status: number): string {
  switch (status) {
    case 1: return 'vat-status--submitted';
    case 2: return 'vat-status--locked';
    default: return 'vat-status--draft';
  }
}

export function clampExtrasValue(value: unknown): number {
  const n = Number(value);
  return Number.isFinite(n) && n > 0 ? n : 0;
}

/** Arrondi comptable à 3 décimales (millimes), aligné sur l'arrondi backend des taxes. */
export function round3(value: number): number {
  return Math.round((value + Number.EPSILON) * 1000) / 1000;
}

/** Valeur suggérée = base × taux%/100, arrondie au millime ; null si base ou taux absent. */
export function suggestedTaxAmount(base: number | null | undefined, ratePercent: number | null | undefined): number | null {
  if (base == null || ratePercent == null) return null;
  return round3(base * (ratePercent / 100));
}

/** Écart significatif (> 0,001) entre le montant saisi et la valeur suggérée Base × Taux. */
export function hasSuggestionMismatch(amount: number | null | undefined, suggested: number | null | undefined): boolean {
  if (amount == null || suggested == null) return false;
  return Math.abs(amount - suggested) > 0.001;
}

export function shiftPeriod(year: number, month: number, delta: number): { year: number; month: number } {
  const d = new Date(year, month - 1 + delta, 1);
  return { year: d.getFullYear(), month: d.getMonth() + 1 };
}

export function buildTaxRows(
  d: VatDeclarationDto,
  extras: VatDeclarationExtras,
  totalToPay: number,
  fodecRatePercent = 1,
  tclRatePercent = 0.2
): VatTaxTableRow[] {
  const rows: VatTaxTableRow[] = [];

  for (const rate of [19, 13, 7]) {
    const breakdown = d.collectedVatBreakdown?.find(r => r.ratePercent === rate);
    const vat = rate === 19 ? d.collectedVat19 : rate === 13 ? d.collectedVat13 : d.collectedVat7;
    rows.push({
      taxLabel: `TVA collectée ${rate} %`,
      taxableBase: breakdown?.taxableBase ?? null,
      ratePercent: rate,
      amountToPay: vat,
      deductibleAmount: null,
      netAmount: vat
    });
  }

  rows.push({
    taxLabel: 'TVA déductible — biens et services',
    taxableBase: d.deductiblePurchasesTaxableBase ?? null,
    ratePercent: null,
    amountToPay: null,
    deductibleAmount: d.deductibleVatGoods,
    netAmount: null
  });

  rows.push({
    taxLabel: 'TVA déductible — immobilisations',
    taxableBase: null,
    ratePercent: null,
    amountToPay: null,
    deductibleAmount: d.deductibleVatAssets,
    netAmount: null
  });

  rows.push({
    taxLabel: 'Crédit antérieur',
    taxableBase: null,
    ratePercent: null,
    amountToPay: null,
    deductibleAmount: d.previousCredit,
    netAmount: null
  });

  rows.push({
    taxLabel: 'TVA due',
    taxableBase: null,
    ratePercent: null,
    amountToPay: d.vatDue,
    deductibleAmount: null,
    netAmount: d.vatDue,
    highlight: true,
    bold: true
  });

  // Quand la TVA déductible dépasse la TVA collectée, la TVA due est nulle et le solde est un
  // crédit reporté sur la période suivante : le rendre visible à l'écran (jusqu'ici seul le PDF le montrait).
  if (d.creditToCarry > 0) {
    rows.push({
      taxLabel: 'Crédit de TVA à reporter',
      taxableBase: null,
      ratePercent: null,
      amountToPay: null,
      deductibleAmount: d.creditToCarry,
      netAmount: null
    });
  }

  if (d.monthlyDeclarationV2Enabled) {
    rows.push({
      taxLabel: 'FODEC',
      taxableBase: d.fodecTaxableBase ?? d.salesTaxableBase,
      ratePercent: fodecRatePercent,
      amountToPay: extras.fodec,
      deductibleAmount: null,
      netAmount: extras.fodec,
      editableKey: 'fodec',
      suggestedAmount: suggestedTaxAmount(d.fodecTaxableBase ?? d.salesTaxableBase, fodecRatePercent)
    });
    rows.push({
      taxLabel: 'Droit de timbre',
      taxableBase: null,
      ratePercent: null,
      amountToPay: extras.droitTimbre,
      deductibleAmount: null,
      netAmount: extras.droitTimbre,
      editableKey: 'droitTimbre'
    });
    rows.push({
      taxLabel: 'TCL',
      taxableBase: d.salesGrossBase,
      ratePercent: tclRatePercent,
      amountToPay: extras.tcl,
      deductibleAmount: null,
      netAmount: extras.tcl,
      editableKey: 'tcl',
      suggestedAmount: suggestedTaxAmount(d.salesGrossBase, tclRatePercent)
    });
    rows.push({
      taxLabel: 'TFP',
      taxableBase: null,
      ratePercent: null,
      amountToPay: extras.tfp,
      deductibleAmount: null,
      netAmount: extras.tfp,
      editableKey: 'tfp'
    });
    rows.push({
      taxLabel: 'FOPROLOS',
      taxableBase: null,
      ratePercent: null,
      amountToPay: extras.foprolos,
      deductibleAmount: null,
      netAmount: extras.foprolos,
      editableKey: 'foprolos'
    });
    rows.push({
      taxLabel: 'Retenues à la source (RS)',
      taxableBase: null,
      ratePercent: null,
      amountToPay: extras.withholdingTax,
      deductibleAmount: null,
      netAmount: extras.withholdingTax,
      highlight: true,
      editableKey: 'withholdingTax'
    });
    rows.push({
      taxLabel: 'Acomptes provisionnels (déduits)',
      taxableBase: null,
      ratePercent: null,
      amountToPay: null,
      deductibleAmount: extras.acomptes,
      netAmount: extras.acomptes > 0 ? -extras.acomptes : 0,
      editableKey: 'acomptes'
    });
  }

  rows.push({
    taxLabel: 'TOTAL À PAYER',
    taxableBase: null,
    ratePercent: null,
    amountToPay: null,
    deductibleAmount: null,
    netAmount: totalToPay,
    bold: true,
    highlight: true
  });

  return rows;
}

export function buildChartSegments(
  d: VatDeclarationDto,
  extras: VatDeclarationExtras,
  totalToPay: number
): VatChartSegment[] {
  if (!d.monthlyDeclarationV2Enabled) {
    return [{ label: 'TVA due', value: d.vatDue }];
  }
  const segments: VatChartSegment[] = [
    { label: 'TVA due', value: d.vatDue },
    { label: 'FODEC', value: extras.fodec },
    { label: 'Droit de timbre', value: extras.droitTimbre },
    { label: 'TCL', value: extras.tcl },
    { label: 'TFP', value: extras.tfp },
    { label: 'FOPROLOS', value: extras.foprolos },
    { label: 'RS', value: extras.withholdingTax }
  ];
  return segments.filter(s => s.value > 0).length > 0
    ? segments.filter(s => s.value > 0)
    : [{ label: 'Total', value: totalToPay }];
}

export function buildDocLinks(d: VatDeclarationDto, year: number, month: number): VatDocLinkRow[] {
  const range = periodDateRange(year, month);
  const totalCollected = computeTotalCollected(d);
  const hasPurchases = (d.deductiblePurchasesTaxableBase ?? 0) > 0 || d.deductibleVatGoods > 0;

  return [
    {
      label: 'Journal des ventes',
      route: '/accounting/journal',
      queryParams: { from: range.from, to: range.to },
      status: totalCollected > 0 ? 'ok' : 'neutral',
      statusLabel: totalCollected > 0 ? 'OK' : '—'
    },
    {
      label: 'Journal des achats',
      route: '/accounting/journal',
      queryParams: { from: range.from, to: range.to },
      status: hasPurchases ? 'ok' : 'neutral',
      statusLabel: hasPurchases ? 'OK' : '—'
    },
    {
      label: 'Retenues à la source',
      route: '/withholding-tax',
      status: d.withholdingTax > 0 ? 'ok' : 'neutral',
      statusLabel: d.withholdingTax > 0 ? 'OK' : '—'
    },
    {
      label: 'Échéancier fiscal',
      route: '/accounting/fiscal-schedule',
      queryParams: { fiscalYear: String(year), periodMonth: String(month) },
      status: d.status === 1 ? 'ok' : 'neutral',
      statusLabel: d.status === 1 ? 'Déposée' : 'À suivre'
    },
    {
      label: 'Contrôles de pré-clôture',
      route: '/accounting/pre-closing',
      status: 'neutral',
      statusLabel: 'Info'
    }
  ];
}

export function shouldShowFilingAlert(d: VatDeclarationDto): boolean {
  if (!d.filingDeadline) return false;
  const deadline = new Date(d.filingDeadline);
  if (Number.isNaN(deadline.getTime())) return false;
  if (d.status === 1 || d.status === 2) {
    return deadline.getTime() >= Date.now();
  }
  return true;
}

export function buildCompanyFromAuth(auth: User | null): CompanyDisplayInfo {
  return {
    companyName: auth?.contextCompanyName ?? auth?.companyName ?? '—',
    nif: null,
    taxRegimeDisplay: null,
    tradeName: null
  };
}

/** Priorité : DTO TVA → company settings → contexte auth. */
export function resolveCompanyDisplay(
  d: VatDeclarationDto | null,
  company: Company | null,
  auth: User | null
): CompanyDisplayInfo {
  if (d?.companyName) {
    return {
      companyName: d.companyName,
      nif: d.nif ?? null,
      taxRegimeDisplay: d.taxRegimeDisplay ?? null,
      tradeName: d.tradeName ?? null
    };
  }
  if (company) {
    return {
      companyName: company.companyName,
      nif: company.nif,
      taxRegimeDisplay: company.taxRegimeDisplay,
      tradeName: company.tradeName
    };
  }
  return buildCompanyFromAuth(auth);
}
