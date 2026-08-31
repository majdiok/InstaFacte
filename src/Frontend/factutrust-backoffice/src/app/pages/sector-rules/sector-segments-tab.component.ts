import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, Output, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { MessageService, MenuItem } from 'primeng/api';

import { PlatformSectorRulesService } from '@core/services/platform-sector-rules.service';
import { PlatformPermissionsService } from '@core/services/platform-permissions.service';
import { PlatformPermission } from '@core/models/platform.models';
import type {
  CreateSectorSegmentRequest,
  SectorModuleRuleDto,
  SectorSegmentDto,
  UpdateSectorSegmentRequest
} from '@core/models/sector-rules.models';
import { moduleLabel } from '@core/models/module-catalog';

import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import { FtCellActionsMenuComponent } from '@core/ui/table-cells/ft-cell-actions-menu.component';
import { FtConfirmActionComponent } from '@core/ui/confirm-action/ft-confirm-action.component';

import { SECTOR_RULES_FR } from './sector-rules.i18n.fr';

/**
 * Phase 2 (WP-F7) — Onglet « Segments » de la page Règles sectorielles.
 *
 * Table CRUD sur `SectorSegmentDto` (champs backend camelCase : `labelFr`, `descriptionFr`,
 * `iconKey`). Reçoit `segments`/`moduleRules` en entrée (dump complet chargé une fois par la
 * page parente) et émet `changed` après toute mutation réussie pour que la page recharge le
 * dump entier. La désactivation est un soft-delete backend (DELETE) ; il n'existe pas d'endpoint
 * de réactivation, donc seule l'action « Désactiver » est proposée sur les segments actifs.
 */
