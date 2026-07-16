import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputNumberModule } from 'primeng/inputnumber';
import { ButtonComponent } from '@shared/components/button/button.component';
import { EmployeeService, EmployeeListItem } from '@core/services/employee.service';
import { PayrollService, PayrollOvertimeLine, UpsertOvertimeLineRequest } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { OVERTIME_RATE_OPTIONS } from '../payroll-options';
import { PayrollSectionComponent, PayrollAmountPipe } from '../shared';

@Component({
  selector: 'app-payroll-overtime-grid',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    TagModule,
    DialogModule,
    DropdownModule,
    InputNumberModule,
    ButtonComponent,
    PayrollSectionComponent,
    PayrollAmountPipe
  ],
  template: `
    <app-payroll-section
      title="Éléments variables — Heures supplémentaires"
      subtitle="Montant = (salaire base ÷ 173,33) × heures × taux majoré. Override possible."
      icon="pi-clock">
      @if (!readOnly) {
        <div class="payroll-toolbar mb-3">
          <app-button variant="primary" icon="pi-plus" iconPos="left" (click)="openDialog()">Ajouter une ligne</app-button>
        </div>
      }

      <div class="payroll-table-scroll">
      <p-table [value]="lines()" styleClass="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>Salarié</th>
            <th class="text-right">Heures</th>
            <th class="text-right">Taux</th>
            <th class="text-right">Calculé</th>
            <th class="text-right">Montant appliqué</th>
            @if (!readOnly) { <th></th> }
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-line>
          <tr>
            <td>{{ line.employeeName }}</td>
            <td class="text-right">{{ line.hours }}</td>
            <td class="text-right">{{ line.ratePercentDisplay }}</td>
            <td class="text-right">{{ line.computedAmount | payrollAmount }}</td>
            <td class="text-right">
              {{ line.effectiveAmount | payrollAmount }}
              @if (line.isOverridden) {
                <p-tag value="Ajusté manuellement" severity="warning" class="ml-2" />
              }
            </td>
            @if (!readOnly) {
              <td class="actions">
                <button type="button" class="p-button p-button-text p-button-sm" (click)="editLine(line)">Modifier</button>
                <button type="button" class="p-button p-button-text p-button-danger p-button-sm" (click)="confirmDelete(line)">Supprimer</button>
              </td>
            }
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr><td [attr.colspan]="readOnly ? 5 : 6">Aucune heure supplémentaire saisie pour ce mois.</td></tr>
        </ng-template>
      </p-table>
      </div>
    </app-payroll-section>

    <p-dialog [header]="editingId ? 'Modifier heures sup.' : 'Nouvelles heures sup.'" [(visible)]="dialogVisible" [modal]="true" [style]="{ width: '480px' }">
      <div class="payroll-form-group mb-2">
        <label>Salarié</label>
        <p-dropdown [options]="employees()" optionLabel="fullName" optionValue="id" [(ngModel)]="formEmployeeId" (ngModelChange)="refreshPreview()" appendTo="body" styleClass="w-full" [disabled]="!!editingId" />
      </div>
      <div class="payroll-form-row">
        <div class="payroll-form-group">
          <label>Heures</label>
          <p-inputNumber [(ngModel)]="formHours" [min]="0.5" [step]="0.5" [locale]="'fr-TN'" (ngModelChange)="refreshPreview()" styleClass="w-full" />
        </div>
        <div class="payroll-form-group">
          <label>Taux majoré</label>
          <p-dropdown [options]="rateOptions" optionLabel="label" optionValue="value" [(ngModel)]="formRatePercent" (ngModelChange)="refreshPreview()" appendTo="body" styleClass="w-full" />
        </div>
      </div>
      <div class="payroll-form-group mb-2">
        <label>Montant override (optionnel)</label>
        <p-inputNumber [(ngModel)]="formOverride" [minFractionDigits]="3" [maxFractionDigits]="3" [locale]="'fr-TN'" (ngModelChange)="refreshPreview()" styleClass="w-full" />
      </div>
      <p class="payroll-info-text">Formule : (salaire base ÷ 173,33) × heures × (taux % ÷ 100)</p>
      @if (previewAmount() !== null) {
        <p>Montant appliqué : <strong>{{ previewAmount() | payrollAmount }}</strong></p>
      }
      <ng-template pTemplate="footer">
        <app-button variant="outline" (click)="dialogVisible = false">Annuler</app-button>
        <app-button variant="primary" (click)="save()">Enregistrer</app-button>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .mb-2 { margin-bottom: var(--spacing-2); }
    .mb-3 { margin-bottom: var(--spacing-4); }
    .ml-2 { margin-left: var(--spacing-2); }
    .actions { white-space: nowrap; }
    .w-full { width: 100%; }
  `]
})
export class PayrollOvertimeGridComponent implements OnInit {
  @Input({ required: true }) year!: number;
  @Input({ required: true }) month!: number;
  @Input() readOnly = false;
  @Input() initialLines: PayrollOvertimeLine[] = [];

