import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { TabViewModule } from 'primeng/tabview';
import { TagModule } from 'primeng/tag';
import { MessageModule } from 'primeng/message';
import { TableModule } from 'primeng/table';
import { EmployeeService, EmployeeDetail, EmploymentContract } from '@core/services/employee.service';
import { PayrollService } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { canManagePayrollEmployees, canManageGarnishments as userCanManageGarnishments, isPayrollConsultMode } from '@core/utils/payroll-access';
import { ContractFormDialogComponent } from './contract-form-dialog.component';
import { EmployeeLeavesTabComponent } from './employee-leaves-tab.component';
import { EmployeeAdvancesTabComponent } from './employee-advances-tab.component';
import { EmployeeSocialFundsTabComponent } from './employee-social-funds-tab.component';
import { EmployeeInKindBenefitsTabComponent } from './employee-in-kind-benefits-tab.component';
import { EmployeeLoansTabComponent } from './employee-loans-tab.component';
import { EmployeeGarnishmentsTabComponent } from './employee-garnishments-tab.component';
import { PayrollConsultBannerComponent, PayrollAmountPipe, formatPayrollAmount } from '../shared';

@Component({
  selector: 'app-employee-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    TabViewModule,
    TagModule,
    MessageModule,
    TableModule,
    PageHeaderComponent,
    ButtonComponent,
    ContractFormDialogComponent,
    EmployeeLeavesTabComponent,
    EmployeeAdvancesTabComponent,
    EmployeeSocialFundsTabComponent,
    EmployeeInKindBenefitsTabComponent,
    EmployeeLoansTabComponent,
    EmployeeGarnishmentsTabComponent,
    PayrollConsultBannerComponent,
    PayrollAmountPipe
  ],
  template: `
    @if (loading()) {
      <p>Chargement…</p>
    } @else if (employee()) {
      <app-page-header [title]="employee()!.fullName" [subtitle]="'Matricule ' + employee()!.employeeNumber">
        @if (canManage()) {
          <app-button variant="outline" icon="pi-pencil" iconPos="left" [routerLink]="['/payroll/employees', employee()!.id, 'edit']">Modifier</app-button>
          <app-button variant="outline" [icon]="employee()!.isActive ? 'pi-ban' : 'pi-check'" iconPos="left" (click)="toggleActive()">
            {{ employee()!.isActive ? 'Désactiver' : 'Réactiver' }}
          </app-button>
          <app-button variant="danger" icon="pi-trash" iconPos="left" (click)="confirmDelete()">Supprimer</app-button>
        }
      </app-page-header>

      <app-payroll-consult-banner
        [visible]="showConsultBanner()"
        message="Mode consultation — la gestion des dossiers salariés est réservée à la société cliente." />

      @if (alerts().length) {
        <div class="payroll-alert-list">
          @for (alert of alerts(); track alert) {
            <p-message [severity]="alert.severity" [text]="alert.text" styleClass="w-full" />
          }
        </div>
      }

      <p-tabView styleClass="ft-tabs">
        <p-tabPanel>
          <ng-template pTemplate="header">
            <i class="pi pi-id-card mr-2"></i>
            <span>Informations</span>
          </ng-template>
          <dl class="payroll-detail-grid card p-3">
            <div>
              <dt>CIN</dt>
              <dd>{{ employee()!.cin || '—' }}</dd>
            </div>
            <div>
              <dt>CNSS</dt>
              <dd>
                @if (employee()!.cnssNumber) {
                  {{ employee()!.cnssNumber }}
                } @else {
                  <p-tag value="CNSS manquant" severity="warn" />
                }
              </dd>
            </div>
            <div>
              <dt>Catégorie</dt>
              <dd>{{ employee()!.category || '—' }}</dd>
            </div>
            <div>
              <dt>Échelon</dt>
              <dd>{{ employee()!.echelon || '—' }}</dd>
            </div>
            <div>
              <dt>Date naissance</dt>
              <dd>{{ employee()!.dateOfBirth ? (employee()!.dateOfBirth | date:'dd/MM/yyyy') : '—' }}</dd>
            </div>
            <div>
              <dt>Embauche</dt>
              <dd>{{ employee()!.hireDate | date:'dd/MM/yyyy' }}</dd>
            </div>
            @if (employee()!.terminationDate) {
              <div>
                <dt>Date de sortie</dt>
                <dd>{{ employee()!.terminationDate | date:'dd/MM/yyyy' }}</dd>
              </div>
            }
            <div>
              <dt>Situation familiale</dt>
              <dd>{{ employee()!.maritalStatusDisplay }}</dd>
            </div>
            <div>
              <dt>Chef de famille</dt>
              <dd>{{ employee()!.isHeadOfFamily ? 'Oui' : 'Non' }}</dd>
            </div>
            <div>
              <dt>Enfants à charge</dt>
              <dd>{{ employee()!.dependentChildren }}</dd>
            </div>
            <div>
              <dt>Dont étudiants / infirmes</dt>
              <dd>{{ employee()!.studentChildren }} / {{ employee()!.disabledChildren }}</dd>
            </div>
            <div>
              <dt>Parents à charge</dt>
              <dd>{{ employee()!.dependentParents }}</dd>
            </div>
            <div>
              <dt>Email</dt>
              <dd>{{ employee()!.email || '—' }}</dd>
            </div>
            <div>
              <dt>Téléphone</dt>
              <dd>{{ employee()!.phone || '—' }}</dd>
            </div>
            <div>
              <dt>RIB</dt>
              <dd>{{ employee()!.rib || '—' }}</dd>
            </div>
            <div>
              <dt>Adresse</dt>
              <dd>{{ employee()!.address?.fullAddress || formatAddress() }}</dd>
            </div>
            <div>
              <dt>Statut</dt>
              <dd>
                <p-tag [value]="employee()!.isActive ? 'Actif' : 'Inactif'" [severity]="employee()!.isActive ? 'success' : 'secondary'" />
              </dd>
            </div>
          </dl>
        </p-tabPanel>

        <p-tabPanel>
          <ng-template pTemplate="header">
            <i class="pi pi-file mr-2"></i>
            <span>Contrats</span>
          </ng-template>
          @if (canManage()) {
            <app-button variant="primary" icon="pi-plus" iconPos="left" (click)="openContractDialog()" class="mb-3">Ajouter un contrat</app-button>
          }
          @for (c of employee()!.contracts; track c.id) {
            <div class="card p-3 mb-3">
              <div class="contract-header">
                <div>
                  <strong>{{ c.typeDisplay }}</strong> — {{ c.regimeDisplay }} — {{ c.weeklyRegimeDisplay }}
                  @if (!c.isActive) { <p-tag value="Inactif" severity="secondary" class="ml-2" /> }
                </div>
                @if (canManage()) {
                  <span class="contract-actions">
                    <button type="button" class="p-button p-button-text p-button-sm" (click)="editContract(c)">Modifier</button>
                    <button type="button" class="p-button p-button-text p-button-danger p-button-sm" (click)="confirmDeleteContract(c)">Supprimer</button>
                  </span>
                }
              </div>
              <p>Du {{ c.startDate | date:'dd/MM/yyyy' }} @if (c.endDate) { au {{ c.endDate | date:'dd/MM/yyyy' }} }</p>
              <p>Salaire base : {{ c.baseSalary | payrollAmount }} — AT : {{ c.workAccidentRate }} %</p>
              @if (c.jobTitle) { <p>Poste : {{ c.jobTitle }}</p> }
              @if (c.allowances.length) {
                <p-table [value]="c.allowances" styleClass="p-datatable-sm mt-2">
                  <ng-template pTemplate="header">
                    <tr><th>Prime</th><th class="text-right">Montant</th><th>Imposable</th><th>CNSS</th></tr>
                  </ng-template>
                  <ng-template pTemplate="body" let-a>
                    <tr>
                      <td>{{ a.label }}</td>
                      <td class="text-right">{{ a.amount | payrollAmount }}</td>
                      <td>{{ a.taxable ? 'Oui' : 'Non' }}</td>
                      <td>{{ a.subjectToCnss ? 'Oui' : 'Non' }}</td>
                    </tr>
                  </ng-template>
                </p-table>
              }
            </div>
          } @empty {
            <p>Aucun contrat enregistré.</p>
          }
        </p-tabPanel>

        <p-tabPanel>
          <ng-template pTemplate="header">
            <i class="pi pi-calendar mr-2"></i>
            <span>Congés</span>
          </ng-template>
          <app-employee-leaves-tab [employeeId]="employee()!.id" [readOnly]="!canManage()" />
        </p-tabPanel>

        <p-tabPanel>
          <ng-template pTemplate="header">
            <i class="pi pi-wallet mr-2"></i>
            <span>Avances</span>
          </ng-template>
          <app-employee-advances-tab [employeeId]="employee()!.id" [readOnly]="!canManage()" />
        </p-tabPanel>

        <p-tabPanel>
          <ng-template pTemplate="header">
            <i class="pi pi-heart mr-2"></i>
            <span>Mutuelles</span>
          </ng-template>
          <app-employee-social-funds-tab [employeeId]="employee()!.id" [readOnly]="!canManage()" />
        </p-tabPanel>

        <p-tabPanel>
          <ng-template pTemplate="header">
            <i class="pi pi-home mr-2"></i>
            <span>Avantages en nature</span>
          </ng-template>
          <app-employee-in-kind-benefits-tab [employeeId]="employee()!.id" [readOnly]="!canManage()" />
        </p-tabPanel>

        <p-tabPanel>
          <ng-template pTemplate="header">
            <i class="pi pi-credit-card mr-2"></i>
            <span>Prêts</span>
          </ng-template>
          <app-employee-loans-tab [employeeId]="employee()!.id" [readOnly]="!canManage()" />
        </p-tabPanel>

        <p-tabPanel>
          <ng-template pTemplate="header">
            <i class="pi pi-exclamation-triangle mr-2"></i>
            <span>Saisies</span>
          </ng-template>
          <app-employee-garnishments-tab [employeeId]="employee()!.id" [readOnly]="!canManageGarnishments()" />
        </p-tabPanel>
      </p-tabView>

      <app-contract-form-dialog
        [(visible)]="contractDialogVisible"
        [employeeId]="employee()!.id"
        [editContract]="editingContract"
        (saved)="reload()" />
    }
  `,
  styles: [`
    .contract-header { display: flex; justify-content: space-between; align-items: flex-start; gap: var(--spacing-4); }
    .contract-actions { white-space: nowrap; }
    .mb-3 { margin-bottom: var(--spacing-4); display: block; }
    .ml-2 { margin-left: var(--spacing-2); }
    .mt-2 { margin-top: var(--spacing-2); }
    .mr-2 { margin-right: var(--spacing-2); }
  `]
})
export class EmployeeDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly employees = inject(EmployeeService);
  private readonly payroll = inject(PayrollService);
  private readonly toast = inject(ToastService);
  private readonly confirmation = inject(ConfirmationService);
  readonly auth = inject(AuthService);

  loading = signal(true);
  employee = signal<EmployeeDetail | null>(null);
  monthlySmig = signal<number | null>(null);
  contractDialogVisible = false;
  editingContract: EmploymentContract | null = null;

  canManage = computed(() => canManagePayrollEmployees(this.auth));
  canManageGarnishments = computed(() => userCanManageGarnishments(this.auth));
  showConsultBanner = computed(() => isPayrollConsultMode(this.auth));

  alerts = computed(() => {
    const e = this.employee();
    if (!e) return [];
    const items: { severity: 'warn' | 'error' | 'info'; text: string }[] = [];
    if (!e.cnssNumber?.trim()) {
      items.push({ severity: 'warn', text: 'Numéro CNSS manquant — requis pour la déclaration sociale et la paie.' });
    }
    const activeContract = e.contracts.find(c => c.isActive);
    if (!activeContract) {
      items.push({ severity: 'warn', text: 'Aucun contrat actif — impossible de calculer la paie pour ce salarié.' });
    } else {
      const smig = this.monthlySmig();
      if (smig != null && activeContract.baseSalary < smig) {
        items.push({ severity: 'error', text: `Salaire de base (${formatPayrollAmount(activeContract.baseSalary)}) inférieur au SMIG mensuel (${formatPayrollAmount(smig)}).` });
      }
    }
    return items;
  });

  ngOnInit(): void {
    this.loadSmig();
    this.reload();
  }

  private loadSmig(): void {
    this.payroll.getParameters(new Date().getFullYear()).subscribe({
      next: res => this.monthlySmig.set(res.data?.monthlySmig ?? null),
      error: () => this.monthlySmig.set(null)
    });
  }

  reload(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.loading.set(true);
    this.employees.getById(id).subscribe({
      next: res => {
        this.employee.set(res.data ?? null);
        this.loading.set(false);
      },
      error: () => {
        this.toast.add({ severity: 'error', summary: 'Salarié', detail: 'Salarié introuvable.' });
        this.loading.set(false);
      }
    });
  }

  formatAddress(): string {
    const a = this.employee()?.address;
    if (!a) return '—';
    return [a.street, a.streetLine2, a.city, a.governorate].filter(Boolean).join(', ') || '—';
  }

  openContractDialog(): void {
    if (!this.canManage()) return;
    this.editingContract = null;
    this.contractDialogVisible = true;
  }

  editContract(c: EmploymentContract): void {
    if (!this.canManage()) return;
    this.editingContract = c;
    this.contractDialogVisible = true;
  }

  confirmDeleteContract(c: EmploymentContract): void {
    if (!this.canManage()) return;
    this.confirmation.confirm({
      message: `Supprimer le contrat ${c.typeDisplay} ?`,
      header: 'Confirmation',
      acceptLabel: 'Supprimer',
      rejectLabel: 'Annuler',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.employees.deleteContract(c.id).subscribe({
          next: () => {
            this.toast.add({ severity: 'success', summary: 'Contrat', detail: 'Contrat supprimé.' });
            this.reload();
          },
          error: err => this.toast.add({ severity: 'error', summary: 'Contrat', detail: err.error?.message ?? 'Suppression impossible.' })
        });
      }
    });
  }

  toggleActive(): void {
    if (!this.canManage() || !this.employee()) return;
    this.employees.toggleActive(this.employee()!.id).subscribe({
      next: () => {
        this.toast.add({ severity: 'success', summary: 'Salarié', detail: 'Statut mis à jour.' });
        this.reload();
      },
      error: err => this.toast.add({ severity: 'error', summary: 'Salarié', detail: err.error?.message ?? 'Action impossible.' })
    });
  }

  confirmDelete(): void {
    if (!this.canManage() || !this.employee()) return;
    this.confirmation.confirm({
      message: `Supprimer le salarié « ${this.employee()!.fullName} » ?`,
      header: 'Confirmation de suppression',
      acceptLabel: 'Supprimer',
      rejectLabel: 'Annuler',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.employees.delete(this.employee()!.id).subscribe({
          next: () => {
            this.toast.add({ severity: 'success', summary: 'Salarié', detail: 'Salarié supprimé.' });
            this.router.navigate(['/payroll/employees']);
          },
          error: err => this.toast.add({ severity: 'error', summary: 'Salarié', detail: err.error?.message ?? 'Suppression impossible.' })
        });
      }
    });
  }
}
