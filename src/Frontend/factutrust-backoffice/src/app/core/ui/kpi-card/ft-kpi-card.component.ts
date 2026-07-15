import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
  computed,
  input
} from '@angular/core';
import { DecimalPipe, PercentPipe } from '@angular/common';
import { FtSkeletonComponent } from '../skeleton/ft-skeleton.component';
import type { FtTone } from '../badge/ft-badge.component';

/**
 * KPI Card avec valeur principale, delta vs période précédente et sparkline 30 jours.
 *
 * - `tone` détermine la couleur de la sparkline (success / warning / danger / info / neutral / accent).
 * - `clickable` rend la carte interactive (cursor pointer, focus ring, hover state).
 * - `loading` affiche un squelette à la place du contenu.
 * - `delta` est exprimé en pourcentage relatif (ex: 0.12 = +12 %).
 *
 * Usage :
 *  ```html
 *  <ft-kpi-card label="Entreprises"
 *               [value]="124"
 *               hint="vs mois dernier"
 *               [delta]="0.12"
 *               [sparkline]="signups"
 *               tone="info"
 *               icon="pi pi-building"
 *               [clickable]="true"
 *               (cardClick)="filterAll()" />
 *  ```
 */
@Component({
  selector: 'ft-kpi-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, PercentPipe, FtSkeletonComponent],
  template: `
    @if (loading()) {
      <div class="kpi kpi--loading">
        <ft-skeleton shape="line" width="40%" height="0.7rem" />
        <ft-skeleton shape="rect" width="60%" height="1.8rem" />
        <ft-skeleton shape="line" width="100%" height="1.5rem" />
      </div>
    } @else {
      <button
        type="button"
        class="kpi"
        [class.kpi--clickable]="clickable()"
        [class.kpi--active]="active()"
        [class.kpi--success]="tone() === 'success'"
        [class.kpi--warning]="tone() === 'warning'"
        [class.kpi--danger]="tone() === 'danger'"
        [class.kpi--info]="tone() === 'info'"
        [class.kpi--neutral]="tone() === 'neutral'"
        [class.kpi--accent]="tone() === 'accent'"
        [disabled]="!clickable()"
        [attr.aria-pressed]="clickable() && active() ? true : null"
        (click)="onClick()"
      >
        <div class="kpi__head">
          <span class="kpi__label">{{ label() }}</span>
          @if (icon()) {
            <i class="kpi__icon {{ icon() }}" aria-hidden="true"></i>
          }
        </div>
        <div class="kpi__row">
          <span class="kpi__value">{{ value() | number: '1.0-0' }}</span>
          @if (delta() !== null && delta() !== undefined) {
            <span
              class="kpi__delta"
              [class.kpi__delta--up]="(delta() ?? 0) >= 0"
              [class.kpi__delta--down]="(delta() ?? 0) < 0"
            >
              <span aria-hidden="true">{{ (delta() ?? 0) >= 0 ? '↗' : '↘' }}</span>
              {{ delta() | percent: '1.0-1' }}
            </span>
          }
        </div>
        @if (sparkline() && sparkline()!.length > 1) {
          <svg class="kpi__spark" viewBox="0 0 100 24" preserveAspectRatio="none" aria-hidden="true">
            <path [attr.d]="sparkPath()" stroke="currentColor" stroke-width="1.5" fill="none" />
            <path [attr.d]="sparkAreaPath()" fill="currentColor" opacity="0.18" />
          </svg>
        }
        @if (hint()) {
          <span class="kpi__hint">{{ hint() }}</span>
        }
      </button>
    }
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .kpi {
        all: unset;
        box-sizing: border-box;
        display: flex;
        flex-direction: column;
        gap: var(--gap-xs);
        background: var(--ft-surface);
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius-lg);
        padding: var(--gap-card);
        min-height: 132px;
        position: relative;
        transition: border-color var(--duration-normal) var(--easing-standard),
          background var(--duration-normal) var(--easing-standard),
          transform var(--duration-fast) var(--easing-standard);
        color: var(--ft-text);
        cursor: default;
        width: 100%;
      }

      .kpi--clickable {
        cursor: pointer;
      }

      .kpi--clickable:hover {
        border-color: var(--ft-accent-border);
        background: var(--ft-surface-3);
      }

      .kpi--active {
        border-color: var(--ft-accent);
        box-shadow: 0 0 0 1px var(--ft-accent-muted);
      }

      .kpi:focus-visible {
        outline: 2px solid var(--ft-accent);
        outline-offset: 2px;
      }

      .kpi__head {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--gap-xs);
      }

      .kpi__label {
        font-size: 0.72rem;
        font-weight: 600;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        color: var(--ft-text-muted);
      }

      .kpi__icon {
        font-size: 1.05rem;
        color: var(--ft-text-subtle);
      }

      .kpi__row {
        display: flex;
        align-items: baseline;
        gap: var(--gap-xs);
        flex-wrap: wrap;
      }

      .kpi__value {
        font-size: 1.85rem;
        font-weight: 650;
        letter-spacing: -0.02em;
        color: var(--ft-text);
        font-feature-settings: var(--font-feature-tabular);
        font-variant-numeric: tabular-nums lining-nums;
        line-height: 1;
      }

      .kpi__delta {
        display: inline-flex;
        align-items: center;
        gap: 0.25rem;
        font-size: 0.78rem;
        font-weight: 600;
        padding: 0.18rem 0.45rem;
        border-radius: var(--ft-radius-pill);
      }

      .kpi__delta--up {
        color: var(--ft-success-text);
        background: var(--ft-success-surface);
      }

      .kpi__delta--down {
        color: var(--ft-danger-text);
        background: var(--ft-danger-surface);
      }

      .kpi__spark {
        width: 100%;
        height: 28px;
        display: block;
        margin-top: var(--gap-xxs);
      }

      .kpi__hint {
        font-size: 0.78rem;
        color: var(--ft-text-muted);
      }

      /* Couleur de la sparkline et accent par tone */
      .kpi--success .kpi__spark { color: var(--ft-success-text); }
      .kpi--warning .kpi__spark { color: var(--ft-warning-text); }
      .kpi--danger  .kpi__spark { color: var(--ft-danger-text);  }
      .kpi--info    .kpi__spark { color: var(--ft-info-text);    }
      .kpi--accent  .kpi__spark { color: var(--ft-accent);       }
      .kpi--neutral .kpi__spark { color: var(--ft-text-muted);   }

      .kpi--loading {
        display: flex;
        flex-direction: column;
        gap: var(--gap-xs);
        padding: var(--gap-card);
        background: var(--ft-surface);
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius-lg);
        min-height: 132px;
      }
    `
  ]
})
export class FtKpiCardComponent {
  readonly label = input.required<string>();
  readonly value = input<number | null | undefined>(null);
  readonly delta = input<number | null | undefined>(null);
  readonly hint = input<string | null>(null);
  readonly icon = input<string | null>(null);
  readonly sparkline = input<number[] | null>(null);
  readonly tone = input<FtTone>('neutral');
  readonly clickable = input<boolean>(false);
  readonly active = input<boolean>(false);
  readonly loading = input<boolean>(false);

  @Output() cardClick = new EventEmitter<void>();

  readonly sparkPath = computed(() => this.computeSparkPath(false));
  readonly sparkAreaPath = computed(() => this.computeSparkPath(true));

  onClick(): void {
    if (this.clickable()) {
      this.cardClick.emit();
    }
  }

  private computeSparkPath(asArea: boolean): string {
    const data = this.sparkline();
    if (!data || data.length < 2) {
      return '';
    }
    const min = Math.min(...data);
    const max = Math.max(...data);
    const range = max - min || 1;
    const width = 100;
    const height = 24;
    const stepX = width / (data.length - 1);

    const points = data.map((v, i) => {
      const x = i * stepX;
      const y = height - ((v - min) / range) * (height - 4) - 2;
      return [x, y] as const;
    });

    let path = `M ${points[0][0].toFixed(2)} ${points[0][1].toFixed(2)}`;
    for (let i = 1; i < points.length; i++) {
      path += ` L ${points[i][0].toFixed(2)} ${points[i][1].toFixed(2)}`;
    }

    if (asArea) {
      path += ` L ${width} ${height} L 0 ${height} Z`;
    }
    return path;
  }
}
