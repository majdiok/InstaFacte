import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';

export interface PayrollStatItem {
  label: string;
  value: string | number;
  icon?: string;
  variant?: 'primary' | 'success' | 'warning' | 'error';
  featured?: boolean;
  valueTitle?: string;
}

export type PayrollStatGridDensity = 'default' | 'run-detail' | 'compact';

@Component({
  selector: 'app-payroll-stat-grid',
  standalone: true,
  imports: [CommonModule, StatCardComponent],
  template: `
    <div
      class="payroll-stat-grid"
      [class.payroll-stat-grid--compact]="isCompact"
      [class.payroll-stat-grid--run-detail]="density === 'run-detail' && !isCompact">
      @for (item of items; track item.label) {
        <app-stat-card
          [label]="item.label"
          [value]="item.value"
          [valueTitle]="item.valueTitle"
          [icon]="item.icon ?? 'pi-chart-bar'"
          [variant]="item.variant ?? 'primary'"
          [featured]="item.featured ?? false" />
      }
    </div>
  `,
  styles: [`
    .payroll-stat-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(11.5rem, 1fr));
      gap: var(--spacing-4);
      min-width: 0;
    }
    .payroll-stat-grid > app-stat-card {
      min-width: 0;
    }
    .payroll-stat-grid--compact {
      grid-template-columns: repeat(auto-fit, minmax(8rem, 1fr));
      gap: var(--spacing-3);
    }
    .payroll-stat-grid--run-detail {
      grid-template-columns: repeat(auto-fit, minmax(13.5rem, 1fr));
    }
    @media (max-width: 1200px) {
      .payroll-stat-grid--run-detail {
        grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr));
      }
    }
    @media (max-width: 640px) {
      .payroll-stat-grid--run-detail {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
    }
  `]
})
export class PayrollStatGridComponent {
  @Input({ required: true }) items: PayrollStatItem[] = [];
  /** @deprecated Prefer density="compact" */
  @Input() compact = false;
  @Input() density: PayrollStatGridDensity = 'default';

  get isCompact(): boolean {
    return this.compact || this.density === 'compact';
  }
}
