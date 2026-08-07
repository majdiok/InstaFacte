import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { InputSwitchModule } from 'primeng/inputswitch';
import { CheckboxModule } from 'primeng/checkbox';
import { CardModule } from 'primeng/card';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import {
  APP_MODULE_OPTIONS,
  AppModule,
  CreateTenantUserItem,
  TenantUsersService,
  UserModuleAccessItem,
  UserRole
} from '@core/services/tenant-users.service';
import {
  getModuleFeatureOptions,
  isSubFeatureOn,
  toModuleAccessApiPayload,
  withSubFeatureToggled
} from '@core/config/module-features.config';
import { ToastService } from '@core/services/toast.service';

interface BulkRow {
  email: string;
  firstName: string;
  lastName: string;
  password: string;
  role: UserRole;
  moduleAccess: UserModuleAccessItem[];
}

@Component({
  selector: 'app-tenant-users-bulk',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    TableModule,
    ButtonModule,
    InputTextModule,
    PasswordModule,
    SelectModule,
    DialogModule,
    InputSwitchModule,
    CheckboxModule,
    CardModule,
    PageHeaderComponent,
    BreadcrumbComponent
  ],
  template: `
    <app-breadcrumb [items]="breadcrumbItems"></app-breadcrumb>

    <app-page-header
      title="Ajouter des utilisateurs"
      subtitle="Créez plusieurs comptes avec rôle et modules pour chacun">
      <p-button label="Retour à la liste" icon="pi pi-arrow-left" [outlined]="true" routerLink="/settings/users"></p-button>
    </app-page-header>

    <p-card styleClass="bulk-card">
      <div class="toolbar">
        <p-button label="Ajouter une ligne" icon="pi pi-plus" [outlined]="true" (onClick)="addRow()"></p-button>
        <p-button
          label="Dupliquer modules (ligne 1 → toutes)"
          icon="pi pi-copy"
          [outlined]="true"
          severity="secondary"
          (onClick)="copyModulesFromFirst()"
          [disabled]="rows().length < 2"></p-button>
      </div>

      <p-table [value]="rows()" styleClass="bulk-table">
        <ng-template pTemplate="header">
          <tr>
            <th scope="col">Prénom</th>
            <th scope="col">Nom</th>
            <th scope="col">Email</th>
            <th scope="col">Mot de passe</th>
            <th scope="col">Rôle</th>
            <th scope="col" style="width: 12rem">Modules</th>
            <th scope="col" class="actions-col" aria-label="Actions"></th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-row let-ri="rowIndex">
          <tr>
            <td data-label="Prénom">
              <input
                pInputText
                class="w-full"
                [(ngModel)]="row.firstName"
                [name]="'bulk-firstName-' + ri"
                autocomplete="given-name"
                placeholder="Prénom" />
            </td>
            <td data-label="Nom">
              <input
                pInputText
                class="w-full"
                [(ngModel)]="row.lastName"
                [name]="'bulk-lastName-' + ri"
                autocomplete="family-name"
                placeholder="Nom" />
            </td>
            <td data-label="Email">
              <input
                pInputText
                class="w-full"
                [(ngModel)]="row.email"
                type="email"
                [name]="'bulk-email-' + ri"
                autocomplete="off"
                placeholder="nom@entreprise.fr" />
            </td>
            <td data-label="Mot de passe">
              <p-password
                [(ngModel)]="row.password"
                [ngModelOptions]="{standalone: true}"
                [inputId]="'bulk-password-' + ri"
                [feedback]="false"
                [toggleMask]="true"
                placeholder="Mot de passe"
                styleClass="w-full"
                inputStyleClass="w-full bulk-password-input"></p-password>
            </td>
            <td data-label="Rôle">
              <p-select
                [options]="roleOptions"
                [(ngModel)]="row.role"
                [name]="'bulk-role-' + ri"
                optionLabel="label"
                optionValue="value"
                placeholder="Rôle"
                styleClass="w-full"></p-select>
            </td>
            <td data-label="Modules">
              <div class="module-cell">
                <p-button
                  label="Configurer"
                  icon="pi pi-sliders-h"
                  [outlined]="true"
                  (onClick)="openModules(ri)"></p-button>
              </div>
            </td>
            <td data-label="Supprimer">
              <p-button
                icon="pi pi-trash"
                [rounded]="true"
                [text]="true"
                severity="danger"
                ariaLabel="Supprimer la ligne"
                (onClick)="removeRow(ri)"
                [disabled]="rows().length <= 1"></p-button>
            </td>
          </tr>
        </ng-template>
      </p-table>

      <p class="rows-hint">{{ bulkRowsHint() }}</p>

      <div class="footer-actions">
        <p-button label="Enregistrer tout" icon="pi pi-check" (onClick)="submit()" [loading]="submitting()"></p-button>
      </div>
    </p-card>

    <p-dialog
      [(visible)]="modulesVisible"
      header="Modules autorisés"
      [modal]="true"
      [style]="{ width: 'min(480px, 96vw)' }"
      (onHide)="onModulesDialogHide()">
      <p class="hint">Tout sélectionner correspond au maximum permis par le rôle de la ligne.</p>
      <div class="module-actions">
        <p-button label="Tout activer" [text]="true" (onClick)="setAllModules(true)"></p-button>
        <p-button label="Tout désactiver" [text]="true" (onClick)="setAllModules(false)"></p-button>
      </div>
      <div class="module-list">
        @for (m of workingModules(); track m.module) {
          <div class="module-row">
            <div class="module-row-main">
              <span class="module-title">{{ moduleLabel(m.module) }}</span>
              <p-inputSwitch
                [(ngModel)]="m.enabled"
                (ngModelChange)="afterModuleEnabledChange(m)"></p-inputSwitch>
            </div>
            @if (
              getModuleFeatureOptions(m.module).length &&
              m.enabled &&
              !isModuleToggleIneffectiveForRole(bulkModulesRole(), m.module)
            ) {
              <div
                class="module-subfeatures"
                role="group"
                [attr.aria-label]="'Sous-modules : ' + moduleLabel(m.module)">
                @for (opt of getModuleFeatureOptions(m.module); track opt.key) {
                  <label class="subfeature-row">
                    <p-checkbox
                      [binary]="true"
                      [ngModel]="isSubFeatureOn(m, opt.key)"
                      (ngModelChange)="onWorkingSubFeatureChange(m.module, opt.key, $event)"
                      [inputId]="'bulkFeat-' + m.module + '-' + opt.key"></p-checkbox>
                    <span class="subfeature-label-text">{{ opt.label }}</span>
                  </label>
                }
              </div>
            }
          </div>
        }
      </div>
      <div class="dialog-actions">
        <p-button label="Fermer" (onClick)="modulesVisible = false"></p-button>
      </div>
    </p-dialog>
  `,
  styles: [`
    :host ::ng-deep .bulk-card.p-card {
      align-self: flex-start;
      width: 100%;
    }
    :host ::ng-deep .bulk-card .p-card-body {
      display: flex;
      flex-direction: column;
    }

    .bulk-card { margin-top: var(--spacing-4); }

    .toolbar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--spacing-3);
      margin-bottom: var(--spacing-4);
    }

    .rows-hint {
      margin: var(--spacing-3) 0 0;
      font-size: var(--font-size-sm);
      color: var(--color-text-secondary);
    }

    .footer-actions {
      display: flex;
      justify-content: flex-end;
      margin-top: var(--spacing-5);
      padding-top: var(--spacing-5);
      border-top: 1px solid var(--color-border-subtle);
    }

    .module-cell {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
    }

    :host ::ng-deep .bulk-table.p-datatable .p-datatable-tbody > tr:nth-child(even):not(.overdue) {
      background: transparent;
    }
    :host ::ng-deep .bulk-table.p-datatable .p-datatable-tbody > tr:hover {
      transform: none;
      box-shadow: none;
      cursor: default;
      background: var(--color-background-hover);
    }
    :host ::ng-deep .bulk-table.p-datatable .p-datatable-tbody > tr:active {
      transform: none;
      background: var(--color-background-hover);
    }
    :host ::ng-deep .bulk-table.p-datatable .p-datatable-tbody > tr > td {
      vertical-align: middle;
    }

    :host ::ng-deep .bulk-table .p-inputtext,
    :host ::ng-deep .bulk-table .bulk-password-input.p-inputtext,
    :host ::ng-deep .bulk-table .p-password .p-inputtext {
      width: 100%;
      min-width: 8rem;
      min-height: 2.75rem;
    }
    :host ::ng-deep .bulk-table .p-password,
    :host ::ng-deep .bulk-table .p-password .p-password-input,
    :host ::ng-deep .bulk-table .p-select {
      width: 100%;
      min-width: 8rem;
    }
    :host ::ng-deep .bulk-table .p-select .p-select-label {
      display: flex;
      align-items: center;
      min-height: 2.75rem;
    }

    .actions-col {
      width: 4rem;
    }

    .hint { font-size: var(--font-size-sm); color: var(--color-neutral-600); margin-top: 0; }
    .module-actions { display: flex; gap: var(--spacing-2); margin-bottom: var(--spacing-2); }
    .module-list { display: flex; flex-direction: column; gap: var(--spacing-2); }
    .module-row {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);
      padding: var(--spacing-2) 0;
      border-bottom: 1px solid var(--color-neutral-100);
    }
    .module-row-main { display: flex; justify-content: space-between; align-items: center; gap: var(--spacing-3); }
    .module-title { font-size: var(--font-size-sm); font-weight: var(--font-weight-medium); color: var(--color-neutral-800); }
    .module-subfeatures {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-1);
      padding-top: var(--spacing-2);
      border-top: 1px dashed var(--color-neutral-200);
    }
    .subfeature-row {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
      margin: 0;
      font-size: var(--font-size-sm);
      color: var(--color-neutral-700);
      cursor: pointer;
    }
    .subfeature-label-text { line-height: 1.35; }
    .dialog-actions { display: flex; justify-content: flex-end; margin-top: var(--spacing-3); }
  `]
})
export class TenantUsersBulkComponent {
  private readonly tenantUsers = inject(TenantUsersService);
  private readonly toast = inject(ToastService);

