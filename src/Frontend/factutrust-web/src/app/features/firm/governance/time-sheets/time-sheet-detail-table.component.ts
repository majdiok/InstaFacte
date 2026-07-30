import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { SelectModule } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { FirmActivityCode, FirmTimeSheetEntry } from '@core/services/firm-governance.service';
import { FirmClientDossier } from '@core/services/firm-assignment.service';

@Component({
  selector: 'app-time-sheet-detail-table',
  standalone: true,
  imports: [
    CommonModule, FormsModule, TableModule, ButtonModule, TagModule,
    SelectModule, CheckboxModule, InputTextModule
  ],
  template: `
    <div class="fc-card">
      <h3>Détail des temps saisis</h3>
      @if (activityCodes.length === 0) {
        <p class="activity-hint">Aucun type d'activité configuré. Un manager peut les ajouter depuis Paramètres cabinet.</p>
      }
      <p-table
        [value]="entries"
        [(selection)]="selection"
        (selectionChange)="selectionChange.emit($event)"
        dataKey="id"
        [loading]="loading"
        [paginator]="true"
        [rows]="20"
        styleClass="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th style="width: 3rem"><p-tableHeaderCheckbox /></th>
            <th>Date</th>
            <th>Dossier / Client</th>
            <th>Activité</th>
            <th>Description</th>
            <th>Heures</th>
            <th>Taux facturable</th>
            <th>Facturable</th>
            <th>Tags</th>
            <th>Lieu</th>
            <th>Statut</th>
            <th></th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-row>
          <tr [class.readonly]="!canEdit(row)">
            <td><p-tableCheckbox [value]="row" /></td>
            <td>
              @if (canEdit(row)) {
                <input pInputText type="date" [ngModel]="row.workDate.slice(0,10)" (ngModelChange)="emitPatch(row, { workDate: $event })" />
              } @else {
                {{ row.workDate | date:'EEE dd/MM' }}
              }
            </td>
            <td>
              @if (canEdit(row)) {
                <p-select
                  [options]="clientOptions"
                  optionLabel="label"
                  optionValue="value"
                  [ngModel]="row.firmClientAssignmentId"
                  (ngModelChange)="emitPatch(row, { firmClientAssignmentId: $event })"
                  [showClear]="true"
                  placeholder="—"
                  styleClass="cell-select" />
              } @else {
                {{ row.clientCompanyName || '—' }}
              }
            </td>
            <td>
              @if (canEdit(row)) {
                <p-select
                  [options]="activityOptions"
                  optionLabel="label"
                  optionValue="value"
                  [ngModel]="row.activityCode"
                  (ngModelChange)="emitPatch(row, { activityCode: $event })"
                  [showClear]="true"
                  placeholder="—"
                  [emptyMessage]="'Aucun type d\\'activité configuré'"
                  styleClass="cell-select" />
              } @else {
                {{ row.activityCode || '—' }}
              }
            </td>
            <td>
              @if (canEdit(row)) {
                <input pInputText [ngModel]="row.notes" (ngModelChange)="emitPatch(row, { notes: $event })" />
              } @else {
                <span class="notes">{{ row.notes || '—' }}</span>
              }
            </td>
            <td>
              @if (canEdit(row) && !(row.startTime && row.endTime)) {
                <input pInputText type="number" step="0.25" min="0.25" [ngModel]="row.hours" (ngModelChange)="emitPatch(row, { hours: +$event })" style="width:4.5rem" />
              } @else if (row.startTime && row.endTime) {
                {{ row.startTime }}–{{ row.endTime }}
                <small>({{ row.hours | number:'1.2-2' }} h)</small>
              } @else {
                {{ row.hours | number:'1.2-2' }} h
              }
            </td>
            <td>{{ row.isBillable ? '100 %' : '0 %' }}</td>
            <td>
              @if (canEdit(row)) {
                <p-checkbox [binary]="true" [ngModel]="row.isBillable" (ngModelChange)="emitPatch(row, { isBillable: $event })" />
              } @else {
                {{ row.isBillable ? 'Oui' : 'Non' }}
              }
            </td>
            <td>
              @if (canEdit(row)) {
                <input pInputText [ngModel]="row.tags" (ngModelChange)="emitPatch(row, { tags: $event })" style="width:6rem" />
              } @else {
                {{ row.tags || '—' }}
              }
            </td>
            <td>
              <span class="lieu"><i class="pi" [ngClass]="locationIcon(row.workLocation)"></i> {{ locationLabel(row.workLocation) }}</span>
            </td>
            <td>
              <p-tag [value]="row.statusDisplay || statusLabel(row)" [severity]="severity(row)" />
            </td>
            <td class="acts">
              @if (canEdit(row) && !periodLocked) {
                <button type="button" pButton icon="pi pi-play" class="p-button-text p-button-sm" title="Chronomètre" (click)="startTimer.emit(row)"></button>
                <button type="button" pButton icon="pi pi-copy" class="p-button-text p-button-sm" title="Dupliquer J+1" (click)="duplicate.emit(row)"></button>
                <button type="button" pButton icon="pi pi-pencil" class="p-button-text p-button-sm" (click)="edit.emit(row)"></button>
                <button type="button" pButton icon="pi pi-trash" class="p-button-text p-button-danger p-button-sm" (click)="remove.emit(row)"></button>
                @if ((row.status ?? 0) === 0) {
                  <button type="button" pButton icon="pi pi-send" class="p-button-text p-button-sm" (click)="submitOne.emit(row)"></button>
                }
              }
              @if (isManager && !periodLocked) {
                @if (!row.isValidated) {
                  <button type="button" pButton icon="pi pi-check" class="p-button-text p-button-sm" (click)="validate.emit(row)"></button>
                } @else {
                  <button type="button" pButton icon="pi pi-replay" class="p-button-text p-button-sm" (click)="unvalidate.emit(row)"></button>
                }
              }
            </td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr><td colspan="12">Aucune saisie.</td></tr>
        </ng-template>
      </p-table>
    </div>
  `,
  styles: [`
    .fc-card {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border-subtle, #e2e8f0);
      border-radius: var(--radius-xl, 16px);
      padding: .75rem; margin-bottom: 1rem;
    }
    h3 { margin: 0 0 .5rem; font-size: .95rem; }
    .activity-hint { margin: 0 0 .5rem; color: var(--color-text-secondary, #64748b); font-size: .85rem; }
    .notes { max-width: 140px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; display: inline-block; }
    .acts { white-space: nowrap; }
    .lieu { display: inline-flex; gap: .3rem; align-items: center; font-size: .85rem; }
    tr.readonly { opacity: .94; }
    :host ::ng-deep .cell-select { min-width: 110px; }
  `]
})
export class TimeSheetDetailTableComponent {
  @Input() entries: FirmTimeSheetEntry[] = [];
  @Input() clients: FirmClientDossier[] = [];
  @Input() activityCodes: FirmActivityCode[] = [];
  @Input() loading = false;
  @Input() isManager = false;
  @Input() periodLocked = false;
  @Input() selection: FirmTimeSheetEntry[] = [];
  @Output() selectionChange = new EventEmitter<FirmTimeSheetEntry[]>();
  @Output() edit = new EventEmitter<FirmTimeSheetEntry>();
  @Output() remove = new EventEmitter<FirmTimeSheetEntry>();
  @Output() duplicate = new EventEmitter<FirmTimeSheetEntry>();
  @Output() validate = new EventEmitter<FirmTimeSheetEntry>();
  @Output() unvalidate = new EventEmitter<FirmTimeSheetEntry>();
  @Output() submitOne = new EventEmitter<FirmTimeSheetEntry>();
  @Output() startTimer = new EventEmitter<FirmTimeSheetEntry>();
  @Output() patch = new EventEmitter<{ entry: FirmTimeSheetEntry; changes: Record<string, unknown> }>();

