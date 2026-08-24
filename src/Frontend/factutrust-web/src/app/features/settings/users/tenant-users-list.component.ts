import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { InputSwitchModule } from 'primeng/inputswitch';
import { CheckboxModule } from 'primeng/checkbox';
import { PasswordModule } from 'primeng/password';
import { InputTextModule } from 'primeng/inputtext';
import { CardModule } from 'primeng/card';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { TableTotalsBarComponent, TotalMetric } from '@shared/components/table-totals-bar/table-totals-bar.component';
import {
  APP_MODULE_OPTIONS,
  AppModule,
  TenantUserListItem,
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
import { AuthService } from '@core/services/auth.service';

@Component({
  selector: 'app-tenant-users-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    TableModule,
    ButtonModule,
    TagModule,
    DialogModule,
    SelectModule,
    InputSwitchModule,
    CheckboxModule,
    PasswordModule,
    InputTextModule,
    CardModule,
    PageHeaderComponent,
    BreadcrumbComponent,
    TableTotalsBarComponent
  ],
  template: `
    <app-breadcrumb [items]="breadcrumbItems"></app-breadcrumb>

    <app-page-header
      title="Utilisateurs et rôles"
      subtitle="Gérez les accès de votre équipe">
      <p-button label="Retour" icon="pi pi-arrow-left" [outlined]="true" routerLink="/settings"></p-button>
      <p-button label="Ajouter un utilisateur" icon="pi pi-user-plus" (onClick)="openCreate()"></p-button>
      <p-button label="Ajouter plusieurs" icon="pi pi-users" [outlined]="true" routerLink="/settings/users/add"></p-button>
    </app-page-header>

    <app-table-totals-bar [metrics]="summaryMetrics()" [loading]="loading()"></app-table-totals-bar>

    <p-card styleClass="users-card">
      <p-table [value]="users()" [loading]="loading()" [rows]="15" [paginator]="true" responsiveLayout="scroll">
        <ng-template pTemplate="header">
          <tr>
            <th>Utilisateur</th>
            <th>Rôle</th>
            <th>Dernière connexion</th>
            <th>Statut</th>
            <th style="width: 8rem">Actions</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-row>
          <tr>
            <td>
              <div class="user-cell">
                <span class="name">{{ row.firstName }} {{ row.lastName }}</span>
                <span class="email">{{ row.email }}</span>
              </div>
            </td>
            <td>{{ row.roleDisplay }}</td>
            <td>{{ formatDate(row.lastLoginAt) }}</td>
            <td>
              <p-tag [severity]="row.isActive ? 'success' : 'danger'" [value]="row.isActive ? 'Actif' : 'Inactif'"></p-tag>
            </td>
            <td>
              <p-button icon="pi pi-cog" [rounded]="true" [text]="true" (onClick)="openEdit(row)" ariaLabel="Configurer"></p-button>
            </td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr>
            <td colspan="5" class="empty-msg">Aucun utilisateur à afficher</td>
          </tr>
        </ng-template>
      </p-table>
    </p-card>

    <p-dialog
      [(visible)]="createVisible"
      [modal]="true"
      [draggable]="false"
      styleClass="user-form-dialog"
      [style]="{ width: 'min(700px, 96vw)' }"
      [contentStyle]="{ overflow: 'auto', maxHeight: 'min(62vh, 520px)', padding: 0 }"
      header="Ajouter un utilisateur"
      (onHide)="resetCreateForm()">
      <div class="user-dialog-body">
        <div class="user-form-grid">
          <div class="form-field">
            <label class="field-label" for="createFirstName">Prénom</label>
            <input pInputText id="createFirstName" class="w-full" [(ngModel)]="createFirstName" autocomplete="given-name" />
          </div>
          <div class="form-field">
            <label class="field-label" for="createLastName">Nom</label>
            <input pInputText id="createLastName" class="w-full" [(ngModel)]="createLastName" autocomplete="family-name" />
          </div>
          <div class="form-field field-span-full">
            <label class="field-label" for="createEmail">Email</label>
            <input pInputText id="createEmail" class="w-full" [(ngModel)]="createEmail" type="email" autocomplete="off" />
          </div>
          <div class="form-field field-span-full">
            <label class="field-label" for="createPassword">Mot de passe</label>
            <p-password
              inputId="createPassword"
              [(ngModel)]="createPassword"
              [feedback]="true"
              [toggleMask]="true"
              styleClass="w-full"
              inputStyleClass="w-full"></p-password>
          </div>
          <div class="form-field">
            <label class="field-label" for="createPhone">Téléphone (optionnel)</label>
            <input pInputText id="createPhone" class="w-full" [(ngModel)]="createPhone" type="tel" autocomplete="tel" />
          </div>
          <div class="form-field">
            <label class="field-label" for="createRole">Rôle</label>
            <p-select
              inputId="createRole"
              [options]="roleOptions"
              [(ngModel)]="createRole"
              optionLabel="label"
              optionValue="value"
              styleClass="w-full"></p-select>
          </div>
        </div>

        <section class="form-section modules-section" aria-labelledby="create-modules-heading">
          <div class="form-section-head">
            <h4 id="create-modules-heading" class="form-section-title">Modules</h4>
            <p class="modules-hint">
              Les droits effectifs combinent le rôle et les modules enregistrés ici. Certains rôles peuvent
              recevoir des modules supplémentaires définis par la politique produit (ex. Magasinier et Clients)
              uniquement après enregistrement. Un module n’apparaît dans le menu que s’il correspond à au moins
              une permission effective après connexion.
            </p>
          </div>
          <div class="module-list">
            @for (m of createModuleDraft(); track m.module) {
              <div class="module-row">
                <div class="module-row-main">
                  <label class="module-row-label" [for]="'createMod' + m.module">{{ moduleLabel(m.module) }}</label>
                  <p-inputSwitch
                    [(ngModel)]="m.enabled"
                    (ngModelChange)="afterCreateModuleEnabledChange(m)"
                    [inputId]="'createMod' + m.module"
                    [disabled]="isModuleToggleIneffectiveForRole(createRole, m.module)"></p-inputSwitch>
                </div>
                @if (getModuleFeatureOptions(m.module).length && m.enabled && !isModuleToggleIneffectiveForRole(createRole, m.module)) {
                  <div
                    class="module-subfeatures"
                    role="group"
                    [attr.aria-label]="'Sous-modules : ' + moduleLabel(m.module)">
                    @for (opt of getModuleFeatureOptions(m.module); track opt.key) {
                      <label class="subfeature-row">
                        <p-checkbox
                          [binary]="true"
                          [ngModel]="isSubFeatureOn(m, opt.key)"
                          (ngModelChange)="onCreateSubFeatureChange(m.module, opt.key, $event)"
                          [inputId]="'createFeat-' + m.module + '-' + opt.key"></p-checkbox>
                        <span class="subfeature-label-text">{{ opt.label }}</span>
                      </label>
                    }
                  </div>
                }
              </div>
            }
          </div>
        </section>
      </div>
      <ng-template pTemplate="footer">
        <div class="user-dialog-footer-actions">
          <p-button label="Annuler" [outlined]="true" type="button" (onClick)="closeCreate()"></p-button>
          <p-button label="Créer" icon="pi pi-check" type="button" (onClick)="saveCreate()" [loading]="createSaving()"></p-button>
        </div>
      </ng-template>
    </p-dialog>

    <p-dialog
      [(visible)]="editVisible"
      [modal]="true"
      [draggable]="false"
      styleClass="user-form-dialog"
      [style]="{ width: 'min(700px, 96vw)' }"
      [contentStyle]="{ overflow: 'auto', maxHeight: 'min(62vh, 520px)', padding: 0 }"
      header="Modifier l'utilisateur"
      (onHide)="closeEdit()">
      @if (editUser()) {
        <div class="user-dialog-body">
          <p class="edit-email">{{ editUser()!.email }}</p>

          <div class="user-form-grid edit-role-grid">
            <div class="form-field">
              <label class="field-label" for="editRole">Rôle</label>
              <p-select
                inputId="editRole"
                [options]="roleOptions"
                [(ngModel)]="editRole"
                optionLabel="label"
                optionValue="value"
                styleClass="w-full"></p-select>
            </div>
            <div class="form-field switch-field">
              <label class="field-label" for="activeSw">Compte actif</label>
              <div class="switch-field-control">
                <p-inputSwitch inputId="activeSw" [(ngModel)]="editActive"></p-inputSwitch>
              </div>
            </div>
          </div>

          <div class="form-field field-span-full">
            <label class="field-label" for="editNewPassword">Nouveau mot de passe (optionnel)</label>
            <p-password
              inputId="editNewPassword"
              [(ngModel)]="editNewPassword"
              [feedback]="false"
              [toggleMask]="true"
              styleClass="w-full"
              inputStyleClass="w-full"></p-password>
          </div>

          <section class="form-section modules-section" aria-labelledby="edit-modules-heading">
            <div class="form-section-head">
              <h4 id="edit-modules-heading" class="form-section-title">Modules</h4>
              <p class="modules-hint">
                Les droits effectifs combinent le rôle et les modules enregistrés ici. Certains rôles peuvent
                recevoir des modules supplémentaires définis par la politique produit (ex. Magasinier et Clients)
                uniquement après enregistrement. Un module n’apparaît dans le menu que s’il correspond à au moins
                une permission effective après connexion.
              </p>
            </div>
            <div class="module-list">
              @for (m of moduleDraft(); track m.module) {
                <div class="module-row">
                  <div class="module-row-main">
                    <label class="module-row-label" [for]="'editMod' + m.module">{{ moduleLabel(m.module) }}</label>
                    <p-inputSwitch
                      [(ngModel)]="m.enabled"
                      (ngModelChange)="afterEditModuleEnabledChange(m)"
                      [inputId]="'editMod' + m.module"
                      [disabled]="
                        isAdministrationModuleLockedForEdit(m) ||
                        isModuleToggleIneffectiveForRole(editRole, m.module)
                      "></p-inputSwitch>
                  </div>
                  @if (
                    getModuleFeatureOptions(m.module).length &&
                    m.enabled &&
                    !isModuleToggleIneffectiveForRole(editRole, m.module)
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
                            (ngModelChange)="onEditSubFeatureChange(m.module, opt.key, $event)"
                            [inputId]="'editFeat-' + m.module + '-' + opt.key"
                            [disabled]="isAdministrationModuleLockedForEdit(m)"></p-checkbox>
                          <span class="subfeature-label-text">{{ opt.label }}</span>
                        </label>
                      }
                    </div>
                  }
                </div>
              }
            </div>
          </section>
        </div>
      }
      <ng-template pTemplate="footer">
        @if (editUser()) {
          <div class="user-dialog-footer-actions">
            <p-button label="Annuler" [outlined]="true" type="button" (onClick)="closeEdit()"></p-button>
            <p-button label="Enregistrer" type="button" (onClick)="saveEdit()" [loading]="saving()"></p-button>
          </div>
        }
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .users-card { margin-top: var(--spacing-4); }
    .user-cell { display: flex; flex-direction: column; gap: 0.15rem; }
    .user-cell .name { font-weight: var(--font-weight-medium); color: var(--color-neutral-900); }
    .user-cell .email { font-size: var(--font-size-sm); color: var(--color-neutral-500); }
    .empty-msg { text-align: center; padding: var(--spacing-6); color: var(--color-neutral-500); }

    .user-dialog-body {
      padding: var(--spacing-5) var(--spacing-6);
      display: flex;
      flex-direction: column;
      gap: var(--spacing-5);
    }

    .user-form-grid {
      display: grid;
      grid-template-columns: 1fr;
      gap: var(--spacing-3) var(--spacing-4);
    }

    @media (min-width: 560px) {
      .user-form-grid {
        grid-template-columns: 1fr 1fr;
      }
      .field-span-full {
        grid-column: 1 / -1;
      }
    }

    .form-field {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);
      min-width: 0;
    }

    .edit-email {
      margin: 0;
      color: var(--color-neutral-600);
      font-size: var(--font-size-sm);
    }

    .field-label {
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-medium);
      color: var(--color-neutral-700);
    }

    .switch-field .switch-field-control {
      display: flex;
      align-items: center;
      min-height: 2.5rem;
    }

    @media (min-width: 560px) {
      .edit-role-grid {
        align-items: end;
      }
      .switch-field .field-label {
        margin-bottom: 0;
      }
    }

    .form-section {
      margin: 0;
      padding: 0;
      border: none;
    }

    .form-section-head {
      margin-bottom: var(--spacing-2);
    }

    .form-section-title {
      margin: 0 0 var(--spacing-1);
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semibold);
      color: var(--color-text-tertiary, var(--color-neutral-500));
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }

    .modules-hint {
      margin: 0;
      font-size: var(--font-size-sm);
      color: var(--color-neutral-500);
      line-height: 1.45;
    }

    .modules-section {
      padding-top: var(--spacing-2);
      border-top: 1px solid var(--color-border-subtle, var(--color-neutral-100));
    }

    .module-list {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);
      margin-top: var(--spacing-3);
    }

    .module-row {
      display: flex;
      flex-direction: column;
      align-items: stretch;
      gap: var(--spacing-2);
      padding: var(--spacing-3);
      background: var(--color-neutral-50, #f8fafc);
      border: 1px solid var(--color-border-subtle, var(--color-neutral-100));
      border-radius: var(--radius-lg, 0.5rem);
    }

    .module-row-main {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--spacing-3);
    }

    .module-subfeatures {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-1);
      padding: var(--spacing-2) 0 0;
      margin: 0;
      border-top: 1px dashed var(--color-border-subtle, var(--color-neutral-200));
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

    .subfeature-label-text {
      line-height: 1.35;
    }

    .module-row-label {
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-medium);
      color: var(--color-neutral-800);
      cursor: pointer;
      line-height: 1.35;
      flex: 1;
      min-width: 0;
    }

    .user-dialog-footer-actions {
      display: flex;
      justify-content: flex-end;
      gap: var(--spacing-3);
      width: 100%;
    }

    .user-dialog-body .w-full { width: 100%; }

    :host ::ng-deep .user-form-dialog.p-dialog {
      border-radius: var(--radius-xl, 0.75rem);
      box-shadow: var(--shadow-xl);
      overflow: hidden;
    }

    :host ::ng-deep .user-form-dialog .p-dialog-header {
      padding: var(--spacing-4) var(--spacing-6);
      border-bottom: 1px solid var(--color-border-subtle, #e2e8f0);
      background: var(--color-background-elevated, var(--color-white));
    }

    :host ::ng-deep .user-form-dialog .p-dialog-title {
      font-size: var(--font-size-lg);
      font-weight: var(--font-weight-semibold);
      color: var(--color-text-primary, var(--color-neutral-900));
    }

    :host ::ng-deep .user-form-dialog .p-dialog-content {
      border-radius: 0;
    }

    :host ::ng-deep .user-form-dialog .p-dialog-footer {
      padding: var(--spacing-4) var(--spacing-6);
      border-top: 1px solid var(--color-border-subtle, #e2e8f0);
      background: var(--color-background-subtle, #f8fafc);
    }

    :host ::ng-deep .user-form-dialog .p-select,
    :host ::ng-deep .user-form-dialog .p-password,
    :host ::ng-deep .user-form-dialog .p-password .p-inputtext {
      width: 100%;
    }
  `]
})
export class TenantUsersListComponent implements OnInit {
  private readonly tenantUsers = inject(TenantUsersService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  /** Exposés au template (fonctions importées). */
  readonly getModuleFeatureOptions = getModuleFeatureOptions;
  readonly isSubFeatureOn = isSubFeatureOn;

  readonly breadcrumbItems: BreadcrumbItem[] = [
    { label: 'Tableau de bord', route: '/dashboard', icon: 'pi-home' },
    { label: 'Paramètres', route: '/settings' },
    { label: 'Utilisateurs' }
  ];

  users = signal<TenantUserListItem[]>([]);
  loading = signal(false);
  saving = signal(false);
  createSaving = signal(false);

  /** Totaux de la zone, calculés sur les utilisateurs chargés (liste côté client). */
  summaryMetrics = computed<TotalMetric[]>(() => {
    const rows = this.users();
    return [
      { label: 'Utilisateurs', value: rows.length, format: 'number', icon: 'pi-users', tone: 'primary' },
      { label: 'Actifs', value: rows.filter(u => u.isActive).length, format: 'number', icon: 'pi-check-circle', tone: 'emerald' },
      { label: 'Inactifs', value: rows.filter(u => !u.isActive).length, format: 'number', icon: 'pi-ban', tone: 'neutral' }
    ];
  });

  createVisible = false;
  createFirstName = '';
  createLastName = '';
  createEmail = '';
  createPassword = '';
  createPhone = '';
  createRole: UserRole = 'Accountant';
  createModuleDraft = signal<UserModuleAccessItem[]>(this.defaultModulesAllEnabled());

  editVisible = false;
  editUser = signal<TenantUserListItem | null>(null);
  editRole: UserRole = 'Accountant';
  editActive = true;
  editNewPassword = '';
  moduleDraft = signal<UserModuleAccessItem[]>([]);

  readonly roleOptions = [
    { label: 'Administrateur', value: 'Administrator' as UserRole },
    { label: 'Comptable', value: 'Accountant' as UserRole },
    { label: 'Commercial', value: 'SalesRep' as UserRole },
    { label: 'Responsable Commercial', value: 'SalesManager' as UserRole },
    { label: 'Magasinier', value: 'Warehouse' as UserRole },
    { label: 'Acheteur', value: 'Purchaser' as UserRole },
    { label: 'Caissier', value: 'Cashier' as UserRole },
    { label: 'Auditeur', value: 'Auditor' as UserRole },
    { label: 'Superviseur', value: 'Supervisor' as UserRole },
    { label: 'Développeur', value: 'Developer' as UserRole }
  ];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.tenantUsers.list().subscribe({
      next: res => {
        this.loading.set(false);
        if (res.success && res.data) {
          this.users.set(res.data);
        } else {
          this.toast.add({ severity: 'error', summary: 'Erreur', detail: res.errors?.[0] ?? 'Chargement impossible' });
        }
      },
      error: () => {
        this.loading.set(false);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Impossible de charger les utilisateurs' });
      }
    });
  }