  private readonly employeesApi = inject(EmployeeService);
  private readonly payroll = inject(PayrollService);
  private readonly toast = inject(ToastService);
  private readonly confirmation = inject(ConfirmationService);

  readonly rateOptions = OVERTIME_RATE_OPTIONS;
  lines = signal<PayrollOvertimeLine[]>([]);
  employees = signal<EmployeeListItem[]>([]);
  dialogVisible = false;
  editingId: string | null = null;
  formEmployeeId: string | null = null;
  formHours = 1;
  formRatePercent = 125;
  formOverride: number | null = null;
  previewAmount = signal<number | null>(null);

  ngOnInit(): void {
    this.lines.set(this.initialLines ?? []);
    this.loadEmployees();
  }

  reload(): void {
    this.payroll.listOvertime(this.year, this.month).subscribe({
      next: res => this.lines.set(res.data ?? []),
      error: () => this.toast.add({ severity: 'error', summary: 'Heures sup.', detail: 'Chargement impossible.' })
    });
  }

  private loadEmployees(): void {
    this.employeesApi.list(undefined, true, 1, 500).subscribe({
      next: res => this.employees.set(res.data?.items ?? []),
      error: () => {}
    });
  }

  openDialog(): void {
    if (this.readOnly) return;
    this.editingId = null;
    this.formEmployeeId = null;
    this.formHours = 1;
    this.formRatePercent = 125;
    this.formOverride = null;
    this.previewAmount.set(null);
    this.dialogVisible = true;
  }

  editLine(line: PayrollOvertimeLine): void {
    if (this.readOnly) return;
    this.editingId = line.id;
    this.formEmployeeId = line.employeeId;
    this.formHours = line.hours;
    this.formRatePercent = line.ratePercent;
    this.formOverride = line.overrideAmount ?? null;
    this.previewAmount.set(line.effectiveAmount);
    this.dialogVisible = true;
  }

  refreshPreview(): void {
    const employee = this.employees().find(e => e.id === this.formEmployeeId);
    const baseSalary = employee?.currentBaseSalary ?? 0;
    if (!baseSalary || this.formHours <= 0) {
      this.previewAmount.set(null);
      return;
    }
    this.payroll.previewOvertime({
      baseSalary,
      hours: this.formHours,
      ratePercent: this.formRatePercent,
      overrideAmount: this.formOverride ?? undefined
    }).subscribe({
      next: res => this.previewAmount.set(res.data?.effectiveAmount ?? null),
      error: () => this.previewAmount.set(null)
    });
  }

  save(): void {
    if (this.readOnly || !this.formEmployeeId) return;
    const body: UpsertOvertimeLineRequest = {
      employeeId: this.formEmployeeId,
      year: this.year,
      month: this.month,
      hours: this.formHours,
      ratePercent: this.formRatePercent,
      overrideAmount: this.formOverride ?? undefined
    };
    const req = this.editingId
      ? this.payroll.updateOvertime(this.editingId, body)
      : this.payroll.createOvertime(body);

    req.subscribe({
      next: () => {
        this.toast.add({ severity: 'success', summary: 'Heures sup.', detail: 'Enregistré.' });
        this.dialogVisible = false;
        this.reload();
      },
      error: err => this.toast.add({
        severity: 'error',
        summary: 'Heures sup.',
        detail: err?.error?.message ?? 'Enregistrement impossible.'
      })
    });
  }

  confirmDelete(line: PayrollOvertimeLine): void {
    if (this.readOnly) return;
    this.confirmation.confirm({
      message: `Supprimer les heures sup. de ${line.employeeName} ?`,
      header: 'Confirmation',
      acceptLabel: 'Supprimer',
      rejectLabel: 'Annuler',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(line.id)
    });
  }

  private delete(id: string): void {
    this.payroll.deleteOvertime(id).subscribe({
      next: () => {
        this.toast.add({ severity: 'success', summary: 'Heures sup.', detail: 'Ligne supprimée.' });
        this.reload();
      },
      error: err => this.toast.add({
        severity: 'error',
        summary: 'Heures sup.',
        detail: err?.error?.message ?? 'Suppression impossible.'
      })
    });
  }
}
