import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TabsModule } from 'primeng/tabs';
import { PlatformMeService } from '@core/services/platform-me.service';
import { PlatformPermissionsService } from '@core/services/platform-permissions.service';
import { PlatformPermission } from '@core/models/platform.models';
import type { FailedLoginAttemptsPageDto, UserSessionsPageDto } from '@core/models/platform.models';
import { FtPageHeaderComponent } from '@core/ui/page-header/ft-page-header.component';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import { FtCellRelativeDateComponent } from '@core/ui/table-cells/ft-cell-relative-date.component';

@Component({
  selector: 'app-me-audit-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    TableModule,
    ButtonModule,
    TabsModule,
    FtPageHeaderComponent,
    FtBadgeComponent,
    FtSkeletonComponent,
    FtEmptyStateComponent,
    FtCellRelativeDateComponent
  ],
  template: `
    <ft-page-header title="Mon audit" subtitle="Vos sessions actives et tentatives de connexion récentes.">
      <ng-container ftActions>
        @if (canSeeFullAudit()) {
          <p-button label="Journal d'audit complet" icon="pi pi-history" [outlined]="true" routerLink="/audit" />
        }
        <p-button label="Retour" icon="pi pi-arrow-left" [outlined]="true" routerLink="/tenants" />
      </ng-container>
    </ft-page-header>

    <p-tabs value="0">
      <p-tablist>
        <p-tab value="0">Mes sessions</p-tab>
        <p-tab value="1">Mes tentatives échouées</p-tab>
      </p-tablist>
      <p-tabpanels>
        <p-tabpanel value="0">
          @if (sessionsLoading()) {
            <ft-skeleton shape="rect" width="100%" height="8rem" />
          } @else if ((sessionsPage()?.items?.length ?? 0) === 0) {
            <ft-empty-state
              title="Aucune session"
              description="Aucune session active enregistrée pour votre compte."
            />
          } @else {
            <p-table
              [value]="sessionsPage()?.items ?? []"
              styleClass="p-datatable-sm ft-table"
              responsiveLayout="scroll"
            >
              <ng-template pTemplate="header">
                <tr>
                  <th>Appareil / IP</th>
                  <th>Créée</th>
                  <th>Expire</th>
                  <th>Statut</th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-row>
                <tr>
                  <td>
                    <div>{{ row.userAgent || '—' }}</div>
                    <code class="muted">{{ row.ipAddress || '—' }}</code>
                  </td>
                  <td><ft-cell-relative-date [date]="row.issuedAt" /></td>
                  <td>{{ row.expiresAt | date: 'dd/MM/yyyy HH:mm' }}</td>
                  <td>
                    <ft-badge [tone]="row.isActive ? 'success' : 'neutral'" size="sm">
                      {{ row.isActive ? 'Active' : 'Expirée' }}
                    </ft-badge>
                  </td>
                </tr>
              </ng-template>
            </p-table>
          }
        </p-tabpanel>

        <p-tabpanel value="1">
          @if (failedLoading()) {
            <ft-skeleton shape="rect" width="100%" height="8rem" />
          } @else if ((failedPage()?.items?.length ?? 0) === 0) {
            <ft-empty-state
              title="Aucune tentative"
              description="Aucune tentative de connexion échouée récente pour votre compte."
            />
          } @else {
            <p-table
              [value]="failedPage()?.items ?? []"
              styleClass="p-datatable-sm ft-table"
              responsiveLayout="scroll"
            >
              <ng-template pTemplate="header">
                <tr>
                  <th>Date</th>
                  <th>IP</th>
                  <th>Raison</th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-row>
                <tr>
                  <td>{{ row.attemptAt | date: 'dd/MM/yyyy HH:mm:ss' }}</td>
                  <td><code>{{ row.ipAddress }}</code></td>
                  <td>{{ row.reasonDisplay }}</td>
                </tr>
              </ng-template>
            </p-table>
          }
        </p-tabpanel>
      </p-tabpanels>
    </p-tabs>
  `,
  styles: [
    `
      .muted {
        color: var(--ft-text-muted);
        font-size: 0.78rem;
      }
    `
  ]
})
export class MeAuditPageComponent implements OnInit {
  private readonly meService = inject(PlatformMeService);
  private readonly permissions = inject(PlatformPermissionsService);

  readonly sessionsLoading = signal(true);
  readonly failedLoading = signal(true);
  readonly sessionsPage = signal<UserSessionsPageDto | null>(null);
  readonly failedPage = signal<FailedLoginAttemptsPageDto | null>(null);

  readonly canSeeFullAudit = computed(() => this.permissions.has(PlatformPermission.AuditRead));

  ngOnInit(): void {
    this.loadSessions();
    this.loadFailedLogins();
  }

  private loadSessions(): void {
    this.meService.getMySessions().subscribe({
      next: res => {
        this.sessionsLoading.set(false);
        if (res.success && res.data) this.sessionsPage.set(res.data);
      },
      error: () => this.sessionsLoading.set(false)
    });
  }

  private loadFailedLogins(): void {
    this.meService.getMyFailedLogins().subscribe({
      next: res => {
        this.failedLoading.set(false);
        if (res.success && res.data) this.failedPage.set(res.data);
      },
      error: () => this.failedLoading.set(false)
    });
  }
}
