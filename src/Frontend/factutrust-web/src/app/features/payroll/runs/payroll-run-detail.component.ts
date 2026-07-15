import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';
import { PayrollService, PayrollRunDetail, PayslipDetail } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';
import { canRunPayroll, canValidatePayroll, canManagePayrollEmployees } from '@core/utils/payroll-access';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { PayrollOvertimeGridComponent } from './payroll-overtime-grid.component';
import { PayrollStatGridComponent, PayrollSectionComponent, PayrollAmountPipe, type PayrollStatItem } from '../shared';

@Component({
  selector: 'app-payroll-run-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    TableModule,
    DialogModule,
    TagModule,
    PageHeaderComponent,
    ButtonComponent,
    PayrollOvertimeGridComponent,
    PayrollStatGridComponent,
    PayrollSectionComponent,
    PayrollAmountPipe
  ],
  template: `
    @if (run()) {
      <app-page-header [title]="run()!.label" [subtitle]="run()!.statusDisplay">
        @if (canRun() && (run()!.status === 'Draft' || run()!.status === 'Calculated')) {
          <app-button variant="primary" icon="pi-calculator" iconPos="left" (click)="calculate()">Calculer</app-button>
        }
        @if (canValidate() && run()!.status === 'Calculated') {
          <app-button variant="primary" icon="pi-check" iconPos="left" (click)="validate()">Valider</app-button>
        }
        @if (canValidate() && run()!.status === 'Validated') {
          <app-button variant="outline" icon="pi-replay" iconPos="left" (click)="reopen()">Rouvrir</app-button>
          <app-button variant="primary" icon="pi-lock" iconPos="left" (click)="close()">Clôturer</app-button>
        }
      </app-page-header>

      <app-payroll-stat-grid [items]="totalsStats()" class="mb-4" />

      @if (run()!.status === 'Draft' || run()!.status === 'Calculated') {
        <app-payroll-overtime-grid
          [year]="run()!.year"
          [month]="run()!.month"
          [readOnly]="!canManageEmployees()"
          [initialLines]="run()!.overtimeLines ?? []" />
      }

      <app-payroll-section title="Bulletins de paie" icon="pi-file">
        <p-table [value]="run()!.payslips" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th>Salarié</th>
              <th class="text-right">Brut</th>
              <th class="text-right">Net</th>
              <th></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-p>
            <tr>
              <td>{{ p.employeeName }}</td>
              <td class="text-right">{{ p.grossSalary | payrollAmount }}</td>
              <td class="text-right">{{ p.netSalary | payrollAmount }}</td>
              <td class="actions">
                <app-button variant="ghost" size="sm" (click)="showPayslip(p.id)">Détail</app-button>
                <app-button variant="ghost" size="sm" (click)="downloadPdf(p.id)">PDF</app-button>
              </td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr><td colspan="4">Aucun bulletin — lancez le calcul.</td></tr>
          </ng-template>
        </p-table>
      </app-payroll-section>
    }

    <p-dialog header="Détail du bulletin" [(visible)]="payslipDialogVisible" [modal]="true" [style]="{ width: '640px' }">
      @if (selectedPayslip()) {
        <div class="mb-3">
          <strong>{{ selectedPayslip()!.employeeName }}</strong>
          <span class="ml-2 text-secondary">{{ selectedPayslip()!.employeeNumber }}</span>
        </div>
        <p-table [value]="selectedPayslip()!.lines" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th>Libellé</th>
              <th class="text-right">Base</th>
              <th class="text-right">Taux</th>
              <th class="text-right">Montant</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-line>
            <tr>
              <td>{{ line.label }}</td>
              <td class="text-right">{{ line.base != null ? (line.base | payrollAmount:false) : '—' }}</td>
              <td class="text-right">{{ line.rate != null ? (line.rate + ' %') : '—' }}</td>
              <td class="text-right">{{ line.amount | payrollAmount }}</td>
            </tr>
          </ng-template>
        </p-table>
        <div class="mt-3 text-right">
          <strong>Net à payer : {{ selectedPayslip()!.netSalary | payrollAmount }}</strong>
        </div>
      }
      <ng-template pTemplate="footer">
        <app-button variant="outline" (click)="payslipDialogVisible = false">Fermer</app-button>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .mb-3 { margin-bottom: var(--spacing-4); }
    .mb-4 { margin-bottom: var(--spacing-6); display: block; }
    .mt-3 { margin-top: var(--spacing-4); }
    .ml-2 { margin-left: var(--spacing-2); }
    .text-secondary { color: var(--color-text-secondary); }
    .actions { display: flex; gap: var(--spacing-2); white-space: nowrap; }
  `]
})
export class PayrollRunDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly payroll = inject(PayrollService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  run = signal<PayrollRunDetail | null>(null);
  selectedPayslip = signal<PayslipDetail | null>(null);
  payslipDialogVisible = false;
  private runId = '';

  canRun = computed(() => canRunPayroll(this.auth));
  canValidate = computed(() => canValidatePayroll(this.auth));
  canManageEmployees = computed(() => canManagePayrollEmployees(this.auth));

  totalsStats = computed((): PayrollStatItem[] => {
    const r = this.run();
    if (!r) return [];
    return [
      { label: 'Brut', value: r.totalGross, icon: 'pi-money-bill', variant: 'primary' },
      { label: 'Net', value: r.totalNet, icon: 'pi-wallet', variant: 'success', featured: true },
      { label: 'IRPP', value: r.totalIrpp, icon: 'pi-percentage', variant: 'warning' },
      { label: 'CNSS sal.', value: r.totalCnssEmployee, icon: 'pi-user', variant: 'primary' },
      { label: 'CNSS pat.', value: r.totalCnssEmployer, icon: 'pi-building', variant: 'primary' },
      { label: 'TFP', value: r.totalTfp, icon: 'pi-chart-line', variant: 'warning' },
      { label: 'FOPROLOS', value: r.totalFoprolos, icon: 'pi-briefcase', variant: 'warning' }
    ];
  });

  ngOnInit(): void {
    this.runId = this.route.snapshot.paramMap.get('id')!;
    this.reload();
  }

  reload(): void {
    this.payroll.getRun(this.runId).subscribe({
      next: res => this.run.set(res.data ?? null),
      error: () => this.toast.add({ severity: 'error', summary: 'Paie', detail: 'Cycle introuvable.' })
    });
  }

  calculate(): void {
    if (!this.canRun()) return;
    this.payroll.calculateRun(this.runId).subscribe({
      next: () => { this.toast.add({ severity: 'success', summary: 'Paie', detail: 'Cycle calculé.' }); this.reload(); },
      error: err => this.toast.add({ severity: 'error', summary: 'Paie', detail: err?.error?.message ?? 'Calcul impossible.' })
    });
  }

  validate(): void {
    if (!this.canValidate()) return;
    this.payroll.validateRun(this.runId).subscribe({
      next: () => { this.toast.add({ severity: 'success', summary: 'Paie', detail: 'Cycle validé.' }); this.reload(); },
      error: err => this.toast.add({ severity: 'error', summary: 'Paie', detail: err?.error?.message ?? 'Validation impossible.' })
    });
  }

  reopen(): void {
    if (!this.canValidate()) return;
    this.payroll.reopenRun(this.runId).subscribe({
      next: () => { this.toast.add({ severity: 'success', summary: 'Paie', detail: 'Cycle rouvert.' }); this.reload(); },
      error: err => this.toast.add({ severity: 'error', summary: 'Paie', detail: err?.error?.message ?? 'Réouverture impossible.' })
    });
  }

  close(): void {
    if (!this.canValidate()) return;
    this.payroll.closeRun(this.runId).subscribe({
      next: () => { this.toast.add({ severity: 'success', summary: 'Paie', detail: 'Cycle clôturé.' }); this.reload(); },
      error: err => this.toast.add({ severity: 'error', summary: 'Paie', detail: err?.error?.message ?? 'Clôture impossible.' })
    });
  }

  showPayslip(payslipId: string): void {
    this.payroll.getPayslip(payslipId).subscribe({
      next: res => {
        this.selectedPayslip.set(res.data ?? null);
        this.payslipDialogVisible = true;
      },
      error: () => this.toast.add({ severity: 'error', summary: 'Bulletin', detail: 'Impossible de charger le détail.' })
    });
  }

  downloadPdf(payslipId: string): void {
    this.payroll.downloadPayslipPdf(payslipId).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `bulletin-${payslipId}.pdf`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.toast.add({ severity: 'error', summary: 'PDF', detail: 'Export PDF impossible.' })
    });
  }
}
