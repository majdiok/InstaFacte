import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-page-header',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="page-header">
      <div class="page-header-content">
        <h1 class="page-title">{{ title }}</h1>
        <p class="page-subtitle" *ngIf="subtitle">{{ subtitle }}</p>
      </div>
      <div class="page-header-actions">
        <ng-content></ng-content>
      </div>
    </div>
  `,
  styles: [`
    .page-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      margin-bottom: var(--spacing-8);
      padding-bottom: var(--spacing-5);
      flex-wrap: wrap;
      gap: var(--spacing-4);
      animation: fadeIn 0.3s ease-out;
      border-bottom: 1px solid transparent;
      border-image: linear-gradient(
        90deg,
        var(--color-border-subtle, #e2e8f0) 0%,
        var(--color-accent-100, #e0e7ff) 30%,
        transparent 70%
      ) 1;
    }

    @keyframes fadeIn {
      from {
        opacity: 0;
        transform: translateY(-4px);
      }
      to {
        opacity: 1;
        transform: translateY(0);
      }
    }

    .page-header-content {
      flex: 1;
      min-width: 0;
    }

    .page-title {
      font-size: var(--font-size-3xl);
      font-weight: var(--font-weight-bold);
      color: var(--color-neutral-900);
      margin: 0 0 var(--spacing-2);
      line-height: 1.2;
      letter-spacing: -0.02em;
    }

    .page-subtitle {
      color: var(--color-neutral-600);
      font-size: var(--font-size-base);
      margin: 0;
      font-weight: var(--font-weight-normal);
    }

    .page-header-actions {
      display: flex;
      gap: var(--spacing-3);
      align-items: center;
    }

    @media (max-width: 768px) {
      .page-header {
        margin-bottom: var(--spacing-6);
      }

      .page-title {
        font-size: var(--font-size-2xl);
      }

      .page-header-actions {
        width: 100%;
        justify-content: flex-start;
      }
    }
  `]
})
export class PageHeaderComponent {
  @Input() title = '';
  @Input() subtitle?: string;
}
