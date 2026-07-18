import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { MenuModule } from 'primeng/menu';
import { MenuItem } from 'primeng/api';
import { FISCAL_SCHEDULE_SHARED_STYLES } from './fiscal-schedule-shared.styles';

@Component({
  selector: 'app-fiscal-schedule-toolbar',
  standalone: true,
  imports: [CommonModule, MenuModule],
  template: `
    <div class="toolbar" role="toolbar" aria-label="Actions echeancier fiscal">
      <button class="icon-button primary" type="button" title="Creer" (click)="create.emit()" [disabled]="!canCreate">
        <i class="fa-solid fa-plus"></i><span>Creer</span>
      </button>
      <button class="icon-button" type="button" title="Modifier" (click)="edit.emit()" [disabled]="!canEdit || !hasSelection">
        <i class="fa-solid fa-pen"></i><span>Modifier</span>
      </button>
      <button class="icon-button danger" type="button" title="Supprimer" (click)="delete.emit()" [disabled]="!canDelete || !hasSelection">
        <i class="fa-solid fa-trash"></i><span>Supprimer</span>
      </button>
      <button class="icon-button" type="button" title="Actualiser" (click)="refresh.emit()">
        <i class="fa-solid fa-rotate"></i><span>Actualiser</span>
      </button>
      <div class="menu-wrap">
        <button class="icon-button" type="button" title="Imprimer" (click)="printMenu.toggle($event)">
          <i class="fa-solid fa-print"></i><span>Imprimer</span><i class="fa-solid fa-caret-down caret"></i>
        </button>
        <p-menu #printMenu [popup]="true" [model]="printItems" appendTo="body"></p-menu>
      </div>
      <div class="menu-wrap">
        <button class="icon-button" type="button" title="Exporter" (click)="exportMenu.toggle($event)">
          <i class="fa-solid fa-file-export"></i><span>Exporter</span><i class="fa-solid fa-caret-down caret"></i>
        </button>
        <p-menu #exportMenu [popup]="true" [model]="exportItems" appendTo="body"></p-menu>
      </div>
      <button class="icon-button" type="button" title="Planifier les rappels" (click)="planReminder.emit()" [disabled]="!canCreate || !hasSelection">
        <i class="fa-regular fa-bell"></i><span>Planifier les rappels</span>
      </button>
    </div>
  `,
  styles: [FISCAL_SCHEDULE_SHARED_STYLES, `
    .toolbar {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-wrap: wrap;
    }
    .menu-wrap { position: relative; }
    .caret { font-size: 10px; opacity: .7; }
  `]
})
export class FiscalScheduleToolbarComponent implements OnChanges {
  @Input() canCreate = false;
  @Input() canEdit = false;
  @Input() canDelete = false;
  @Input() hasSelection = false;

  @Output() create = new EventEmitter<void>();
  @Output() edit = new EventEmitter<void>();
  @Output() delete = new EventEmitter<void>();
  @Output() refresh = new EventEmitter<void>();
  @Output() planReminder = new EventEmitter<void>();
  @Output() printCurrent = new EventEmitter<void>();
  @Output() printSelected = new EventEmitter<void>();
  @Output() exportFiltered = new EventEmitter<void>();
  @Output() exportPage = new EventEmitter<void>();

  printItems: MenuItem[] = [];
  exportItems: MenuItem[] = [];

  ngOnChanges(_changes: SimpleChanges): void {
    this.printItems = [
      { label: 'Vue courante', icon: 'fa-solid fa-table', command: () => this.printCurrent.emit() },
      { label: 'Echeance selectionnee', icon: 'fa-solid fa-file-lines', command: () => this.printSelected.emit(), disabled: !this.hasSelection }
    ];
    this.exportItems = [
      { label: 'CSV filtres courants', icon: 'fa-solid fa-filter', command: () => this.exportFiltered.emit() },
      { label: 'CSV page courante', icon: 'fa-solid fa-file-csv', command: () => this.exportPage.emit() }
    ];
  }
}