  readonly getModuleFeatureOptions = getModuleFeatureOptions;
  readonly isSubFeatureOn = isSubFeatureOn;

  readonly breadcrumbItems: BreadcrumbItem[] = [
    { label: 'Tableau de bord', route: '/dashboard', icon: 'pi-home' },
    { label: 'Paramètres', route: '/settings' },
    { label: 'Utilisateurs', route: '/settings/users' },
    { label: 'Ajout groupé' }
  ];

  readonly roleOptions = [
    { label: 'Administrateur', value: 'Administrator' as UserRole },
    { label: 'Comptable', value: 'Accountant' as UserRole },
    { label: 'Client', value: 'Client' as UserRole },
    { label: 'Commercial', value: 'SalesRep' as UserRole },
    { label: 'Responsable Commercial', value: 'SalesManager' as UserRole },
    { label: 'Magasinier', value: 'Warehouse' as UserRole },
    { label: 'Acheteur', value: 'Purchaser' as UserRole },
    { label: 'Caissier', value: 'Cashier' as UserRole },
    { label: 'Auditeur', value: 'Auditor' as UserRole },
    { label: 'Superviseur', value: 'Supervisor' as UserRole },
    { label: 'Développeur', value: 'Developer' as UserRole }
  ];

  rows = signal<BulkRow[]>([this.emptyRow()]);
  submitting = signal(false);

