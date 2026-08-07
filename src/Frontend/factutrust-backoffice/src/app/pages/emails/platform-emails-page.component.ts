import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

import { PlatformEmailsService } from '@core/services/platform-emails.service';
import type {
  EmailMessageDetailDto,
  EmailMessagesPageDto,
  EmailMessageStatusValue
} from '@core/models/platform.models';

import { FtPageHeaderComponent } from '@core/ui/page-header/ft-page-header.component';
import { FtKpiCardComponent } from '@core/ui/kpi-card/ft-kpi-card.component';
import { FtFilterToolbarComponent } from '@core/ui/filter-toolbar/ft-filter-toolbar.component';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtDrawerComponent } from '@core/ui/drawer/ft-drawer.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import { FtCellRelativeDateComponent } from '@core/ui/table-cells/ft-cell-relative-date.component';
import type { FtTone } from '@core/ui/badge/ft-badge.component';

import { EMAILS_FR } from './emails.i18n.fr';

@Component({
  selector: 'app-platform-emails-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    FormsModule,
    TableModule,
    InputTextModule,
    ButtonModule,
    SelectModule,
    DialogModule,
    TooltipModule,
    FtPageHeaderComponent,
    FtKpiCardComponent,
    FtFilterToolbarComponent,
    FtBadgeComponent,
    FtDrawerComponent,
    FtSkeletonComponent,
    FtEmptyStateComponent,
    FtCellRelativeDateComponent
  ],
  template: `
    <ft-page-header [title]="t('list.title')" [subtitle]="t('list.subtitle')">
      <ng-container ftActions>
        <p-button
          [label]="t('list.actions.refresh')"
          icon="pi pi-refresh"
          [outlined]="true"
          [disabled]="loading()"
          (onClick)="load()" />
        <p-button
          [label]="t('list.actions.sendTest')"
          icon="pi pi-send"
          severity="primary"
          (onClick)="sendTestDialogOpen = true" />
      </ng-container>
    </ft-page-header>

    <section class="kpi-row" role="region" aria-label="Statistiques emails">
      <ft-kpi-card
        [label]="t('kpi.total')"
        [value]="page()?.totalCount ?? null"
        tone="info"
        icon="pi pi-envelope"
        [loading]="loading()" />
      <ft-kpi-card
        [label]="t('kpi.queued')"
        [value]="page()?.queuedCount ?? null"
        tone="warning"
        icon="pi pi-clock"
        [loading]="loading()" />
      <ft-kpi-card
        [label]="t('kpi.sent')"
        [value]="page()?.sentCount ?? null"
        tone="success"
        icon="pi pi-check-circle"
        [loading]="loading()" />
      <ft-kpi-card
        [label]="t('kpi.failed')"
        [value]="page()?.failedCount ?? null"
        [tone]="(page()?.failedCount ?? 0) > 0 ? 'danger' : 'neutral'"
        icon="pi pi-times-circle"
        [loading]="loading()" />
      <ft-kpi-card
        [label]="t('kpi.last24h')"
        [value]="page()?.last24h ?? null"
        tone="accent"
        icon="pi pi-history"
        [loading]="loading()" />
    </section>

    <ft-filter-toolbar>
      <input
        type="text"
        pInputText
        [(ngModel)]="search"
        (ngModelChange)="onSearchChange()"
        [placeholder]="t('filter.search')" />
      <p-select
        [options]="statusOptions"
        [ngModel]="filterStatus()"
        (ngModelChange)="onStatusChange($event)"
        optionLabel="label"
        optionValue="value"
        [placeholder]="t('filter.status')"
        styleClass="ft-dd" />
    </ft-filter-toolbar>

    <p-table
      [value]="page()?.items ?? []"
      [loading]="loading()"
      [lazy]="true"
      [first]="tableFirst()"
      (onLazyLoad)="onLazyLoad($event)"
      [paginator]="(page()?.totalCount ?? 0) > 0"
      [rows]="pageSize()"
      [totalRecords]="page()?.totalCount ?? 0"
      [rowsPerPageOptions]="[25, 50, 100]"
      [showCurrentPageReport]="true"
      [currentPageReportTemplate]="'{first}–{last} sur {totalRecords}'"
      responsiveLayout="scroll"
      styleClass="p-datatable-sm ft-emails-table"
      [tableStyle]="{ 'min-width': '60rem' }">
      <ng-template pTemplate="header">
        <tr>
          <th scope="col">{{ t('col.createdAt') }}</th>
          <th scope="col">{{ t('col.to') }}</th>
          <th scope="col">{{ t('col.template') }}</th>
          <th scope="col">{{ t('col.subject') }}</th>
          <th scope="col">{{ t('col.status') }}</th>
          <th scope="col">{{ t('col.attempts') }}</th>
          <th scope="col" class="col-actions">{{ t('col.actions') }}</th>
        </tr>
      </ng-template>

      <ng-template pTemplate="body" let-row>
        <tr (click)="openDetail(row.id)" class="clickable">
          <td>
            <ft-cell-relative-date [date]="row.createdAt" />
          </td>
          <td>
            <code class="cell-mono">{{ row.toEmail }}</code>
            @if (row.toName) {
              <small class="muted block">{{ row.toName }}</small>
            }
          </td>
          <td><ft-badge tone="accent" size="sm">{{ row.templateCode }}</ft-badge></td>
          <td class="subject-cell" [pTooltip]="row.subject">{{ row.subject }}</td>
          <td>
            <ft-badge [tone]="statusTone(row.status)" [withDot]="true" size="sm">
              {{ row.statusDisplay }}
            </ft-badge>
          </td>
          <td>{{ row.attemptsCount }}</td>
          <td class="col-actions" (click)="$event.stopPropagation()">
            @if (canRetry(row.status)) {
              <p-button
                icon="pi pi-replay"
                [text]="true"
                size="small"
                [pTooltip]="t('action.retry')"
                tooltipPosition="left"
                [disabled]="busy()"
                (onClick)="retry(row.id)" />
            }
          </td>
        </tr>
      </ng-template>

      <ng-template pTemplate="emptymessage">
        <tr>
          <td colspan="7">
            <ft-empty-state
              variant="all-clear"
              [title]="t('empty.title')"
              [description]="t('empty.desc')" />
          </td>
        </tr>
      </ng-template>

      <ng-template pTemplate="loadingbody">
        @for (i of skeletonRows; track $index) {
          <tr>
            <td><ft-skeleton shape="line" width="5rem" /></td>
            <td><ft-skeleton shape="line" width="60%" /></td>
            <td><ft-skeleton shape="line" width="5rem" /></td>
            <td><ft-skeleton shape="line" width="60%" /></td>
            <td><ft-skeleton shape="line" width="4rem" /></td>
            <td><ft-skeleton shape="line" width="2rem" /></td>
            <td class="col-actions"><ft-skeleton shape="circle" width="1.5rem" height="1.5rem" /></td>
          </tr>
        }
      </ng-template>
    </p-table>

    <!-- Drawer detail -->
    <ft-drawer
      [(visible)]="drawerOpen"
      [title]="t('detail.title')"
      [subtitle]="detail()?.subject ?? null"
      [width]="'620px'">
      @if (detail(); as d) {
        <h4>{{ t('detail.section.header') }}</h4>
        <dl class="kv">
          <div><dt>{{ t('detail.field.to') }}</dt><dd><code>{{ d.toEmail }}</code></dd></div>
          @if (d.toName) {
            <div><dt>Nom</dt><dd>{{ d.toName }}</dd></div>
          }
          <div><dt>{{ t('detail.field.subject') }}</dt><dd>{{ d.subject }}</dd></div>
          <div><dt>{{ t('detail.field.template') }}</dt><dd><code>{{ d.templateCode }}</code></dd></div>
          <div>
            <dt>{{ t('detail.field.status') }}</dt>
            <dd>
              <ft-badge [tone]="statusTone(d.status)" [withDot]="true">
                {{ d.statusDisplay }}
              </ft-badge>
            </dd>
          </div>
          <div><dt>{{ t('detail.field.created') }}</dt><dd>{{ d.createdAt | date: 'dd/MM/yyyy HH:mm:ss' }}</dd></div>
          @if (d.sentAt) {
            <div><dt>{{ t('detail.field.sent') }}</dt><dd>{{ d.sentAt | date: 'dd/MM/yyyy HH:mm:ss' }}</dd></div>
          }
          <div><dt>{{ t('detail.field.attempts') }}</dt><dd>{{ d.attemptsCount }}</dd></div>
          @if (d.providerMessageId) {
            <div><dt>{{ t('detail.field.providerId') }}</dt><dd><code>{{ d.providerMessageId }}</code></dd></div>
          }
        </dl>

        @if (d.errorMessage) {
          <h4>{{ t('detail.section.error') }}</h4>
          <pre class="error-block">{{ d.errorMessage }}</pre>
        }

        @if (d.renderedHtml) {
          <h4>{{ t('detail.section.preview') }}</h4>
          <iframe
            class="preview-iframe"
            [srcdoc]="d.renderedHtml"
            sandbox="allow-same-origin"
            title="Aperçu HTML"
          ></iframe>
        }
      } @else if (detailLoading()) {
        <ft-skeleton shape="line" />
        <ft-skeleton shape="line" />
        <ft-skeleton shape="rect" height="20rem" width="100%" />
      }

      <ng-container ftFooter>
        @if (detail(); as d) {
          @if (canRetry(d.status)) {
            <p-button
              [label]="t('action.retry')"
              icon="pi pi-replay"
              severity="warn"
              [outlined]="true"
              [disabled]="busy()"
              (onClick)="retry(d.id)" />
          }
        }
      </ng-container>
    </ft-drawer>

    <!-- Dialog send test -->
    <p-dialog
      [(visible)]="sendTestDialogOpen"
      [modal]="true"
      [closable]="!busy()"
      [draggable]="false"
      [resizable]="false"
      [style]="{ width: '28rem', maxWidth: '95vw' }"
      [header]="t('sendTest.title')">
      <p class="muted">{{ t('sendTest.intro') }}</p>
      <div class="field">
        <label for="test-email">{{ t('sendTest.field.to') }}</label>
        <input
          id="test-email"
          type="email"
          pInputText
          [(ngModel)]="testEmail"
          autocomplete="email"
          class="w-full"
          [disabled]="busy()" />
      </div>
      <ng-template pTemplate="footer">
        <p-button label="Annuler" [text]="true" severity="secondary" [disabled]="busy()" (onClick)="sendTestDialogOpen = false" />
        <p-button
          [label]="t('sendTest.confirm')"
          icon="pi pi-send"
          severity="primary"
          [disabled]="busy() || !testEmail.includes('@')"
          [loading]="busy()"
          (onClick)="onSendTestConfirmed()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .kpi-row {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr));
        gap: var(--gap-md);
        margin-bottom: var(--gap-md);
      }

      .clickable {
        cursor: pointer;
      }

      .cell-mono {
        font-family: ui-monospace, SFMono-Regular, monospace;
        font-size: 0.82rem;
      }

      .block {
        display: block;
      }

      .muted {
        color: var(--ft-text-muted);
      }

      .subject-cell {
        max-width: 16rem;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      .col-actions {
        text-align: end;
        white-space: nowrap;
        width: 4rem;
      }

      :host ::ng-deep .ft-dd {
        min-width: 11rem;
      }

      :host ::ng-deep .ft-emails-table.p-datatable .p-datatable-thead > tr > th {
        font-size: 0.72rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--ft-text-muted);
        border-color: var(--ft-border);
        background: var(--ft-surface-2);
        font-weight: 600;
      }

      :host ::ng-deep .ft-emails-table.p-datatable .p-datatable-tbody > tr:hover {
        background: var(--ft-surface-3);
      }

      h4 {
        margin: var(--gap-md) 0 var(--gap-xs);
        font-size: 0.78rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--ft-text-muted);
        font-weight: 600;
      }

      .kv {
        margin: 0;
        padding: 0;
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
      }

      .kv > div {
        display: grid;
        grid-template-columns: 9rem 1fr;
        gap: 0.6rem;
        font-size: 0.88rem;
      }

      .kv dt {
        margin: 0;
        color: var(--ft-text-muted);
        font-size: 0.78rem;
      }

      .kv dd {
        margin: 0;
        color: var(--ft-text);
        word-break: break-word;
      }

      .kv code {
        font-family: ui-monospace, SFMono-Regular, monospace;
        font-size: 0.85em;
        background: var(--ft-surface-2);
        padding: 0.05rem 0.3rem;
        border-radius: var(--ft-radius-sm);
        color: var(--ft-accent);
      }

      .error-block {
        margin: 0 0 var(--gap-md);
        padding: var(--gap-sm) var(--gap-md);
        background: var(--ft-danger-surface);
        border: 1px solid var(--ft-danger-border);
        border-radius: var(--ft-radius);
        color: var(--ft-text);
        font-family: ui-monospace, SFMono-Regular, monospace;
        font-size: 0.78rem;
        white-space: pre-wrap;
        word-break: break-word;
      }

      .preview-iframe {
        width: 100%;
        height: 24rem;
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius);
        background: white;
      }

      .field {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
        margin-top: var(--gap-sm);
      }

      .field label {
        font-size: 0.78rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--ft-text-muted);
        font-weight: 600;
      }

      .w-full {
        width: 100%;
      }
    `
  ]
})
export class PlatformEmailsPageComponent implements OnInit {
  private readonly api = inject(PlatformEmailsService);
  private readonly toast = inject(MessageService);

