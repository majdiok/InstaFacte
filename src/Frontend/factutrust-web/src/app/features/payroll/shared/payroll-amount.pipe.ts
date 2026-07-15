import { Pipe, PipeTransform } from '@angular/core';
import { DecimalPipe } from '@angular/common';

/** Formats monetary amounts in Tunisian dinars (3 decimal places). */
@Pipe({ name: 'payrollAmount', standalone: true })
export class PayrollAmountPipe implements PipeTransform {
  private readonly decimal = new DecimalPipe('fr-TN');

  transform(value: number | null | undefined, showCurrency = true): string {
    if (value == null || Number.isNaN(value)) return '—';
    const formatted = this.decimal.transform(value, '1.3-3') ?? '0,000';
    return showCurrency ? `${formatted} TND` : formatted;
  }
}
