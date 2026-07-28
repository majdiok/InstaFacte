import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Carte graphique Superieur — titre + actions header + slot contenu (p-chart / custom).
 */
@Component({
  selector: 'app-chart-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="chart-card">
      <header class="chart-card__header">
        <h3 class="chart-card__title">{{ title }}</h3>
        <div class="chart-card__actions">
          <ng-content select="[chart-actions]"></ng-content>
        </div>
      </header>
      @if (subtitle) {
        <p class="chart-card__subtitle">{{ subtitle }}</p>
      }
      <div class="chart-card__body">
        <ng-content></ng-content>
      </div>
    </section>
  `,
  styles: [`
    .chart-card {
      background: #fff;
      border-radius: var(--superieur-card-radius, 8px);
      box-shadow: var(--superieur-card-shadow, 0 0 20px rgba(0,0,0,0.08));
      border: 1px solid rgba(0,0,0,0.04);
      padding: 1.25rem 1.5rem;
      height: 100%;
      display: flex;
      flex-direction: column;
    }

    .chart-card__header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
      margin-bottom: 0.5rem;
    }

    .chart-card__title {
      margin: 0;
      font-size: 1.05rem;
      font-weight: 600;
      color: var(--color-text-primary, #0f172a);
    }

    .chart-card__subtitle {
      margin: 0 0 0.75rem;
      font-size: 0.8125rem;
      color: var(--color-text-secondary, #64748b);
    }

    .chart-card__actions {
      display: flex;
      align-items: center;
      gap: 0.35rem;
      color: var(--color-text-tertiary, #94a3b8);
    }

    .chart-card__body {
      flex: 1;
      min-height: 0;
      position: relative;
    }
  `]
})
export class ChartCardComponent {
  @Input() title = '';
  @Input() subtitle?: string;
}