import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { InputTextModule } from 'primeng/inputtext';
import { CheckboxModule } from 'primeng/checkbox';
import { FirmActivityCode } from '@core/services/firm-governance.service';
import { FirmClientDossier } from '@core/services/firm-assignment.service';
import { WORK_LOCATION_OPTIONS } from './time-sheet-work-location';

@Component({
  selector: 'app-time-sheet-entry-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    DialogModule,
    ButtonModule,
    SelectModule,
    InputTextModule,
    CheckboxModule
  ],
  template: `
    <p-dialog
      [header]="editing ? 'Modifier le temps' : 'Ajouter du temps'"
      [visible]="visible"
      (visibleChange)="visibleChange.emit($event)"
      [modal]="true"
      [draggable]="false"
      [style]="{ width: 'min(560px, 96vw)' }"
      [dismissableMask]="true"
      appendTo="body">
      <form [formGroup]="form" class="dialog-form" (ngSubmit)="submit.emit()">
        <label>Activité
          @if (activityCodes.length > 0) {
            <p-select
              formControlName="activityCode"
              [options]="activityCodes"
              optionLabel="label"
              optionValue="code"
              placeholder="Sélectionner"
              [showClear]="true"
              [filter]="true"
              filterBy="label,code"
              appendTo="body"
              (onChange)="activityCodeChange.emit($event.value)" />
          } @else {
            <input pInputText formControlName="activityCode" placeholder="COMPTA, REVUE…" />
          }
        </label>
        <label>Client
          <p-select
            formControlName="firmClientAssignmentId"
            [options]="clients"
            optionLabel="companyName"
            optionValue="assignmentId"
            placeholder="Sélectionner (optionnel)"
            [showClear]="true"
            [filter]="true"
            filterBy="companyName"
            appendTo="body" />
        </label>
        <label>Date
          <input pInputText type="date" formControlName="workDate" />
        </label>
        <div class="row">
          <label>Début
            <input pInputText type="time" formControlName="startTime" />
          </label>
          <label>Fin
            <input pInputText type="time" formControlName="endTime" />
          </label>
          <label>Heures
            <input
              pInputText
              type="number"
              step="0.25"
              min="0.25"
              max="24"
              formControlName="hours"
              [readonly]="hasTimeSlot"
              [attr.title]="hasTimeSlot ? 'Durée calculée depuis le créneau Début/Fin' : null" />
          </label>
        </div>
        <label>Notes
          <input pInputText formControlName="notes" />
        </label>
        <div class="row">
          <label>Lieu
            <p-select
              formControlName="workLocation"
              [options]="locationOptions"
              optionLabel="label"
              optionValue="value"
              placeholder="Sélectionner (optionnel)"
              [showClear]="true"
              appendTo="body" />
          </label>
          <label>Tags
            <input pInputText formControlName="tags" placeholder="urgent,client" />
          </label>
        </div>
        <label class="checkbox-row">
          <p-checkbox formControlName="isBillable" [binary]="true" inputId="dialog-billable" />
          <span>Facturable</span>
        </label>
      </form>
      <ng-template pTemplate="footer">
        <button type="button" pButton label="Annuler" class="p-button-text" (click)="cancel.emit()"></button>
        <button
          type="button"
          pButton
          [label]="editing ? 'Enregistrer' : 'Enregistrer'"
          icon="pi pi-check"
          [disabled]="locked || form.invalid"
          (click)="submit.emit()"></button>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .dialog-form {
      display: flex; flex-direction: column; gap: .75rem;
    }
    .dialog-form label {
      display: flex; flex-direction: column; gap: .3rem; font-size: .875rem;
    }
    .row {
      display: grid; grid-template-columns: repeat(auto-fit, minmax(120px, 1fr)); gap: .75rem;
    }
    .checkbox-row {
      flex-direction: row !important; align-items: center; gap: .5rem;
    }
    input[readonly] {
      background: var(--color-surface-muted, #f1f5f9);
      cursor: default;
    }
  `]
})
export class TimeSheetEntryDialogComponent {
  @Input() visible = false;
  @Input({ required: true }) form!: FormGroup;
  @Input() clients: FirmClientDossier[] = [];
  @Input() activityCodes: FirmActivityCode[] = [];
  @Input() editing = false;
  @Input() locked = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() submit = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();
  @Output() activityCodeChange = new EventEmitter<string | null>();

  readonly locationOptions = WORK_LOCATION_OPTIONS;

  get hasTimeSlot(): boolean {
    const start = (this.form?.value?.startTime ?? '').toString().trim();
    const end = (this.form?.value?.endTime ?? '').toString().trim();
    return !!start && !!end;
  }
}
