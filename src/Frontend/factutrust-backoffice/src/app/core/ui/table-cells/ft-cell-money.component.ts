import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { FtTndCurrencyPipe } from '../../pipes/ft-tnd-currency.pipe';

/**
 * Cellule monétaire : montant aligné à droite, chiffres tabulaires, formaté en TND.
 *
 * Usage :
 *  ```html
 *  <ft-cell-money [amount]="row.mrr" />
 *  <ft-cell-money [amount]="row.amount" [fractionDigits]="0" />
 *  ```
 */
@Component({
  selector: 'ft-cell-money',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FtTndCurrencyPipe],
  template: `
    <span class="cell-money cell-num">
      {{ amount | ftTndCurrency: { fractionDigits: fractionDigits, withSymbol: withSymbol } }}
    </span>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .cell-money {
        display: inline-block;
        text-align: right;
        font-feature-settings: var(--font-feature-tabular);
        font-variant-numeric: tabular-nums lining-nums;
        color: var(--ft-text);
      }
    `
  ]
})
export class FtCellMoneyComponent {
  @Input() amount: number | null = null;
  @Input() fractionDigits = 3;
  @Input() withSymbol = true;
}
