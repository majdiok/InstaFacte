import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

export type FtSkeletonShape = 'rect' | 'circle' | 'line';

/**
 * Skeleton loader avec animation shimmer (cf. keyframes ft-shimmer dans styles.scss).
 *
 * Usage :
 *  ```html
 *  <ft-skeleton shape="rect" width="100%" height="2.5rem" />
 *  <ft-skeleton shape="circle" width="2rem" height="2rem" />
 *  <ft-skeleton shape="line" />
 *  ```
 */
@Component({
  selector: 'ft-skeleton',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span
      class="skel"
      [class.skel--rect]="shape === 'rect'"
      [class.skel--circle]="shape === 'circle'"
      [class.skel--line]="shape === 'line'"
      [style.width]="width"
      [style.height]="height"
      [attr.aria-hidden]="true"
    ></span>
  `,
  styles: [
    `
      :host {
        display: inline-block;
      }

      .skel {
        display: inline-block;
        background: linear-gradient(
          90deg,
          var(--ft-surface) 0%,
          var(--ft-surface-3) 50%,
          var(--ft-surface) 100%
        );
        background-size: 200% 100%;
        animation: ft-shimmer 1.4s linear infinite;
        will-change: background-position;
      }

      .skel--rect {
        border-radius: var(--ft-radius);
      }

      .skel--circle {
        border-radius: var(--ft-radius-pill);
      }

      .skel--line {
        height: 0.85rem;
        width: 100%;
        border-radius: var(--ft-radius-sm);
      }
    `
  ]
})
export class FtSkeletonComponent {
  @Input() shape: FtSkeletonShape = 'rect';
  @Input() width = '100%';
  @Input() height = '1rem';
}
