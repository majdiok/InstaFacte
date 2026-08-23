import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService, MenuItem } from 'primeng/api';

import { PlatformAdminsService } from '@core/services/platform-admins.service';
import { PlatformPermissionsService } from '@core/services/platform-permissions.service';
import {
  PlatformPermission,
  PlatformRole,
  type CreatePlatformAdminRequest,
  type PlatformAdminListItemDto,
  type PlatformAdminListPageDto
} from '@core/models/platform.models';

import { FtPageHeaderComponent } from '@core/ui/page-header/ft-page-header.component';
import { FtKpiCardComponent } from '@core/ui/kpi-card/ft-kpi-card.component';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtAvatarComponent } from '@core/ui/avatar/ft-avatar.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';
import { FtCellRelativeDateComponent } from '@core/ui/table-cells/ft-cell-relative-date.component';
import { FtCellActionsMenuComponent } from '@core/ui/table-cells/ft-cell-actions-menu.component';
import type { FtTone } from '@core/ui/badge/ft-badge.component';

import { ADMINS_FR, roleLabel } from './admins.i18n.fr';
import { AdminCreateDialogComponent } from './admin-create-dialog.component';
import { AdminRoleDialogComponent } from './admin-role-dialog.component';
import { AdminResetPasswordDialogComponent } from './admin-reset-password-dialog.component';

@Component({
  selector: 'app-platform-admins-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TableModule,
    ButtonModule,
    TooltipModule,
    FtPageHeaderComponent,
    FtKpiCardComponent,
    FtBadgeComponent,
    FtAvatarComponent,
    FtEmptyStateComponent,
    FtSkeletonComponent,
    FtCellRelativeDateComponent,
    FtCellActionsMenuComponent,
    AdminCreateDialogComponent,
    AdminRoleDialogComponent,
    AdminResetPasswordDialogComponent
  ],
  template: `
    <ft-page-header [title]="t('list.title')" [subtitle]="t('list.subtitle')">
      <ng-container ftActions>
        <p-button
          [label]="t('list.actions.refresh')"
          icon="pi pi-refresh"
          [outlined]="true"
          [disabled]="loading()"
          (onClick)="load()"
        />
        <p-button
          [label]="t('list.actions.create')"
          icon="pi pi-user-plus"
          severity="primary"
          [disabled]="!canManage()"
          [pTooltip]="canManage() ? '' : 'Permission admins:manage requise'"
          tooltipPosition="bottom"
          (onClick)="openCreateDialog()"
        />
      </ng-container>
    </ft-page-header>

    <!-- KPIs -->
    <section class="kpi-row" role="region" aria-label="Indicateurs admins">
      <ft-kpi-card
        [label]="t('kpi.total')"
        [value]="page()?.totalCount ?? null"
        [hint]="t('kpi.total.hint')"
        tone="info"
        icon="pi pi-users"
        [loading]="loading()"
      />
      <ft-kpi-card
        [label]="t('kpi.active')"
        [value]="page()?.activeCount ?? null"
        [hint]="t('kpi.active.hint')"
        tone="success"
        icon="pi pi-check-circle"
        [loading]="loading()"
      />
      <ft-kpi-card
        [label]="t('kpi.locked')"
        [value]="page()?.lockedCount ?? null"
        [hint]="t('kpi.locked.hint')"
        tone="warning"
        icon="pi pi-lock"
        [loading]="loading()"
      />
      <ft-kpi-card
        [label]="t('kpi.superAdmin')"
        [value]="page()?.superAdminCount ?? null"
        [hint]="t('kpi.superAdmin.hint')"
        tone="accent"
        icon="pi pi-shield"
        [loading]="loading()"
      />
    </section>

    <!-- Table -->
    <p-table
      [value]="page()?.items ?? []"
      [loading]="loading()"
      styleClass="p-datatable-sm ft-table"
      [tableStyle]="{ 'min-width': '60rem' }"
      responsiveLayout="scroll"
    >
      <ng-template pTemplate="header">
        <tr>
          <th scope="col">{{ t('col.name') }}</th>
          <th scope="col">{{ t('col.email') }}</th>
          <th scope="col">{{ t('col.role') }}</th>
          <th scope="col">{{ t('col.lastLogin') }}</th>
          <th scope="col">{{ t('col.state') }}</th>
          <th scope="col" class="col-actions">{{ t('col.actions') }}</th>
        </tr>
      </ng-template>

      <ng-template pTemplate="body" let-row>
        <tr>
          <td>
            <div class="cell-name">
              <ft-avatar [name]="row.fullName" [seed]="row.id" size="sm" />
              <strong>{{ row.fullName }}</strong>
            </div>
          </td>
          <td><code class="cell-mono">{{ row.email }}</code></td>
          <td>
            @for (r of row.roles; track r) {
              <ft-badge [tone]="roleTone(r)" size="sm">
                {{ roleLabel(r) }}
              </ft-badge>
            }
          </td>
          <td>
            @if (row.lastLoginAt) {
              <ft-cell-relative-date [date]="row.lastLoginAt" />
            } @else {
              <span class="muted">{{ t('state.never') }}</span>
            }
          </td>
          <td>
            @if (row.isLockedOut) {
              <ft-badge tone="warning" [withDot]="true" size="sm">{{ t('state.locked') }}</ft-badge>
            } @else if (!row.isActive) {
              <ft-badge tone="danger" [withDot]="true" size="sm">{{ t('state.disabled') }}</ft-badge>
            } @else {
              <ft-badge tone="success" [withDot]="true" size="sm">{{ t('state.active') }}</ft-badge>
            }
          </td>
          <td class="col-actions">
            <ft-cell-actions-menu [items]="rowActionsById().get(row.id) ?? []" />
          </td>
        </tr>
      </ng-template>

      <ng-template pTemplate="emptymessage">
        <tr>
          <td colspan="6">
            <ft-empty-state variant="table-empty" [title]="t('empty.title')" [description]="t('empty.desc')">
              @if (canManage()) {
                <p-button
                  [label]="t('list.actions.create')"
                  icon="pi pi-user-plus"
                  severity="primary"
                  (onClick)="openCreateDialog()"
                />
              }
            </ft-empty-state>
          </td>
        </tr>
      </ng-template>

      <ng-template pTemplate="loadingbody">
        @for (i of skeletonRows; track $index) {
          <tr>
            <td>
              <div class="cell-name">
                <ft-skeleton shape="circle" width="1.75rem" height="1.75rem" />
                <ft-skeleton shape="line" width="50%" />
              </div>
            </td>
            <td><ft-skeleton shape="line" width="60%" /></td>
            <td><ft-skeleton shape="line" width="6rem" /></td>
            <td><ft-skeleton shape="line" width="5rem" /></td>
            <td><ft-skeleton shape="line" width="5rem" /></td>
            <td class="col-actions"><ft-skeleton shape="circle" width="1.6rem" height="1.6rem" /></td>
          </tr>
        }
      </ng-template>
    </p-table>

    <!-- Modals -->
    <app-admin-create-dialog
      [(visible)]="createDialogOpen"
      [busy]="busy()"
      (confirmed)="onCreateConfirmed($event)"
    />
    <app-admin-role-dialog
      [(visible)]="roleDialogOpen"
      [data]="dialogTarget()"
      [busy]="busy()"
      (confirmed)="onRoleConfirmed($event)"
    />
    <app-admin-reset-password-dialog
      [(visible)]="resetPasswordDialogOpen"
      [data]="dialogTarget()"
      [busy]="busy()"
      (confirmed)="onResetPasswordConfirmed($event)"
    />
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .cell-name {
        display: inline-flex;
        align-items: center;
        gap: var(--gap-sm);
      }

      .cell-name strong {
        color: var(--ft-text);
        font-weight: 600;
      }
    `
  ]
})
export class PlatformAdminsPageComponent implements OnInit {
  private readonly api = inject(PlatformAdminsService);
  private readonly permissions = inject(PlatformPermissionsService);
  private readonly toast = inject(MessageService);

