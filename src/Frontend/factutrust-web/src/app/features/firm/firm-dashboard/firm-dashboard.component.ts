import { Component, inject, OnInit, signal } from '@angular/core';

import { CommonModule } from '@angular/common';

import { RouterModule } from '@angular/router';

import { ButtonModule } from 'primeng/button';

import { TagModule } from 'primeng/tag';

import { AuthService } from '@core/services/auth.service';

import { FirmAssignmentService } from '@core/services/firm-assignment.service';

import {

  FirmDashboardService,

  FirmDashboardData,
  FirmDashboardInvitationRow

} from '@core/services/firm-dashboard.service';

import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';

import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';



@Component({

  selector: 'app-firm-dashboard',

  standalone: true,

  imports: [

    CommonModule,

    RouterModule,

    ButtonModule,

    TagModule,

    PageHeaderComponent,

    StatCardComponent

  ],

  template: `

    <app-page-header

      [title]="'Tableau de bord cabinet'"

      [subtitle]="welcomeSubtitle()" />



    @if (loading()) {

      <p class="loading">Chargement…</p>

    } @else {

      <div class="stats-grid">

        <app-stat-card

          label="Dossiers actifs"

          [value]="dashboard()?.activeClientsCount ?? 0"

          icon="fa-solid fa-briefcase"

          variant="primary"

          routerLink="/firm/clients" />

        <app-stat-card

          label="Invitations en attente"

          [value]="dashboard()?.pendingInvitationsCount ?? 0"

          icon="fa-solid fa-envelope-open-text"

          [variant]="(dashboard()?.pendingInvitationsCount ?? 0) > 0 ? 'warning' : 'success'"

          routerLink="/firm/invitations" />

        <app-stat-card

          label="Dossiers inactifs (30j)"

          [value]="dashboard()?.inactiveDossiersCount ?? 0"

          icon="fa-solid fa-clock"

          [variant]="(dashboard()?.inactiveDossiersCount ?? 0) > 0 ? 'warning' : 'success'" />

        <app-stat-card

          label="Déclarations TVA brouillon"

          [value]="dashboard()?.vatDraftsCount ?? 0"

          icon="fa-solid fa-file-invoice"

          variant="primary" />

      </div>



      <section class="panel">

        <div class="panel-header">

          <h2>Mes dossiers</h2>

          <a routerLink="/firm/clients" pButton label="Voir tous" class="p-button-text"></a>

        </div>

        @if ((dashboard()?.clients?.length ?? 0) === 0) {

          <p class="empty">Aucun dossier client actif. Acceptez une invitation pour commencer.</p>

        } @else {

          <table class="dossier-table">

            <thead>

              <tr>

                <th>Société</th>

                <th>Actif depuis</th>

                <th>Dernière écriture</th>

                <th>Statut</th>

                <th></th>

              </tr>

            </thead>

            <tbody>

              @for (c of dashboard()?.clients ?? []; track c.assignmentId) {

                <tr>

                  <td>{{ c.companyName }}</td>

                  <td>{{ c.activeSince | date:'shortDate' }}</td>

                  <td>{{ c.lastJournalEntryDate ? (c.lastJournalEntryDate | date:'shortDate') : '—' }}</td>

                  <td>

                    @if (c.isInactive30Days) {

                      <p-tag severity="warn" value="Inactif 30j" />

                    } @else {

                      <p-tag severity="success" value="Actif" />

                    }

                  </td>

                  <td>

                    <a [routerLink]="['/firm/open', c.companyTenantId]" pButton label="Ouvrir comptabilité" class="p-button-sm"></a>

                  </td>

                </tr>

              }

            </tbody>

          </table>

        }

      </section>



      <section class="panel">

        <div class="panel-header">

          <h2>Invitations en attente</h2>

          <a routerLink="/firm/invitations" pButton label="Gérer" class="p-button-text"></a>

        </div>

        @if ((dashboard()?.pendingInvitations?.length ?? 0) === 0) {

          <p class="empty">Aucune invitation en attente.</p>

        } @else {

          <ul class="invitation-list">

            @for (inv of dashboard()?.pendingInvitations ?? []; track inv.id) {

              <li>

                <div>

                  <strong>{{ inv.companyName }}</strong>

                  <span class="muted">{{ inv.requestedAt | date:'short' }}</span>

                  @if (inv.notes) {

                    <p class="notes">{{ inv.notes }}</p>

                  }

                </div>

                <div class="actions">

                  <button type="button" pButton label="Accepter" class="p-button-sm p-button-success" (click)="accept(inv)"></button>

                  <button type="button" pButton label="Refuser" class="p-button-sm p-button-outlined" (click)="reject(inv)"></button>

                </div>

              </li>

            }

          </ul>

        }

      </section>

    }

  `,

  styles: [`

    .loading, .empty { color: var(--color-neutral-600); padding: 1rem 0; }

    .stats-grid {

      display: grid;

      grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));

      gap: 1rem;

      margin-bottom: 1.5rem;

    }

    .panel {

      background: var(--color-background-elevated, #fff);

      border: 1px solid var(--color-border-subtle, #e2e8f0);

      border-radius: var(--radius-xl, 12px);

      padding: 1.25rem;

      margin-bottom: 1.25rem;

    }

    .panel-header {

      display: flex;

      align-items: center;

      justify-content: space-between;

      margin-bottom: 1rem;

    }

    .panel-header h2 { margin: 0; font-size: 1.1rem; }

    .dossier-table { width: 100%; border-collapse: collapse; font-size: 0.9rem; }

    .dossier-table th, .dossier-table td { padding: 0.6rem 0.5rem; text-align: left; border-bottom: 1px solid var(--color-border-subtle, #e2e8f0); }

    .invitation-list { list-style: none; margin: 0; padding: 0; }

    .invitation-list li {

      display: flex;

      justify-content: space-between;

      align-items: flex-start;

      gap: 1rem;

      padding: 0.75rem 0;

      border-bottom: 1px solid var(--color-border-subtle, #e2e8f0);

    }

    .muted { color: var(--color-neutral-500); margin-left: 0.5rem; font-size: 0.85rem; }

    .notes { margin: 0.25rem 0 0; font-size: 0.85rem; color: var(--color-neutral-600); }

    .actions { display: flex; gap: 0.5rem; flex-shrink: 0; }

  `]

})

export class FirmDashboardComponent implements OnInit {

  readonly auth = inject(AuthService);

  private readonly dashboardService = inject(FirmDashboardService);

  private readonly assignments = inject(FirmAssignmentService);



  readonly loading = signal(true);

  readonly dashboard = signal<FirmDashboardData | null>(null);



  ngOnInit(): void {

    this.loadDashboard();

  }



  welcomeSubtitle(): string {

    const name = this.auth.user()?.companyName ?? 'Cabinet';

    return `Bienvenue, ${name} — ${new Date().toLocaleDateString('fr-FR', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })}`;

  }



  loadDashboard(): void {

    this.loading.set(true);

    this.dashboardService.getDashboard().subscribe({

      next: r => {

        if (r.success) this.dashboard.set(r.data);

        this.loading.set(false);

      },

      error: () => this.loading.set(false)

    });

  }



  accept(inv: FirmDashboardInvitationRow): void {

    this.assignments.acceptInvitation(inv.id).subscribe(() => this.loadDashboard());

  }



  reject(inv: FirmDashboardInvitationRow): void {

    this.assignments.rejectInvitation(inv.id).subscribe(() => this.loadDashboard());

  }

}

