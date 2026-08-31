import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, computed, inject, signal } from '@angular/core';
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
  CreateSectorDomainRequest,
  SectorDomainDto,
  UpdateSectorDomainRequest
} from '@core/models/sector-rules.models';

import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import { FtCellActionsMenuComponent } from '@core/ui/table-cells/ft-cell-actions-menu.component';

import { SECTOR_RULES_FR } from './sector-rules.i18n.fr';

const TONE_OPTIONS = [
  { label: 'Accent', value: 'accent' },
  { label: 'Succès', value: 'success' },
  { label: 'Info', value: 'info' },
  { label: 'Avertissement', value: 'warning' },
  { label: 'Neutre', value: 'neutral' }
];

/** Phase 2 (WP-F7) — Onglet « Domaines » de la page Règles sectorielles. */
@Component({
  selector: 'app-sector-domains-tab',
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
    FtCellActionsMenuComponent
  ],
  template: `
    <div class="tab-header">
      <p-button [label]="t('domains.form.title.create')" icon="pi pi-plus" severity="primary" [disabled]="!canManage()" (onClick)="openCreate()" />
    </div>
    <p-table [value]="domains" styleClass="p-datatable-sm ft-table" [tableStyle]="{ 'min-width': '40rem' }" responsiveLayout="scroll">
      <ng-template pTemplate="header">
        <tr>
          <th scope="col">{{ t('domains.col.code') }}</th>
          <th scope="col">{{ t('domains.col.label') }}</th>
          <th scope="col">{{ t('domains.col.order') }}</th>
          <th scope="col">{{ t('domains.col.active') }}</th>
          <th scope="col" class="col-actions">{{ t('domains.col.actions') }}</th>
        </tr>
      </ng-template>
      <ng-template pTemplate="body" let-row>
        <tr>
          <td><code class="cell-mono">{{ row.code }}</code></td>
          <td>{{ row.label }}</td>
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
          <td colspan="5">
            <ft-empty-state variant="table-empty" [title]="t('domains.empty.title')" [description]="t('domains.empty.desc')" />
          </td>
        </tr>
      </ng-template>
    </p-table>

    <p-dialog
      [header]="editing() ? t('domains.form.title.edit') : t('domains.form.title.create')"
      [(visible)]="formVisible"
      [modal]="true"
      [draggable]="false"
      [style]="{ width: 'min(30rem, 94vw)' }"
    >
      <div class="dialog-field">
        <label class="field-label" for="domCode">{{ t('domains.form.code') }}</label>
        <input id="domCode" pInputText [(ngModel)]="form.code" [disabled]="!!editing()" class="w-full" autocomplete="off" />
        @if (!editing()) {
          <span class="field-hint">{{ t('domains.form.code.hint') }}</span>
        }
      </div>
      <div class="dialog-field">
        <label class="field-label" for="domLabel">{{ t('domains.form.label') }}</label>
        <input id="domLabel" pInputText [(ngModel)]="form.label" class="w-full" autocomplete="off" />
      </div>
      <div class="dialog-row">
        <div class="dialog-field">
          <label class="field-label" for="domIcon">{{ t('domains.form.icon') }}</label>
          <input id="domIcon" pInputText [(ngModel)]="form.icon" class="w-full" autocomplete="off" />
        </div>
        <div class="dialog-field">
          <label class="field-label" for="domTone">{{ t('domains.form.tone') }}</label>
          <p-select inputId="domTone" [options]="toneOptions" [(ngModel)]="form.tone" optionLabel="label" optionValue="value" styleClass="w-full" />
        </div>
      </div>
      <div class="dialog-field">
        <label class="field-label" for="domOrder">{{ t('domains.form.order') }}</label>
        <p-inputNumber inputId="domOrder" [(ngModel)]="form.sortOrder" [min]="0" styleClass="w-full" />
      </div>
      @if (editing()) {
        <div class="dialog-field switch-field">
          <p-inputSwitch [(ngModel)]="form.isActive" />
          <label class="field-label">{{ t('domains.form.active') }}</label>
        </div>
      }
      <ng-template pTemplate="footer">
        <p-button [label]="t('common.cancel')" [text]="true" severity="secondary" (onClick)="formVisible = false" [disabled]="busy()" />
        <p-button
          [label]="editing() ? t('domains.confirm.update') : t('domains.confirm.create')"
          icon="pi pi-check"
          [loading]="busy()"
          [disabled]="!canSubmit()"
          (onClick)="submit()"
        />
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
      .dialog-row { display: grid; grid-template-columns: 1fr 1fr; gap: 0.85rem; }
      .field-label { font-size: 0.85rem; font-weight: 600; color: var(--ft-text-muted, #8b949e); }
      .field-hint { font-size: 0.78rem; color: var(--ft-text-muted, #8b949e); }
      .switch-field { flex-direction: row; align-items: center; gap: 0.6rem; }
      .w-full { width: 100%; }
      :host ::ng-deep .p-dialog .p-select, :host ::ng-deep .p-dialog .p-inputnumber { width: 100%; }
    `
  ]
})
export class SectorDomainsTabComponent {
  private readonly api = inject(PlatformSectorRulesService);
  private readonly permissions = inject(PlatformPermissionsService);
  private readonly toast = inject(MessageService);

