import { Component, Input, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { CalendarModule } from 'primeng/calendar';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextarea } from 'primeng/inputtextarea';
import { ButtonComponent } from '@shared/components/button/button.component';
import { EmployeeService } from '@core/services/employee.service';
import { PayrollService, LeaveRequest, LeaveBalance } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { LEAVE_TYPE_OPTIONS } from '../payroll-options';
import { PayrollStatGridComponent, type PayrollStatItem } from '../shared';

function toIsoDate(value: Date | null | undefined): string | undefined {
  if (!value) return undefined;
  const y = value.getFullYear();
  const m = String(value.getMonth() + 1).padStart(2, '0');
  const d = String(value.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

@Component({
  selector: 'app-employee-leaves-tab',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    TagModule,
    DialogModule,
    DropdownModule,
    CalendarModule,
    InputNumberModule,
    InputTextModule,
    InputTextarea,
    ButtonComponent,
    PayrollStatGridComponent
  ],
  template: `
    <div class="payroll-toolbar mb-3">
      <label for="balanceYear" class="sr-only">Exercice</label>
      <p-dropdown
        id="balanceYear"
        [options]="yearOptions"
        [(ngModel)]="balanceYear"
        (ngModelChange)="reloadBalance()"
        optionLabel="label"
        optionValue="value"
        placeholder="Exercice"
        styleClass="w-10rem" />
    </div>

    <app-payroll-stat-grid [items]="balanceStats()" [compact]="true" />

    <p class="payroll-info-text">
      Acquisition : 1 jour par 26 jours travaillés, créditée à la validation du cycle de paie.
      Seuls les congés approuvés impactent le calcul de paie ; les absences sans solde et injustifiées réduisent le brut.
    </p>

    @if (!readOnly) {
      <div class="payroll-toolbar">
        <app-button variant="primary" icon="pi-plus" iconPos="left" (click)="openDialog()">Ajouter un congé</app-button>
        <app-button variant="outline" (click)="openOpeningDialog()">Solde initial</app-button>
      </div>
    }

    <p-table [value]="items()" styleClass="p-datatable-sm">
      <ng-template pTemplate="header">
        <tr>
          <th>Type</th>
          <th>Début</th>
          <th>Fin</th>
          <th class="text-right">Jours</th>
          <th>Statut</th>
          @if (!readOnly) { <th></th> }
        </tr>
      </ng-template>
      <ng-template pTemplate="body" let-l>
        <tr>
          <td>{{ l.typeDisplay }}</td>
          <td>{{ l.startDate | date:'dd/MM/yyyy' }}</td>
          <td>{{ l.endDate | date:'dd/MM/yyyy' }}</td>
          <td class="text-right">{{ l.days }}</td>
          <td>
            <p-tag [value]="l.isApproved ? 'Approuvé' : 'En attente'" [severity]="l.isApproved ? 'success' : 'warn'" />
          </td>
          @if (!readOnly) {
            <td class="actions">
              @if (!l.isApproved) {
                <button type="button" class="p-button p-button-text p-button-sm" (click)="approve(l)">Approuver</button>
              }
              <button type="button" class="p-button p-button-text p-button-danger p-button-sm" (click)="confirmDelete(l)">Supprimer</button>
            </td>
          }
        </tr>
      </ng-template>
      <ng-template pTemplate="emptymessage">
        <tr><td [attr.colspan]="readOnly ? 5 : 6">Aucun congé enregistré.</td></tr>
      </ng-template>
    </p-table>

    <p-dialog header="Nouveau congé" [(visible)]="dialogVisible" [modal]="true" [style]="{ width: '480px' }">
      <div class="payroll-form-group mb-2">
        <label>Type</label>
        <p-dropdown [options]="leaveTypeOptions" [(ngModel)]="formType" optionLabel="label" optionValue="value" appendTo="body" styleClass="w-full" />
      </div>
      <div class="payroll-form-row">
        <div class="payroll-form-group">
          <label>Début</label>
          <p-calendar [(ngModel)]="formStart" dateFormat="dd/mm/yy" appendTo="body" styleClass="w-full" (ngModelChange)="computeDays()" />
        </div>
        <div class="payroll-form-group">
          <label>Fin</label>
          <p-calendar [(ngModel)]="formEnd" dateFormat="dd/mm/yy" appendTo="body" styleClass="w-full" (ngModelChange)="computeDays()" />
        </div>
      </div>
      <div class="payroll-form-group mb-2">
        <label>Jours</label>
        <p-inputNumber [(ngModel)]="formDays" [min]="0.5" [step]="0.5" styleClass="w-full" />
        @if (computingDays()) {
          <span class="field-hint">Calcul en cours…</span>
        }
      </div>
      <div class="payroll-form-group mb-2">
        <label>Motif</label>
        <textarea pInputTextarea [(ngModel)]="formReason" rows="2" class="w-full"></textarea>
      </div>
      <ng-template pTemplate="footer">
        <app-button variant="outline" (click)="dialogVisible = false">Annuler</app-button>
        <app-button variant="primary" (click)="create()">Enregistrer</app-button>
      </ng-template>
    </p-dialog>

    <p-dialog header="Solde initial de congés" [(visible)]="openingDialogVisible" [modal]="true" [style]="{ width: '400px' }">
      <p class="payroll-info-text mb-2">Reprise d'historique ou ajustement du solde avant la première validation de paie.</p>
      <div class="payroll-form-group">
        <label>Jours (solde initial)</label>
        <p-inputNumber [(ngModel)]="openingBalanceDays" [min]="0" [minFractionDigits]="1" [maxFractionDigits]="3" styleClass="w-full" />
      </div>
      <ng-template pTemplate="footer">
        <app-button variant="outline" (click)="openingDialogVisible = false">Annuler</app-button>
        <app-button variant="primary" (click)="saveOpeningBalance()">Enregistrer</app-button>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .mb-2 { margin-bottom: var(--spacing-2); }
    .mb-3 { margin-bottom: var(--spacing-4); }
    .actions { white-space: nowrap; }
    .w-full { width: 100%; }
    .sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0,0,0,0); border: 0; }
  `]
})
export class EmployeeLeavesTabComponent implements OnInit {
  @Input({ required: true }) employeeId!: string;
  @Input() readOnly = false;

