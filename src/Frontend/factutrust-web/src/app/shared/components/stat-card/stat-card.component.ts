import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

type StatVariant = 'primary' | 'success' | 'warning' | 'error';

/**
 * Carte KPI — style maquette factutrust_design.html :
 * badge d'icône en teinte claire (haut-gauche) + badge de tendance (haut-droite),
 * libellé, valeur, puis emplacement projeté (barre de progression / sparkline / sous-titre)
 * et blob d'angle décoratif teinté.
 *
 * API rétro-compatible : les @Input existants (label, value, icon, variant, change, featured)
 * sont préservés. Ajouts optionnels : `tone` (teinte précise) ; navigation via `routerLink`.
 */
@Component({
  selector: 'app-stat-card',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    @if (isNavigable) {
      <a
        class="stat-card-link"
        [routerLink]="routerLink!"
        [queryParams]="queryParams"
        [attr.aria-label]="navigationAriaLabel || label">
        <ng-container *ngTemplateOutlet="cardContent"></ng-container>
      </a>
    } @else {
      <ng-container *ngTemplateOutlet="cardContent"></ng-container>
    }

    <ng-template #cardContent>
      <div [class]="cardClasses">
        <div class="stat-body">
          <div class="stat-top">
            <span [class]="badgeClasses">
              <i [class]="iconClass" aria-hidden="true"></i>
            </span>
            @if (change) {
              <span class="stat-trend" [class.positive]="change > 0" [class.negative]="change < 0">
                <i class="fa-solid" [class.fa-arrow-trend-up]="change > 0" [class.fa-arrow-trend-down]="change < 0" aria-hidden="true"></i>
                {{ change > 0 ? '+' : '' }}{{ change }}%
              </span>
            }
          </div>
          <span class="stat-label">{{ label }}</span>
          <span class="stat-value">{{ value }}</span>
          <ng-content></ng-content>
        </div>
      </div>
    </ng-template>
  `,
  styles: [`
    .stat-card-link {
      display: block;
      text-decoration: none;
      color: inherit;
      border-radius: var(--radius-2xl, 1rem);
    }

    .stat-card-link:focus-visible {
      outline: 2px solid var(--color-primary-500);
      outline-offset: 2px;
    }

    .stat-card {
      background: var(--color-background-elevated);
      border-radius: var(--radius-2xl, 1rem);
      padding: var(--spacing-5);
      border: 1px solid var(--color-border-subtle);
      box-shadow: var(--shadow-soft-sm, var(--shadow-sm));
      position: relative;
      overflow: hidden;
      transition: box-shadow var(--duration-moderate, 260ms) var(--ease-out-soft, cubic-bezier(0.16, 1, 0.3, 1)),
                  transform var(--duration-moderate, 260ms) var(--ease-out-soft, cubic-bezier(0.16, 1, 0.3, 1)),
                  border-color var(--duration-moderate, 260ms) var(--ease-out-soft, cubic-bezier(0.16, 1, 0.3, 1));
    }

    .stat-card::after {
      content: '';
      position: absolute;
      top: 0;
      right: 0;
      width: 8rem;
      height: 8rem;
      background: radial-gradient(circle at top right, currentColor 0%, transparent 70%);
      opacity: 0.08;
      border-bottom-left-radius: 9999px;
      pointer-events: none;
      transition: opacity var(--duration-moderate, 260ms) var(--ease-out-soft);
    }

    .stat-card-link .stat-card,
    .stat-card:hover {
      box-shadow: var(--shadow-soft-lg, var(--shadow-lg));
      transform: translateY(-3px);
      border-color: var(--color-border-default);
    }

    .stat-card-link .stat-card:hover::after,
    .stat-card:hover::after { opacity: 0.14; }

    .stat-card--primary:hover { border-color: var(--color-primary-300); }
    .stat-card--success:hover { border-color: var(--color-success-200, #bbf7d0); }
    .stat-card--warning:hover { border-color: var(--color-warning-200, #fde68a); }
    .stat-card--error:hover   { border-color: var(--color-error-200, #fecaca); }

    .stat-body {
      position: relative;
      z-index: 1;
    }

    .stat-top {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: var(--spacing-4);
    }

    .stat-icon {
      transition: transform var(--duration-moderate, 260ms) var(--ease-out-back, cubic-bezier(0.34, 1.56, 0.64, 1));
    }
    .stat-card-link .stat-card:hover .stat-icon,
    .stat-card:hover .stat-icon {
      transform: scale(1.08) rotate(-3deg);
    }

    .stat-trend {
      display: inline-flex;
      align-items: center;
      gap: var(--spacing-1);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-bold);
      padding: var(--spacing-1) var(--spacing-2);
      border-radius: var(--radius-lg);
    }
    .stat-trend.positive { color: var(--color-success-600); background: var(--color-success-50); }
    .stat-trend.negative { color: var(--color-error-600); background: var(--color-error-50); }

    .stat-label {
      display: block;
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semibold);
      color: var(--color-text-secondary);
      text-transform: uppercase;
      letter-spacing: 0.06em;
      margin-bottom: var(--spacing-1);
    }

    .stat-value {
      display: block;
      font-size: var(--font-size-2xl);
      font-weight: 700;
      color: var(--color-text-primary);
      line-height: 1.15;
      font-family: var(--font-family-mono, 'JetBrains Mono', 'SF Mono', 'Monaco', 'Consolas', monospace);
      font-variant-numeric: tabular-nums;
      letter-spacing: -0.02em;
    }

    .stat-card--featured { padding: var(--spacing-6); }
    .stat-card--featured .stat-value { font-size: var(--font-size-3xl); }

    @media (max-width: 768px) {
      .stat-card { padding: var(--spacing-4); }
      .stat-value { font-size: var(--font-size-xl); }
      .stat-card--featured .stat-value { font-size: var(--font-size-2xl); }
    }
  `]
})
export class StatCardComponent {
  @Input() label = '';
  @Input() value: string | number = '';
  @Input() icon = 'pi-chart-line';
  @Input() variant: StatVariant = 'primary';
  @Input() change?: number;
  @Input() featured?: boolean;
  /** Teinte précise du badge/blob (clé de la palette $ft-icon-tones). Défaut : dérivée de `variant`. */
  @Input() tone?: string;
  @Input() routerLink?: string | any[];
  @Input() queryParams?: Record<string, string>;
  @Input() clickable = false;
  @Input() navigationAriaLabel?: string;

  private static readonly VARIANT_TONE: Record<StatVariant, string> = {
    primary: 'primary',
    success: 'emerald',
    warning: 'amber',
    error: 'rose'
  };

  get isNavigable(): boolean {
    return (this.clickable || this.routerLink !== undefined) && this.routerLink != null && this.routerLink !== '';
  }

  get resolvedTone(): string {
    return this.tone ?? StatCardComponent.VARIANT_TONE[this.variant] ?? 'primary';
  }

  get cardClasses(): string {
    return [
      'stat-card',
      'stat-card--' + this.variant,
      'ft-icon-tone--' + this.resolvedTone,
      this.featured ? 'stat-card--featured' : ''
    ].join(' ').trim();
  }

  get badgeClasses(): string {
    return [
      'stat-icon',
      'ft-icon-badge',
      'ft-icon-badge--' + this.resolvedTone,
      this.featured ? 'ft-icon-badge--lg' : ''
    ].join(' ').trim();
  }

  get iconClass(): string {
    const icon = this.icon || '';
    return icon.startsWith('fa') ? icon : 'pi ' + icon;
  }
}