  formatDate(iso: string | null): string {
    if (!iso) return '—';
    try {
      return new Date(iso).toLocaleString('fr-FR', { dateStyle: 'short', timeStyle: 'short' });
    } catch {
      return '—';
    }
  }

  moduleLabel(m: AppModule): string {
    return APP_MODULE_OPTIONS.find(o => o.value === m)?.label ?? String(m);
  }

  /** Aligné avec l’API : un administrateur ne peut pas retirer le module Administration sur son propre compte. */
  isAdministrationModuleLockedForEdit(m: UserModuleAccessItem): boolean {
    if (m.module !== AppModule.Administration) {
      return false;
    }
    const row = this.editUser();
    const me = this.auth.user();
    return !!row && !!me && row.id === me.id;
  }

  /**
   * Rôle Client : aucune permission issue de ces modules (plafond rôle), donc bascule sans effet — désactivée pour éviter les faux positifs.
   */
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

  openCreate(): void {
    this.closeEdit();
    this.resetCreateForm();
    this.createVisible = true;
  }

  closeCreate(): void {
    this.createVisible = false;
    this.resetCreateForm();
  }

  resetCreateForm(): void {
    this.createFirstName = '';
    this.createLastName = '';
    this.createEmail = '';
    this.createPassword = '';
    this.createPhone = '';
    this.createRole = 'Accountant';
    this.createModuleDraft.set(this.defaultModulesAllEnabled());
  }