  private readonly employees = inject(EmployeeService);
  private readonly payroll = inject(PayrollService);
  private readonly toast = inject(ToastService);
  private readonly confirmation = inject(ConfirmationService);

  readonly leaveTypeOptions = LEAVE_TYPE_OPTIONS;
  items = signal<LeaveRequest[]>([]);
  balance = signal<LeaveBalance | null>(null);
  balanceYear = new Date().getFullYear();
  yearOptions = Array.from({ length: 5 }, (_, i) => {
    const y = new Date().getFullYear() - 2 + i;
    return { value: y, label: String(y) };
  });
  dialogVisible = false;
  openingDialogVisible = false;
  openingBalanceDays = 0;
  formType = 'Paid';
  formStart: Date | null = null;
  formEnd: Date | null = null;
  formDays = 1;
  formReason = '';
  computingDays = signal(false);

  balanceStats = computed((): PayrollStatItem[] => {
    const b = this.balance();
    return [
      { label: 'Acquis', value: b ? `${b.totalAcquired} j` : '—', icon: 'pi-calendar-plus', variant: 'primary' },
      { label: 'Consommés', value: b ? `${b.consumed} j` : '—', icon: 'pi-calendar-minus', variant: 'warning' },
      { label: 'Restants', value: b ? `${b.remaining} j` : '—', icon: 'pi-calendar', variant: 'success', featured: true }
    ];
  });

  ngOnInit(): void {
    this.reload();
    this.reloadBalance();
  }