  @Input({ required: true }) domains: SectorDomainDto[] = [];
  @Output() changed = new EventEmitter<void>();

  protected readonly toneOptions = TONE_OPTIONS;

  protected t(key: keyof typeof SECTOR_RULES_FR): string {
    return SECTOR_RULES_FR[key];
  }

  readonly canManage = computed(() => this.permissions.has(PlatformPermission.SectorRulesManage));

  readonly busy = signal(false);
  readonly editing = signal<SectorDomainDto | null>(null);
  formVisible = false;

  form = this.blankForm();

  private blankForm() {
    return { code: '', label: '', icon: '', tone: 'accent', sortOrder: (this.domains?.length ?? 0) + 1, isActive: true };
  }

  readonly canSubmit = computed(() => this.form.code.trim().length > 0 && this.form.label.trim().length > 0);

  openCreate(): void {
    this.editing.set(null);
    this.form = this.blankForm();
    this.formVisible = true;
  }

  private openEdit(row: SectorDomainDto): void {
    this.editing.set(row);
    this.form = { code: row.code, label: row.label, icon: row.icon ?? '', tone: row.tone ?? 'accent', sortOrder: row.sortOrder, isActive: row.isActive };
    this.formVisible = true;
  }

  rowActions(row: SectorDomainDto): MenuItem[] {
    return [
      {
        label: SECTOR_RULES_FR['domains.action.edit'],
        icon: 'pi pi-pencil',
        disabled: !this.canManage(),
        command: () => this.openEdit(row)
      },
      {
        label: row.isActive ? SECTOR_RULES_FR['domains.action.deactivate'] : SECTOR_RULES_FR['domains.action.activate'],
        icon: row.isActive ? 'pi pi-ban' : 'pi pi-check',
        disabled: !this.canManage(),
        command: () => (row.isActive ? this.deactivate(row) : this.reactivate(row))
      }
    ];
  }

  private deactivate(row: SectorDomainDto): void {
    this.busy.set(true);
    this.api.deactivateDomain(row.id).subscribe({
      next: res => this.handleSaveResult(res, SECTOR_RULES_FR['domains.toast.deactivate.success'], row.label),
      error: err => this.handleSaveError(err)
    });
  }

  private reactivate(row: SectorDomainDto): void {
    this.busy.set(true);
    const request: UpdateSectorDomainRequest = { label: row.label, icon: row.icon, tone: row.tone, sortOrder: row.sortOrder, isActive: true };
    this.api.updateDomain(row.id, request).subscribe({
      next: res => this.handleSaveResult(res, SECTOR_RULES_FR['domains.toast.update.success'], row.label),
      error: err => this.handleSaveError(err)
    });
  }

  submit(): void {
    const editing = this.editing();
    this.busy.set(true);
    if (editing) {
      const request: UpdateSectorDomainRequest = {
        label: this.form.label.trim(),
        icon: this.form.icon.trim() || null,
        tone: this.form.tone || null,
        sortOrder: this.form.sortOrder,
        isActive: this.form.isActive
      };
      this.api.updateDomain(editing.id, request).subscribe({
        next: res => this.handleSaveResult(res, SECTOR_RULES_FR['domains.toast.update.success'], this.form.label, true),
        error: err => this.handleSaveError(err)
      });
    } else {
      const request: CreateSectorDomainRequest = {
        code: this.form.code.trim(),
        label: this.form.label.trim(),
        icon: this.form.icon.trim() || null,
        tone: this.form.tone || null,
        sortOrder: this.form.sortOrder
      };
      this.api.createDomain(request).subscribe({
        next: res => this.handleSaveResult(res, SECTOR_RULES_FR['domains.toast.create.success'], this.form.label, true),
        error: err => this.handleSaveError(err)
      });
    }
  }

  private handleSaveResult(
    res: { success: boolean; message: string | null },
    successSummary: string,
    detail: string,
    closeDialog = false
  ): void {
    this.busy.set(false);
    if (res.success) {
      if (closeDialog) this.formVisible = false;
      this.toast.add({ severity: 'success', summary: successSummary, detail });
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
