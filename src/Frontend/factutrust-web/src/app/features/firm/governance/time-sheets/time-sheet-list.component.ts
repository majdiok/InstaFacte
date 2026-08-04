import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { FirmTimeSheetEntry } from '@core/services/firm-governance.service';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';

@Component({
  selector: 'app-time-sheet-list',
  standalone: true,
  imports: [CommonModule, TableModule, ButtonModule, TagModule, EmptyStateComponent],
  template: `
    <div class="fc-card">
      @if (!loading && entries.length === 0) {
        <app-empty-state title="Aucune feuille de temps" description="Saisissez vos heures pour la période sélectionnée." icon="pi pi-clock" />
      } @else {
        @if (isManager && selection.length > 0) {
          <div class="bulk-bar">
            <span>{{ selection.length }} sélectionnée(s)</span>
            <button type="button" pButton label="Valider la sélection" icon="pi pi-check" class="p-button-sm p-button-outlined" [loading]="bulkValidating" (click)="validateSelection.emit()"></button>
          </div>
        }
        <p-table [value]="entries" [loading]="loading" [selection]="selection" (selectionChange)="selectionChange.emit($event)" dataKey="id">
          <ng-template pTemplate="header">
            <tr>
              @if (isManager) { <th style="width: 3rem"><p-tableHeaderCheckbox></p-tableHeaderCheckbox></th> }
              <th>Date</th>
              <th>Collaborateur</th>
              <th>Client</th>
              <th>Heures</th>
              <th>Activité</th>
              <th>Facturable</th>
              <th>Statut</th>
              <th>Validée par</th>
              <th style="width: 220px">Actions</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-e>
            <tr>
              @if (isManager) {
                <td>@if ((e.status ?? 0) === 1) { <p-tableCheckbox [value]="e"></p-tableCheckbox> }</td>
              }
              <td>{{ e.workDate | date:'shortDate' }}</td>
              <td>{{ e.userDisplayName }}</td>
              <td>{{ e.clientCompanyName || '—' }}</td>
              <td>{{ e.hours | number:'1.2-2' }}</td>
              <td>{{ e.activityCode || '—' }}</td>
              <td>{{ e.isBillable ? 'Oui' : 'Non' }}</td>
              <td><p-tag [severity]="statusSeverity(e)" [value]="statusLabel(e)" /></td>
              <td class="trace">
                @if (e.isValidated) {
                  {{ e.validatedByDisplayName || '—' }}
                  <small>{{ e.validatedAt | date:'short' }}</small>
                } @else { — }
              </td>
              <td class="actions">
                @if ((e.status ?? 0) === 0) {
                  <button type="button" pButton icon="pi pi-clone" class="p-button-text p-button-sm" [disabled]="periodLocked" (click)="duplicate.emit(e)" title="Dupliquer au jour suivant"></button>
                  <button type="button" pButton icon="pi pi-pencil" class="p-button-text p-button-sm" [disabled]="periodLocked" (click)="edit.emit(e)" title="Modifier"></button>
                  <button type="button" pButton icon="pi pi-trash" class="p-button-text p-button-danger p-button-sm" [disabled]="periodLocked" (click)="remove.emit(e)" title="Supprimer"></button>
                }
                @if (isManager && (e.status ?? 0) === 1) {
                  <button type="button" pButton icon="pi pi-check" class="p-button-text p-button-success p-button-sm" [disabled]="periodLocked" (click)="validate.emit(e)" title="Valider"></button>
                }
                @if (isManager && e.isValidated) {
                  <button type="button" pButton icon="pi pi-undo" class="p-button-text p-button-sm" [disabled]="periodLocked" (click)="unvalidate.emit(e)" title="Repasser en brouillon"></button>
                }
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>
  `,
  styles: [`
    .fc-card {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border-subtle, #e2e8f0);
      border-radius: var(--radius-xl, 16px);
      box-shadow: var(--shadow-soft-sm, 0 1px 2px rgba(15, 23, 42, 0.05));
      padding: var(--spacing-3, 12px);
      margin-bottom: 1rem;
    }
    .bulk-bar { display: flex; align-items: center; gap: .75rem; padding-bottom: .75rem; font-size: .875rem; }
    .trace { font-size: .8125rem; }
    .trace small { display: block; color: var(--color-text-muted, #64748b); }
    .actions { display: flex; gap: .1rem; }
  `]
})
export class TimeSheetListComponent {
  @Input() entries: FirmTimeSheetEntry[] = [];
  @Input() loading = false;
  @Input() isManager = false;
  @Input() bulkValidating = false;
  @Input() periodLocked = false;
  @Input() selection: FirmTimeSheetEntry[] = [];
  @Output() selectionChange = new EventEmitter<FirmTimeSheetEntry[]>();
  @Output() validateSelection = new EventEmitter<void>();
  @Output() edit = new EventEmitter<FirmTimeSheetEntry>();
  @Output() remove = new EventEmitter<FirmTimeSheetEntry>();
  @Output() validate = new EventEmitter<FirmTimeSheetEntry>();
  @Output() unvalidate = new EventEmitter<FirmTimeSheetEntry>();
  @Output() duplicate = new EventEmitter<FirmTimeSheetEntry>();

  statusLabel(e: FirmTimeSheetEntry): string {
    return e.statusDisplay || (e.isValidated ? 'Validée' : (e.status === 1 ? 'Soumis' : 'Brouillon'));
  }

  statusSeverity(e: FirmTimeSheetEntry): 'success' | 'warn' | 'info' | 'secondary' {
    if (e.isValidated || e.status === 2) return 'success';
    if (e.status === 1) return 'warn';
    return 'secondary';
  }
}
