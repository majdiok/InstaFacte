import { CommonModule, Location } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { FirmActivityCode, FirmGovernanceService, SaveFirmActivityCodeBody } from '@core/services/firm-governance.service';
import { ToastService } from '@core/services/toast.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToggleSwitchModule } from 'primeng/toggleswitch';

@Component({
  selector: 'app-firm-activity-codes',
  standalone: true,
  providers: [ConfirmationService],
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    ReactiveFormsModule,
    ButtonModule,
    ConfirmDialogModule,
    DialogModule,
    InputNumberModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
    ToggleSwitchModule,
    PageHeaderComponent
  ],
  template: `
    <p-confirmDialog></p-confirmDialog>

    <app-page-header
      title="Types d’activité"
      subtitle="Nomenclature des diligences du cabinet pour la saisie des temps">
      <p-button label="Retour" icon="pi pi-arrow-left" [outlined]="true" (onClick)="back()"></p-button>
      <p-button *ngIf="showSeedAction()" label="Charger la nomenclature par défaut" icon="pi pi-download" (onClick)="confirmSeedDefaults()"></p-button>
      <p-button label="Ajouter" icon="pi pi-plus" [outlined]="true" (onClick)="openCreateDialog()"></p-button>
      <p-button label="Modifier" icon="pi pi-pencil" [outlined]="true" [disabled]="!selected()" (onClick)="openEditDialog()"></p-button>
      <p-button
        [label]="selected()?.isActive ? 'Désactiver' : 'Réactiver'"
        [icon]="selected()?.isActive ? 'pi pi-ban' : 'pi pi-refresh'"
        [severity]="selected()?.isActive ? 'danger' : 'success'"
        [outlined]="true"
        [disabled]="!selected()"
        (onClick)="toggleSelectedStatus()"></p-button>
    </app-page-header>

    <div class="fc-card">
      <div class="filters">
        <input pInputText [value]="search()" (input)="search.set($any($event.target).value ?? '')" placeholder="Rechercher code/libellé" />
        <p-select
          [options]="statusOptions"
          optionLabel="label"
          optionValue="value"
          [ngModel]="statusFilter()"
          (ngModelChange)="statusFilter.set($event)" />
        <p-select
          [options]="categoryOptions"
          optionLabel="label"
          optionValue="value"
          [ngModel]="categoryFilter()"
          (ngModelChange)="categoryFilter.set($event)" />
      </div>

      @if (!loading() && codes().length === 0) {
        <div class="empty-box">
          <h3>Aucun type d’activité configuré</h3>
          <p>Chargez la nomenclature par défaut, puis adaptez-la à votre cabinet.</p>
          <div class="empty-box__actions">
            <p-button label="Charger la nomenclature par défaut" icon="pi pi-download" (onClick)="confirmSeedDefaults()"></p-button>
            <p-button label="Créer un code manuellement" icon="pi pi-plus" [outlined]="true" (onClick)="openCreateDialog()"></p-button>
          </div>
        </div>
      } @else {
        <p-table
          [value]="filteredCodes()"
          [loading]="loading()"
          dataKey="id"
          [(selection)]="selectedRow"
          (selectionChange)="onSelectionChange($event)"
          selectionMode="single"
          styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th style="width: 3rem"></th>
              <th>Code</th>
              <th>Libellé</th>
              <th>Catégorie</th>
              <th>Facturable par défaut</th>
              <th>Honoraire HT</th>
              <th>Ordre</th>
              <th>Statut</th>
              <th style="width: 12rem"></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-row>
            <tr [pSelectableRow]="row">
              <td><p-tableRadioButton [value]="row"></p-tableRadioButton></td>
              <td><strong>{{ row.code }}</strong></td>
              <td>{{ row.label }}</td>
              <td>{{ row.categoryDisplay }}</td>
              <td><p-tag [value]="row.isBillableByDefault ? 'Oui' : 'Non'" [severity]="row.isBillableByDefault ? 'success' : 'secondary'" /></td>
              <td>{{ formatHonoraire(row.defaultUnitPrice) }}</td>
              <td>{{ row.sortOrder }}</td>
              <td><p-tag [value]="row.isActive ? 'Actif' : 'Inactif'" [severity]="row.isActive ? 'info' : 'warn'" /></td>
              <td class="row-actions">
                <button type="button" pButton icon="pi pi-pencil" class="p-button-text p-button-sm" (click)="openEditDialog(row)"></button>
                <button
                  type="button"
                  pButton
                  class="p-button-text p-button-sm"
                  [icon]="row.isActive ? 'pi pi-ban' : 'pi pi-refresh'"
                  [severity]="row.isActive ? 'danger' : 'success'"
                  (click)="toggleStatus(row)"></button>
              </td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr><td colspan="9">Aucun type d’activité ne correspond aux filtres.</td></tr>
          </ng-template>
        </p-table>
      }
    </div>

    <p-dialog
      [(visible)]="dialogVisible"
      [modal]="true"
      [draggable]="false"
      [resizable]="false"
      [style]="{ width: '38rem', maxWidth: '95vw' }"
      [header]="isEditMode() ? 'Modifier un type d’activité' : 'Ajouter un type d’activité'">
      <form [formGroup]="form" class="dialog-form">
        <label>Code activité</label>
        <input pInputText formControlName="code" [readonly]="isEditMode()" [class.readonly]="isEditMode()" />
        @if (isEditMode()) {
          <small>Le code est immuable car les saisies historiques le référencent.</small>
        }

        <label>Libellé</label>
        <input pInputText formControlName="label" />

        <label>Catégorie</label>
        <p-select formControlName="category" [options]="categoryOptionsNoAll" optionLabel="label" optionValue="value"></p-select>

        <label>Honoraire unitaire HT (TND)</label>
        <p-inputNumber
          formControlName="defaultUnitPrice"
          mode="decimal"
          [minFractionDigits]="3"
          [maxFractionDigits]="3"
          [min]="0"
          [max]="999999999.999"
          placeholder="Ex. 200,000"
          styleClass="w-full" />
        <small>Tarif proposé automatiquement lors de la facturation. Laissez vide si non applicable.</small>

        <label>Ordre d’affichage</label>
        <input pInputText type="number" formControlName="sortOrder" />

        <label class="switch-line">
          <span>Facturable par défaut</span>
          <p-toggleswitch formControlName="isBillableByDefault"></p-toggleswitch>
        </label>
      </form>
      <ng-template pTemplate="footer">
        <p-button label="Annuler" [outlined]="true" (onClick)="dialogVisible = false"></p-button>
        <p-button label="Enregistrer" [disabled]="form.invalid || saving()" [loading]="saving()" (onClick)="save()"></p-button>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .fc-card { background: var(--color-surface, #fff); border: 1px solid var(--color-border-subtle, #e2e8f0); border-radius: var(--radius-xl, 16px); padding: 1rem; }
    .filters { display: flex; flex-wrap: wrap; gap: .75rem; margin-bottom: .75rem; }
    .filters input { min-width: 250px; }
    .empty-box { border: 1px dashed var(--color-border-subtle, #cbd5e1); border-radius: 12px; padding: 1rem; text-align: center; }
    .empty-box h3 { margin: 0 0 .5rem 0; }
    .empty-box p { margin: 0 0 .75rem 0; color: var(--color-text-secondary, #64748b); }
    .empty-box__actions { display: inline-flex; flex-wrap: wrap; gap: .5rem; }
    .row-actions { white-space: nowrap; }
    .dialog-form { display: grid; gap: .5rem; }
    .dialog-form label { font-weight: 600; margin-top: .25rem; }
    .dialog-form input.readonly { background: #f8fafc; }
    .dialog-form small { color: var(--color-text-secondary, #64748b); margin-bottom: .25rem; }
    .dialog-form .w-full { width: 100%; }
    .switch-line { display: flex; justify-content: space-between; align-items: center; margin-top: .5rem; }
  `]
})
export class FirmActivityCodesComponent {
  private readonly service = inject(FirmGovernanceService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmationService);
  private readonly location = inject(Location);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly codes = signal<FirmActivityCode[]>([]);
  readonly selected = signal<FirmActivityCode | null>(null);
  readonly search = signal('');
  readonly statusFilter = signal<'all' | 'active' | 'inactive'>('all');
  readonly categoryFilter = signal<number | null>(null);

