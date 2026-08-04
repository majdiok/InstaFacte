import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SelectModule } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';
import { FirmActivityCode } from '@core/services/firm-governance.service';
import { FirmClientDossier } from '@core/services/firm-assignment.service';
import { WORK_LOCATION_OPTIONS } from './time-sheet-work-location';

export interface TimeSheetFilters {
  clientId: string | null;
  activityCode: string | null;
  accountantUserId: string | null;
  billableOnly: boolean | null;
  workLocation: string | null;
  showValidated: boolean;
}

@Component({
  selector: 'app-time-sheet-filters-bar',
  standalone: true,
  imports: [CommonModule, FormsModule, SelectModule, CheckboxModule],
  template: `
    <div class="filters">
      <p-select
        [options]="clientOptions"
        optionLabel="label"
        optionValue="value"
        [ngModel]="filters.clientId"
        (ngModelChange)="patch({ clientId: $event })"
        placeholder="Dossier / Client"
        [showClear]="true" />
      <p-select
        [options]="activityOptions"
        optionLabel="label"
        optionValue="value"
        [ngModel]="filters.activityCode"
        (ngModelChange)="patch({ activityCode: $event })"
        [placeholder]="activityCodes.length ? 'Type d\\'activité' : 'Aucun type d\\'activité configuré'"
        [emptyMessage]="'Aucun type d\\'activité configuré'"
        [showClear]="true" />
      <p-select
        [options]="accountantOptions"
        optionLabel="label"
        optionValue="id"
        [ngModel]="filters.accountantUserId"
        (ngModelChange)="patch({ accountantUserId: $event })"
        placeholder="Responsable du dossier"
        [showClear]="true" />
      <p-select
        [options]="billableOptions"
        optionLabel="label"
        optionValue="value"
        [ngModel]="filters.billableOnly"
        (ngModelChange)="patch({ billableOnly: $event })"
        placeholder="Taux facturable"
        [showClear]="true" />
      <p-select
        [options]="locationOptions"
        optionLabel="label"
        optionValue="value"
        [ngModel]="filters.workLocation"
        (ngModelChange)="patch({ workLocation: $event })"
        placeholder="Lieu de travail"
        [showClear]="true" />
      <label class="chk">
        <p-checkbox [binary]="true" [ngModel]="filters.showValidated" (ngModelChange)="patch({ showValidated: $event })" />
        Afficher temps validés
      </label>
    </div>
  `,
  styles: [`
    .filters {
      display: flex; flex-wrap: wrap; gap: .5rem; align-items: center;
      margin-bottom: .75rem;
    }
    .chk { display: inline-flex; gap: .4rem; align-items: center; font-size: .9rem; }
    :host ::ng-deep .p-select { min-width: 150px; }
  `]
})
export class TimeSheetFiltersBarComponent {
  @Input() filters: TimeSheetFilters = {
    clientId: null,
    activityCode: null,
    accountantUserId: null,
    billableOnly: null,
    workLocation: null,
    showValidated: true
  };
  @Input() clients: FirmClientDossier[] = [];
  @Input() activityCodes: FirmActivityCode[] = [];
  @Input() accountants: { id: string; label: string }[] = [];
  @Output() filtersChange = new EventEmitter<TimeSheetFilters>();

  readonly billableOptions = [
    { label: '100 % (facturable)', value: true },
    { label: '0 % (non facturable)', value: false }
  ];
  readonly locationOptions = WORK_LOCATION_OPTIONS;

  get clientOptions() {
    return this.clients.map(c => ({ label: c.companyName, value: c.assignmentId }));
  }

  get activityOptions() {
    return this.activityCodes.map(a => ({ label: `${a.code} — ${a.label}`, value: a.code }));
  }

  get accountantOptions() {
    return this.accountants;
  }

  patch(partial: Partial<TimeSheetFilters>): void {
    this.filtersChange.emit({ ...this.filters, ...partial });
  }
}