  /** Compteur d’aide sous le tableau (sans effet sur la soumission). */
  bulkRowsHint(): string {
    const n = this.rows().length;
    if (n <= 1) {
      return '1 ligne dans le formulaire — chaque ligne complète crée un utilisateur à l’enregistrement.';
    }
    return `${n} lignes dans le formulaire — chaque ligne complète crée un utilisateur à l’enregistrement.`;
  }

  modulesVisible = false;
  configRowIndex: number | null = null;
  workingModules = signal<UserModuleAccessItem[]>([]);
  bulkModulesRole = signal<UserRole>('Accountant');

  addRow(): void {
    this.rows.update(list => [...list, this.emptyRow()]);
  }

  removeRow(index: number): void {
    this.rows.update(list => (list.length <= 1 ? list : list.filter((_, i) => i !== index)));
  }

  emptyRow(): BulkRow {
    return {
      email: '',
      firstName: '',
      lastName: '',
      password: '',
      role: 'Accountant',
      moduleAccess: APP_MODULE_OPTIONS.map(o => ({
        module: o.value,
        enabled: true,
        enabledFeatureKeys: null
      }))
    };
  }

  openModules(rowIndex: number): void {
    this.configRowIndex = rowIndex;
    const row = this.rows()[rowIndex];
    this.bulkModulesRole.set(row.role);
    this.workingModules.set(
      row.moduleAccess.map(x => ({
        ...x,
        enabledFeatureKeys: x.enabledFeatureKeys ?? null
      }))
    );
    this.modulesVisible = true;
  }

  afterModuleEnabledChange(m: UserModuleAccessItem): void {
    this.workingModules.update(draft => {
      const i = draft.findIndex(x => x.module === m.module);
      if (i < 0) return draft;
      const prev = draft[i];
      const next = [...draft];
      next[i] = m.enabled
        ? { ...prev, enabled: true }
        : { ...prev, enabled: false, enabledFeatureKeys: null };
      return next;
    });
  }

  onWorkingSubFeatureChange(module: AppModule, featureKey: string, checked: boolean): void {
    this.workingModules.update(draft => {
      const i = draft.findIndex(x => x.module === module);
      if (i < 0) return draft;
      const next = [...draft];
      next[i] = withSubFeatureToggled(draft[i], featureKey, checked);
      return next;
    });
  }