@Component({
  selector: 'app-sector-segments-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    TableModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    InputNumberModule,
    FtBadgeComponent,
    FtEmptyStateComponent,
    FtCellActionsMenuComponent,
    FtConfirmActionComponent
  ],
  template: `
    <p-table
      [value]="segments"
      styleClass="p-datatable-sm ft-table"
      [tableStyle]="{ 'min-width': '55rem' }"
      responsiveLayout="scroll"
    >
      <ng-template pTemplate="header">
        <tr>
          <th scope="col">{{ t('segments.col.code') }}</th>
          <th scope="col">{{ t('segments.col.label') }}</th>
          <th scope="col">{{ t('segments.col.modules') }}</th>
          <th scope="col">{{ t('segments.col.warehouse') }}</th>
          <th scope="col">{{ t('segments.col.order') }}</th>
          <th scope="col">{{ t('segments.col.active') }}</th>
          <th scope="col" class="col-actions">{{ t('segments.col.actions') }}</th>
        </tr>
      </ng-template>
      <ng-template pTemplate="body" let-row>
        <tr>
          <td><code class="cell-mono">{{ row.code }}</code></td>
          <td>{{ row.labelFr }}</td>
          <td>
            @for (id of recommendedModuleIds(row.id); track id) {
              <ft-badge tone="accent" size="sm">{{ moduleLabel(id) }}</ft-badge>
            } @empty {
              <span class="muted">{{ t('common.none') }}</span>
            }
          </td>
          <td>{{ row.defaultWarehouseName || t('common.none') }}</td>
          <td>{{ row.sortOrder }}</td>
          <td>
            @if (row.isActive) {
              <ft-badge tone="success" size="sm" [withDot]="true">{{ t('common.yes') }}</ft-badge>
            } @else {
              <ft-badge tone="neutral" size="sm">{{ t('common.no') }}</ft-badge>
            }
          </td>
          <td class="col-actions">
            <ft-cell-actions-menu [items]="rowActions(row)" />
          </td>
        </tr>
      </ng-template>
      <ng-template pTemplate="emptymessage">
        <tr>
          <td colspan="7">
            <ft-empty-state variant="table-empty" [title]="t('segments.empty.title')" [description]="t('segments.empty.desc')">
              @if (canManage()) {
                <p-button [label]="t('page.actions.newSegment')" icon="pi pi-plus" severity="primary" (onClick)="openCreate()" />
              }
            </ft-empty-state>
          </td>
        </tr>
      </ng-template>
    </p-table>

    <p-dialog
      [header]="editing() ? t('segments.form.title.edit') : t('segments.form.title.create')"
      [(visible)]="formVisible"
      [modal]="true"
      [draggable]="false"
      [style]="{ width: 'min(32rem, 94vw)' }"
    >
      <div class="dialog-field">
        <label class="field-label" for="segCode">{{ t('segments.form.code') }}</label>
        <input id="segCode" pInputText [(ngModel)]="form.code" [disabled]="!!editing()" class="w-full" autocomplete="off" />
        @if (!editing()) {
          <span class="field-hint">{{ t('segments.form.code.hint') }}</span>
        }
      </div>
      <div class="dialog-field">
        <label class="field-label" for="segLabel">{{ t('segments.form.label') }}</label>
        <input id="segLabel" pInputText [(ngModel)]="form.labelFr" class="w-full" autocomplete="off" />
      </div>
      <div class="dialog-field">
        <label class="field-label" for="segDescription">{{ t('segments.form.description') }}</label>
        <input id="segDescription" pInputText [(ngModel)]="form.descriptionFr" class="w-full" autocomplete="off" />
      </div>
      <div class="dialog-field">
        <label class="field-label" for="segIcon">{{ t('segments.form.icon') }}</label>
        <input id="segIcon" pInputText [(ngModel)]="form.iconKey" class="w-full" autocomplete="off" placeholder="pi pi-shop" />
      </div>
      <div class="dialog-row">
        <div class="dialog-field">
          <label class="field-label" for="segOrder">{{ t('segments.form.order') }}</label>
          <p-inputNumber inputId="segOrder" [(ngModel)]="form.sortOrder" [min]="0" styleClass="w-full" />
        </div>
        <div class="dialog-field">
          <label class="field-label" for="segWarehouse">{{ t('segments.form.warehouse') }}</label>
          <input id="segWarehouse" pInputText [(ngModel)]="form.defaultWarehouseName" class="w-full" autocomplete="off" />
        </div>
      </div>
      <ng-template pTemplate="footer">
        <p-button [label]="t('common.cancel')" [text]="true" severity="secondary" (onClick)="formVisible = false" [disabled]="busy()" />
        <p-button
          [label]="editing() ? t('segments.confirm.update') : t('segments.confirm.create')"
          icon="pi pi-check"
          [loading]="busy()"
          [disabled]="!canSubmit()"
          (onClick)="submit()"
        />
      </ng-template>
    </p-dialog>

    <ft-confirm-action
      [(visible)]="deactivateVisible"
      [title]="t('segments.deactivate.title')"
      [description]="t('segments.deactivate.desc')"
      variant="soft"
      confirmLabel="Désactiver"
      confirmIcon="pi pi-ban"
      [busy]="busy()"
      (confirmed)="confirmDeactivate()"
    />
  `,
  styles: [
    `
      :host { display: block; }
      .col-actions { text-align: right; width: 3rem; }
      .cell-mono { font-size: 0.85rem; }
      .muted { color: var(--ft-text-muted, #8b949e); }
      .dialog-field { display: flex; flex-direction: column; gap: 0.4rem; margin-bottom: 0.85rem; }
      .dialog-row { display: grid; grid-template-columns: 1fr 1fr; gap: 0.85rem; }
      .field-label { font-size: 0.85rem; font-weight: 600; color: var(--ft-text-muted, #8b949e); }
      .field-hint { font-size: 0.78rem; color: var(--ft-text-muted, #8b949e); }
      .w-full { width: 100%; }
      :host ::ng-deep .p-dialog .p-inputnumber { width: 100%; }
    `
  ]
})
export class SectorSegmentsTabComponent implements OnChanges {
  private readonly api = inject(PlatformSectorRulesService);
  private readonly permissions = inject(PlatformPermissionsService);
  private readonly toast = inject(MessageService);

  @Input({ required: true }) segments: SectorSegmentDto[] = [];
  @Input({ required: true }) moduleRules: SectorModuleRuleDto[] = [];
  @Output() changed = new EventEmitter<void>();

  protected readonly moduleLabel = moduleLabel;

  protected t(key: keyof typeof SECTOR_RULES_FR): string {
    return SECTOR_RULES_FR[key];
  }

  readonly canManage = computed(() => this.permissions.has(PlatformPermission.SectorRulesManage));

  readonly busy = signal(false);
  readonly editing = signal<SectorSegmentDto | null>(null);
  formVisible = false;
  deactivateVisible = false;
  private deactivateTarget: SectorSegmentDto | null = null;

  form: {
    code: string;
    labelFr: string;
    descriptionFr: string;
    iconKey: string;
    sortOrder: number;
    defaultWarehouseName: string;
  } = this.blankForm();

  ngOnChanges(): void {
    // Rien à faire — les inputs sont déjà des snapshots immuables passés par la page parente.
  }

  private blankForm() {
    return {
      code: '',
      labelFr: '',
      descriptionFr: '',
      iconKey: '',
      sortOrder: (this.segments?.length ?? 0) + 1,
      defaultWarehouseName: ''
    };
  }

