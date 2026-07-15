import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, takeUntil } from 'rxjs';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { InputSwitchModule } from 'primeng/inputswitch';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import {
  StockService,
  Warehouse,
  CreateWarehouseRequest,
  UpdateWarehouseRequest
} from '@core/services/stock.service';

@Component({
  selector: 'app-warehouses',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    TableModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    InputTextareaModule,
    InputSwitchModule,
    CardModule,
    TagModule,
    PageHeaderComponent,
    BreadcrumbComponent
  ],
  template: `
    <app-breadcrumb [items]="breadcrumbItems"></app-breadcrumb>

    <app-page-header
      title="Entrepôts"
      subtitle="Lieux de stockage utilisés pour le stock, les transferts et les documents">
      <div class="header-actions">
        <p-button
          label="Retour"
          icon="pi pi-arrow-left"
          [outlined]="true"
          severity="secondary"
          routerLink="/settings">
        </p-button>
        <p-button
          label="Ajouter"
          icon="pi pi-plus"
          severity="primary"
          (onClick)="openCreateDialog()"
          [disabled]="loading()">
        </p-button>
      </div>
    </app-page-header>

    @if (loadError()) {
      <div class="error-banner" role="alert">
        <i class="pi pi-exclamation-triangle" aria-hidden="true"></i>
        <p>{{ loadError() }}</p>
        <p-button label="Réessayer" icon="pi pi-refresh" [outlined]="true" (onClick)="loadWarehouses()"></p-button>
      </div>
    }

    @if (loading() && !warehouses().length && !loadError()) {
      <div class="loading-container" role="status" aria-live="polite">
        <i class="pi pi-spin pi-spinner" style="font-size: 2rem" aria-hidden="true"></i>
        <p>Chargement des entrepôts...</p>
      </div>
    } @else if (!loadError()) {
      <div class="warehouses-page">
        <p-card header="Liste des entrepôts" styleClass="warehouses-card">
          <div class="card-toolbar">
            <div class="search-field">
              <label class="sr-only" for="warehouse-search">Rechercher un entrepôt</label>
              <span class="search-input-wrap">
                <i class="pi pi-search search-icon" aria-hidden="true"></i>
                <input
                  id="warehouse-search"
                  type="text"
                  pInputText
                  class="search-input"
                  placeholder="Rechercher par code, nom ou adresse…"
                  [value]="searchQuery()"
                  (input)="onSearchInput($event)"
                  autocomplete="off" />
              </span>
              @if (searchQuery().trim()) {
                <p-button
                  type="button"
                  label="Effacer"
                  icon="pi pi-times"
                  [text]="true"
                  (onClick)="clearSearch()"
                  styleClass="p-button-sm">
                </p-button>
              }
            </div>
          </div>

          @if (warehouses().length > 0 && filteredWarehouses().length === 0 && searchQuery().trim()) {
            <div class="filter-empty" role="status">
              <i class="pi pi-filter-slash" aria-hidden="true"></i>
              <p>Aucun résultat pour cette recherche.</p>
              <p-button label="Réinitialiser le filtre" [outlined]="true" (onClick)="clearSearch()"></p-button>
            </div>
          } @else {
            <p-table
              [value]="filteredWarehouses()"
              [paginator]="filteredWarehouses().length > 10"
              [rows]="10"
              responsiveLayout="scroll"
              styleClass="p-datatable-sm warehouses-table">
              <ng-template pTemplate="header">
                <tr>
                  <th scope="col">Code</th>
                  <th scope="col">Nom</th>
                  <th scope="col">Adresse</th>
                  <th scope="col">Par défaut</th>
                  <th scope="col">État</th>
                  <th scope="col" class="col-actions">Actions</th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-row>
                <tr>
                  <td><span class="code-cell">{{ row.code }}</span></td>
                  <td>{{ row.name }}</td>
                  <td class="address-cell">{{ row.address || '—' }}</td>
                  <td>
                    <p-tag
                      [value]="row.isDefault ? 'Oui' : 'Non'"
                      [severity]="row.isDefault ? 'success' : 'secondary'">
                    </p-tag>
                  </td>
                  <td>
                    <p-tag
                      [value]="row.isActive ? 'Actif' : 'Inactif'"
                      [severity]="row.isActive ? 'success' : 'warning'">
                    </p-tag>
                  </td>
                  <td class="col-actions">
                    <p-button
                      icon="pi pi-pencil"
                      [text]="true"
                      [rounded]="true"
                      (onClick)="openEditDialog(row)"
                      [attr.aria-label]="'Modifier ' + row.name">
                    </p-button>
                  </td>
                </tr>
              </ng-template>
              <ng-template pTemplate="emptymessage">
                <tr>
                  <td colspan="6" class="empty-cell">
                    Aucun entrepôt. Utilisez « Ajouter » pour créer votre premier lieu de stockage.
                  </td>
                </tr>
              </ng-template>
            </p-table>
          }
        </p-card>
      </div>
    }

    <p-dialog
      [header]="dialogTitle()"
      [(visible)]="dialogVisible"
      [modal]="true"
      [style]="{ width: 'min(520px, 95vw)' }"
      [draggable]="false"
      [dismissableMask]="true"
      [breakpoints]="{ '576px': '95vw' }"
      (onHide)="onDialogHide()"
      [attr.aria-modal]="true"
      styleClass="warehouses-dialog">
      <p class="dialog-intro">
        Indiquez un <strong>nom</strong> court (libellé interne : magasin, dépôt principal, etc.).
        L’<strong>adresse</strong>, facultative, peut reprendre rue, ville, code postal et pays sur plusieurs lignes.
      </p>
      <form [formGroup]="form" (ngSubmit)="onSubmitForm()" class="wh-form">
        @if (isEditMode()) {
          <label class="form-field" for="wh-code-readonly">
            <span class="label-text">Code</span>
            <input
              pInputText
              id="wh-code-readonly"
              [value]="editingWarehouse()?.code ?? ''"
              class="w-full"
              readonly
              tabindex="-1" />
            <small class="hint">Le code ne peut pas être modifié après la création.</small>
          </label>
        } @else {
          <label class="form-field" for="wh-code">
            <span class="label-text">Code <span class="required" aria-hidden="true">*</span></span>
            <input
              pInputText
              id="wh-code"
              formControlName="code"
              class="w-full"
              autocomplete="off"
              maxlength="20"
              [class.ng-invalid]="isInvalid('code')" />
            @if (isInvalid('code')) {
              <small class="error-text">{{ fieldError('code') }}</small>
            }
          </label>
        }

        <label class="form-field" for="wh-name">
          <span class="label-text">Nom <span class="required" aria-hidden="true">*</span></span>
          <input
            pInputText
            id="wh-name"
            formControlName="name"
            class="w-full"
            autocomplete="organization"
            maxlength="100"
            [class.ng-invalid]="isInvalid('name')" />
          @if (isInvalid('name')) {
            <small class="error-text">{{ fieldError('name') }}</small>
          }
        </label>

        <label class="form-field" for="wh-address">
          <span class="label-text">Adresse</span>
          <textarea
            pInputTextarea
            id="wh-address"
            formControlName="address"
            class="w-full"
            [rows]="3"
            placeholder="Rue, ville, code postal, pays…">
          </textarea>
        </label>

        <div class="form-field switch-block">
          <div class="switch-field">
            <span class="label-text" id="wh-default-label">Entrepôt par défaut</span>
            <p-inputSwitch
              formControlName="isDefault"
              inputId="wh-default"
              [attr.aria-labelledby]="'wh-default-label'">
            </p-inputSwitch>
          </div>
          <small class="hint switch-hint">
            Utilisé par défaut pour le stock et les documents lorsqu’aucun autre entrepôt n’est précisé.
          </small>
        </div>

        <div class="dialog-actions">
          <p-button
            type="button"
            label="Annuler"
            [outlined]="true"
            severity="secondary"
            (onClick)="dialogVisible = false">
          </p-button>
          <p-button
            type="submit"
            label="Enregistrer"
            icon="pi pi-check"
            severity="primary"
            [loading]="saving()"
            [disabled]="form.invalid || saving()">
          </p-button>
        </div>
      </form>
    </p-dialog>
  `,
  styles: [`
    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border: 0;
    }

    .warehouses-page {
      width: 100%;
      max-width: min(1200px, 100%);
      margin: 0 auto;
    }

    :host ::ng-deep .warehouses-card.p-card {
      border-radius: var(--radius-xl);
      box-shadow: var(--shadow-sm, 0 1px 3px rgba(0, 0, 0, 0.08));
      border: 1px solid var(--color-neutral-200);
    }

    :host ::ng-deep .warehouses-card .p-card-title {
      font-size: var(--font-size-lg);
      font-weight: var(--font-weight-semibold);
      color: var(--color-neutral-900);
      margin-bottom: 0;
    }

    :host ::ng-deep .warehouses-card .p-card-body {
      padding-top: var(--spacing-3);
    }

    .card-toolbar {
      margin-bottom: var(--spacing-4);
    }

    .search-field {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--spacing-2);
    }

    .search-input-wrap {
      position: relative;
      flex: 1;
      min-width: min(100%, 280px);
      max-width: 420px;
    }

    .search-icon {
      position: absolute;
      left: 0.75rem;
      top: 50%;
      transform: translateY(-50%);
      color: var(--color-neutral-400);
      font-size: 0.9rem;
      pointer-events: none;
      z-index: 1;
    }

    .search-input {
      width: 100%;
      padding-left: 2.25rem !important;
    }

    .filter-empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: var(--spacing-3);
      padding: var(--spacing-8) var(--spacing-4);
      text-align: center;
      color: var(--color-neutral-600);
      background: var(--color-neutral-50);
      border-radius: var(--radius-lg);
      border: 1px dashed var(--color-neutral-200);
    }

    .filter-empty i {
      font-size: 1.75rem;
      color: var(--color-neutral-400);
    }

    .filter-empty p {
      margin: 0;
    }

    :host ::ng-deep .warehouses-table.p-datatable .p-datatable-thead > tr > th {
      background: var(--color-neutral-50);
      color: var(--color-neutral-700);
      font-weight: var(--font-weight-semibold);
      font-size: var(--font-size-xs);
      text-transform: uppercase;
      letter-spacing: 0.03em;
      border-bottom: 1px solid var(--color-neutral-200);
    }

    :host ::ng-deep .warehouses-table.p-datatable .p-datatable-tbody > tr > td {
      border-color: var(--color-neutral-100);
      vertical-align: middle;
    }

    :host ::ng-deep .warehouses-table.p-datatable .p-datatable-tbody > tr:nth-child(even) {
      background: var(--color-neutral-25, rgba(0, 0, 0, 0.02));
    }

    :host ::ng-deep .warehouses-table.p-datatable .p-datatable-tbody > tr:hover {
      background: var(--color-primary-50, rgba(59, 130, 246, 0.06));
    }

    :host ::ng-deep .warehouses-table.p-datatable {
      border-radius: var(--radius-md);
      overflow: hidden;
    }

    .header-actions {
      display: flex;
      gap: var(--spacing-2);
      flex-wrap: wrap;
      align-items: center;
    }
    .loading-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: var(--spacing-8);
      gap: var(--spacing-3);
      color: var(--color-neutral-600);
    }
    .error-banner {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--spacing-3);
      padding: var(--spacing-4);
      margin-bottom: var(--spacing-4);
      background: var(--color-error-50, #fef2f2);
      border: 1px solid var(--color-error-200, #fecaca);
      border-radius: var(--radius-lg);
      color: var(--color-neutral-800);
    }
    .error-banner i {
      color: var(--color-error-600, #dc2626);
    }
    .error-banner p {
      margin: 0;
      flex: 1;
      min-width: 200px;
    }
    .dialog-intro {
      margin: 0 0 var(--spacing-4);
      padding: var(--spacing-3);
      font-size: var(--font-size-sm);
      color: var(--color-neutral-600);
      line-height: 1.5;
      background: var(--color-neutral-50);
      border-radius: var(--radius-md);
      border: 1px solid var(--color-neutral-100);
    }
    .wh-form {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-4);
    }
    .form-field {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);
    }
    .switch-block {
      gap: var(--spacing-1);
    }
    .switch-hint {
      margin-top: 0;
    }
    .label-text {
      font-weight: var(--font-weight-medium);
      color: var(--color-neutral-700);
      font-size: var(--font-size-sm);
    }
    .required {
      color: var(--color-error-500);
    }
    .hint {
      font-size: var(--font-size-xs);
      color: var(--color-neutral-500);
    }
    .error-text {
      color: var(--color-error-600);
      font-size: var(--font-size-xs);
    }
    .switch-field {
      flex-direction: row;
      align-items: center;
      justify-content: space-between;
    }
    .dialog-actions {
      display: flex;
      justify-content: flex-end;
      gap: var(--spacing-2);
      margin-top: var(--spacing-2);
      padding-top: var(--spacing-4);
      border-top: 1px solid var(--color-neutral-200);
    }
    .empty-cell {
      text-align: center;
      color: var(--color-neutral-500);
      padding: var(--spacing-6) !important;
    }
    .col-actions {
      white-space: nowrap;
      text-align: right;
    }
    .code-cell {
      font-family: ui-monospace, monospace;
      font-size: var(--font-size-sm);
    }
    .address-cell {
      max-width: 280px;
      white-space: pre-wrap;
      word-break: break-word;
    }
  `]
})
export class WarehousesComponent implements OnInit, OnDestroy {
  private readonly stockService = inject(StockService);
  private readonly toast = inject(ToastService);
  private readonly errorHandler = inject(ErrorHandlerService);
  private readonly fb = inject(FormBuilder);
  private readonly destroy$ = new Subject<void>();

