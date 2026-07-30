import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-time-sheet-quick-actions',
  standalone: true,
  imports: [CommonModule, ButtonModule],
  template: `
    <div class="actions">
      <button type="button" pButton label="Dupliquer la semaine" icon="pi pi-copy" class="p-button-outlined p-button-sm" [disabled]="locked" (click)="duplicateWeek.emit()"></button>
      <button type="button" pButton label="Exporter CSV" icon="pi pi-download" class="p-button-outlined p-button-sm" (click)="exportCsv.emit()"></button>
      <button type="button" pButton label="Soumettre pour validation" icon="pi pi-send" class="p-button-sm" [disabled]="locked || !draftCount" (click)="submitDrafts.emit()"></button>
      @if (isManager) {
        <button type="button" pButton label="Valider la sélection" icon="pi pi-check" class="p-button-sm" [disabled]="locked || bulkValidating || !selectionCount" [loading]="bulkValidating" (click)="validateSelection.emit()"></button>
      }
    </div>
  `,
  styles: [`
    .actions { display: flex; flex-wrap: wrap; gap: .5rem; margin-bottom: .75rem; }
  `]
})
export class TimeSheetQuickActionsComponent {
  @Input() locked = false;
  @Input() isManager = false;
  @Input() bulkValidating = false;
  @Input() draftCount = 0;
  @Input() selectionCount = 0;
  @Output() duplicateWeek = new EventEmitter<void>();
  @Output() exportCsv = new EventEmitter<void>();
  @Output() submitDrafts = new EventEmitter<void>();
  @Output() validateSelection = new EventEmitter<void>();
}
