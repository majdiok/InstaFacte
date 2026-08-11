import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { SelectModule } from 'primeng/select';
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
    SelectModule,
    PageHeaderComponent,
    ButtonComponent,
    PayrollConsultBannerComponent,
    PayrollEmptyStateComponent,
    PayrollAmountPipe
  ],
  template: `
    <app-page-header
      [title]="firmInternal() ? 'Salariés du cabinet' : 'Salariés'"
      [subtitle]="firmInternal() ? 'Paie interne — collaborateurs du cabinet.' : 'Dossiers salariés et contrats de travail.'">
      @if (canManage()) {
        <app-button variant="primary" icon="pi-plus" iconPos="left" [routerLink]="routeBase() + '/employees/new'">Nouveau salarié</app-button>
      }
    </app-page-header>

    <app-payroll-consult-banner
      [visible]="showConsultBanner()"
      message="Mode consultation — la gestion des dossiers salariés est réservée à la société cliente." />

    <div class="payroll-toolbar">
      <input pInputText type="text" placeholder="Rechercher…" [(ngModel)]="search" (ngModelChange)="load()" class="w-full md:w-20rem" />
      <p-select
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
        [actionRoute]="routeBase() + '/employees/new'" />
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
              @if (e.parentClaimsStatus === 'Incomplete') {
                <p-tag value="Parents à compléter" severity="warn" class="ml-1" />
              }
              @if (e.parentClaimsStatus === 'Conflict') {
                <p-tag value="Conflit parents" severity="danger" class="ml-1" />
              }
            </td>
            <td>
              <app-button variant="outline" size="sm" icon="pi-eye" iconPos="left" [routerLink]="[routeBase() + '/employees', e.id]">Voir</app-button>
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
  private readonly route = inject(ActivatedRoute);

  routeBase = signal('/payroll');
  firmInternal = signal(false);

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
  showConsultBanner = computed(() => !this.firmInternal() && isPayrollConsultMode(this.auth));

  ngOnInit(): void {
    const data = this.route.snapshot.data;
    this.routeBase.set(data['payrollRouteBase'] ?? '/payroll');
    this.firmInternal.set(!!data['firmInternalPayroll']);
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
