import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { DropdownModule } from 'primeng/dropdown';
import { EmployeeService, EmployeeListItem } from '@core/services/employee.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { canManagePayrollEmployees, isPayrollConsultMode } from '@core/utils/payroll-access';
import {
  PayrollConsultBannerComponent,
  PayrollEmptyStateComponent,
  PayrollAmountPipe
} from '../shared';

type StatusFilter = 'all' | 'active' | 'inactive';

@Component({
  selector: 'app-employee-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    InputTextModule,
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
    <app-page-header title="Salariés" subtitle="Dossiers salariés et contrats de travail.">
      @if (canManage()) {
        <app-button variant="primary" icon="pi-plus" iconPos="left" routerLink="/payroll/employees/new">Nouveau salarié</app-button>
      }
    </app-page-header>

    <app-payroll-consult-banner
      [visible]="showConsultBanner()"
      message="Mode consultation — la gestion des dossiers salariés est réservée à la société cliente." />

    <div class="payroll-toolbar">
      <input pInputText type="text" placeholder="Rechercher…" [(ngModel)]="search" (ngModelChange)="load()" class="w-full md:w-20rem" />
      <p-dropdown
        [options]="statusOptions"
        [(ngModel)]="statusFilter"
        (ngModelChange)="load()"
        optionLabel="label"
        optionValue="value"
        placeholder="Statut"
        styleClass="w-12rem" />
    </div>

    @if (loading() || !items().length) {
      <app-payroll-empty-state
        [loading]="loading()"
        [skeletonColumns]="7"
        icon="pi-users"
        title="Aucun salarié"
        [description]="loading() ? undefined : 'Aucun salarié ne correspond à vos critères.'"
        [showAction]="canManage() && !loading()"
        actionLabel="Nouveau salarié"
        actionRoute="/payroll/employees/new" />
    }

    @if (!loading() && items().length) {
      <p-table [value]="items()" [paginator]="true" [rows]="20" styleClass="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>Matricule</th>
            <th>Nom</th>
            <th>CNSS</th>
            <th>Poste</th>
            <th class="text-right">Salaire base</th>
            <th>Statut</th>
            <th></th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-e>
          <tr>
            <td>{{ e.employeeNumber }}</td>
            <td>{{ e.fullName }}</td>
            <td>
              @if (e.cnssNumber) {
                {{ e.cnssNumber }}
              } @else {
                <p-tag value="CNSS manquant" severity="warn" />
              }
            </td>
            <td>{{ e.jobTitle || '—' }}</td>
            <td class="text-right">{{ e.currentBaseSalary | payrollAmount }}</td>
            <td>
              <p-tag [value]="e.isActive ? 'Actif' : 'Inactif'" [severity]="e.isActive ? 'success' : 'secondary'" />
            </td>
            <td>
              <app-button variant="outline" size="sm" icon="pi-eye" iconPos="left" [routerLink]="['/payroll/employees', e.id]">Voir</app-button>
            </td>
          </tr>
        </ng-template>
      </p-table>
    }
  `
})
export class EmployeeListComponent implements OnInit {
  private readonly employees = inject(EmployeeService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  search = '';
  statusFilter: StatusFilter = 'all';
  loading = signal(false);
  items = signal<EmployeeListItem[]>([]);

  readonly statusOptions = [
    { value: 'all' as const, label: 'Tous' },
    { value: 'active' as const, label: 'Actifs' },
    { value: 'inactive' as const, label: 'Inactifs' }
  ];

  canManage = computed(() => canManagePayrollEmployees(this.auth));
  showConsultBanner = computed(() => isPayrollConsultMode(this.auth));

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    const isActive = this.statusFilter === 'all' ? undefined : this.statusFilter === 'active';
    this.employees.list(this.search || undefined, isActive).subscribe({
      next: res => {
        this.items.set(res.data?.items ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.toast.add({ severity: 'error', summary: 'Salariés', detail: 'Impossible de charger les salariés.' });
        this.loading.set(false);
      }
    });
  }
}
