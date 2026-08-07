import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormGroup, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { InputTextModule } from 'primeng/inputtext';
import { CheckboxModule } from 'primeng/checkbox';
import { FirmActivityCode } from '@core/services/firm-governance.service';
import { FirmClientDossier } from '@core/services/firm-assignment.service';
import { WORK_LOCATION_OPTIONS } from './time-sheet-work-location';

@Component({
  selector: 'app-time-sheet-entry-form',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    ButtonModule,
    SelectModule,
    InputTextModule,
    CheckboxModule
  ],
  template: `
    <div class="fc-card entry-card">
      <form [formGroup]="form" (ngSubmit)="submit.emit()" class="entry-form">
        <label>Date
          <input pInputText type="date" formControlName="workDate" (keydown.enter)="handleEnter($event)" />
        </label>
        @if (rich) {
          <label>Début
            <input pInputText type="time" formControlName="startTime" (keydown.enter)="handleEnter($event)" />
          </label>
          <label>Fin
            <input pInputText type="time" formControlName="endTime" (keydown.enter)="handleEnter($event)" />
          </label>
        }
        <label>Heures
          <input
            pInputText
            type="number"
            step="0.25"
            min="0.25"
            max="24"
            formControlName="hours"
            [readonly]="rich && hasTimeSlot"
            [attr.title]="rich && hasTimeSlot ? 'Durée calculée depuis le créneau Début/Fin' : null"
            (keydown.enter)="handleEnter($event)" />
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
        <label>Code activité
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
            <input pInputText formControlName="activityCode" placeholder="COMPTA, REVUE…" (keydown.enter)="handleEnter($event)" />
          }
        </label>
        <label>Notes
          <input pInputText formControlName="notes" (keydown.enter)="handleEnter($event)" />
        </label>
        @if (rich) {
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
            <input pInputText formControlName="tags" placeholder="urgent,client" (keydown.enter)="handleEnter($event)" />
          </label>
        }
        <label class="checkbox-row">
          <p-checkbox formControlName="isBillable" [binary]="true" inputId="billable" />
          <span>Facturable</span>
        </label>
        <button type="submit" pButton [label]="editing ? 'Enregistrer' : 'Ajouter'" [icon]="editing ? 'pi pi-check' : 'pi pi-plus'" [disabled]="locked"></button>
        @if (editing) {
          <button type="button" pButton label="Annuler" class="p-button-text" (click)="cancel.emit()"></button>
        }
      </form>
      <p class="summary">
        Total période : <strong>{{ totalHours | number:'1.2-2' }}</strong> h
        — facturables {{ billableHours | number:'1.2-2' }} h,
        non facturables {{ nonBillableHours | number:'1.2-2' }} h
        (taux de facturabilité {{ billableRatio | number:'1.0-1' }} %)
      </p>
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
    .entry-form { display: flex; flex-wrap: wrap; gap: .75rem; align-items: flex-end; }
    .entry-form label { display: flex; flex-direction: column; font-size: .875rem; gap: .25rem; min-width: 140px; }
    .checkbox-row { flex-direction: row !important; align-items: center; gap: .5rem; min-width: auto; }
    .summary { margin: .75rem 0 0; font-size: .875rem; color: var(--color-text-muted, #64748b); }
    input[readonly] {
      background: var(--color-surface-muted, #f1f5f9);
      cursor: default;
    }
  `]
})
export class TimeSheetEntryFormComponent {
  @Input({ required: true }) form!: FormGroup;
  @Input() clients: FirmClientDossier[] = [];
  @Input() activityCodes: FirmActivityCode[] = [];
  @Input() editing = false;
  @Input() locked = false;
  @Input() totalHours = 0;
  @Input() billableHours = 0;
  @Input() nonBillableHours = 0;
  @Input() billableRatio = 0;
  @Input() rich = false;
  @Output() submit = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();
  @Output() activityCodeChange = new EventEmitter<string | null>();

  readonly locationOptions = WORK_LOCATION_OPTIONS;

  get hasTimeSlot(): boolean {
    const start = (this.form?.value?.startTime ?? '').toString().trim();
    const end = (this.form?.value?.endTime ?? '').toString().trim();
    return !!start && !!end;
  }

  handleEnter(event: Event): void {
    event.preventDefault();
    this.submit.emit();
  }
}
