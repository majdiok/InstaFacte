import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';

export interface PayrollStatItem {
  label: string;
  value: string | number;
  icon?: string;
  variant?: 'primary' | 'success' | 'warning' | 'error';
  featured?: boolean;
}

@Component({
  selector: 'app-payroll-stat-grid',
  standalone: true,
  imports: [CommonModule, StatCardComponent],
  template: `
    <div class="payroll-stat-grid" [class.payroll-stat-grid--compact]="compact">
      @for (item of items; track item.label) {
        <app-stat-card
          [label]="item.label"
          [value]="item.value"
          [icon]="item.icon ?? 'pi-chart-bar'"
          [variant]="item.variant ?? 'primary'"
          [featured]="item.featured ?? false" />
      }
    </div>
  `,
  styles: [`
    .payroll-stat-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr));
      gap: var(--spacing-4);
    }
    .payroll-stat-grid--compact {
      grid-template-columns: repeat(auto-fit, minmax(8rem, 1fr));
      gap: var(--spacing-3);
    }
  `]
})
export class PayrollStatGridComponent {
  @Input({ required: true }) items: PayrollStatItem[] = [];
  @Input() compact = false;
}