  protected readonly skeletonRows = Array.from({ length: 6 });

  protected t(key: keyof typeof EMAILS_FR): string {
    return EMAILS_FR[key];
  }

  // ----- State -----
  readonly page = signal<EmailMessagesPageDto | null>(null);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly tableFirst = signal(0);
  readonly pageSize = signal(25);
  readonly filterStatus = signal<EmailMessageStatusValue | null>(null);
  protected search = '';
  private searchDebounce: ReturnType<typeof setTimeout> | null = null;

  // Drawer
  drawerOpen = false;
  readonly detail = signal<EmailMessageDetailDto | null>(null);
  readonly detailLoading = signal(false);

  // Send test
  sendTestDialogOpen = false;
  protected testEmail = '';

  protected readonly statusOptions = [
    { label: EMAILS_FR['filter.allStatuses'], value: null },
    { label: EMAILS_FR['status.queued'], value: 0 },
    { label: EMAILS_FR['status.sending'], value: 1 },
    { label: EMAILS_FR['status.sent'], value: 2 },
    { label: EMAILS_FR['status.failed'], value: 3 },
    { label: EMAILS_FR['status.bounced'], value: 4 },
    { label: EMAILS_FR['status.cancelled'], value: 5 },
    { label: EMAILS_FR['status.skipped'], value: 6 }
  ];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    const page = Math.floor(this.tableFirst() / this.pageSize()) + 1;
    this.api
      .list({
        status: this.filterStatus() ?? undefined,
        search: this.search || undefined,
        page,
        pageSize: this.pageSize()
      })
      .subscribe({
        next: (res) => {
          this.loading.set(false);
          if (res.success && res.data) this.page.set(res.data);
        },
        error: () => {
          this.loading.set(false);
          this.toastError();
        }
      });
  }

  onLazyLoad(event: TableLazyLoadEvent): void {
    this.tableFirst.set(event.first ?? 0);
    this.pageSize.set(event.rows ?? 25);
    this.load();
  }

  onSearchChange(): void {
    if (this.searchDebounce) clearTimeout(this.searchDebounce);
    this.searchDebounce = setTimeout(() => {
      this.tableFirst.set(0);
      this.load();
    }, 350);
  }

  onStatusChange(value: EmailMessageStatusValue | null): void {
    this.filterStatus.set(value);
    this.tableFirst.set(0);
    this.load();
  }

  // ----- Detail drawer -----
  openDetail(id: string): void {
    this.drawerOpen = true;
    this.detailLoading.set(true);
    this.detail.set(null);
    this.api.getById(id).subscribe({
      next: (res) => {
        this.detailLoading.set(false);
        if (res.success && res.data) this.detail.set(res.data);
      },
      error: () => {
        this.detailLoading.set(false);
        this.toastError();
      }
    });
  }

  // ----- Retry -----
  canRetry(status: EmailMessageStatusValue): boolean {
    // Tout sauf Sent (2) et Cancelled (5)
    return status !== 2 && status !== 5;
  }

  retry(id: string): void {
    this.busy.set(true);
    this.api.retry(id).subscribe({
      next: (res) => {
        this.busy.set(false);
        if (res.success) {
          this.toast.add({
            severity: 'success',
            summary: EMAILS_FR['toast.retry.success'],
            detail: ''
          });
          this.load();
          if (this.detail()?.id === id) this.openDetail(id);
        } else {
          this.toastError(res.message);
        }
      },
      error: () => {
        this.busy.set(false);
        this.toastError();
      }
    });
  }

  // ----- Send test -----
  onSendTestConfirmed(): void {
    if (!this.testEmail.includes('@')) return;
    this.busy.set(true);
    this.api.sendTest(this.testEmail).subscribe({
      next: (res) => {
        this.busy.set(false);
        this.sendTestDialogOpen = false;
        if (res.success) {
          this.toast.add({
            severity: 'success',
            summary: EMAILS_FR['toast.sendTest.success'],
            detail: this.testEmail
          });
          this.testEmail = '';
          this.load();
        } else {
          this.toastError(res.message);
        }
      },
      error: () => {
        this.busy.set(false);
        this.toastError();
      }
    });
  }

  // ----- Helpers -----
  statusTone(status: EmailMessageStatusValue): FtTone {
    switch (status) {
      case 2: // Sent
        return 'success';
      case 0: // Queued
      case 1: // Sending
        return 'warning';
      case 3: // Failed
      case 4: // Bounced
        return 'danger';
      case 5: // Cancelled
      case 6: // Skipped
      default:
        return 'neutral';
    }
  }

  private toastError(detail?: string | null): void {
    this.toast.add({
      severity: 'error',
      summary: EMAILS_FR['toast.error.title'],
      detail: detail ?? EMAILS_FR['toast.error.detail']
    });
  }
}
