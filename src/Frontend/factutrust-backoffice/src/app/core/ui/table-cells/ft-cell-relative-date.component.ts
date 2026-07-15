import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { TooltipModule } from 'primeng/tooltip';
import { FtRelativeDatePipe } from '../../pipes/ft-relative-date.pipe';

/**
 * Cellule de date relative ("il y a 2 j") avec tooltip qui révèle la date absolue
 * (`dd/MM/yyyy HH:mm`). Si la valeur est nulle, affiche un tiret muted.
 *
 * Usage :
 *  ```html
 *  <ft-cell-relative-date [date]="row.lastActivityAt" />
 *  ```
 */
@Component({
  selector: 'ft-cell-relative-date',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, TooltipModule, FtRelativeDatePipe],
  template: `
    @if (date) {
      <span class="cell-relative" [pTooltip]="(date | date: 'dd/MM/yyyy HH:mm') ?? ''" tooltipPosition="top">
        {{ date | ftRelativeDate }}
      </span>
    } @else {
      <span class="cell-empty" aria-label="Pas de date">—</span>
    }
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .cell-relative {
        color: var(--ft-text);
        cursor: help;
        font-size: 0.875rem;
      }

      .cell-empty {
        color: var(--ft-text-subtle);
      }
    `
  ]
})
export class FtCellRelativeDateComponent {
  @Input() date: string | Date | null = null;
}
