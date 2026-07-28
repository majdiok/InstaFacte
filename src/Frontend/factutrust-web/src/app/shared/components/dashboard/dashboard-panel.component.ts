import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Panneau dashboard Superieur — carte blanche pour tables / listes.
 */
@Component({
  selector: 'app-dashboard-panel',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="dashboard-panel">
      @if (title || hasActions) {
        <header class="dashboard-panel__header">
          <div>
            @if (title) {
              <h3 class="dashboard-panel__title">{{ title }}</h3>
            }
            @if (subtitle) {
              <p class="dashboard-panel__subtitle">{{ subtitle }}</p>
            }
          </div>
          <div class="dashboard-panel__actions">
            <ng-content select="[panel-actions]"></ng-content>
          </div>
        </header>
      }
      <div class="dashboard-panel__body" [class.dashboard-panel__body--flush]="flush">
        <ng-content></ng-content>
      </div>
    </section>
  `,
  styles: [`
    .dashboard-panel {
      background: #fff;
      border-radius: var(--superieur-card-radius, 8px);
      box-shadow: var(--superieur-card-shadow, 0 0 20px rgba(0,0,0,0.08));
      border: 1px solid rgba(0,0,0,0.04);
      overflow: hidden;
      height: 100%;
      display: flex;
      flex-direction: column;
    }

    .dashboard-panel__header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 1rem;
      padding: 1.15rem 1.35rem 0.75rem;
    }

    .dashboard-panel__title {
      margin: 0;
      font-size: 1.05rem;
      font-weight: 600;
      color: var(--color-text-primary, #0f172a);
    }

    .dashboard-panel__subtitle {
      margin: 0.25rem 0 0;
      font-size: 0.8125rem;
      color: var(--color-text-secondary, #64748b);
    }

    .dashboard-panel__actions {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      flex-shrink: 0;
    }

    .dashboard-panel__body {
      padding: 0.5rem 1.35rem 1.35rem;
      flex: 1;
      min-height: 0;
    }

    .dashboard-panel__body--flush {
      padding: 0;
    }
  `]
})
export class DashboardPanelComponent {
  @Input() title = '';
  @Input() subtitle?: string;
  @Input() flush = false;
  @Input() hasActions = false;
}