  readonly statusOptions = [
    { label: 'Tous statuts', value: 'all' as const },
    { label: 'Actifs', value: 'active' as const },
    { label: 'Inactifs', value: 'inactive' as const }
  ];
  readonly categoryOptions = [
    { label: 'Toutes catégories', value: null },
    { label: 'Comptabilité', value: 1 },
    { label: 'Fiscal', value: 2 },
    { label: 'Social', value: 3 },
    { label: 'Audit', value: 4 },
    { label: 'Conseil', value: 5 },
    { label: 'Interne', value: 6 }
  ];
  readonly categoryOptionsNoAll = this.categoryOptions.filter(opt => opt.value !== null);

  readonly filteredCodes = computed(() => {
    const term = this.search().trim().toLowerCase();
    return this.codes().filter(code => {
      if (this.statusFilter() === 'active' && !code.isActive) return false;
      if (this.statusFilter() === 'inactive' && code.isActive) return false;
      if (this.categoryFilter() != null && code.category !== this.categoryFilter()) return false;
      if (!term) return true;
      return code.code.toLowerCase().includes(term) || code.label.toLowerCase().includes(term);
    });
  });
  readonly showSeedAction = computed(() => !this.loading() && this.codes().length === 0);
  readonly isEditMode = signal(false);

  selectedRow: FirmActivityCode | null = null;
  dialogVisible = false;
  editingCodeId: string | null = null;