  get clientOptions() {
    return this.clients.map(c => ({ label: c.companyName, value: c.assignmentId }));
  }

  get activityOptions() {
    return this.activityCodes.map(a => ({ label: a.label, value: a.code }));
  }

  canEdit(row: FirmTimeSheetEntry): boolean {
    return !this.periodLocked && !row.isValidated && (row.status ?? 0) === 0;
  }

  emitPatch(row: FirmTimeSheetEntry, changes: Record<string, unknown>): void {
    this.patch.emit({ entry: row, changes });
  }

  statusLabel(row: FirmTimeSheetEntry): string {
    if (row.isValidated) return 'Validé';
    if (row.status === 1) return 'Soumis';
    return 'Brouillon';
  }

  severity(row: FirmTimeSheetEntry): 'success' | 'warn' | 'info' | 'secondary' {
    if (row.isValidated || row.status === 2) return 'success';
    if (row.status === 1) return 'warn';
    return 'secondary';
  }

  locationIcon(loc?: string): string {
    switch (loc) {
      case 'Remote': return 'pi-home';
      case 'Client': return 'pi-building';
      case 'Office': return 'pi-briefcase';
      default: return 'pi-map-marker';
    }
  }

  locationLabel(loc?: string): string {
    switch (loc) {
      case 'Remote': return 'Télétravail';
      case 'Client': return 'Client';
      case 'Office': return 'Bureau';
      case 'Other': return 'Autre';
      default: return '—';
    }
  }
}
