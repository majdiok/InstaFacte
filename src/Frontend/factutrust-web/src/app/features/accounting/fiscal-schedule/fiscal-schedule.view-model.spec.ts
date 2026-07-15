import { FiscalObligationType, FiscalScheduleStatus } from '../services/fiscal-schedule.service';
import {
  defaultObligationLabel,
  fiscalSourceRoute,
  formatFiscalAmount,
  formatFiscalDate,
  statusClass,
  statusIcon,
  toInputDate
} from './fiscal-schedule.view-model';

describe('fiscal-schedule.view-model', () => {
  it('maps status to stable classes and icons', () => {
    expect(statusClass(FiscalScheduleStatus.Overdue)).toBe('status-overdue');
    expect(statusClass(FiscalScheduleStatus.UpcomingWithin7Days)).toBe('status-soon');
    expect(statusIcon(FiscalScheduleStatus.Deposited)).toBe('fa-regular fa-circle-check');
  });

  it('formats dates and amounts for fiscal display', () => {
    expect(toInputDate('2026-07-10T12:30:00Z')).toBe('2026-07-10');
    expect(formatFiscalDate(null)).toBe('-');
    expect(formatFiscalAmount(1250, 'TND')).toContain('TND');
  });

  it('keeps declaration source routes deterministic', () => {
    expect(defaultObligationLabel(FiscalObligationType.Fodec)).toBe('FODEC');
    expect(fiscalSourceRoute({
      id: '1',
      obligationType: FiscalObligationType.MonthlyDeclaration,
      obligationTypeDisplay: 'Declaration mensuelle',
      obligationLabel: 'Declaration mensuelle',
      fiscalYear: 2026,
      periodMonth: 7,
      periodDisplay: 'Juillet 2026',
      dueDate: '2026-08-22',
      estimatedAmount: 0,
      currency: 'TND',
      status: FiscalScheduleStatus.UpcomingAfter7Days,
      statusDisplay: 'A venir',
      sourceType: 1,
      attachmentCount: 0,
      historyCount: 0,
      createdAt: '2026-07-10',
      version: 1
    })).toBe('/accounting/vat-declaration?year=2026&month=7');
    expect(fiscalSourceRoute({
      id: '2',
      obligationType: FiscalObligationType.QuarterlyVat,
      obligationTypeDisplay: 'TVA trimestrielle',
      obligationLabel: 'TVA trimestrielle',
      fiscalYear: 2026,
      periodQuarter: 2,
      periodDisplay: 'Trimestre 2 2026',
      dueDate: '2026-08-22',
      estimatedAmount: 0,
      currency: 'TND',
      status: FiscalScheduleStatus.UpcomingAfter7Days,
      statusDisplay: 'A venir',
      sourceType: 1,
      attachmentCount: 0,
      historyCount: 0,
      createdAt: '2026-07-10',
      version: 1
    })).toBe('/accounting/vat-declaration?year=2026&month=6');
  });
});
