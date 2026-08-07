import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { InputNumberModule } from 'primeng/inputnumber';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { PayrollService, TerminationSettlement } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { PayrollAmountPipe } from '../shared';

@Component({
  selector: 'app-termination-settlement-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, TableModule, TagModule, InputNumberModule, PageHeaderComponent, ButtonComponent, PayrollAmountPipe],
  template: `
    <app-page-header title="Soldes de tout compte" subtitle="Indemnités de rupture et fin de contrat.">
      <app-button variant="primary" icon="pi-plus" routerLink="/payroll/terminations/new">Nouveau solde</app-button>
    </app-page-header>

    <div class="payroll-toolbar mb-3">
      <label>Année</label>
      <p-inputNumber [(ngModel)]="year" (ngModelChange)="load()" [useGrouping]="false" />
      <label class="ml-3">Mois</label>
      <p-inputNumber [(ngModel)]="month" (ngModelChange)="load()" [min]="1" [max]="12" [useGrouping]="false" />
    </div>

    <p-table [value]="items()" [loading]="loading()" styleClass="p-datatable-sm">
      <ng-template pTemplate="header">
        <tr>
          <th>Salarié</th>
          <th>Date sortie</th>
          <th>Motif</th>
          <th>Statut</th>
          <th class="text-right">Total indemnités</th>
          <th></th>
        </tr>
      </ng-template>
      <ng-template pTemplate="body" let-row>
        <tr>
          <td>{{ row.employeeName || row.employeeId }}</td>
          <td>{{ row.terminationDate | date:'dd/MM/yyyy' }}</td>
          <td>{{ row.reasonDisplay }}</td>
          <td><p-tag [value]="row.statusDisplay" /></td>
          <td class="text-right">{{ row.totalIndemnityAmount | payrollAmount }}</td>
          <td>
            <a [routerLink]="['/payroll/terminations', row.id]">Détail</a>
          </td>
        </tr>
      </ng-template>
    </p-table>
  `
})
export class TerminationSettlementListComponent implements OnInit {
  private readonly payroll = inject(PayrollService);
  private readonly toast = inject(ToastService);

  year = new Date().getFullYear();
  month = new Date().getMonth() + 1;
  readonly items = signal<TerminationSettlement[]>([]);
  readonly loading = signal(false);

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.payroll.listTerminationSettlements(this.year, this.month).subscribe({
      next: res => { this.items.set(res.data ?? []); this.loading.set(false); },
      error: () => { this.toast.error('Impossible de charger les soldes.'); this.loading.set(false); }
    });
  }
}