  recommendedModuleIds(segmentId: string): number[] {
    return this.moduleRules
      .filter(r => r.ruleKind === 'SegmentBase' && r.segmentId === segmentId && r.isActive)
      .map(r => r.moduleId);
  }

  readonly canSubmit = computed(() => {
    const labelOk = this.form.labelFr.trim().length > 0;
    const descOk = this.form.descriptionFr.trim().length > 0;
    const iconOk = this.form.iconKey.trim().length > 0;
    // En édition le code est immuable ; on ne le valide qu'à la création.
    if (this.editing()) return labelOk && descOk && iconOk;
    return labelOk && descOk && iconOk && this.form.code.trim().length > 0;
  });

  openCreate(): void {
    this.editing.set(null);
    this.form = this.blankForm();
    this.formVisible = true;
  }

  private openEdit(row: SectorSegmentDto): void {
    this.editing.set(row);
    this.form = {
      code: row.code,
      labelFr: row.labelFr,
      descriptionFr: row.descriptionFr,
      iconKey: row.iconKey,
      sortOrder: row.sortOrder,
      defaultWarehouseName: row.defaultWarehouseName ?? ''
    };
    this.formVisible = true;
  }

  rowActions(row: SectorSegmentDto): MenuItem[] {
    const actions: MenuItem[] = [
      {
        label: SECTOR_RULES_FR['segments.action.edit'],
        icon: 'pi pi-pencil',
        disabled: !this.canManage(),
        command: () => this.openEdit(row)
      }
    ];
    // Pas d'endpoint de réactivation côté backend : on ne propose que la désactivation.
    if (row.isActive) {
      actions.push({
        label: SECTOR_RULES_FR['segments.action.deactivate'],
        icon: 'pi pi-ban',
        disabled: !this.canManage(),
        command: () => this.openDeactivate(row)
      });
    }
    return actions;
  }

  private openDeactivate(row: SectorSegmentDto): void {
    this.deactivateTarget = row;
    this.deactivateVisible = true;
  }

  confirmDeactivate(): void {
    if (!this.deactivateTarget) return;
    const row = this.deactivateTarget;
    this.busy.set(true);
    this.api.deactivateSegment(row.id).subscribe({
      next: res => {
        this.busy.set(false);
        this.deactivateVisible = false;
        if (res.success) {
          this.toast.add({ severity: 'success', summary: SECTOR_RULES_FR['segments.toast.deactivate.success'], detail: row.labelFr });
          this.changed.emit();
        } else {
          this.toastError(res.message);
        }
      },
      error: err => {
        this.busy.set(false);
        this.deactivateVisible = false;
        this.toastError((err as { error?: { message?: string } })?.error?.message);
      }
    });
  }

  submit(): void {
    const editing = this.editing();
    this.busy.set(true);
    if (editing) {
      const request: UpdateSectorSegmentRequest = {
        labelFr: this.form.labelFr.trim(),
        descriptionFr: this.form.descriptionFr.trim(),
        iconKey: this.form.iconKey.trim(),
        sortOrder: this.form.sortOrder,
        defaultWarehouseName: this.form.defaultWarehouseName.trim() || null
      };
      this.api.updateSegment(editing.id, request).subscribe({
        next: res => this.handleSaveResult(res, SECTOR_RULES_FR['segments.toast.update.success']),
        error: err => this.handleSaveError(err)
      });
    } else {
      const request: CreateSectorSegmentRequest = {
        code: this.form.code.trim(),
        labelFr: this.form.labelFr.trim(),
        descriptionFr: this.form.descriptionFr.trim(),
        iconKey: this.form.iconKey.trim(),
        sortOrder: this.form.sortOrder,
        defaultWarehouseName: this.form.defaultWarehouseName.trim() || null
      };
      this.api.createSegment(request).subscribe({
        next: res => this.handleSaveResult(res, SECTOR_RULES_FR['segments.toast.create.success']),
        error: err => this.handleSaveError(err)
      });
    }
  }

  private handleSaveResult(res: { success: boolean; message: string | null }, successSummary: string): void {
    this.busy.set(false);
    if (res.success) {
      this.formVisible = false;
      this.toast.add({ severity: 'success', summary: successSummary, detail: this.form.labelFr });
      this.changed.emit();
    } else {
      this.toastError(res.message);
    }
  }

  private handleSaveError(err: unknown): void {
    this.busy.set(false);
    this.toastError((err as { error?: { message?: string } })?.error?.message);
  }

  private toastError(message?: string | null): void {
    this.toast.add({
      severity: 'error',
      summary: SECTOR_RULES_FR['toast.error.title'],
      detail: message ?? SECTOR_RULES_FR['toast.error.generic']
    });
  }
}
