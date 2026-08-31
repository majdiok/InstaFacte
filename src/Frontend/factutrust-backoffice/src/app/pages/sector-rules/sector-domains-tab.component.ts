import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, computed, inject, signal } from '@angular/core';
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
  CreateSectorDomainRequest,
  SectorDomainDto,
  UpdateSectorDomainRequest
} from '@core/models/sector-rules.models';

import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import { FtCellActionsMenuComponent } from '@core/ui/table-cells/ft-cell-actions-menu.component';

import { SECTOR_RULES_FR } from './sector-rules.i18n.fr';

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
          <td>{{ row.labelFr }}</td>
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
        <input id="domLabel" pInputText [(ngModel)]="form.labelFr" class="w-full" autocomplete="off" />
      </div>
      <div class="dialog-field">
        <label class="field-label" for="domOrder">{{ t('domains.form.order') }}</label>
        <p-inputNumber inputId="domOrder" [(ngModel)]="form.sortOrder" [min]="0" styleClass="w-full" />
      </div>
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
      .field-label { font-size: 0.85rem; font-weight: 600; color: var(--ft-text-muted, #8b949e); }
      .field-hint { font-size: 0.78rem; color: var(--ft-text-muted, #8b949e); }
      .w-full { width: 100%; }
      :host ::ng-deep .p-dialog .p-inputnumber { width: 100%; }
    `
  ]
})
export class SectorDomainsTabComponent {
  private readonly api = inject(PlatformSectorRulesService);
  private readonly permissions = inject(PlatformPermissionsService);
  private readonly toast = inject(MessageService);

  @Input({ required: true }) domains: SectorDomainDto[] = [];
  @Output() changed = new EventEmitter<void>();

  protected t(key: keyof typeof SECTOR_RULES_FR): string {
    return SECTOR_RULES_FR[key];
  }

  readonly canManage = computed(() => this.permissions.has(PlatformPermission.SectorRulesManage));

  readonly busy = signal(false);
  readonly editing = signal<SectorDomainDto | null>(null);
  formVisible = false;

  form = this.blankForm();

  private blankForm() {
    return { code: '', labelFr: '', sortOrder: (this.domains?.length ?? 0) + 1 };
  }

  readonly canSubmit = computed(() => {
    const labelOk = this.form.labelFr.trim().length > 0;
    if (this.editing()) return labelOk;
    return labelOk && this.form.code.trim().length > 0;
  });

  openCreate(): void {
    this.editing.set(null);
    this.form = this.blankForm();
    this.formVisible = true;
  }

  private openEdit(row: SectorDomainDto): void {
    this.editing.set(row);
    this.form = { code: row.code, labelFr: row.labelFr, sortOrder: row.sortOrder };
    this.formVisible = true;
  }

  rowActions(row: SectorDomainDto): MenuItem[] {
    const actions: MenuItem[] = [
      {
        label: SECTOR_RULES_FR['domains.action.edit'],
        icon: 'pi pi-pencil',
        disabled: !this.canManage(),
        command: () => this.openEdit(row)
      }
    ];
    // Pas d'endpoint de réactivation côté backend : on ne propose que la désactivation.
    if (row.isActive) {
      actions.push({
        label: SECTOR_RULES_FR['domains.action.deactivate'],
        icon: 'pi pi-ban',
        disabled: !this.canManage(),
        command: () => this.deactivate(row)
      });
    }
    return actions;
  }

  private deactivate(row: SectorDomainDto): void {
    this.busy.set(true);
    this.api.deactivateDomain(row.id).subscribe({
      next: res => this.handleSaveResult(res, SECTOR_RULES_FR['domains.toast.deactivate.success'], row.labelFr),
      error: err => this.handleSaveError(err)
    });
  }

  submit(): void {
    const editing = this.editing();
    this.busy.set(true);
    if (editing) {
      const request: UpdateSectorDomainRequest = {
        labelFr: this.form.labelFr.trim(),
        sortOrder: this.form.sortOrder
      };
      this.api.updateDomain(editing.id, request).subscribe({
        next: res => this.handleSaveResult(res, SECTOR_RULES_FR['domains.toast.update.success'], this.form.labelFr, true),
        error: err => this.handleSaveError(err)
      });
    } else {
      const request: CreateSectorDomainRequest = {
        code: this.form.code.trim(),
        labelFr: this.form.labelFr.trim(),
        sortOrder: this.form.sortOrder
      };
      this.api.createDomain(request).subscribe({
        next: res => this.handleSaveResult(res, SECTOR_RULES_FR['domains.toast.create.success'], this.form.labelFr, true),
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