  readonly form = this.fb.group({
    code: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(50)]),
    label: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(200)]),
    category: this.fb.nonNullable.control(1, [Validators.required]),
    isBillableByDefault: this.fb.nonNullable.control(true),
    defaultUnitPrice: this.fb.control<number | null>(null, [Validators.min(0)]),
    sortOrder: this.fb.nonNullable.control(10, [Validators.required])
  });

  constructor() {
    this.loadCodes();
  }

  back(): void {
    this.location.back();
  }

  onSelectionChange(row: FirmActivityCode | null): void {
    this.selected.set(row);
  }

  openCreateDialog(): void {
    this.isEditMode.set(false);
    this.editingCodeId = null;
    this.form.reset({
      code: '',
      label: '',
      category: 1,
      isBillableByDefault: true,
      defaultUnitPrice: null,
      sortOrder: this.nextSortOrder()
    });
    this.dialogVisible = true;
  }

  openEditDialog(row?: FirmActivityCode): void {
    const target = row ?? this.selected();
    if (!target) return;
    this.isEditMode.set(true);
    this.editingCodeId = target.id;
    this.form.reset({
      code: target.code,
      label: target.label,
      category: target.category,
      isBillableByDefault: target.isBillableByDefault,
      defaultUnitPrice: target.defaultUnitPrice ?? null,
      sortOrder: target.sortOrder
    });
    this.dialogVisible = true;
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    const body: SaveFirmActivityCodeBody = {
      code: (raw.code ?? '').trim().toUpperCase(),
      label: (raw.label ?? '').trim(),
      category: Number(raw.category),
      isBillableByDefault: !!raw.isBillableByDefault,
      defaultUnitPrice: raw.defaultUnitPrice == null ? null : Number(raw.defaultUnitPrice),
      sortOrder: Number(raw.sortOrder)
    };

    this.saving.set(true);
    const request$ = this.isEditMode() && this.editingCodeId
      ? this.service.updateActivityCode(this.editingCodeId, body)
      : this.service.createActivityCode(body);

    request$.subscribe({
      next: (res) => {
        this.saving.set(false);
        if (!res.success) {
          this.notifyError(res.message ?? 'Enregistrement impossible.');
          return;
        }
        this.dialogVisible = false;
        this.toast.add({
          severity: 'success',
          summary: this.isEditMode() ? 'Type d’activité modifié' : 'Type d’activité créé',
          detail: `${body.code} — ${body.label}`
        });
        this.loadCodes();
      },
      error: (err) => {
        this.saving.set(false);
        this.notifyError(err?.error?.message ?? 'Erreur lors de l’enregistrement.');
      }
    });
  }

  toggleSelectedStatus(): void {
    const row = this.selected();
    if (!row) return;
    this.toggleStatus(row);
  }

  toggleStatus(row: FirmActivityCode): void {
    const action = row.isActive ? 'désactiver' : 'réactiver';
    this.confirm.confirm({
      header: `${row.isActive ? 'Désactivation' : 'Réactivation'} du code`,
      message: `Confirmer la ${action} de ${row.code} ?`,
      acceptLabel: row.isActive ? 'Désactiver' : 'Réactiver',
      rejectLabel: 'Annuler',
      accept: () => {
        const request$ = row.isActive
          ? this.service.deactivateActivityCode(row.id)
          : this.service.activateActivityCode(row.id);
        request$.subscribe({
          next: (res) => {
            if (!res.success) {
              this.notifyError(res.message ?? `Impossible de ${action} ce code.`);
              return;
            }
            this.toast.add({
              severity: 'success',
              summary: row.isActive ? 'Code désactivé' : 'Code réactivé',
              detail: row.code
            });
            this.loadCodes();
          },
          error: (err) => this.notifyError(err?.error?.message ?? `Erreur lors de la ${action}.`)
        });
      }
    });
  }

  confirmSeedDefaults(): void {
    this.confirm.confirm({
      header: 'Installer la nomenclature par défaut',
      message: 'Installer les 13 codes activité par défaut (tenue, fiscal, social, audit...) ? Les saisies existantes restent visibles.',
      acceptLabel: 'Installer',
      rejectLabel: 'Annuler',
      accept: () => {
        this.loading.set(true);
        this.service.seedDefaultActivityCodes().subscribe({
          next: (res) => {
            this.loading.set(false);
            if (!res.success) {
              this.notifyError(res.message ?? 'Installation impossible.');
              return;
            }
            this.toast.add({ severity: 'success', summary: 'Nomenclature installée' });
            this.loadCodes();
          },
          error: (err) => {
            this.loading.set(false);
            this.notifyError(err?.error?.message ?? 'Erreur lors de l’installation des codes par défaut.');
          }
        });
      }
    });
  }

  private loadCodes(): void {
    this.loading.set(true);
    this.service.listActivityCodes(true).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (!res.success) {
          this.codes.set([]);
          this.notifyError(res.message ?? 'Chargement des types d’activité impossible.');
          return;
        }
        const list = (res.data ?? []).slice().sort((a, b) => a.sortOrder - b.sortOrder || a.code.localeCompare(b.code));
        this.codes.set(list);
        const selectedId = this.selected()?.id;
        this.selected.set(selectedId ? list.find(x => x.id === selectedId) ?? null : null);
        this.selectedRow = this.selected();
      },
      error: (err) => {
        this.loading.set(false);
        this.codes.set([]);
        this.notifyError(err?.error?.message ?? 'Erreur lors du chargement des types d’activité.');
      }
    });
  }

  private nextSortOrder(): number {
    const max = this.codes().reduce((acc, item) => Math.max(acc, item.sortOrder || 0), 0);
    return max > 0 ? max + 10 : 10;
  }

  formatHonoraire(value: number | null | undefined): string {
    if (value == null || value === 0) return '—';
    return `${value.toLocaleString('fr-TN', { minimumFractionDigits: 3, maximumFractionDigits: 3 })} TND`;
  }

  private notifyError(detail: string): void {
    this.toast.add({ severity: 'error', summary: 'Types d’activité', detail });
  }
}
