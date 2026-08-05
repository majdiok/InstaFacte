import { TestBed } from '@angular/core/testing';
import {
  EntryFormStore,
  applyAutoBalanceToLines,
  computeAutoBalanceGap,
  computeTotals,
  hasMeaningfulInput,
  isBalancedTotals
} from './entry-form.store';
import { createDefaultLines, createEmptyLine } from '../models/entry-form.model';

describe('EntryFormStore pure helpers', () => {
  it('computeTotals sums debit and credit', () => {
    const lines = [
      { ...createEmptyLine(), debit: 100, credit: null },
      { ...createEmptyLine(), debit: null, credit: 50 }
    ];
    expect(computeTotals(lines)).toEqual({ debit: 100, credit: 50 });
  });

  it('isBalancedTotals uses millime precision', () => {
    expect(isBalancedTotals({ debit: 10.0004, credit: 10 })).toBe(true);
    expect(isBalancedTotals({ debit: 0, credit: 0 })).toBe(false);
    expect(isBalancedTotals({ debit: 10, credit: 9 })).toBe(false);
  });

  it('computeAutoBalanceGap rounds to 3 decimals', () => {
    expect(computeAutoBalanceGap({ debit: 100.0005, credit: 50 })).toBe(50.001);
  });

  it('applyAutoBalanceToLines fills last empty line', () => {
    const lines = createDefaultLines();
    lines[0] = { ...lines[0], debit: 100 };
    const result = applyAutoBalanceToLines(lines);
    expect(result[1].credit).toBe(100);
  });

  it('applyAutoBalanceToLines adds line when no empty slot', () => {
    const lines = [
      { ...createEmptyLine(), debit: 100 },
      { ...createEmptyLine(), credit: 30 }
    ];
    const result = applyAutoBalanceToLines(lines);
    expect(result.length).toBe(3);
    expect(result[2].credit).toBe(70);
  });

  it('hasMeaningfulInput detects label and lines', () => {
    expect(hasMeaningfulInput('', '', '', createDefaultLines())).toBe(false);
    expect(hasMeaningfulInput('Test', '', '', createDefaultLines())).toBe(true);
    const lines = [{ ...createEmptyLine(), accountNumber: '607', debit: 10 }];
    expect(hasMeaningfulInput('', '', '', lines)).toBe(true);
  });
});

describe('EntryFormStore', () => {
  let store: EntryFormStore;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [EntryFormStore] });
    store = TestBed.inject(EntryFormStore);
  });

  it('excludes mutual debit/credit on same line in validation', () => {
    store.entryLabel.set('Test');
    store.lines.set([
      { ...createEmptyLine(), accountNumber: '607', debit: 100, credit: 50 },
      { ...createEmptyLine(), accountNumber: '4011', credit: 100 }
    ]);
    const result = store.validateSubmit();
    expect(result.valid).toBe(false);
  });

  it('buildCreateRequest maps lines with fallback label', () => {
    store.entryLabel.set('Ecriture test');
    store.journalCode.set('JA');
    store.entryDate.set('2026-08-02');
    store.lines.set([
      { ...createEmptyLine(), accountNumber: '607', debit: 100, lineLabel: '' },
      { ...createEmptyLine(), accountNumber: '4011', credit: 100, lineLabel: 'Ligne 2' }
    ]);
    const { request } = store.buildCreateRequest();
    expect(request.lines[0].lineLabel).toBe('Ecriture test');
    expect(request.lines[1].lineLabel).toBe('Ligne 2');
    expect(request.journalCode).toBe('JA');
  });

  it('onDebitChange clears credit', () => {
    store.lines.set([{ ...createEmptyLine(), debit: 10, credit: 5 }]);
    store.onDebitChange(0);
    expect(store.lines()[0].credit).toBeNull();
  });

  it('onDebitChange normalizes amount to millime', () => {
    store.lines.set([{ ...createEmptyLine(), debit: 10.0004, credit: null }]);
    store.onDebitChange(0);
    expect(store.lines()[0].debit).toBe(10);
  });

  it('onCreditChange clears debit and normalizes', () => {
    store.lines.set([{ ...createEmptyLine(), debit: 5, credit: 20.0006 }]);
    store.onCreditChange(0);
    expect(store.lines()[0].debit).toBeNull();
    expect(store.lines()[0].credit).toBe(20.001);
  });

  it('onDebitPreview clears opposite side in place and keeps clientLineId', () => {
    const line = { ...createEmptyLine(), debit: 10, credit: 5 };
    const id = line.clientLineId;
    store.lines.set([line]);
    store.onDebitPreview(0);
    expect(store.lines()[0].credit).toBeNull();
    expect(store.lines()[0].clientLineId).toBe(id);
    expect(store.lines()[0]).toBe(line);
  });

  it('onDebitChange preserves clientLineId', () => {
    const line = { ...createEmptyLine(), debit: 10.0004, credit: 2 };
    const id = line.clientLineId;
    store.lines.set([line]);
    store.onDebitChange(0);
    expect(store.lines()[0].clientLineId).toBe(id);
    expect(store.lines()[0].debit).toBe(10);
    expect(store.lines()[0].credit).toBeNull();
  });

  it('createEmptyLine assigns clientLineId', () => {
    const a = createEmptyLine();
    const b = createEmptyLine();
    expect(a.clientLineId).toBeTruthy();
    expect(b.clientLineId).toBeTruthy();
    expect(a.clientLineId).not.toBe(b.clientLineId);
  });

  it('periodClosed blocks when period is closed', () => {
    store.setPeriods([{
      id: 'p1',
      fiscalYear: 2026,
      month: 8,
      startDate: '2026-08-01',
      endDate: '2026-08-31',
      isClosed: true
    }]);
    store.entryDate.set('2026-08-15');
    expect(store.periodClosed()).toBe(true);
  });

  it('getLineStatus returns unknown for missing account', () => {
    store.setAccounts([]);
    expect(store.getLineStatus({ ...createEmptyLine(), accountNumber: '999' })).toBe('unknown');
  });
});
