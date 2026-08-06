import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { CalendarModule } from 'primeng/calendar';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextarea } from 'primeng/inputtextarea';
import { MessageModule } from 'primeng/message';
import { ButtonComponent } from '@shared/components/button/button.component';
import { EmployeeService } from '@core/services/employee.service';
import { ToastService } from '@core/services/toast.service';

function toIsoDate(value: Date | null | undefined): string | undefined {
  if (!value) return undefined;
  const y = value.getFullYear();
  const m = String(value.getMonth() + 1).padStart(2, '0');
  const d = String(value.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

@Component({
  selector: 'app-employee-departure-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DialogModule,
    CalendarModule,
    CheckboxModule,
    InputTextarea,
    MessageModule,
    ButtonComponent
  ],
  template: `
    <p-dialog
      header="Déclarer un départ"
      [(visible)]="visible"
      [modal]="true"
      [style]="{ width: '480px' }"
      [draggable]="false"
      (onHide)="onHide()">
      <p-message
        severity="info"
        text="La date de sortie sera enregistrée sur le dossier salarié. Le contrat actif peut être clôturé à cette date. Pensez à lancer la régularisation IRPP (solde de tout compte) si l'option est activée."
        styleClass="mb-3 w-full" />

      <div class="payroll-form-group mb-2">
        <label>Date de sortie</label>
        <p-calendar [(ngModel)]="terminationDate" dateFormat="dd/mm/yy" [showIcon]="true" appendTo="body" styleClass="w-full" [maxDate]="today" />
      </div>
      <div class="payroll-form-group mb-2">
        <label>Motif</label>
        <textarea pInputTextarea [(ngModel)]="reason" rows="2" class="w-full" placeholder="Optionnel"></textarea>
      </div>
      <div class="payroll-form-group checkbox-row mb-2">
        <p-checkbox inputId="closeContract" [(ngModel)]="closeActiveContract" [binary]="true" />
        <label for="closeContract">Clôturer le contrat actif à la date de sortie</label>
      </div>

      <ng-template pTemplate="footer">
        <app-button variant="outline" (click)="visible = false">Annuler</app-button>
        <app-button variant="primary" (click)="submit()" [disabled]="saving || !terminationDate">Enregistrer</app-button>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .mb-2 { margin-bottom: var(--spacing-2); }
    .mb-3 { margin-bottom: var(--spacing-4); display: block; }
    .w-full { width: 100%; }
    .checkbox-row { display: flex; align-items: center; gap: var(--spacing-2); }
  `]
})
export class EmployeeDepartureDialogComponent {
  @Input({ required: true }) employeeId!: string;
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() completed = new EventEmitter<void>();

  private readonly employees = inject(EmployeeService);
  private readonly toast = inject(ToastService);

  terminationDate: Date | null = new Date();
  reason = '';
  closeActiveContract = true;
  saving = false;
  readonly today = new Date();

  onHide(): void {
    this.visibleChange.emit(false);
  }

  submit(): void {
    if (!this.terminationDate || this.saving) return;
    this.saving = true;
    this.employees.terminate(this.employeeId, {
      terminationDate: toIsoDate(this.terminationDate)!,
      reason: this.reason || undefined,
      closeActiveContract: this.closeActiveContract,
      deactivateNow: true
    }).subscribe({
      next: res => {
        this.saving = false;
        const warning = res.data?.warning;
        this.toast.add({
          severity: 'success',
          summary: 'Départ',
          detail: warning ?? 'Départ enregistré.',
          life: warning ? 10000 : 3000
        });
        this.visible = false;
        this.visibleChange.emit(false);
        this.completed.emit();
      },
      error: err => {
        this.saving = false;
        this.toast.add({
          severity: 'error',
          summary: 'Départ',
          detail: err.error?.message ?? 'Enregistrement impossible.'
        });
      }
    });
  }
}
