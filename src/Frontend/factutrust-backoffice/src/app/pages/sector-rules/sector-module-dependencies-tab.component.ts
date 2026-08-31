import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

import { PlatformSectorRulesService } from '@core/services/platform-sector-rules.service';
import { PlatformPermissionsService } from '@core/services/platform-permissions.service';
import { PlatformPermission } from '@core/models/platform.models';
import type { ModuleDependencyDto } from '@core/models/sector-rules.models';
import { SECTOR_ELIGIBLE_MODULES, moduleLabel } from '@core/models/module-catalog';

import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import { FtConfirmActionComponent } from '@core/ui/confirm-action/ft-confirm-action.component';

import { SECTOR_RULES_FR } from './sector-rules.i18n.fr';

// TODO(v2): visualisation du graphe de dépendances — v1 se limite à une table plate.

/** Phase 2 (WP-F7b) — Onglet « Dépendances entre modules ». */
@Component({
  selector: 'app-sector-module-dependencies-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, TableModule, ButtonModule, SelectModule, TooltipModule, FtEmptyStateComponent, FtConfirmActionComponent],
  template: `
    @if (canManage()) {
      <div class="add-row">
        <p-select
          [options]="moduleOptions"
          [(ngModel)]="newModuleId"
          optionLabel="label"
          optionValue="value"
          [placeholder]="t('deps.add.module')"
          styleClass="add-select"
        />
        <i class="pi pi-arrow-right" aria-hidden="true"></i>
        <p-select
          [options]="moduleOptions"
          [(ngModel)]="newRequiresModuleId"
          optionLabel="label"
          optionValue="value"
          [placeholder]="t('deps.add.requires')"
          styleClass="add-select"
        />
        <p-button [label]="t('deps.add.action')" icon="pi pi-plus" [disabled]="!canAdd()" [loading]="busy()" (onClick)="add()" />
      </div>
    }

    <p-table [value]="dependencies" styleClass="p-datatable-sm ft-table" [tableStyle]="{ 'min-width': '30rem' }" responsiveLayout="scroll">
      <ng-template pTemplate="header">
        <tr>
          <th scope="col">{{ t('deps.col.module') }}</th>
          <th scope="col">{{ t('deps.col.requires') }}</th>
          <th scope="col" class="col-actions">{{ t('deps.col.actions') }}</th>
        </tr>
      </ng-template>
      <ng-template pTemplate="body" let-row>
        <tr>
          <td>{{ moduleLabel(row.moduleId) }}</td>
          <td>{{ moduleLabel(row.requiresModuleId) }}</td>
          <td class="col-actions">
            <p-button
              icon="pi pi-trash"
              [text]="true"
              severity="danger"
              [disabled]="!canManage()"
              [pTooltip]="t('deps.action.delete')"
              (onClick)="openDelete(row)"
            />
          </td>
        </tr>
      </ng-template>
      <ng-template pTemplate="emptymessage">
        <tr>
          <td colspan="3">
            <ft-empty-state variant="table-empty" [title]="t('deps.empty.title')" [description]="t('deps.empty.desc')" />
          </td>
        </tr>
      </ng-template>
    </p-table>

    <ft-confirm-action
      [(visible)]="deleteVisible"
      [title]="t('deps.delete.title')"
      [description]="t('deps.delete.desc')"
      variant="soft"
      confirmLabel="Supprimer"
      confirmIcon="pi pi-trash"
      [busy]="busy()"
      (confirmed)="confirmDelete()"
    />
  `,
  styles: [
    `
      :host { display: block; }
      .add-row { display: flex; align-items: center; gap: 0.6rem; margin-bottom: 1rem; flex-wrap: wrap; }
      .add-row i { color: var(--ft-text-muted, #8b949e); }
      .col-actions { text-align: right; width: 3rem; }
      :host ::ng-deep .add-select .p-select { min-width: 14rem; }
    `
  ]
})
export class SectorModuleDependenciesTabComponent {
  private readonly api = inject(PlatformSectorRulesService);
  private readonly permissions = inject(PlatformPermissionsService);
  private readonly toast = inject(MessageService);

  @Input({ required: true }) dependencies: ModuleDependencyDto[] = [];
  @Output() changed = new EventEmitter<void>();

  protected readonly moduleLabel = moduleLabel;
  protected readonly moduleOptions = SECTOR_ELIGIBLE_MODULES;

  protected t(key: keyof typeof SECTOR_RULES_FR): string {
    return SECTOR_RULES_FR[key];
  }

  readonly canManage = computed(() => this.permissions.has(PlatformPermission.SectorRulesManage));
  readonly busy = signal(false);

  newModuleId: number | null = null;
  newRequiresModuleId: number | null = null;

  deleteVisible = false;
  private deleteTarget: ModuleDependencyDto | null = null;

  canAdd(): boolean {
    return this.newModuleId != null && this.newRequiresModuleId != null && this.newModuleId !== this.newRequiresModuleId;
  }

  add(): void {
    if (!this.canAdd() || this.newModuleId == null || this.newRequiresModuleId == null) return;
    this.busy.set(true);
    this.api.createDependency({ moduleId: this.newModuleId, requiresModuleId: this.newRequiresModuleId }).subscribe({
      next: res => {
        this.busy.set(false);
        if (res.success) {
          this.newModuleId = null;
          this.newRequiresModuleId = null;
          this.toast.add({ severity: 'success', summary: SECTOR_RULES_FR['deps.toast.create.success'] });
          this.changed.emit();
        } else {
          // Surface le message backend en cas de dépendance circulaire (400) — libellé exact
          // attendu : "Dépendance circulaire détectée entre modules."
          this.toastError(res.message);
        }
      },
      error: err => {
        this.busy.set(false);
        this.toastError((err as { error?: { message?: string } })?.error?.message);
      }
    });
  }

  openDelete(row: ModuleDependencyDto): void {
    this.deleteTarget = row;
    this.deleteVisible = true;
  }

  confirmDelete(): void {
    if (!this.deleteTarget) return;
    const row = this.deleteTarget;
    this.busy.set(true);
    this.api.deleteDependency(row.id).subscribe({
      next: res => {
        this.busy.set(false);
        this.deleteVisible = false;
        if (res.success) {
          this.toast.add({ severity: 'success', summary: SECTOR_RULES_FR['deps.toast.delete.success'] });
          this.changed.emit();
        } else {
          this.toastError(res.message);
        }
      },
      error: err => {
        this.busy.set(false);
        this.deleteVisible = false;
        this.toastError((err as { error?: { message?: string } })?.error?.message);
      }
    });
  }

  private toastError(message?: string | null): void {
    this.toast.add({ severity: 'error', summary: SECTOR_RULES_FR['toast.error.title'], detail: message ?? SECTOR_RULES_FR['toast.error.generic'] });
  }
}