  isModuleToggleIneffectiveForRole(role: UserRole, module: AppModule): boolean {
    if (role === 'Administrator' || role === 'Supervisor') return false;
    
    // Rôles avec accès métier complet (modulo configuration)
    if (role === 'Accountant' || role === 'Auditor') return false;

    // Studio (low-code) : pertinent uniquement pour le Développeur
    // (Admin/Superviseur/Comptable/Auditeur déjà traités au-dessus).
    if (module === AppModule.Studio) {
      return role !== 'Developer';
    }

    // Rôles très restreints : retourne true pour les modules qu'ils n'auront jamais
    switch (role) {
      case 'Developer':
        // Le Développeur ne lit que Clients/Produits/Ventes(factures)/Rapports ; le reste est sans effet.
        return module !== AppModule.Clients
          && module !== AppModule.Products
          && module !== AppModule.Sales
          && module !== AppModule.Reports;

      case 'Client':
        return module !== AppModule.Sales && module !== AppModule.Treasury && module !== AppModule.Administration;
      
      case 'SalesRep':
      case 'SalesManager':
        return module === AppModule.Administration || module === AppModule.Purchases || module === AppModule.Stock;
        
      case 'Warehouse':
        return module === AppModule.Administration || module === AppModule.Sales || module === AppModule.Purchases || module === AppModule.Treasury || module === AppModule.Reports;
        
      case 'Purchaser':
        return module === AppModule.Administration || module === AppModule.Sales || module === AppModule.Treasury || module === AppModule.Reports;
        
      case 'Cashier':
        return module === AppModule.Administration || module === AppModule.Purchases || module === AppModule.Reports;
    }

    return false;
  }

  persistModulesFromWorking(): void {
    const i = this.configRowIndex;
    if (i === null) return;
    const next = [...this.rows()];
    next[i] = {
      ...next[i],
      moduleAccess: this.workingModules().map(x => ({
        ...x,
        enabledFeatureKeys: x.enabledFeatureKeys == null ? null : [...x.enabledFeatureKeys]
      }))
    };
    this.rows.set(next);
  }

  setAllModules(enabled: boolean): void {
    this.workingModules.update(list =>
      list.map(x => ({
        ...x,
        enabled,
        enabledFeatureKeys: enabled ? x.enabledFeatureKeys : null
      }))
    );
  }

  moduleLabel(m: AppModule): string {
    return APP_MODULE_OPTIONS.find(o => o.value === m)?.label ?? String(m);
  }

  onModulesDialogHide(): void {
    this.persistModulesFromWorking();
  }

  copyModulesFromFirst(): void {
    const list = this.rows();
    if (list.length < 2) return;
    const template = this.cloneModuleAccess(list[0].moduleAccess);
    this.rows.set(
      list.map((r, i) => (i === 0 ? r : { ...r, moduleAccess: this.cloneModuleAccess(template) }))
    );
    this.toast.add({ severity: 'info', summary: 'Modules copiés', detail: 'Configuration de la première ligne appliquée aux autres.' });
  }

  private cloneModuleAccess(items: UserModuleAccessItem[]): UserModuleAccessItem[] {
    return items.map(x => ({
      ...x,
      enabledFeatureKeys: x.enabledFeatureKeys == null ? null : [...x.enabledFeatureKeys]
    }));
  }

  submit(): void {
    this.persistModulesFromWorking();
    const items: CreateTenantUserItem[] = [];
    for (const r of this.rows()) {
      if (!r.email.trim() && !r.firstName.trim() && !r.lastName.trim() && !r.password.trim()) continue;
      if (!r.email.trim() || !r.firstName.trim() || !r.lastName.trim() || !r.password.trim()) {
        this.toast.add({
          severity: 'warn',
          summary: 'Formulaire incomplet',
          detail: 'Chaque ligne renseignée doit avoir prénom, nom, email et mot de passe.'
        });
        return;
      }
      items.push({
        email: r.email.trim(),
        firstName: r.firstName.trim(),
        lastName: r.lastName.trim(),
        password: r.password,
        role: r.role,
        moduleAccess: toModuleAccessApiPayload(r.moduleAccess)
      });
    }
    if (items.length === 0) {
      this.toast.add({ severity: 'warn', summary: 'Aucune donnée', detail: 'Ajoutez au moins un utilisateur.' });
      return;
    }

    this.submitting.set(true);
    this.tenantUsers.batchCreate(items).subscribe({
      next: res => {
        this.submitting.set(false);
        if (res.success) {
          this.toast.add({ severity: 'success', summary: 'Créé', detail: res.message ?? 'Utilisateurs créés' });
          this.rows.set([this.emptyRow()]);
        } else {
          this.toast.add({ severity: 'error', summary: 'Erreur', detail: res.errors?.[0] ?? 'Échec' });
        }
      },
      error: () => {
        this.submitting.set(false);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Échec de la création' });
      }
    });
  }
}
