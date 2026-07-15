import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-payroll-section',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="payroll-section">
      <div class="payroll-section__header">
        @if (icon) {
          <i class="pi {{ icon }}" aria-hidden="true"></i>
        }
        <div>
          <h3 class="payroll-section__title">{{ title }}</h3>
          @if (subtitle) {
            <p class="payroll-section__subtitle">{{ subtitle }}</p>
          }
        </div>
      </div>
      <div class="payroll-section__body">
        <ng-content></ng-content>
      </div>
    </section>
  `,
  styles: [`
    .payroll-section {
      margin-bottom: var(--spacing-6);
    }
    .payroll-section__header {
      display: flex;
      align-items: flex-start;
      gap: var(--spacing-3);
      margin-bottom: var(--spacing-4);
      padding-bottom: var(--spacing-3);
      border-bottom: 1px solid var(--color-border-subtle);
    }
    .payroll-section__header .pi {
      font-size: var(--font-size-lg);
      color: var(--color-primary-600);
      margin-top: 0.15rem;
    }
    .payroll-section__title {
      margin: 0;
      font-size: var(--font-size-lg);
      font-weight: var(--font-weight-semibold);
      color: var(--color-text-primary);
    }
    .payroll-section__subtitle {
      margin: var(--spacing-1) 0 0;
      font-size: var(--font-size-sm);
      color: var(--color-text-secondary);
    }
  `]
})
export class PayrollSectionComponent {
  @Input({ required: true }) title = '';
  @Input() subtitle?: string;
  @Input() icon?: string;
}
