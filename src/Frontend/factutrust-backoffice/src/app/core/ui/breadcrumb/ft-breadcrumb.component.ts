import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';

export interface FtBreadcrumbItem {
  /** Libellé visible */
  label: string;
  /** Lien interne Angular ; absent = item courant non cliquable */
  routerLink?: string | string[];
  /** Icône PrimeIcons optionnelle (ex: `pi-home`) */
  icon?: string;
}

/**
 * Fil d'Ariane simple et standardisé.
 *
 * Usage :
 *  ```html
 *  <ft-breadcrumb [items]="[
 *    {label: 'Plateforme', icon: 'pi-home', routerLink: '/'},
 *    {label: 'Entreprises'}
 *  ]" />
 *  ```
 */
@Component({
  selector: 'ft-breadcrumb',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <nav class="bc" aria-label="Fil d’Ariane">
      <ol class="bc__list">
        @for (item of items; track item.label; let last = $last) {
          <li class="bc__item">
            @if (item.icon) {
              <i class="pi {{ item.icon }} bc__icon" aria-hidden="true"></i>
            }
            @if (item.routerLink && !last) {
              <a [routerLink]="item.routerLink" class="bc__link">{{ item.label }}</a>
            } @else {
              <span class="bc__current" [attr.aria-current]="last ? 'page' : null">
                {{ item.label }}
              </span>
            }
            @if (!last) {
              <span class="bc__sep" aria-hidden="true">›</span>
            }
          </li>
        }
      </ol>
    </nav>
  `,
  styles: [
    `
      :host {
        display: inline-block;
      }

      .bc__list {
        display: flex;
        align-items: center;
        gap: 0.4rem;
        margin: 0;
        padding: 0;
        list-style: none;
        font-size: 0.78rem;
        color: var(--ft-text-muted);
      }

      .bc__item {
        display: inline-flex;
        align-items: center;
        gap: 0.4rem;
      }

      .bc__icon {
        font-size: 0.85rem;
      }

      .bc__link {
        color: var(--ft-text-muted);
        text-decoration: none;
        transition: color var(--duration-fast) var(--easing-standard);
      }

      .bc__link:hover {
        color: var(--ft-accent);
        text-decoration: underline;
      }

      .bc__current {
        color: var(--ft-text);
        font-weight: 500;
      }

      .bc__sep {
        color: var(--ft-text-subtle);
        font-size: 0.95rem;
        line-height: 1;
      }
    `
  ]
})
export class FtBreadcrumbComponent {
  @Input({ required: true }) items: FtBreadcrumbItem[] = [];
}
