import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { DropdownModule } from 'primeng/dropdown';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { MessageModule } from 'primeng/message';
import { finalize } from 'rxjs';
import { PayrollService, CnssContributionRemittance } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';
import { canPayPayroll } from '@core/utils/payroll-access';
import { ButtonComponent } from '@shared/components/button/button.component';
import {
  PayrollStatGridComponent,
  PayrollEmptyStateComponent,
  PayrollAmountPipe,
  formatPayrollAmount,
  type PayrollStatItem
} from '../shared';
import { CnssRemittancePaymentDialogComponent } from './cnss-remittance-payment-dialog.component';

const MONTH_LABELS = [
  '',
  'Janvier', 'Février', 'Mars', 'Avril', 'Mai', 'Juin',
  'Juillet', 'Août', 'Septembre', 'Octobre', 'Novembre', 'Décembre'
];

@Component({
  selector: 'app-cnss-remittance-tab',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    DropdownModule,
    TagModule,
    TooltipModule,
    MessageModule,
    ButtonComponent,
    PayrollStatGridComponent,
    PayrollEmptyStateComponent,
    PayrollAmountPipe,
    CnssRemittancePaymentDialogComponent
  ],
  template: `
    <div class="payroll-toolbar mb-3">
      <p-dropdown
        [options]="yearOptions"
        [(ngModel)]="year"
        (ngModelChange)="load()"
        optionLabel="label"
        optionValue="value"
        placeholder="Année"
        styleClass="w-10rem" />
      <p-dropdown
        [options]="monthOptions"
        [(ngModel)]="month"
        (ngModelChange)="load()"
        optionLabel="label"
        optionValue="value"
        placeholder="Mois"
        styleClass="w-10rem" />
      <app-button variant="primary" icon="pi-refresh" iconPos="left" (click)="load()">Générer</app-button>
      <app-button
        variant="outline"
        icon="pi-download"
        iconPos="left"
        (click)="exportCsv()"
        [disabled]="!canExport()">Exporter CSV</app-button>
      <app-button
        variant="outline"
        icon="pi-file-pdf"
        iconPos="left"
        (click)="exportPdf()"
        [disabled]="!canExport()">Exporter PDF</app-button>
      @if (canPay()) {
        <app-button
          variant="success"
          icon="pi-wallet"
          iconPos="left"
          (click)="openPaymentDialog()"
          [disabled]="!canRecordPayment()">Enregistrer le versement</app-button>
      }
    </div>

    @for (w of remittance()?.warnings ?? []; track w) {
      <p-message severity="warn" [text]="w" styleClass="mb-3 w-full" />
    }

    @if (remittance()?.hasExistingPayment && remittance()?.payment) {
      <p-message
        severity="success"
        [text]="paymentMessage()"
        styleClass="mb-3 w-full" />
    }

    @if (loading()) {
      <app-payroll-empty-state [loading]="true" [skeletonColumns]="6" />
    } @else if (!remittance()) {
      <app-payroll-empty-state
        icon="pi-exclamation-circle"
        title="Impossible de charger le bordereau"
        description="Vérifiez que la fonctionnalité est activée et que vous disposez des droits nécessaires."
        [showAction]="true"
        actionLabel="Réessayer"
        (actionClick)="load()" />
    } @else if (!remittance()!.isEligible) {
      <app-payroll-empty-state
        icon="pi-calendar"
        title="Cycle de paie non disponible"
        [description]="'Aucun cycle validé ou clôturé pour ' + monthLabel() + '.'"
        [showAction]="true"
        actionLabel="Voir les cycles de paie"
        actionRoute="/payroll/runs" />
    } @else if (remittance()!.lines.length === 0) {
      <app-payroll-empty-state
        icon="pi-file"
        title="Aucun salarié"
        description="Le cycle ne contient aucun bulletin."
        [showAction]="true"
        actionLabel="Voir les cycles de paie"
        actionRoute="/payroll/runs" />
    } @else {
      <app-payroll-stat-grid [items]="summaryStats()" class="mb-4" />

      <p-table [value]="remittance()!.lines" styleClass="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>Salarié</th>
            <th>CNSS</th>
            <th class="text-right">Brut CNSS</th>
            <th class="text-right">CNSS sal.</th>
            <th class="text-right">CNSS pat.</th>
            <th class="text-right">AT</th>
            <th class="text-right">Total</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-l>
          <tr>
            <td>{{ l.employeeName }}</td>
            <td>
              @if (l.cnssNumber) {
                {{ l.cnssNumber }}
              } @else {
                <p-tag value="Manquant" severity="warn" />
              }
            </td>
            <td class="text-right">{{ l.cnssableGross | payrollAmount }}</td>
            <td class="text-right">{{ l.cnssEmployee | payrollAmount }}</td>
            <td class="text-right">{{ l.cnssEmployer | payrollAmount }}</td>
            <td class="text-right">{{ l.workAccident | payrollAmount }}</td>
            <td class="text-right">{{ l.lineTotal | payrollAmount }}</td>
          </tr>
        </ng-template>
      </p-table>
    }

    <app-cnss-remittance-payment-dialog
      [visible]="paymentDialogVisible()"
      [year]="year"
      [month]="month"
      [totalDue]="remittance()?.totalDue ?? 0"
      (visibleChange)="paymentDialogVisible.set($event)"
      (recorded)="onPaymentRecorded()" />
  `,
  styles: [`
    .mb-3 { margin-bottom: var(--spacing-4); }
    .mb-4 { margin-bottom: var(--spacing-6); display: block; }
  `]
})
export class CnssRemittanceTabComponent implements OnInit {
  private readonly payroll = inject(PayrollService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  readonly initialYear = input<number | null>(null);
  readonly initialMonth = input<number | null>(null);

  year = new Date().getFullYear();
  month = new Date().getMonth() + 1;
  yearOptions = Array.from({ length: 5 }, (_, i) => {
    const y = new Date().getFullYear() - 2 + i;
    return { value: y, label: String(y) };
  });
  monthOptions = MONTH_LABELS.slice(1).map((label, i) => ({ value: i + 1, label }));

  remittance = signal<CnssContributionRemittance | null>(null);
  loading = signal(false);
  paymentDialogVisible = signal(false);

  canExport = computed(() => {
    const r = this.remittance();
    return !!r && r.isEligible && r.employeeCount > 0;
  });

  canRecordPayment = computed(() => {
    const r = this.remittance();
    return !!r && r.isEligible && !r.hasExistingPayment && r.totalDue > 0;
  });

  canPay = computed(() => canPayPayroll(this.auth));

  summaryStats = computed((): PayrollStatItem[] => {
    const r = this.remittance();
    if (!r) return [];
    return [
      { label: 'Salariés', value: r.employeeCount, icon: 'pi-users', variant: 'primary' },
      { label: 'CNSS salarié', value: formatPayrollAmount(r.totalCnssEmployee, false), icon: 'pi-user', variant: 'warning' },
      { label: 'CNSS patronale', value: formatPayrollAmount(r.totalCnssEmployer, false), icon: 'pi-building', variant: 'warning' },
      { label: 'Accident travail', value: formatPayrollAmount(r.totalWorkAccident, false), icon: 'pi-shield', variant: 'primary' },
      { label: 'Total à verser', value: formatPayrollAmount(r.totalDue, false), icon: 'pi-wallet', variant: 'success', featured: true }
    ];
  });

  ngOnInit(): void {
    const y = this.initialYear();
    const m = this.initialMonth();
    if (y != null && y >= 2000 && y <= 2100) this.year = y;
    if (m != null && m >= 1 && m <= 12) this.month = m;
    this.load();
  }

  monthLabel(): string {
    return `${MONTH_LABELS[this.month]} ${this.year}`;
  }

  paymentMessage(): string {
    const p = this.remittance()?.payment;
    if (!p) return '';
    const date = new Date(p.paymentDate).toLocaleDateString('fr-TN');
    return `Versement enregistré le ${date}${p.reference ? ' — Réf. ' + p.reference : ''}`;
  }

  load(): void {
    this.loading.set(true);
    this.payroll.getCnssRemittance(this.year, this.month).pipe(
      finalize(() => this.loading.set(false))
    ).subscribe({
      next: res => this.remittance.set(res.data ?? null),
      error: err => {
        this.remittance.set(null);
        this.toast.add({
          severity: 'error',
          summary: 'Bordereau CNSS',
          detail: err?.error?.message ?? 'Impossible de générer le bordereau.'
        });
      }
    });
  }

  exportCsv(): void {
    if (!this.canExport()) return;
    this.payroll.exportCnssRemittanceCsv(this.year, this.month).subscribe({
      next: blob => downloadBlob(blob, `bordereau_cnss_${this.year}_${String(this.month).padStart(2, '0')}.csv`),
      error: () => this.toast.add({ severity: 'error', summary: 'Bordereau CNSS', detail: 'Export CSV impossible.' })
    });
  }

  exportPdf(): void {
    if (!this.canExport()) return;
    this.payroll.exportCnssRemittancePdf(this.year, this.month).subscribe({
      next: blob => downloadBlob(blob, `bordereau_cnss_${this.year}_${String(this.month).padStart(2, '0')}.pdf`),
      error: () => this.toast.add({ severity: 'error', summary: 'Bordereau CNSS', detail: 'Export PDF impossible.' })
    });
  }

  openPaymentDialog(): void {
    if (!this.canRecordPayment()) return;
    this.paymentDialogVisible.set(true);
  }

  onPaymentRecorded(): void {
    this.paymentDialogVisible.set(false);
    this.load();
  }
}

function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
