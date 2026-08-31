import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

import { PlatformSectorRulesService } from '@core/services/platform-sector-rules.service';
import { PlatformPermissionsService } from '@core/services/platform-permissions.service';
import { PlatformPermission } from '@core/models/platform.models';
import type {
  CreateSectorDefaultSettingRequest,
  SectorDefaultSettingDto,
  SectorDomainDto,
  SectorSegmentDto,
  SectorSettingValueType,
  UpdateSectorDefaultSettingRequest
} from '@core/models/sector-rules.models';

import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';

import { SECTOR_RULES_FR } from './sector-rules.i18n.fr';

const VALUE_TYPE_OPTIONS: { label: string; value: SectorSettingValueType }[] = [
  { label: 'Texte', value: 'string' },
  { label: 'Entier', value: 'int' },
  { label: 'Booléen', value: 'bool' },
  { label: 'JSON', value: 'json' }
];

/** Phase 2 (WP-F7) — Onglet « Paramètres par défaut » (ex: nom d'entrepôt par défaut). */
@Component({
  selector: 'app-sector-default-settings-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, TableModule, ButtonModule, DialogModule, InputTextModule, SelectModule, TooltipModule, FtEmptyStateComponent],
  template: `
    <div class="tab-header">
      <p-button [label]="t('settings.add.title')" icon="pi pi-plus" severity="primary" [disabled]="!canManage()" (onClick)="openCreate()" />
    </div>
    <p-table [value]="defaultSettings" styleClass="p-datatable-sm ft-table" [tableStyle]="{ 'min-width': '45rem' }" responsiveLayout="scroll">
      <ng-template pTemplate="header">
        <tr>
          <th scope="col">{{ t('settings.col.segment') }}</th>
          <th scope="col">{{ t('settings.col.domain') }}</th>
          <th scope="col">{{ t('settings.col.key') }}</th>
          <th scope="col">{{ t('settings.col.type') }}</th>
          <th scope="col">{{ t('settings.col.value') }}</th>
          <th scope="col" class="col-actions">{{ t('settings.col.actions') }}</th>
        </tr>
      </ng-template>
      <ng-template pTemplate="body" let-row>
        <tr>
          <td>{{ row.segmentCode || t('common.none') }}</td>
          <td>{{ row.domainCode || t('common.none') }}</td>
          <td><code class="cell-mono">{{ row.settingKey }}</code></td>
          <td>{{ row.valueType }}</td>
          <td>{{ row.settingValue }}</td>
          <td class="col-actions">
            <p-button icon="pi pi-pencil" [text]="true" [disabled]="!canManage()" [pTooltip]="t('settings.action.edit')" (onClick)="openEdit(row)" />
          </td>
        </tr>
      </ng-template>
      <ng-template pTemplate="emptymessage">
        <tr>
          <td colspan="6">
            <ft-empty-state variant="table-empty" [title]="t('settings.empty.title')" [description]="t('settings.empty.desc')" />
          </td>
        </tr>
      </ng-template>
    </p-table>

    <p-dialog
      [header]="editing() ? t('settings.form.title.edit') : t('settings.add.title')"
      [(visible)]="formVisible"
      [modal]="true"
      [draggable]="false"
      [style]="{ width: 'min(28rem, 94vw)' }"
    >
      <div class="dialog-field">
        <label class="field-label" for="setSegment">{{ t('settings.form.segment') }}</label>
        <p-select inputId="setSegment" [options]="segmentOptions" [(ngModel)]="form.segmentCode" optionLabel="label" optionValue="value" [showClear]="true" [disabled]="!!editing()" styleClass="w-full" />
      </div>
      <div class="dialog-field">
        <label class="field-label" for="setDomain">{{ t('settings.form.domain') }}</label>
        <p-select inputId="setDomain" [options]="domainOptions" [(ngModel)]="form.domainCode" optionLabel="label" optionValue="value" [showClear]="true" [disabled]="!!editing()" styleClass="w-full" />
      </div>
      <div class="dialog-field">
        <label class="field-label" for="setKey">{{ t('settings.form.key') }}</label>
        <input id="setKey" pInputText [(ngModel)]="form.settingKey" [disabled]="!!editing()" class="w-full" autocomplete="off" />
      </div>
      <div class="dialog-field">
        <label class="field-label" for="setType">{{ t('settings.form.type') }}</label>
        <p-select inputId="setType" [options]="valueTypeOptions" [(ngModel)]="form.valueType" optionLabel="label" optionValue="value" styleClass="w-full" />
      </div>
      <div class="dialog-field">
        <label class="field-label" for="setValue">{{ t('settings.form.value') }}</label>
        <input id="setValue" pInputText [(ngModel)]="form.settingValue" class="w-full" autocomplete="off" />
      </div>
      <ng-template pTemplate="footer">
        <p-button [label]="t('common.cancel')" [text]="true" severity="secondary" (onClick)="formVisible = false" [disabled]="busy()" />
        <p-button [label]="t('settings.confirm.save')" icon="pi pi-check" [loading]="busy()" [disabled]="!canSubmit()" (onClick)="submit()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      :host { display: block; }
      .tab-header { display: flex; justify-content: flex-end; margin-bottom: 0.85rem; }
      .col-actions { text-align: right; width: 3rem; }
      .cell-mono { font-size: 0.85rem; }
      .dialog-field { display: flex; flex-direction: column; gap: 0.4rem; margin-bottom: 0.85rem; }
      .field-label { font-size: 0.85rem; font-weight: 600; color: var(--ft-text-muted, #8b949e); }
      .w-full { width: 100%; }
      :host ::ng-deep .p-dialog .p-select { width: 100%; }
    `
  ]
})
export class SectorDefaultSettingsTabComponent {
  private readonly api = inject(PlatformSectorRulesService);
  private readonly permissions = inject(PlatformPermissionsService);
  private readonly toast = inject(MessageService);

