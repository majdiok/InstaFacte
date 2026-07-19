import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MenuModule } from 'primeng/menu';
import { MenuItem } from 'primeng/api';
import { AccountingExportFormat } from './accounting-download.util';

/**
 * Bouton « Exporter ▾ » réutilisable proposant PDF / Excel / CSV.
 * Émet le format choisi via (exportFormat) ; le composant hôte réalise l'appel réseau + le téléchargement.
 */
@Component({
  selector: 'app-accounting-export-menu',
  standalone: true,
  imports: [CommonModule, MenuModule],
  template: `
    <p-menu #menu [popup]="true" [model]="items" appendTo="body"></p-menu>
    <button
      type="button"
      class="acc-export-btn"
      (click)="menu.toggle($event)"
      [disabled]="disabled"
      aria-haspopup="true"
      [attr.aria-label]="label">
      <i class="fa-solid fa-download"></i>
      <span>{{ label }}</span>
      <i class="fa-solid fa-caret-down" aria-hidden="true"></i>
    </button>
  `,
  styles: `
    .acc-export-btn {
      display:inline-flex; align-items:center; gap:0.4rem;
      padding:0.45rem 0.85rem; border:1px solid var(--color-border-default, #cbd5e1);
      border-radius:var(--radius-md, 6px); background:var(--color-background-elevated, #fff);
      color:var(--color-text-default, #1e293b); font-size:0.875rem; cursor:pointer;
      transition:background .15s, border-color .15s;
    }
    .acc-export-btn:hover:not(:disabled) { background:var(--color-background-subtle, #f1f5f9); border-color:var(--color-primary-400, #60a5fa); }
    .acc-export-btn:disabled { opacity:.55; cursor:not-allowed; }
  `
})
export class AccountingExportMenuComponent {
  /** Désactive le bouton (chargement en cours, données vides, période invalide). */
  @Input() disabled = false;
  /** Libellé du bouton. */
  @Input() label = 'Exporter';

  /** Format choisi par l'utilisateur. */
  @Output() exportFormat = new EventEmitter<AccountingExportFormat>();

  readonly items: MenuItem[] = [
    { label: 'PDF', icon: 'fa-solid fa-file-pdf', command: () => this.exportFormat.emit('pdf') },
    { label: 'Excel', icon: 'fa-solid fa-file-excel', command: () => this.exportFormat.emit('excel') },
    { label: 'CSV', icon: 'fa-solid fa-file-csv', command: () => this.exportFormat.emit('csv') }
  ];
}
