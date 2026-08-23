import { ChangeDetectionStrategy, Component, Input, OnDestroy, ViewChild } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { MenuModule } from 'primeng/menu';
import { Menu } from 'primeng/menu';
import { MenuItem } from 'primeng/api';

/**
 * Menu kebab pour actions par ligne de tableau.
 * Utilise `p-menu` en mode popup avec `appendTo="body"` pour éviter le clipping
 * des tableaux scrollables ; `hide()` à la destruction pour ne pas laisser
 * d'overlay orphelin dans le document.
 *
 * Usage :
 *  ```html
 *  <ft-cell-actions-menu [items]="rowActionsById().get(row.id) ?? []" />
 *  ```
 */
@Component({
  selector: 'ft-cell-actions-menu',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonModule, MenuModule],
  template: `
    <p-menu #menu [model]="items" [popup]="true" appendTo="body" [baseZIndex]="1000" />
    <p-button
      icon="pi pi-ellipsis-h"
      [text]="true"
      [rounded]="true"
      severity="secondary"
      ariaLabel="Plus d'actions"
      (onClick)="onToggleClick($event)"
    />
  `,
  styles: [
    `
      :host {
        display: inline-flex;
        justify-content: flex-end;
      }
    `
  ]
})
export class FtCellActionsMenuComponent implements OnDestroy {
  @Input({ required: true }) items: MenuItem[] = [];
  @ViewChild('menu') menu?: Menu;

  ngOnDestroy(): void {
    this.menu?.hide();
  }

  onToggleClick(event: Event): void {
    event.stopPropagation();
    this.menu?.toggle(event);
  }
}
