import { Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { InputTextModule } from 'primeng/inputtext';
import { DatePickerModule } from 'primeng/datepicker';
import { CheckboxModule } from 'primeng/checkbox';
import { AuthService } from '@core/services/auth.service';
import { ToastService } from '@core/services/toast.service';
import { FirmCollaboratorsService } from '@core/services/firm-collaborators.service';
import { FirmLeavesService } from './data-access/firm-leaves.service';
import { FIRM_LEAVE_DAY_UNITS, FirmLeaveRequest, FirmLeaveType } from './data-access/firm-leaves.models';

@Component({
  selector: 'app-firm-leave-request-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, DialogModule, ButtonModule,
    SelectModule, InputTextModule, DatePickerModule, CheckboxModule
  ],
  template: `
    <p-dialog
      [header]="editRequest ? 'Modifier la demande' : 'Nouvelle demande'"
      [(visible)]="visible"
      (visibleChange)="visibleChange.emit($event)"
      [modal]="true"
      [style]="{ width: '560px' }"
      appendTo="body">
      <form [formGroup]="form" class="dlg-form">
        @if (isManager()) {
          <label>Collaborateur
            <p-select formControlName="userId" [options]="collaborators()" optionLabel="label" optionValue="value"
              placeholder="Moi-même par défaut" [showClear]="true" [filter]="true" appendTo="body" />
          </label>
        }
        <label>Type d'absence *
          <p-select formControlName="leaveTypeId" [options]="types" optionLabel="label" optionValue="id"
            placeholder="Sélectionner" appendTo="body" />
        </label>
        <div class="row2">
          <label>Date début *
            <p-datepicker formControlName="startDate" dateFormat="dd/mm/yy" [showIcon]="true" appendTo="body" (onSelect)="recomputeDays()" />
          </label>
          <label>Fin de journée
            <p-select formControlName="startUnit" [options]="units" optionLabel="label" optionValue="value" appendTo="body" (onChange)="recomputeDays()" />
          </label>
        </div>
        <div class="row2">
          <label>Date fin *
            <p-datepicker formControlName="endDate" dateFormat="dd/mm/yy" [showIcon]="true" appendTo="body" (onSelect)="recomputeDays()" />
          </label>
          <label>Fin de journée
            <p-select formControlName="endUnit" [options]="units" optionLabel="label" optionValue="value" appendTo="body" (onChange)="recomputeDays()" />
          </label>
        </div>
        <label>Jours ouvrés
          <input pInputText [value]="computedDays()" readonly />
        </label>
        <label>Motif
          <input pInputText formControlName="reason" maxlength="500" />
        </label>
        @if (!editRequest) {
          <label class="chk"><p-checkbox formControlName="submitImmediately" [binary]="true" inputId="submitNow" />
            <span>Soumettre immédiatement</span></label>
        }
      </form>
      <ng-template pTemplate="footer">
        <button type="button" pButton label="Annuler" class="p-button-text" (click)="close()"></button>
        <button type="button" pButton label="Enregistrer" [loading]="saving()" (click)="save()"></button>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .dlg-form { display: grid; gap: .75rem; }
    .dlg-form label { display: grid; gap: .35rem; font-size: .875rem; }
    .row2 { display: grid; grid-template-columns: 1fr 1fr; gap: .75rem; }
    .chk { display: flex !important; flex-direction: row !important; align-items: center; gap: .5rem; }
  `]
})
export class FirmLeaveRequestDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() types: FirmLeaveType[] = [];
  @Input() editRequest: FirmLeaveRequest | null = null;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() saved = new EventEmitter<void>();

  private readonly fb = inject(FormBuilder);
  private readonly api = inject(FirmLeavesService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);
  private readonly collabApi = inject(FirmCollaboratorsService);

  readonly isManager = this.auth.isFirmManager;
  readonly units = FIRM_LEAVE_DAY_UNITS;
  readonly saving = signal(false);
  readonly computedDays = signal(0);
  readonly collaborators = signal<{ label: string; value: string }[]>([]);

  readonly form = this.fb.nonNullable.group({
    userId: ['' as string],
    leaveTypeId: ['', Validators.required],
    startDate: [null as Date | null, Validators.required],
    endDate: [null as Date | null, Validators.required],
    startUnit: [0],
    endUnit: [0],
    reason: [''],
    submitImmediately: [true]
  });

  ngOnChanges(): void {
    if (this.visible) {
      this.loadCollaborators();
      if (this.editRequest) {
        this.form.patchValue({
          userId: this.editRequest.userId,
          leaveTypeId: this.editRequest.leaveTypeId,
          startDate: new Date(this.editRequest.startDate),
          endDate: new Date(this.editRequest.endDate),
          startUnit: this.editRequest.startUnit,
          endUnit: this.editRequest.endUnit,
          reason: this.editRequest.reason ?? '',
          submitImmediately: false
        });
        this.computedDays.set(this.editRequest.days);
      } else {
        this.form.reset({
          userId: '',
          leaveTypeId: this.types[0]?.id ?? '',
          startDate: null,
          endDate: null,
          startUnit: 0,
          endUnit: 0,
          reason: '',
          submitImmediately: true
        });
        this.computedDays.set(0);
      }
    }
  }

  recomputeDays(): void {
    const v = this.form.getRawValue();
    if (!v.startDate || !v.endDate) return;
    this.api.computeDays({
      startDate: this.toIso(v.startDate),
      endDate: this.toIso(v.endDate),
      startUnit: v.startUnit,
      endUnit: v.endUnit
    }).subscribe({
      next: res => this.computedDays.set(res.data?.days ?? 0),
      error: () => this.computedDays.set(0)
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    const body = {
      leaveTypeId: v.leaveTypeId,
      startDate: this.toIso(v.startDate!),
      endDate: this.toIso(v.endDate!),
      startUnit: v.startUnit,
      endUnit: v.endUnit,
      reason: v.reason || undefined
    };

    const req$ = this.editRequest
      ? this.api.update(this.editRequest.id, body)
      : this.api.create({
          ...body,
          userId: v.userId || undefined,
          submitImmediately: v.submitImmediately
        });

    req$.subscribe({
      next: res => {
        this.saving.set(false);
        if (!res.success) {
          this.toast.add({ severity: 'error', summary: 'Erreur', detail: res.message || 'Erreur' });
          return;
        }
        this.toast.add({
          severity: 'success',
          summary: 'Congés',
          detail: this.editRequest ? 'Demande mise à jour' : 'Demande créée'
        });
        this.saved.emit();
        this.close();
      },
      error: err => {
        this.saving.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Erreur',
          detail: err?.error?.message || 'Erreur lors de l’enregistrement'
        });
      }
    });
  }

  close(): void {
    this.visible = false;
    this.visibleChange.emit(false);
  }

  private loadCollaborators(): void {
    if (!this.isManager()) return;
    this.collabApi.list({ isActive: true }).subscribe({
      next: list => {
        this.collaborators.set(list.map(u => ({
          label: `${u.firstName} ${u.lastName}`.trim(),
          value: u.id
        })));
      }
    });
  }

  private toIso(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }
}
