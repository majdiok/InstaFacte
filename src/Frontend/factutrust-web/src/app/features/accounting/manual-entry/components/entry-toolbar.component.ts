import { Component, EventEmitter, Input, Output, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { EntryFormStore } from '../services/entry-form.store';
import { AuthService } from '@core/services/auth.service';
import { PERMISSIONS } from '@core/config/permission-keys';

@Component({
  selector: 'app-entry-toolbar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="entry-toolbar" role="toolbar" aria-label="Actions d'écriture">
      <div class="entry-toolbar__left">
        <div class="dropdown">
          <button type="button" class="btn btn-outline-secondary btn-sm dropdown-toggle"
                  (click)="toggleOptions()" [attr.aria-expanded]="optionsOpen">
            Options d'écriture
          </button>
          @if (optionsOpen) {
            <div class="dropdown-menu show" (click)="$event.stopPropagation()">
              <label class="dropdown-item-check">
                <input type="checkbox" [checked]="store.columnVisibility().piece"
                       (change)="toggleColumn('piece', $event)" />
                Colonne Pièce (aide à la saisie)
              </label>
              <label class="dropdown-item-check">
                <input type="checkbox" [checked]="store.columnVisibility().dueDate"
                       (change)="toggleColumn('dueDate', $event)" />
                Colonne Échéance (aide à la saisie)
              </label>
              <label class="dropdown-item-check">
                <input type="checkbox" [checked]="store.columnVisibility().lettering"
                       (change)="toggleColumn('lettering', $event)" />
                Colonne Lettrage (après enregistrement)
              </label>
              <label class="dropdown-item-check">
                <input type="checkbox" [checked]="store.columnVisibility().vat"
                       (change)="toggleColumn('vat', $event)" />
                Colonne TVA (aide à la saisie)
              </label>
              <div class="dropdown-divider"></div>
              <label class="dropdown-item-check">
                <input type="checkbox" [checked]="store.propagateLabelToLines()"
                       (change)="store.propagateLabelToLines.set($any($event.target).checked)" />
                Reporter le libellé sur les lignes vides
              </label>
              <button type="button" class="dropdown-item" (click)="reset.emit()">Réinitialiser le formulaire</button>
            </div>
          }
        </div>

        <div class="dropdown">
          <button type="button" class="btn btn-outline-secondary btn-sm dropdown-toggle"
                  (click)="toggleTemplate()" [attr.aria-expanded]="templateOpen">
            Modèle
          </button>
          @if (templateOpen) {
            <div class="dropdown-menu show">
              <button type="button" class="dropdown-item" (click)="loadTemplate.emit(); templateOpen = false">
                Charger un modèle
              </button>
              <button type="button" class="dropdown-item" (click)="saveTemplate.emit(); templateOpen = false"
                      [disabled]="!store.canSaveAsTemplate()">
                Sauvegarder comme modèle
              </button>
            </div>
          }
        </div>

        <a routerLink="/accounting/draft-batch" class="btn btn-outline-secondary btn-sm"
           title="Import d'écritures">
          Import
        </a>
      </div>

      <div class="entry-toolbar__right">
        <div class="btn-group">
          <button type="button" class="btn btn-primary"
                  (click)="save.emit('navigate')"
                  [disabled]="!canSave()">
            {{ store.loading() ? 'Enregistrement…' : 'Enregistrer' }}
          </button>
          <button type="button" class="btn btn-primary dropdown-toggle dropdown-toggle-split"
                  (click)="toggleSaveMenu()" [attr.aria-expanded]="saveMenuOpen"
                  [disabled]="!canSave()"></button>
          @if (saveMenuOpen) {
            <div class="dropdown-menu dropdown-menu-end show">
              <button type="button" class="dropdown-item" (click)="save.emit('navigate'); saveMenuOpen = false">
                Enregistrer
              </button>
              <button type="button" class="dropdown-item" (click)="save.emit('reset'); saveMenuOpen = false">
                Enregistrer & nouveau
              </button>
              <button type="button" class="dropdown-item" (click)="save.emit('duplicate'); saveMenuOpen = false">
                Enregistrer & dupliquer
              </button>
            </div>
          }
        </div>
      </div>
    </div>
  `,
  styles: `
    .entry-toolbar { display:flex; flex-wrap:wrap; justify-content:space-between; gap:var(--spacing-3); margin-bottom:var(--spacing-4); }
    .entry-toolbar__left, .entry-toolbar__right { display:flex; flex-wrap:wrap; gap:var(--spacing-2); align-items:center; }
    .dropdown { position:relative; }
    .dropdown-menu { position:absolute; top:100%; left:0; z-index:1050; min-width:14rem; background:var(--color-background-elevated); border:1px solid var(--color-border-default); border-radius:var(--radius-md); box-shadow:var(--shadow-md); padding:var(--spacing-2); }
    .dropdown-menu-end { right:0; left:auto; }
    .dropdown-item { display:block; width:100%; text-align:left; padding:var(--spacing-2) var(--spacing-3); border:none; background:transparent; cursor:pointer; font-size:var(--font-size-sm); border-radius:var(--radius-sm); }
    .dropdown-item:hover { background:var(--color-background-hover); }
    .dropdown-item-check { display:flex; align-items:center; gap:var(--spacing-2); padding:var(--spacing-2) var(--spacing-3); font-size:var(--font-size-sm); cursor:pointer; }
    .dropdown-divider { height:1px; background:var(--color-border-subtle); margin:var(--spacing-1) 0; }
    .btn-group { display:flex; position:relative; }
    .dropdown-toggle-split { padding-left:var(--spacing-2); padding-right:var(--spacing-2); }
  `,
  host: {
    '(document:click)': 'closeMenus()'
  }
})
export class EntryToolbarComponent {
  readonly store = inject(EntryFormStore);
  private readonly auth = inject(AuthService);

  @Output() save = new EventEmitter<'navigate' | 'reset' | 'duplicate'>();
  @Output() loadTemplate = new EventEmitter<void>();
  @Output() saveTemplate = new EventEmitter<void>();
  @Output() reset = new EventEmitter<void>();

  optionsOpen = false;
  templateOpen = false;
  saveMenuOpen = false;

  readonly canSave = computed(() =>
    this.store.canSubmit() && this.auth.hasPermission(PERMISSIONS.accounting.create)
  );

  toggleOptions(): void { this.optionsOpen = !this.optionsOpen; this.templateOpen = false; this.saveMenuOpen = false; }
  toggleTemplate(): void { this.templateOpen = !this.templateOpen; this.optionsOpen = false; this.saveMenuOpen = false; }
  toggleSaveMenu(): void { this.saveMenuOpen = !this.saveMenuOpen; this.optionsOpen = false; this.templateOpen = false; }

  closeMenus(): void {
    this.optionsOpen = false;
    this.templateOpen = false;
    this.saveMenuOpen = false;
  }

  toggleColumn(key: 'piece' | 'dueDate' | 'lettering' | 'vat', event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.store.columnVisibility.update(v => ({ ...v, [key]: checked }));
  }
}
