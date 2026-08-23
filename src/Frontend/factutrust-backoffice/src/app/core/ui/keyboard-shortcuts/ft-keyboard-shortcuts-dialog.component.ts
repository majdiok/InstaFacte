import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';

export interface KeyboardShortcutRow {
  keys: string;
  description: string;
}

@Component({
  selector: 'ft-keyboard-shortcuts-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DialogModule, ButtonModule],
  template: `
    <p-dialog
      [visible]="visible"
      (visibleChange)="onVisibleChange($event)"
      [modal]="true"
      [draggable]="false"
      [resizable]="false"
      header="Raccourcis clavier"
      styleClass="ft-shortcuts-dialog"
      [style]="{ width: '28rem', maxWidth: '95vw' }"
    >
      <ul class="shortcuts-list">
        @for (row of shortcuts; track row.keys) {
          <li class="shortcuts-row">
            <kbd class="shortcuts-keys">{{ row.keys }}</kbd>
            <span class="shortcuts-desc">{{ row.description }}</span>
          </li>
        }
      </ul>
      <ng-template pTemplate="footer">
        <p-button label="Fermer" severity="secondary" (onClick)="onVisibleChange(false)" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      .shortcuts-list {
        list-style: none;
        margin: 0;
        padding: 0;
        display: flex;
        flex-direction: column;
        gap: 0.65rem;
      }

      .shortcuts-row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 1rem;
      }

      .shortcuts-keys {
        font-family: ui-monospace, SFMono-Regular, monospace;
        font-size: 0.78rem;
        padding: 0.2rem 0.45rem;
        border-radius: 6px;
        background: var(--ft-surface-3);
        border: 1px solid var(--ft-border);
        color: var(--ft-text);
        white-space: nowrap;
      }

      .shortcuts-desc {
        font-size: 0.88rem;
        color: var(--ft-text-muted);
        text-align: right;
      }
    `
  ]
})
export class FtKeyboardShortcutsDialogComponent {
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();

  readonly shortcuts: KeyboardShortcutRow[] = [
    { keys: '?', description: 'Afficher cette aide' },
    { keys: 'Échap', description: 'Fermer un drawer ou une modale' },
    { keys: '⌘K', description: 'Recherche globale (bientôt)' }
  ];

  onVisibleChange(value: boolean): void {
    this.visible = value;
    this.visibleChange.emit(value);
  }
}
