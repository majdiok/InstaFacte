import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { DropdownModule } from 'primeng/dropdown';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { PayrollService, DtsDeclaration } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { PayrollStatGridComponent, PayrollAmountPipe, formatPayrollAmount, type PayrollStatItem } from '../shared';

@Component({
  selector: 'app-dts-declaration',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    DropdownModule,
    TagModule,
    TooltipModule,
    PageHeaderComponent,
    ButtonComponent,
    PayrollStatGridComponent,
    PayrollAmountPipe
  ],
  template: `
    <app-page-header title="DTS CNSS" subtitle="Déclaration trimestrielle des salaires. Seuls les cycles de paie validés ou clôturés sont inclus.">
      <app-button variant="outline" icon="pi-download" iconPos="left" (click)="exportCsv()" [disabled]="!dts()">Exporter CSV</app-button>
    </app-page-header>

    <div class="payroll-toolbar">
      <p-dropdown
        [options]="yearOptions"
        [(ngModel)]="year"
        (ngModelChange)="load()"
        optionLabel="label"
        optionValue="value"
        placeholder="Année"
        styleClass="w-10rem" />
      <p-dropdown
        [options]="quarterOptions"
        [(ngModel)]="quarter"
        (ngModelChange)="load()"
        optionLabel="label"
        optionValue="value"
        placeholder="Trimestre"
        styleClass="w-10rem" />
      <app-button variant="primary" icon="pi-refresh" iconPos="left" (click)="load()">Générer</app-button>
    </div>

    @if (dts()) {
      <app-payroll-stat-grid [items]="summaryStats()" class="mb-4" />

      <p-table [value]="dts()!.lines" styleClass="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>Salarié</th>
            <th>CNSS</th>
            <th class="text-right">Brut CNSSable</th>
            <th class="text-right">CNSS sal.</th>
            <th class="text-right">CNSS pat.</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-l>
          <tr>
            <td>{{ l.employeeName }}</td>
            <td>
              @if (l.cnssNumber) {
                {{ l.cnssNumber }}
              } @else {
                <i class="pi pi-exclamation-triangle text-warning" pTooltip="CNSS manquant" tooltipPosition="top"></i>
                <p-tag value="Manquant" severity="warn" class="ml-2" />
              }
            </td>
            <td class="text-right">{{ l.totalCnssableGross | payrollAmount }}</td>
            <td class="text-right">{{ l.cnssEmployee | payrollAmount }}</td>
            <td class="text-right">{{ l.cnssEmployer | payrollAmount }}</td>
          </tr>
        </ng-template>
      </p-table>
    }
  `,
  styles: [`
    .mb-4 { margin-bottom: var(--spacing-6); display: block; }
    .ml-2 { margin-left: var(--spacing-2); }
    .text-warning { color: var(--color-warning-600); }
  `]
})
export class DtsDeclarationComponent implements OnInit {
  private readonly payroll = inject(PayrollService);
  private readonly toast = inject(ToastService);

  year = new Date().getFullYear();
  quarter = Math.ceil((new Date().getMonth() + 1) / 3);
  yearOptions = Array.from({ length: 5 }, (_, i) => {
    const y = new Date().getFullYear() - 2 + i;
    return { value: y, label: String(y) };
  });
  quarterOptions = [1, 2, 3, 4].map(q => ({ value: q, label: `T${q}` }));
  dts = signal<DtsDeclaration | null>(null);

  summaryStats = computed((): PayrollStatItem[] => {
    const d = this.dts();
    if (!d) return [];
    return [
      { label: 'Salariés', value: d.employeeCount, icon: 'pi-users', variant: 'primary' },
      { label: 'Masse CNSSable', value: formatPayrollAmount(d.totalCnssableGross, false), icon: 'pi-chart-bar', variant: 'primary' },
      { label: 'CNSS salarié', value: formatPayrollAmount(d.totalCnssEmployee, false), icon: 'pi-user', variant: 'warning' },
      { label: 'CNSS employeur', value: formatPayrollAmount(d.totalCnssEmployer, false), icon: 'pi-building', variant: 'warning' },
      { label: 'Cotisations totales', value: formatPayrollAmount(d.totalContributions, false), icon: 'pi-wallet', variant: 'success', featured: true }
    ];
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.payroll.getDts(this.year, this.quarter).subscribe({
      next: res => this.dts.set(res.data ?? null),
      error: () => this.toast.add({ severity: 'error', summary: 'DTS', detail: 'Impossible de générer la DTS.' })
    });
  }

  exportCsv(): void {
    this.payroll.exportDtsCsv(this.year, this.quarter).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `dts_${this.year}_T${this.quarter}.csv`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.toast.add({ severity: 'error', summary: 'DTS', detail: 'Export CSV impossible.' })
    });
  }
}
