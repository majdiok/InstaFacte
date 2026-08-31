import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, Output, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputSwitchModule } from 'primeng/inputswitch';
import { SelectModule } from 'primeng/select';
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

const TONE_OPTIONS = [
  { label: 'Accent', value: 'accent' },
  { label: 'Succès', value: 'success' },
  { label: 'Info', value: 'info' },
  { label: 'Avertissement', value: 'warning' },
  { label: 'Neutre', value: 'neutral' }
];

/**
 * Phase 2 (WP-F7) — Onglet « Segments » de la page Règles sectorielles.
 *
 * Table CRUD sur `SectorSegmentDto`. Reçoit `segments`/`moduleRules` en entrée (dump complet
 * chargé une fois par la page parente) et émet `changed` après toute mutation réussie pour que
 * la page recharge le dump entier — plus simple et plus sûr que du patch local sur des volumes
 * de données de configuration (quelques dizaines de lignes au plus).
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
    InputSwitchModule,
    SelectModule,
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
          <td>{{ row.label }}</td>
          <td>
            @for (id of recommendedModuleIds(row.code); track id) {
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
        <input id="segLabel" pInputText [(ngModel)]="form.label" class="w-full" autocomplete="off" />
      </div>
      <div class="dialog-field">
        <label class="field-label" for="segSubtitle">{{ t('segments.form.subtitle') }}</label>
        <input id="segSubtitle" pInputText [(ngModel)]="form.subtitle" class="w-full" autocomplete="off" />
      </div>
      <div class="dialog-row">
        <div class="dialog-field">
          <label class="field-label" for="segIcon">{{ t('segments.form.icon') }}</label>
          <input id="segIcon" pInputText [(ngModel)]="form.icon" class="w-full" autocomplete="off" placeholder="pi pi-shop" />
        </div>
        <div class="dialog-field">
          <label class="field-label" for="segTone">{{ t('segments.form.tone') }}</label>
          <p-select
            inputId="segTone"
            [options]="toneOptions"
            [(ngModel)]="form.tone"
            optionLabel="label"
            optionValue="value"
            styleClass="w-full"
          />
        </div>
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
      @if (editing()) {
        <div class="dialog-field switch-field">
          <p-inputSwitch [(ngModel)]="form.isActive" />
          <label class="field-label">{{ t('segments.form.active') }}</label>
        </div>
      }
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
      .switch-field { flex-direction: row; align-items: center; gap: 0.6rem; }
      .w-full { width: 100%; }
      :host ::ng-deep .p-dialog .p-select, :host ::ng-deep .p-dialog .p-inputnumber { width: 100%; }
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

  protected readonly toneOptions = TONE_OPTIONS;
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
    label: string;
    subtitle: string;
    icon: string;
    tone: string;
    sortOrder: number;
    defaultWarehouseName: string;
    isActive: boolean;
  } = this.blankForm();

  ngOnChanges(): void {
    // Rien à faire — les inputs sont déjà des snapshots immuables passés par la page parente.
  }

  private blankForm() {
    return {
      code: '',
      label: '',
      subtitle: '',
      icon: '',
      tone: 'accent',
      sortOrder: (this.segments?.length ?? 0) + 1,
      defaultWarehouseName: '',
      isActive: true
    };
  }

  recommendedModuleIds(segmentCode: string): number[] {
    return this.moduleRules
      .filter(r => r.ruleKind === 0 && r.segmentCode === segmentCode && r.isActive)
      .map(r => r.moduleId);
  }

  readonly canSubmit = computed(() => {
    return this.form.code.trim().length > 0 && this.form.label.trim().length > 0;
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
      label: row.label,
      subtitle: row.subtitle ?? '',
      icon: row.icon ?? '',
      tone: row.tone ?? 'accent',
      sortOrder: row.sortOrder,
      defaultWarehouseName: row.defaultWarehouseName ?? '',
      isActive: row.isActive
    };
    this.formVisible = true;
  }

  rowActions(row: SectorSegmentDto): MenuItem[] {
    return [
      {
        label: SECTOR_RULES_FR['segments.action.edit'],
        icon: 'pi pi-pencil',
        disabled: !this.canManage(),
        command: () => this.openEdit(row)
      },
      {
        label: row.isActive
          ? SECTOR_RULES_FR['segments.action.deactivate']
          : SECTOR_RULES_FR['segments.action.activate'],
        icon: row.isActive ? 'pi pi-ban' : 'pi pi-check',
        disabled: !this.canManage(),
        command: () => (row.isActive ? this.openDeactivate(row) : this.reactivate(row))
      }
    ];
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
          this.toast.add({ severity: 'success', summary: SECTOR_RULES_FR['segments.toast.deactivate.success'], detail: row.label });
          this.changed.emit();
        } else {
          this.toastError(res.message);
        }
      },
      error: err => {
        this.busy.set(false);
        this.deactivateVisible = false;
        this.toastError(err?.error?.message);
      }
    });
  }

  private reactivate(row: SectorSegmentDto): void {
    this.busy.set(true);
    const request: UpdateSectorSegmentRequest = {
      label: row.label,
      subtitle: row.subtitle,
      icon: row.icon,
      tone: row.tone,
      sortOrder: row.sortOrder,
      defaultWarehouseName: row.defaultWarehouseName,
      isActive: true
    };
    this.api.updateSegment(row.id, request).subscribe({
      next: res => {
        this.busy.set(false);
        if (res.success) {
          this.toast.add({ severity: 'success', summary: SECTOR_RULES_FR['segments.toast.update.success'], detail: row.label });
          this.changed.emit();
        } else {
          this.toastError(res.message);
        }
      },
      error: err => {
        this.busy.set(false);
        this.toastError(err?.error?.message);
      }
    });
  }

  submit(): void {
    const editing = this.editing();
    this.busy.set(true);
    if (editing) {
      const request: UpdateSectorSegmentRequest = {
        label: this.form.label.trim(),
        subtitle: this.form.subtitle.trim() || null,
        icon: this.form.icon.trim() || null,
        tone: this.form.tone || null,
        sortOrder: this.form.sortOrder,
        defaultWarehouseName: this.form.defaultWarehouseName.trim() || null,
        isActive: this.form.isActive
      };
      this.api.updateSegment(editing.id, request).subscribe({
        next: res => this.handleSaveResult(res, SECTOR_RULES_FR['segments.toast.update.success']),
        error: err => this.handleSaveError(err)
      });
    } else {
      const request: CreateSectorSegmentRequest = {
        code: this.form.code.trim(),
        label: this.form.label.trim(),
        subtitle: this.form.subtitle.trim() || null,
        icon: this.form.icon.trim() || null,
        tone: this.form.tone || null,
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
      this.toast.add({ severity: 'success', summary: successSummary, detail: this.form.label });
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
