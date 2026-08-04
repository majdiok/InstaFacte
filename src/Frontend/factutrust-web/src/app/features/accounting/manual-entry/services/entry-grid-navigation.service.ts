import { Injectable } from '@angular/core';
import { hasAccountingAmount } from '../../shared/accounting-amount.utils';

export type EntryGridField = 'account' | 'debit' | 'credit';

export interface EntryGridNavigationContext {
  container: HTMLElement;
  rowIndex: number;
  field: EntryGridField;
  debit: number | null;
  credit: number | null;
  totalRows: number;
  onAddLine?: () => void;
}

@Injectable({ providedIn: 'root' })
export class EntryGridNavigationService {
  focusCell(container: HTMLElement, rowIndex: number, field: EntryGridField): boolean {
    const selector = this.buildSelector(rowIndex, field);
    const el = container.querySelector<HTMLElement>(selector);
    if (!el) {
      return false;
    }

    if (field === 'account') {
      const input = el.querySelector<HTMLInputElement>('input');
      input?.focus();
      input?.select();
      return true;
    }

    const amountHost = el.querySelector<HTMLElement>('app-accounting-amount-input');
    if (amountHost) {
      const input = amountHost.querySelector<HTMLInputElement>('input');
      input?.focus();
      input?.select();
      return true;
    }

    el.focus();
    return true;
  }

  handleAmountTab(ctx: EntryGridNavigationContext, shiftKey: boolean): boolean {
    if (shiftKey) {
      return this.handleShiftTab(ctx);
    }
    return this.handleForwardTab(ctx);
  }

  handleAmountEnter(ctx: EntryGridNavigationContext): boolean {
    return this.handleForwardTab(ctx);
  }

  private handleForwardTab(ctx: EntryGridNavigationContext): boolean {
    const { field, rowIndex, debit, credit, container, totalRows, onAddLine } = ctx;

    if (field === 'debit') {
      if (hasAccountingAmount(debit)) {
        return this.focusNextRowAccount(container, rowIndex, totalRows, onAddLine);
      }
      return this.focusCell(container, rowIndex, 'credit');
    }

    if (field === 'credit') {
      if (hasAccountingAmount(credit)) {
        return this.focusNextRowAccount(container, rowIndex, totalRows, onAddLine);
      }
      return this.focusNextRowAccount(container, rowIndex, totalRows, onAddLine);
    }

    return false;
  }

  private handleShiftTab(ctx: EntryGridNavigationContext): boolean {
    const { field, rowIndex, container } = ctx;

    if (field === 'credit') {
      return this.focusCell(container, rowIndex, 'debit');
    }

    if (field === 'debit' && rowIndex > 0) {
      return this.focusCell(container, rowIndex - 1, 'credit');
    }

    return false;
  }

  private focusNextRowAccount(
    container: HTMLElement,
    rowIndex: number,
    totalRows: number,
    onAddLine?: () => void
  ): boolean {
    const nextRow = rowIndex + 1;
    if (nextRow >= totalRows) {
      onAddLine?.();
      setTimeout(() => {
        this.focusCell(container, nextRow, 'account');
      }, 0);
      return true;
    }
    return this.focusCell(container, nextRow, 'account');
  }

  private buildSelector(rowIndex: number, field: EntryGridField): string {
    return `[data-row="${rowIndex}"][data-field="${field}"]`;
  }
}