  reload(): void {
    this.employees.listLeaves(this.employeeId).subscribe({
      next: res => this.items.set(res.data ?? []),
      error: () => this.toast.add({ severity: 'error', summary: 'Congés', detail: 'Chargement impossible.' })
    });
  }

  reloadBalance(): void {
    this.employees.getLeaveBalance(this.employeeId, this.balanceYear).subscribe({
      next: res => this.balance.set(res.data ?? null),
      error: () => this.balance.set(null)
    });
  }

  openDialog(): void {
    if (this.readOnly) return;
    this.formType = 'Paid';
    this.formStart = new Date();
    this.formEnd = new Date();
    this.formDays = 1;
    this.formReason = '';
    this.dialogVisible = true;
    this.computeDays();
  }

  openOpeningDialog(): void {
    if (this.readOnly) return;
    this.openingBalanceDays = this.balance()?.openingBalance ?? 0;
    this.openingDialogVisible = true;
  }

  computeDays(): void {
    if (!this.formStart || !this.formEnd) return;
    const start = toIsoDate(this.formStart);
    const end = toIsoDate(this.formEnd);
    if (!start || !end) return;
    this.computingDays.set(true);
    this.payroll.computeLeaveDays(start, end).subscribe({
      next: res => {
        if (res.data != null && res.data > 0) this.formDays = res.data;
        this.computingDays.set(false);
      },
      error: () => this.computingDays.set(false)
    });
  }

  saveOpeningBalance(): void {
    if (this.readOnly) return;
    this.employees.setLeaveOpeningBalance(this.employeeId, this.openingBalanceDays).subscribe({
      next: () => {
        this.toast.add({ severity: 'success', summary: 'Congés', detail: 'Solde initial enregistré.' });
        this.openingDialogVisible = false;
        this.reloadBalance();
      },
      error: err => this.toast.add({
        severity: 'error',
        summary: 'Congés',
        detail: err?.error?.message ?? 'Enregistrement impossible.'
      })
    });
  }

  create(): void {
    if (this.readOnly || !this.formStart || !this.formEnd) return;
    this.payroll.createLeave({
      employeeId: this.employeeId,
      type: this.formType,
      startDate: toIsoDate(this.formStart)!,
      endDate: toIsoDate(this.formEnd)!,
      days: this.formDays,
      reason: this.formReason || undefined
    }).subscribe({
      next: () => {
        this.toast.add({ severity: 'success', summary: 'Congés', detail: 'Congé enregistré.' });
        this.dialogVisible = false;
        this.reload();
      },
      error: err => this.toast.add({ severity: 'error', summary: 'Congés', detail: err.error?.message ?? 'Création impossible.' })
    });
  }

  approve(leave: LeaveRequest): void {
    if (this.readOnly) return;
    this.payroll.approveLeave(leave.id).subscribe({
      next: () => {
        this.toast.add({ severity: 'success', summary: 'Congés', detail: 'Congé approuvé.' });
        this.reload();
        this.reloadBalance();
      },
      error: err => this.toast.add({ severity: 'error', summary: 'Congés', detail: err.error?.message ?? 'Approbation impossible.' })
    });
  }

  confirmDelete(leave: LeaveRequest): void {
    if (this.readOnly) return;
    this.confirmation.confirm({
      message: `Supprimer le congé du ${leave.startDate} ?`,
      header: 'Confirmation',
      acceptLabel: 'Supprimer',
      rejectLabel: 'Annuler',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(leave.id)
    });
  }

  private delete(id: string): void {
    this.payroll.deleteLeave(id).subscribe({
      next: () => {
        this.toast.add({ severity: 'success', summary: 'Congés', detail: 'Congé supprimé.' });
        this.reload();
        this.reloadBalance();
      },
      error: err => this.toast.add({ severity: 'error', summary: 'Congés', detail: err.error?.message ?? 'Suppression impossible.' })
    });
  }
}