  private defaultModulesAllEnabled(): UserModuleAccessItem[] {
    return APP_MODULE_OPTIONS.map(o => ({ module: o.value, enabled: true, enabledFeatureKeys: null }));
  }

  onCreateSubFeatureChange(module: AppModule, featureKey: string, checked: boolean): void {
    this.createModuleDraft.update(draft => {
      const i = draft.findIndex(x => x.module === module);
      if (i < 0) return draft;
      const next = [...draft];
      next[i] = withSubFeatureToggled(draft[i], featureKey, checked);
      return next;
    });
  }

  onEditSubFeatureChange(module: AppModule, featureKey: string, checked: boolean): void {
    this.moduleDraft.update(draft => {
      const i = draft.findIndex(x => x.module === module);
      if (i < 0) return draft;
      const next = [...draft];
      next[i] = withSubFeatureToggled(draft[i], featureKey, checked);
      return next;
    });
  }

  afterCreateModuleEnabledChange(m: UserModuleAccessItem): void {
    this.syncModuleEnabledInDraft(this.createModuleDraft, m);
  }

  afterEditModuleEnabledChange(m: UserModuleAccessItem): void {
    this.syncModuleEnabledInDraft(this.moduleDraft, m);
  }

  private syncModuleEnabledInDraft(
    target: { update: (fn: (d: UserModuleAccessItem[]) => UserModuleAccessItem[]) => void },
    m: UserModuleAccessItem
  ): void {
    target.update(draft => {
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

  saveCreate(): void {
    if (!this.createFirstName.trim() || !this.createLastName.trim() || !this.createEmail.trim() || !this.createPassword) {
      this.toast.add({
        severity: 'warn',
        summary: 'Champs requis',
        detail: 'Renseignez le prénom, le nom, l’email et le mot de passe.'
      });
      return;
    }

    this.createSaving.set(true);
    const payload = {
      email: this.createEmail.trim(),
      firstName: this.createFirstName.trim(),
      lastName: this.createLastName.trim(),
      password: this.createPassword,
      role: this.createRole,
      phoneNumber: this.createPhone.trim() || undefined,
      moduleAccess: toModuleAccessApiPayload(this.createModuleDraft())
    };

    this.tenantUsers.create(payload).subscribe({
      next: res => {
        this.createSaving.set(false);
        if (res.success) {
          this.toast.add({ severity: 'success', summary: 'Créé', detail: res.message ?? 'Utilisateur créé' });
          this.createVisible = false;
          this.resetCreateForm();
          this.load();
        } else {
          this.toast.add({ severity: 'error', summary: 'Erreur', detail: res.errors?.[0] ?? 'Échec de la création' });
        }
      },
      error: () => {
        this.createSaving.set(false);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Échec de la création' });
      }
    });
  }

  openEdit(row: TenantUserListItem): void {
    this.createVisible = false;
    this.resetCreateForm();
    this.editUser.set(row);
    this.editRole = row.role;
    this.editActive = row.isActive;
    this.editNewPassword = '';
    this.moduleDraft.set(
      row.moduleFeatures && row.moduleFeatures.length > 0
        ? this.moduleDraftFromStoredGrants(row.moduleFeatures)
        : this.modulesFromEnabledIds(row.enabledModuleIds)
    );
    this.editVisible = true;
  }

  closeEdit(): void {
    this.editVisible = false;
    this.editUser.set(null);
  }

  saveEdit(): void {
    const u = this.editUser();
    if (!u) return;
    this.saving.set(true);
    const body: Parameters<TenantUsersService['update']>[1] = {
      role: this.editRole,
      isActive: this.editActive,
      moduleAccess: toModuleAccessApiPayload(this.moduleDraft())
    };
    if (this.editNewPassword.trim()) {
      body.newPassword = this.editNewPassword.trim();
    }
    this.tenantUsers.update(u.id, body).subscribe({
      next: res => {
        this.saving.set(false);
        if (res.success) {
          this.toast.add({ severity: 'success', summary: 'Enregistré', detail: res.message ?? 'Utilisateur mis à jour' });
          this.closeEdit();
          this.load();
        } else {
          this.toast.add({ severity: 'error', summary: 'Erreur', detail: res.errors?.[0] ?? 'Échec de la mise à jour' });
        }
      },
      error: () => {
        this.saving.set(false);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail: 'Échec de la mise à jour' });
      }
    });
  }

  private modulesFromEnabledIds(ids: number[]): UserModuleAccessItem[] {
    return APP_MODULE_OPTIONS.map(o => ({
      module: o.value,
      enabled: ids.length === 0 ? true : ids.includes(o.value),
      enabledFeatureKeys: null
    }));
  }

  private moduleDraftFromStoredGrants(
    stored: NonNullable<TenantUserListItem['moduleFeatures']>
  ): UserModuleAccessItem[] {
    return APP_MODULE_OPTIONS.map(o => {
      const sf = stored.find(f => f.module === o.value);
      if (!sf) {
        return { module: o.value, enabled: true, enabledFeatureKeys: null };
      }
      const fk = sf.featureKeys == null ? null : [...sf.featureKeys];
      return { module: o.value, enabled: sf.enabled, enabledFeatureKeys: fk };
    });
  }
}
