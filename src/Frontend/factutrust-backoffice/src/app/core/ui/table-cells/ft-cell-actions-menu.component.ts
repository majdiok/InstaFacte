import { ChangeDetectionStrategy, Component, Input, ViewChild } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { MenuModule } from 'primeng/menu';
import { Menu } from 'primeng/menu';
import { MenuItem } from 'primeng/api';

/**
 * Menu kebab pour actions par ligne de tableau.
 * Utilise `p-menu` en mode popup. Le menu est attaché au conteneur du composant
 * pour éviter les fuites z-index.
 *
 * Usage :
 *  ```html
 *  <ft-cell-actions-menu [items]="actionsFor(row)" />
 *  ```
 */
@Component({
  selector: 'ft-cell-actions-menu',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonModule, MenuModule],
  template: `
    <p-menu #menu [model]="items" [popup]="true" appendTo="body" />
    <p-button
      icon="pi pi-ellipsis-h"
      [text]="true"
      [rounded]="true"
      severity="secondary"
      ariaLabel="Plus d'actions"
      (onClick)="menu.toggle($event)"
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
export class FtCellActionsMenuComponent {
  @Input({ required: true }) items: MenuItem[] = [];
  @ViewChild('menu') menu?: Menu;
}
