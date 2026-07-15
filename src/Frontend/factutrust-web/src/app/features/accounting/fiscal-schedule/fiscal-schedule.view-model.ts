import {
  FiscalObligationType,
  FiscalReminderChannel,
  FiscalScheduleEntryDto,
  FiscalScheduleStatus
} from '../services/fiscal-schedule.service';

export const FISCAL_OBLIGATION_OPTIONS = [
  { value: FiscalObligationType.MonthlyDeclaration, label: 'Declaration mensuelle' },
  { value: FiscalObligationType.ProvisionalCorporateTaxInstallment, label: 'Acompte provisionnel IS' },
  { value: FiscalObligationType.WithholdingTax, label: 'Retenue a la source' },
  { value: FiscalObligationType.Fodec, label: 'FODEC' },
  { value: FiscalObligationType.QuarterlyVat, label: 'TVA trimestrielle' },
  { value: FiscalObligationType.FinancialStatements, label: 'Etats financiers' },
  { value: FiscalObligationType.SemiAnnualFinancialStatements, label: 'Etats financiers semestriels' },
  { value: FiscalObligationType.PersonalIncomeTaxInstallment, label: 'IRPP - Acompte' },
  { value: FiscalObligationType.Other, label: 'Autre obligation' }
];

export const FISCAL_STATUS_OPTIONS = [
  { value: FiscalScheduleStatus.UpcomingWithin7Days, label: 'A venir (<= 7 j)' },
  { value: FiscalScheduleStatus.UpcomingAfter7Days, label: 'A venir (> 7 j)' },
  { value: FiscalScheduleStatus.Overdue, label: 'En retard' },
  { value: FiscalScheduleStatus.Deposited, label: 'Deposee' },
  { value: FiscalScheduleStatus.Validated, label: 'Validee' },
  { value: FiscalScheduleStatus.Paid, label: 'Payee' },
  { value: FiscalScheduleStatus.Cancelled, label: 'Annulee' }
];

export const QUARTER_OPTIONS = [
  { value: 1, label: 'Trimestre 1' },
  { value: 2, label: 'Trimestre 2' },
  { value: 3, label: 'Trimestre 3' },
  { value: 4, label: 'Trimestre 4' }
];

export const FISCAL_REMINDER_CHANNEL_OPTIONS = [
  { value: FiscalReminderChannel.Email, label: 'Email' },
  { value: FiscalReminderChannel.InApp, label: 'Notification' },
  { value: FiscalReminderChannel.Sms, label: 'SMS' }
];

export const MONTH_OPTIONS = [
  { value: 1, label: 'Janvier' },
  { value: 2, label: 'Fevrier' },
  { value: 3, label: 'Mars' },
  { value: 4, label: 'Avril' },
  { value: 5, label: 'Mai' },
  { value: 6, label: 'Juin' },
  { value: 7, label: 'Juillet' },
  { value: 8, label: 'Aout' },
  { value: 9, label: 'Septembre' },
  { value: 10, label: 'Octobre' },
  { value: 11, label: 'Novembre' },
  { value: 12, label: 'Decembre' }
];

export function statusClass(status: number): string {
  switch (status) {
    case FiscalScheduleStatus.Overdue:
      return 'status-overdue';
    case FiscalScheduleStatus.UpcomingWithin7Days:
      return 'status-soon';
    case FiscalScheduleStatus.UpcomingAfter7Days:
      return 'status-upcoming';
    case FiscalScheduleStatus.Deposited:
      return 'status-deposited';
    case FiscalScheduleStatus.Validated:
      return 'status-validated';
    case FiscalScheduleStatus.Paid:
      return 'status-done';
    case FiscalScheduleStatus.Cancelled:
      return 'status-cancelled';
    default:
      return 'status-neutral';
  }
}

export function statusIcon(status: number): string {
  switch (status) {
    case FiscalScheduleStatus.Overdue:
      return 'fa-regular fa-circle-xmark';
    case FiscalScheduleStatus.UpcomingWithin7Days:
      return 'fa-regular fa-clock';
    case FiscalScheduleStatus.UpcomingAfter7Days:
      return 'fa-regular fa-calendar';
    case FiscalScheduleStatus.Deposited:
    case FiscalScheduleStatus.Paid:
    case FiscalScheduleStatus.Validated:
      return 'fa-regular fa-circle-check';
    default:
      return 'fa-regular fa-circle';
  }
}

export function formatFiscalDate(value?: string | null): string {
  if (!value) {
    return '-';
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '-';
  }
  return new Intl.DateTimeFormat('fr-TN').format(date);
}

export function toInputDate(value?: string | Date | null): string {
  const date = value ? new Date(value) : new Date();
  if (Number.isNaN(date.getTime())) {
    return '';
  }
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

export function formatFiscalAmount(amount: number, currency = 'TND'): string {
  return new Intl.NumberFormat('fr-TN', {
    minimumFractionDigits: 3,
    maximumFractionDigits: 3
  }).format(amount ?? 0) + ` ${currency}`;
}

export function defaultObligationLabel(type: number): string {
  return FISCAL_OBLIGATION_OPTIONS.find(o => o.value === Number(type))?.label ?? 'Autre obligation';
}

export function fiscalSourceRoute(entry: FiscalScheduleEntryDto): string | null {
  if (entry.obligationType === FiscalObligationType.MonthlyDeclaration) {
    const year = entry.fiscalYear;
    const month = entry.periodMonth ?? 1;
    return `/accounting/vat-declaration?year=${year}&month=${month}`;
  }
  if (entry.obligationType === FiscalObligationType.QuarterlyVat) {
    const year = entry.fiscalYear;
    const quarter = entry.periodQuarter ?? 1;
    const month = quarter * 3;
    return `/accounting/vat-declaration?year=${year}&month=${month}`;
  }
  if (entry.obligationType === FiscalObligationType.FinancialStatements || entry.obligationType === FiscalObligationType.SemiAnnualFinancialStatements) {
    return `/accounting/nct-statements?fiscalYear=${entry.fiscalYear}`;
  }
  if (entry.obligationType === FiscalObligationType.WithholdingTax) {
    return '/withholding-tax/tej-export';
  }
  return null;
}
