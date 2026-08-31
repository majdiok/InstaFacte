import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

import { PlatformSectorRulesService } from '@core/services/platform-sector-rules.service';
import { PlatformPermissionsService } from '@core/services/platform-permissions.service';
import { PlatformPermission } from '@core/models/platform.models';
import type {
  SectorDataTemplateDto,
  UpdateSectorDataTemplateRequest
} from '@core/models/sector-rules.models';

import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';

import { SECTOR_RULES_FR } from './sector-rules.i18n.fr';

/**
 * Phase 2 (WP-F7) — Onglet « Modèles de données ».
 *
 * v1 : lecture de la liste + édition des métadonnées (`labelFr`) uniquement. L'édition fine des
 * `items` (numérotation de documents, plan comptable, paramètres) est hors scope v1 — les modèles
 * sont peuplés par le seeder backend (WP-B6) ; ce tab permet surtout d'auditer leur contenu
 * (dialogue détail en lecture) et de corriger le libellé via `PUT /templates/{id}`.
 */
@Component({
  selector: 'app-sector-data-templates-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, TableModule, ButtonModule, DialogModule, InputTextModule, TooltipModule, FtEmptyStateComponent],
  template: `
    <p-table [value]="dataTemplates" styleClass="p-datatable-sm ft-table" [tableStyle]="{ 'min-width': '45rem' }" responsiveLayout="scroll">
      <ng-template pTemplate="header">
        <tr>
          <th scope="col">{{ t('templates.col.code') }}</th>
          <th scope="col">{{ t('templates.col.label') }}</th>
          <th scope="col">{{ t('templates.col.segment') }}</th>
          <th scope="col">{{ t('templates.col.domain') }}</th>
          <th scope="col">{{ t('templates.col.items') }}</th>
          <th scope="col" class="col-actions">{{ t('templates.col.actions') }}</th>
        </tr>
      </ng-template>
      <ng-template pTemplate="body" let-row>
        <tr>
          <td><code class="cell-mono">{{ row.code }}</code></td>
          <td>{{ row.labelFr }}</td>
          <td>{{ row.segmentCode || t('common.none') }}</td>
          <td>{{ row.domainCode || t('common.none') }}</td>
          <td>{{ row.items.length }}</td>
          <td class="col-actions">
            <p-button icon="pi pi-eye" [text]="true" [pTooltip]="t('templates.action.view')" (onClick)="openDetail(row)" />
          </td>
        </tr>
      </ng-template>
      <ng-template pTemplate="emptymessage">
        <tr>
          <td colspan="6">
            <ft-empty-state variant="table-empty" [title]="t('templates.empty.title')" [description]="t('templates.empty.desc')" />
          </td>
        </tr>
      </ng-template>
    </p-table>

    <p-dialog [header]="dialogTitle()" [(visible)]="detailVisible" [modal]="true" [draggable]="false" [style]="{ width: 'min(40rem, 96vw)' }">
      @if (detail(); as d) {
        <div class="dialog-field">
          <label class="field-label" for="tplLabel">{{ t('templates.col.label') }}</label>
          <input id="tplLabel" pInputText [(ngModel)]="form.labelFr" class="w-full" autocomplete="off" />
        </div>
        <div class="dialog-field">
          <label class="field-label">{{ t('templates.dialog.version') }}</label>
          <span class="muted">v{{ d.version }}</span>
        </div>
        <div class="dialog-field">
          <label class="field-label">{{ t('templates.col.items') }}</label>
          <ul class="items-list">
            @for (item of d.items; track item.id) {
              <li>
                <span class="item-kind">{{ item.itemKind }}</span>
                <code class="item-payload">{{ item.payloadJson }}</code>
              </li>
            } @empty {
              <li class="muted">{{ t('common.none') }}</li>
            }
          </ul>
        </div>
      }
      <ng-template pTemplate="footer">
        <p-button [label]="t('common.cancel')" [text]="true" severity="secondary" (onClick)="detailVisible = false" [disabled]="busy()" />
        <p-button [label]="t('templates.confirm.save')" icon="pi pi-check" [loading]="busy()" [disabled]="!canManage()" (onClick)="save()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      :host { display: block; }
      .col-actions { text-align: right; width: 3rem; }
      .cell-mono { font-size: 0.85rem; }
      .dialog-field { display: flex; flex-direction: column; gap: 0.4rem; margin-bottom: 0.85rem; }
      .field-label { font-size: 0.85rem; font-weight: 600; color: var(--ft-text-muted, #8b949e); }
      .w-full { width: 100%; }
      .muted { color: var(--ft-text-muted, #8b949e); }
      .items-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 0.4rem; max-height: 16rem; overflow-y: auto; }
      .items-list li { display: flex; flex-direction: column; gap: 0.15rem; border: 1px solid var(--ft-border, #30363d); border-radius: 6px; padding: 0.5rem; }
      .item-kind { font-size: 0.75rem; color: var(--ft-accent, #58a6ff); font-weight: 600; }
      .item-payload { font-size: 0.72rem; color: var(--ft-text-muted, #8b949e); word-break: break-all; }
    `
  ]
})
export class SectorDataTemplatesTabComponent {
  private readonly api = inject(PlatformSectorRulesService);
  private readonly permissions = inject(PlatformPermissionsService);
  private readonly toast = inject(MessageService);

  @Input({ required: true }) dataTemplates: SectorDataTemplateDto[] = [];
  @Output() changed = new EventEmitter<void>();

  protected t(key: keyof typeof SECTOR_RULES_FR): string {
    return SECTOR_RULES_FR[key];
  }

  readonly canManage = computed(() => this.permissions.has(PlatformPermission.SectorRulesManage));
  readonly busy = signal(false);

  readonly detail = signal<SectorDataTemplateDto | null>(null);
  detailVisible = false;
  form = { labelFr: '' };

  dialogTitle(): string {
    const d = this.detail();
    return d ? SECTOR_RULES_FR['templates.dialog.title'].replace('{code}', d.code) : '';
  }

  openDetail(row: SectorDataTemplateDto): void {
    this.detail.set(row);
    this.form = { labelFr: row.labelFr };
    this.detailVisible = true;
  }

  save(): void {
    const d = this.detail();
    if (!d) return;
    this.busy.set(true);
    // Code/segment/domaine immuables côté backend (hors UpdateSectorDataTemplateRequest) :
    // on renvoie le libellé édité + la version/ordre courants + les items existants (audit).
    const request: UpdateSectorDataTemplateRequest = {
      labelFr: this.form.labelFr.trim(),
      descriptionFr: d.descriptionFr,
      version: d.version,
      sortOrder: d.sortOrder,
      items: d.items.map(i => ({ itemKind: i.itemKind, payloadJson: i.payloadJson, sortOrder: i.sortOrder }))
    };
    this.api.updateTemplate(d.id, request).subscribe({
      next: res => {
        this.busy.set(false);
        if (res.success) {
          this.detailVisible = false;
          this.toast.add({ severity: 'success', summary: SECTOR_RULES_FR['templates.toast.save.success'] });
          this.changed.emit();
        } else {
          this.toastError(res.message);
        }
      },
      error: err => {
        this.busy.set(false);
        this.toastError((err as { error?: { message?: string } })?.error?.message);
      }
    });
  }

  private toastError(message?: string | null): void {
    this.toast.add({ severity: 'error', summary: SECTOR_RULES_FR['toast.error.title'], detail: message ?? SECTOR_RULES_FR['toast.error.generic'] });
  }
}