  @Input({ required: true }) defaultSettings: SectorDefaultSettingDto[] = [];
  @Input({ required: true }) segments: SectorSegmentDto[] = [];
  @Input({ required: true }) domains: SectorDomainDto[] = [];
  @Output() changed = new EventEmitter<void>();

  protected readonly valueTypeOptions = VALUE_TYPE_OPTIONS;

  protected t(key: keyof typeof SECTOR_RULES_FR): string {
    return SECTOR_RULES_FR[key];
  }

  get segmentOptions() {
    return this.segments.map(s => ({ label: s.labelFr, value: s.code }));
  }

  get domainOptions() {
    return this.domains.map(d => ({ label: d.labelFr, value: d.code }));
  }

  readonly canManage = computed(() => this.permissions.has(PlatformPermission.SectorRulesManage));
  readonly busy = signal(false);
  readonly editing = signal<SectorDefaultSettingDto | null>(null);
  formVisible = false;

  form: {
    segmentCode: string | null;
    domainCode: string | null;
    settingKey: string;
    valueType: SectorSettingValueType;
    settingValue: string;
  } = this.blankForm();

  private blankForm() {
    return { segmentCode: null, domainCode: null, settingKey: '', valueType: 'string' as SectorSettingValueType, settingValue: '' };
  }

  readonly canSubmit = computed(() => {
    const valueOk = this.form.settingValue.trim().length > 0;
    // En édition la clé est immuable ; on ne la valide qu'à la création.
    if (this.editing()) return valueOk;
    return valueOk && this.form.settingKey.trim().length > 0;
  });

  openCreate(): void {
    this.editing.set(null);
    this.form = this.blankForm();
    this.formVisible = true;
  }

  openEdit(row: SectorDefaultSettingDto): void {
    this.editing.set(row);
    this.form = {
      segmentCode: row.segmentCode,
      domainCode: row.domainCode,
      settingKey: row.settingKey,
      valueType: row.valueType,
      settingValue: row.settingValue
    };
    this.formVisible = true;
  }

  submit(): void {
    const editing = this.editing();
    this.busy.set(true);
    if (editing) {
      // Segment/domaine/clé sont immuables côté backend : on ne renvoie que la valeur + type + ordre.
      const request: UpdateSectorDefaultSettingRequest = {
        settingValue: this.form.settingValue.trim(),
        valueType: this.form.valueType,
        sortOrder: editing.sortOrder
      };
      this.api.updateSetting(editing.id, request).subscribe({
        next: res => this.handleSaveResult(res),
        error: err => this.handleSaveError(err)
      });
    } else {
      const request: CreateSectorDefaultSettingRequest = {
        segmentCode: this.form.segmentCode,
        domainCode: this.form.domainCode,
        settingKey: this.form.settingKey.trim(),
        settingValue: this.form.settingValue.trim(),
        valueType: this.form.valueType,
        sortOrder: 0
      };
      this.api.createSetting(request).subscribe({
        next: res => this.handleSaveResult(res),
        error: err => this.handleSaveError(err)
      });
    }
  }

  private handleSaveResult(res: { success: boolean; message: string | null }): void {
    this.busy.set(false);
    if (res.success) {
      this.formVisible = false;
      this.toast.add({ severity: 'success', summary: SECTOR_RULES_FR['settings.toast.save.success'] });
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
    this.toast.add({ severity: 'error', summary: SECTOR_RULES_FR['toast.error.title'], detail: message ?? SECTOR_RULES_FR['toast.error.generic'] });
  }
}