  protected readonly skeletonRows = Array.from({ length: 5 });

  protected t(key: keyof typeof ADMINS_FR): string {
    return ADMINS_FR[key];
  }

  protected roleLabel(role: string): string {
    return roleLabel(role);
  }

  protected roleTone(role: string): FtTone {
    switch (role) {
      case PlatformRole.PlatformAdmin:
        return 'accent';
      case PlatformRole.BillingAdmin:
        return 'info';
      case PlatformRole.SupportAgent:
        return 'success';
      case PlatformRole.MigrationOperator:
        return 'warning';
      case PlatformRole.ReadOnlyAuditor:
        return 'neutral';
      default:
        return 'neutral';
    }
  }

  // ----- State -----
  readonly page = signal<PlatformAdminListPageDto | null>(null);
  readonly loading = signal(false);
  readonly busy = signal(false);

  readonly dialogTarget = signal<PlatformAdminListItemDto | null>(null);
  createDialogOpen = false;
  roleDialogOpen = false;
  resetPasswordDialogOpen = false;

  readonly canManage = computed(() => this.permissions.has(PlatformPermission.AdminsManage));

  /** Cache stable des items kebab par admin id. */
  readonly rowActionsById = computed(() => {
    this.canManage();
    const map = new Map<string, MenuItem[]>();
    for (const row of this.page()?.items ?? []) {
      map.set(row.id, this.buildRowActions(row));
    }
    return map;
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.list().subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success && res.data) {
          this.page.set(res.data);
        } else {
          this.toastError(res.message);
        }
      },
      error: () => {
        this.loading.set(false);
        this.toastError();
      }
    });
  }

  // ----- Modals open -------------------------------------------------------
  openCreateDialog(): void {
    this.createDialogOpen = true;
  }

  openRoleDialog(row: PlatformAdminListItemDto): void {
    this.dialogTarget.set(row);
    this.roleDialogOpen = true;
  }

  openResetPasswordDialog(row: PlatformAdminListItemDto): void {
    this.dialogTarget.set(row);
    this.resetPasswordDialogOpen = true;
  }

  // ----- Actions -----------------------------------------------------------
  private buildRowActions(row: PlatformAdminListItemDto): MenuItem[] {
    return [
      {
        label: ADMINS_FR['action.changeRole'],
        icon: 'pi pi-shield',
        disabled: !this.canManage(),
        command: () => this.openRoleDialog(row)
      },
      {
        label: ADMINS_FR['action.resetPassword'],
        icon: 'pi pi-key',
        disabled: !this.canManage(),
        command: () => this.openResetPasswordDialog(row)
      },
      { separator: true },
      row.isActive
        ? {
            label: ADMINS_FR['action.disable'],
            icon: 'pi pi-ban',
            disabled: !this.canManage(),
            styleClass: 'mu-danger',
            command: () => this.disable(row)
          }
        : {
            label: ADMINS_FR['action.enable'],
            icon: 'pi pi-check',
            disabled: !this.canManage(),
            command: () => this.enable(row)
          },
      { separator: true },
      {
        label: ADMINS_FR['action.copyId'],
        icon: 'pi pi-copy',
        command: () => this.copyId(row.id)
      }
    ];
  }

  // ----- Confirmed handlers ------------------------------------------------
  onCreateConfirmed(request: CreatePlatformAdminRequest): void {
    this.busy.set(true);
    this.api.create(request).subscribe({
      next: (res) => {
        this.busy.set(false);
        if (res.success) {
          this.createDialogOpen = false;
          this.toast.add({
            severity: 'success',
            summary: ADMINS_FR['toast.create.success'],
            detail: request.email
          });
          this.load();
        } else {
          this.toastError(res.message);
        }
      },
      error: (err) => {
        this.busy.set(false);
        this.toastError(err?.error?.message);
      }
    });
  }

  onRoleConfirmed(payload: { data: PlatformAdminListItemDto; role: string }): void {
    this.busy.set(true);
    this.api.changeRole(payload.data.id, { role: payload.role }).subscribe({
      next: (res) => {
        this.busy.set(false);
        if (res.success) {
          this.roleDialogOpen = false;
          this.toast.add({
            severity: 'success',
            summary: ADMINS_FR['toast.role.success'],
            detail: `${payload.data.fullName} → ${roleLabel(payload.role)}`
          });
          this.load();
        } else {
          this.toastError(res.message);
        }
      },
      error: (err) => {
        this.busy.set(false);
        this.toastError(err?.error?.message);
      }
    });
  }

  onResetPasswordConfirmed(payload: { data: PlatformAdminListItemDto; newPassword: string }): void {
    this.busy.set(true);
    this.api.resetPassword(payload.data.id, { newPassword: payload.newPassword }).subscribe({
      next: (res) => {
        this.busy.set(false);
        if (res.success) {
          this.resetPasswordDialogOpen = false;
          this.toast.add({
            severity: 'success',
            summary: ADMINS_FR['toast.reset.success'],
            detail: payload.data.fullName
          });
        } else {
          this.toastError(res.message);
        }
      },
      error: (err) => {
        this.busy.set(false);
        this.toastError(err?.error?.message);
      }
    });
  }

  disable(row: PlatformAdminListItemDto): void {
    this.busy.set(true);
    this.api.disable(row.id).subscribe({
      next: (res) => {
        this.busy.set(false);
        if (res.success) {
          this.toast.add({
            severity: 'success',
            summary: ADMINS_FR['toast.disable.success'],
            detail: row.fullName
          });
          this.load();
        } else {
          this.toastError(res.message);
        }
      },
      error: (err) => {
        this.busy.set(false);
        this.toastError(err?.error?.message);
      }
    });
  }

  enable(row: PlatformAdminListItemDto): void {
    this.busy.set(true);
    this.api.enable(row.id).subscribe({
      next: (res) => {
        this.busy.set(false);
        if (res.success) {
          this.toast.add({
            severity: 'success',
            summary: ADMINS_FR['toast.enable.success'],
            detail: row.fullName
          });
          this.load();
        } else {
          this.toastError(res.message);
        }
      },
      error: (err) => {
        this.busy.set(false);
        this.toastError(err?.error?.message);
      }
    });
  }

  copyId(id: string): void {
    if (navigator.clipboard?.writeText) {
      navigator.clipboard
        .writeText(id)
        .then(() =>
          this.toast.add({
            severity: 'success',
            summary: 'Copié',
            detail: 'Identifiant copié dans le presse-papiers.'
          })
        )
        .catch(() => undefined);
    }
  }

  private toastError(detail?: string | null): void {
    this.toast.add({
      severity: 'error',
      summary: ADMINS_FR['error.title'],
      detail: detail ?? ADMINS_FR['error.detail']
    });
  }
}
