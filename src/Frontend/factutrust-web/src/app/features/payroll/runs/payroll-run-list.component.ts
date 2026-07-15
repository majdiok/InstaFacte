import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { DropdownModule } from 'primeng/dropdown';
import { PayrollService, PayrollRunListItem } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { canRunPayroll, isPayrollConsultMode } from '@core/utils/payroll-access';
import { PayrollConsultBannerComponent, PayrollEmptyStateComponent, PayrollAmountPipe } from '../shared';

@Component({
  selector: 'app-payroll-run-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    TableModule,
    TagModule,
    DropdownModule,
    PageHeaderComponent,
    ButtonComponent,
    PayrollConsultBannerComponent,
    PayrollEmptyStateComponent,
    PayrollAmountPipe
  ],
  template: `
    <app-page-header title="Cycles de paie" subtitle="Paie mensuelle : brouillon → calcul → validation → clôture.">
      @if (canRun()) {
        <app-button variant="primary" icon="pi-plus" iconPos="left" (click)="createCurrentMonth()">Nouveau cycle (mois courant)</app-button>
      }
    </app-page-header>

    <app-payroll-consult-banner
      [visible]="showConsultBanner()"
      message="Mode consultation — la création et le calcul des cycles sont réservés à la société cliente." />

    <div class="payroll-toolbar">
      <p-dropdown
        [options]="yearOptions"
        [(ngModel)]="selectedYear"
        (ngModelChange)="reload()"
        optionLabel="label"
        optionValue="value"
        placeholder="Exercice"
        styleClass="w-10rem" />
    </div>

    @if (loading() || !runs().length) {
      <app-payroll-empty-state
        [loading]="loading()"
        [skeletonColumns]="6"
        icon="pi-calendar"
        title="Aucun cycle de paie"
        [description]="emptyDescription()"
        [showAction]="canRun() && !loading()"
        actionLabel="Créer le cycle du mois courant"
        (actionClick)="createCurrentMonth()" />
    }

    @if (!loading() && runs().length) {
      <p-table [value]="runs()" styleClass="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>Période</th>
            <th>Statut</th>
            <th class="text-right">Bulletins</th>
            <th class="text-right">Brut</th>
            <th class="text-right">Net</th>
            <th></th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-r>
          <tr>
            <td>{{ r.label }}</td>
            <td><p-tag [value]="r.statusDisplay" /></td>
            <td class="text-right">{{ r.payslipCount }}</td>
            <td class="text-right">{{ r.totalGross | payrollAmount }}</td>
            <td class="text-right">{{ r.totalNet | payrollAmount }}</td>
            <td>
              <app-button variant="outline" size="sm" icon="pi-external-link" iconPos="left" [routerLink]="['/payroll/runs', r.id]">Ouvrir</app-button>
            </td>
          </tr>
        </ng-template>
      </p-table>
    }
  `
})
export class PayrollRunListComponent implements OnInit {
  private readonly payroll = inject(PayrollService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  selectedYear = new Date().getFullYear();
  yearOptions = Array.from({ length: 5 }, (_, i) => {
    const y = new Date().getFullYear() - 2 + i;
    return { value: y, label: String(y) };
  });
  loading = signal(false);
  runs = signal<PayrollRunListItem[]>([]);

  canRun = computed(() => canRunPayroll(this.auth));
  showConsultBanner = computed(() => isPayrollConsultMode(this.auth));

  emptyDescription = computed(() => this.loading() ? undefined : 'Aucun cycle pour cet exercice.');

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.payroll.listRuns(this.selectedYear).subscribe({
      next: res => {
        this.runs.set(res.data ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.toast.add({ severity: 'error', summary: 'Paie', detail: 'Impossible de charger les cycles.' });
        this.loading.set(false);
      }
    });
  }

  createCurrentMonth(): void {
    if (!this.canRun()) return;
    const now = new Date();
    this.payroll.createRun(now.getFullYear(), now.getMonth() + 1).subscribe({
      next: () => {
        this.toast.add({ severity: 'success', summary: 'Paie', detail: 'Cycle créé.' });
        this.selectedYear = now.getFullYear();
        this.reload();
      },
      error: err => this.toast.add({
        severity: 'error',
        summary: 'Paie',
        detail: err?.error?.message ?? 'Création impossible.'
      })
    });
  }
}