  breadcrumbItems: BreadcrumbItem[] = [
    { label: 'Tableau de bord', route: '/dashboard', icon: 'pi-home' },
    { label: 'Paramètres', route: '/settings' },
    { label: 'Entrepôts' }
  ];

  loading = signal(false);
  saving = signal(false);
  warehouses = signal<Warehouse[]>([]);
  loadError = signal<string | null>(null);

  searchQuery = signal('');

  filteredWarehouses = computed(() => {
    const q = this.searchQuery().trim().toLowerCase();
    const list = this.warehouses();
    if (!q) {
      return list;
    }
    return list.filter(
      (w) =>
        w.code.toLowerCase().includes(q) ||
        w.name.toLowerCase().includes(q) ||
        (w.address !== null && w.address.toLowerCase().includes(q))
    );
  });

  dialogVisible = false;
  editingWarehouse = signal<Warehouse | null>(null);

  isEditMode = computed(() => this.editingWarehouse() !== null);

  dialogTitle = computed(() => (this.editingWarehouse() ? 'Modifier l’entrepôt' : 'Nouvel entrepôt'));

  form = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(20)]],
    name: ['', [Validators.required, Validators.maxLength(100)]],
    address: [''],
    isDefault: [false]
  });

  ngOnInit(): void {
    this.loadWarehouses();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onSearchInput(event: Event): void {
    const v = (event.target as HTMLInputElement).value;
    this.searchQuery.set(v);
  }

  clearSearch(): void {
    this.searchQuery.set('');
  }

  loadWarehouses(): void {
    this.loadError.set(null);
    this.loading.set(true);
    this.stockService
      .getWarehouses(false)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          if (res.success && res.data) {
            this.warehouses.set([...res.data].sort((a, b) => a.name.localeCompare(b.name, 'fr')));
          } else {
            this.loadError.set(res.errors?.join(', ') || res.message || 'Chargement impossible');
          }
          this.loading.set(false);
        },
        error: (err: HttpErrorResponse) => {
          this.errorHandler.logError('Failed to load warehouses', err);
          this.loadError.set(
            this.errorHandler.extractErrorMessage(err) || 'Impossible de charger les entrepôts'
          );
          this.loading.set(false);
        }
      });
  }

  openCreateDialog(): void {
    this.editingWarehouse.set(null);
    this.form.reset({
      code: '',
      name: '',
      address: '',
      isDefault: false
    });
    this.form.get('code')?.enable();
    this.dialogVisible = true;
  }

  openEditDialog(row: Warehouse): void {
    this.editingWarehouse.set(row);
    this.form.patchValue({
      code: row.code,
      name: row.name,
      address: row.address ?? '',
      isDefault: row.isDefault
    });
    this.form.get('code')?.disable();
    this.dialogVisible = true;
  }

  onDialogHide(): void {
    this.editingWarehouse.set(null);
    this.form.get('code')?.enable();
  }

  isInvalid(name: 'code' | 'name'): boolean {
    const c = this.form.get(name);
    return !!c && c.invalid && (c.dirty || c.touched);
  }

  fieldError(name: 'code' | 'name'): string {
    const c = this.form.get(name);
    if (!c?.errors) return '';
    if (c.errors['required']) return 'Ce champ est obligatoire';
    if (c.errors['maxlength']) return 'Longueur maximale dépassée';
    return '';
  }

  onSubmitForm(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const w = this.editingWarehouse();
    if (w) {
      this.submitUpdate(w.id);
    } else {
      this.submitCreate();
    }
  }

  private submitCreate(): void {
    const v = this.form.getRawValue();
    const code = v.code.trim();
    const name = v.name.trim();
    if (!code || !name) {
      this.toast.add({ severity: 'warn', summary: 'Validation', detail: 'Code et nom sont obligatoires' });
      return;
    }

    const request: CreateWarehouseRequest = {
      code,
      name,
      address: v.address?.trim() || null,
      isDefault: v.isDefault
    };

    this.saving.set(true);
    this.stockService
      .createWarehouse(request)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.saving.set(false);
          if (res.success) {
            this.toast.add({
              severity: 'success',
              summary: 'Succès',
              detail: res.message || 'Entrepôt créé',
              life: 3000
            });
            this.dialogVisible = false;
            this.loadWarehouses();
          } else {
            this.toast.add({
              severity: 'error',
              summary: 'Erreur',
              detail: res.errors?.join(', ') || res.message || 'Création impossible'
            });
          }
        },
        error: (err: HttpErrorResponse) => {
          this.saving.set(false);
          this.errorHandler.logError('Create warehouse failed', err);
          this.toast.add({
            severity: 'error',
            summary: 'Erreur',
            detail: this.errorHandler.extractErrorMessage(err) || 'Création impossible'
          });
        }
      });
  }

  private submitUpdate(id: string): void {
    const v = this.form.getRawValue();
    const name = v.name.trim();
    if (!name) {
      this.toast.add({ severity: 'warn', summary: 'Validation', detail: 'Le nom est obligatoire' });
      return;
    }

    const request: UpdateWarehouseRequest = {
      id,
      name,
      address: v.address?.trim() || null,
      isDefault: v.isDefault
    };

    this.saving.set(true);
    this.stockService
      .updateWarehouse(id, request)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.toast.add({
            severity: 'success',
            summary: 'Succès',
            detail: 'Entrepôt mis à jour',
            life: 3000
          });
          this.dialogVisible = false;
          this.loadWarehouses();
        },
        error: (err: HttpErrorResponse) => {
          this.saving.set(false);
          this.errorHandler.logError('Update warehouse failed', err);
          this.toast.add({
            severity: 'error',
            summary: 'Erreur',
            detail: this.errorHandler.extractErrorMessage(err) || 'Mise à jour impossible'
          });
        }
      });
  }
}
