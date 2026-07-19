import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AutoCompleteModule } from 'primeng/autocomplete';
import {
  FirmAssignmentService,
  AccountingFirmDirectoryItem,
  FirmClientAssignment
} from '@core/services/firm-assignment.service';
import { ToastService } from '@core/services/toast.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { SkeletonComponent } from '@shared/components/skeleton/skeleton.component';
import {
  FirmAssignmentStatusCode,
  firmAssignmentStatusView,
  isOpenAssignment
} from './firm-assignment-status';

const NOTES_MAX = 1000;

@Component({
  selector: 'app-accounting-firm-settings',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    AutoCompleteModule,
    PageHeaderComponent,
    StatusBadgeComponent,
    SkeletonComponent
  ],
  template: `
    <app-page-header
      title="Cabinet comptable"
      subtitle="Confiez la gestion comptable de votre société à un cabinet partenaire">
    </app-page-header>

    @if (loading()) {
      <div class="af-card">
        <app-skeleton width="40%" height="1.4rem"></app-skeleton>
        <app-skeleton width="100%" height="3rem" shape="rounded"></app-skeleton>
        <app-skeleton width="30%" height="2.4rem" shape="rounded"></app-skeleton>
      </div>
    } @else if (current()) {
      @if (current(); as assignment) {
      <div class="af-card">
        <div class="af-liaison">
          <div class="af-liaison__head">
            <div class="af-liaison__identity">
              <span class="af-liaison__firm">{{ assignment.firmDisplayName }}</span>
              <app-status-badge
                [status]="statusView(assignment.status).badge"
                [label]="statusView(assignment.status).label">
              </app-status-badge>
            </div>

            @if (assignment.status === StatusCode.Active) {
              <button type="button" class="af-btn af-btn--danger" (click)="revoke()" [disabled]="acting()">
                <i class="pi pi-times-circle"></i> Révoquer l'affectation
              </button>
            } @else if (assignment.status === StatusCode.PendingFirmApproval) {
              <button type="button" class="af-btn af-btn--danger-outline" (click)="cancelPending()" [disabled]="acting()">
                <i class="pi pi-ban"></i> Annuler la demande
              </button>
            }
          </div>

          <dl class="af-meta">
            @if (assignment.status === StatusCode.Active && assignment.respondedAt) {
              <div class="af-meta__row">
                <dt>Actif depuis</dt>
                <dd>{{ assignment.respondedAt | date: 'dd/MM/yyyy' }}</dd>
              </div>
            } @else {
              <div class="af-meta__row">
                <dt>Demandée le</dt>
                <dd>{{ assignment.requestedAt | date: 'dd/MM/yyyy HH:mm' }}</dd>
              </div>
            }
            @if (assignment.notes) {
              <div class="af-meta__row">
                <dt>Message envoyé</dt>
                <dd class="af-meta__notes">{{ assignment.notes }}</dd>
              </div>
            }
          </dl>

          @if (assignment.status === StatusCode.PendingFirmApproval) {
            <p class="af-hint">
              <i class="pi pi-info-circle"></i>
              Votre demande a été transmise au cabinet. Vous serez notifié dès qu'elle sera acceptée ou refusée.
            </p>
          }
        </div>
      </div>
      }
    } @else {
      <!-- État : aucune liaison ouverte → recherche + éventuel bandeau de refus -->
      @if (lastClosed(); as closed) {
        <div class="af-banner af-banner--{{ statusView(closed.status).badge }}">
          <i class="pi" [ngClass]="closed.status === StatusCode.Rejected ? 'pi-times-circle' : 'pi-info-circle'"></i>
          <div class="af-banner__body">
            <strong>{{ closed.firmDisplayName }} — {{ statusView(closed.status).label }}</strong>
            <span class="af-banner__date">{{ (closed.respondedAt || closed.revokedAt || closed.requestedAt) | date: 'dd/MM/yyyy' }}</span>
            @if (closed.rejectionReason) {
              <p class="af-banner__reason">Motif : {{ closed.rejectionReason }}</p>
            }
            <span class="af-banner__cta">Vous pouvez adresser une nouvelle demande à un cabinet ci-dessous.</span>
          </div>
        </div>
      }

      <div class="af-card">
        <h2 class="af-card__title">Rechercher un cabinet</h2>
        <p class="af-card__subtitle">Recherchez un cabinet partenaire par son nom ou sa ville.</p>

        <div class="af-field">
          <label for="firmSearch">Cabinet</label>
          <p-autoComplete
            inputId="firmSearch"
            [(ngModel)]="selectedFirm"
            [suggestions]="suggestions()"
            (completeMethod)="search($event)"
            [dropdown]="true"
            [forceSelection]="true"
            field="displayName"
            placeholder="Nom ou ville du cabinet"
            [minLength]="2"
            emptyMessage="Aucun cabinet trouvé"
            appendTo="body"
            [style]="{ width: '100%' }"
            [showEmptyMessage]="true">
            <ng-template let-firm pTemplate="item">
              <div class="af-suggestion">
                <span class="af-suggestion__name">{{ firm?.displayName }}</span>
                <span class="af-suggestion__meta">
                  @if (firm?.city || firm?.governorate) {
                    <span><i class="pi pi-map-marker"></i> {{ firm?.city }}{{ firm?.city && firm?.governorate ? ', ' : '' }}{{ firm?.governorate }}</span>
                  }
                  @if (firm?.professionalRegistrationNumber) {
                    <span><i class="pi pi-id-card"></i> {{ firm?.professionalRegistrationNumber }}</span>
                  }
                </span>
              </div>
            </ng-template>
          </p-autoComplete>
          <small class="af-hint-inline">Sélectionnez un cabinet dans la liste des résultats.</small>
        </div>

        <div class="af-field">
          <label for="firmNotes">Message au cabinet <span class="af-optional">(facultatif)</span></label>
          <textarea
            id="firmNotes"
            [(ngModel)]="notes"
            [maxlength]="NOTES_MAX"
            rows="3"
            class="af-textarea"
            placeholder="Précisez votre demande (période, contexte, attentes...)"></textarea>
          <small class="af-hint-inline">{{ notes.length }} / {{ NOTES_MAX }}</small>
        </div>

        <div class="af-actions">
          <button
            type="button"
            class="af-btn af-btn--primary"
            (click)="confirmAndSubmit()"
            [disabled]="!selectedFirm || acting()">
            <i class="pi pi-send"></i> Envoyer la demande
          </button>
        </div>
      </div>

      @if (history().length > 0) {
        <div class="af-card">
          <button type="button" class="af-history__toggle" (click)="historyOpen.set(!historyOpen())">
            <i class="pi" [ngClass]="historyOpen() ? 'pi-chevron-down' : 'pi-chevron-right'"></i>
            Historique des demandes ({{ history().length }})
          </button>
          @if (historyOpen()) {
            <ul class="af-history__list">
              @for (h of history(); track h.id) {
                <li class="af-history__item">
                  <div class="af-history__main">
                    <span class="af-history__firm">{{ h.firmDisplayName }}</span>
                    <app-status-badge [status]="statusView(h.status).badge" [label]="statusView(h.status).label"></app-status-badge>
                  </div>
                  <div class="af-history__side">
                    <span class="af-history__date">{{ h.requestedAt | date: 'dd/MM/yyyy' }}</span>
                    @if (h.rejectionReason) {
                      <span class="af-history__reason">Motif : {{ h.rejectionReason }}</span>
                    }
                  </div>
                </li>
              }
            </ul>
          }
        </div>
      }
    }
  `,
  styles: [`
    :host { display: block; }

    .af-card {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border-subtle, var(--color-neutral-200));
      border-radius: var(--radius-xl, 16px);
      box-shadow: var(--shadow-soft-sm, 0 1px 2px rgba(15, 23, 42, 0.05));
      padding: var(--spacing-6, 24px);
      margin-bottom: var(--spacing-4, 16px);
      display: flex;
      flex-direction: column;
      gap: var(--spacing-4, 16px);
    }

    .af-card__title {
      margin: 0;
      font-size: var(--font-size-lg, 1.125rem);
      font-weight: var(--font-weight-semibold, 600);
      color: var(--color-text-primary, #0f172a);
    }

    .af-card__subtitle {
      margin: -8px 0 0;
      font-size: var(--font-size-sm, 0.875rem);
      color: var(--color-text-secondary, #64748b);
    }

    /* Liaison active / en attente */
    .af-liaison { display: flex; flex-direction: column; gap: var(--spacing-4, 16px); }

    .af-liaison__head {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: var(--spacing-4, 16px);
      flex-wrap: wrap;
    }

    .af-liaison__identity { display: flex; align-items: center; gap: var(--spacing-3, 12px); flex-wrap: wrap; }

    .af-liaison__firm {
      font-size: var(--font-size-lg, 1.125rem);
      font-weight: var(--font-weight-semibold, 600);
      color: var(--color-text-primary, #0f172a);
    }

    .af-meta { margin: 0; display: flex; flex-direction: column; gap: var(--spacing-2, 8px); }
    .af-meta__row { display: flex; gap: var(--spacing-3, 12px); }
    .af-meta__row dt {
      min-width: 130px;
      color: var(--color-text-secondary, #64748b);
      font-size: var(--font-size-sm, 0.875rem);
    }
    .af-meta__row dd { margin: 0; color: var(--color-text-primary, #0f172a); font-size: var(--font-size-sm, 0.875rem); }
    .af-meta__notes { white-space: pre-wrap; }

    .af-hint {
      display: flex;
      align-items: center;
      gap: var(--spacing-2, 8px);
      margin: 0;
      padding: var(--spacing-3, 12px);
      background: var(--color-primary-50, #eff6ff);
      border-radius: var(--radius-lg, 12px);
      color: var(--color-primary-800, #1e40af);
      font-size: var(--font-size-sm, 0.875rem);
    }

    /* Bandeau (refus / révocation) */
    .af-banner {
      display: flex;
      gap: var(--spacing-3, 12px);
      padding: var(--spacing-4, 16px);
      border-radius: var(--radius-xl, 16px);
      margin-bottom: var(--spacing-4, 16px);
      border: 1px solid transparent;
    }
    .af-banner > .pi { font-size: 1.25rem; margin-top: 2px; }
    .af-banner--rejected {
      background: var(--color-danger-50, #fef2f2);
      border-color: var(--color-danger-200, #fecaca);
      color: var(--color-danger-800, #991b1b);
    }
    .af-banner--inactive, .af-banner--cancelled {
      background: var(--color-neutral-50, #f8fafc);
      border-color: var(--color-neutral-200, #e2e8f0);
      color: var(--color-text-secondary, #475569);
    }
    .af-banner__body { display: flex; flex-direction: column; gap: 2px; }
    .af-banner__date { font-size: var(--font-size-xs, 0.75rem); opacity: 0.8; }
    .af-banner__reason { margin: 4px 0 0; font-size: var(--font-size-sm, 0.875rem); }
    .af-banner__cta { margin-top: 4px; font-size: var(--font-size-sm, 0.875rem); }

    /* Champs */
    .af-field { display: flex; flex-direction: column; gap: var(--spacing-2, 8px); }
    .af-field label {
      font-size: var(--font-size-sm, 0.875rem);
      font-weight: var(--font-weight-medium, 500);
      color: var(--color-text-primary, #0f172a);
    }
    .af-optional { color: var(--color-text-tertiary, #94a3b8); font-weight: 400; }

    .af-textarea {
      width: 100%;
      resize: vertical;
      padding: var(--spacing-3, 12px);
      border: 1px solid var(--color-border, var(--color-neutral-300));
      border-radius: var(--radius-lg, 12px);
      font-family: inherit;
      font-size: var(--font-size-sm, 0.875rem);
      color: var(--color-text-primary, #0f172a);
    }
    .af-textarea:focus {
      outline: none;
      border-color: var(--color-primary-400, #60a5fa);
      box-shadow: 0 0 0 3px var(--color-primary-100, rgba(37, 99, 235, 0.15));
    }

    .af-hint-inline { font-size: var(--font-size-xs, 0.75rem); color: var(--color-text-tertiary, #94a3b8); }

    .af-suggestion { display: flex; flex-direction: column; gap: 2px; padding: 2px 0; }
    .af-suggestion__name { font-weight: 600; color: var(--color-text-primary, #0f172a); }
    .af-suggestion__meta {
      display: flex;
      gap: 12px;
      flex-wrap: wrap;
      font-size: var(--font-size-xs, 0.75rem);
      color: var(--color-text-secondary, #64748b);
    }
    .af-suggestion__meta i { margin-right: 4px; }

    .af-actions { display: flex; justify-content: flex-end; }

    /* Boutons */
    .af-btn {
      display: inline-flex;
      align-items: center;
      gap: var(--spacing-2, 8px);
      padding: var(--spacing-3, 10px) var(--spacing-5, 20px);
      border-radius: var(--radius-lg, 12px);
      font-weight: var(--font-weight-medium, 500);
      font-size: var(--font-size-sm, 0.875rem);
      cursor: pointer;
      border: 1px solid transparent;
      transition: all 0.15s ease;
    }
    .af-btn:disabled { opacity: 0.6; cursor: not-allowed; }
    .af-btn--primary { background: var(--color-primary-600, #2563eb); color: #fff; }
    .af-btn--primary:not(:disabled):hover { background: var(--color-primary-700, #1d4ed8); }
    .af-btn--danger { background: var(--color-danger-600, #dc2626); color: #fff; }
    .af-btn--danger:not(:disabled):hover { background: var(--color-danger-700, #b91c1c); }
    .af-btn--danger-outline {
      background: transparent;
      color: var(--color-danger-600, #dc2626);
      border-color: var(--color-danger-300, #fca5a5);
    }
    .af-btn--danger-outline:not(:disabled):hover { background: var(--color-danger-50, #fef2f2); }

    /* Historique */
    .af-history__toggle {
      display: flex;
      align-items: center;
      gap: var(--spacing-2, 8px);
      background: none;
      border: none;
      padding: 0;
      cursor: pointer;
      font-size: var(--font-size-sm, 0.875rem);
      font-weight: var(--font-weight-medium, 500);
      color: var(--color-text-primary, #0f172a);
    }
    .af-history__list { list-style: none; margin: var(--spacing-2, 8px) 0 0; padding: 0; display: flex; flex-direction: column; }
    .af-history__item {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--spacing-3, 12px);
      padding: var(--spacing-3, 12px) 0;
      border-top: 1px solid var(--color-border-subtle, var(--color-neutral-100));
      flex-wrap: wrap;
    }
    .af-history__main { display: flex; align-items: center; gap: var(--spacing-3, 12px); }
    .af-history__firm { font-weight: 500; color: var(--color-text-primary, #0f172a); font-size: var(--font-size-sm, 0.875rem); }
    .af-history__side { display: flex; flex-direction: column; align-items: flex-end; gap: 2px; }
    .af-history__date { font-size: var(--font-size-xs, 0.75rem); color: var(--color-text-tertiary, #94a3b8); }
    .af-history__reason { font-size: var(--font-size-xs, 0.75rem); color: var(--color-text-secondary, #64748b); }
  `]
})
export class AccountingFirmSettingsComponent implements OnInit {
  private readonly assignments = inject(FirmAssignmentService);
  private readonly toast = inject(ToastService);
  private readonly confirmation = inject(ConfirmationService);

  readonly StatusCode = FirmAssignmentStatusCode;
  readonly NOTES_MAX = NOTES_MAX;

  readonly loading = signal(true);
  readonly acting = signal(false);
  readonly current = signal<FirmClientAssignment | null>(null);
  readonly history = signal<FirmClientAssignment[]>([]);
  readonly suggestions = signal<AccountingFirmDirectoryItem[]>([]);
  readonly historyOpen = signal(false);

  selectedFirm: AccountingFirmDirectoryItem | null = null;
  notes = '';

  /** Dernière demande close (refusée/révoquée/annulée) — pour le bandeau, quand aucune liaison n'est ouverte. */
  readonly lastClosed = computed(() => {
    if (this.current()) return null;
    return this.history().find(h => !isOpenAssignment(h.status)) ?? null;
  });

  ngOnInit(): void {
    this.reload();
  }

  statusView(status: number) {
    return firmAssignmentStatusView(status);
  }

  search(event: { query: string }): void {
    this.assignments.searchDirectory(event.query).subscribe({
      next: r => {
        if (r.success) this.suggestions.set(r.data);
      },
      error: () => this.suggestions.set([])
    });
  }

  confirmAndSubmit(): void {
    const firm = this.selectedFirm;
    if (!firm) return;
    this.confirmation.confirm({
      header: 'Envoyer la demande',
      message: `Confirmez-vous l'envoi d'une demande de liaison au cabinet « ${firm.displayName} » ?`,
      icon: 'pi pi-send',
      acceptLabel: 'Envoyer',
      rejectLabel: 'Annuler',
      acceptButtonStyleClass: 'btn-primary',
      accept: () => this.submit(firm)
    });
  }

  private submit(firm: AccountingFirmDirectoryItem): void {
    this.acting.set(true);
    const notes = this.notes.trim() || undefined;
    this.assignments.requestAssignment(firm.firmTenantId, notes).subscribe({
      next: r => {
        this.acting.set(false);
        if (r.success) {
          this.toast.add({ severity: 'success', summary: 'Demande envoyée', detail: `Votre demande a été transmise à ${firm.displayName}.` });
          this.selectedFirm = null;
          this.notes = '';
          this.current.set(r.data);
        } else {
          this.toast.add({ severity: 'error', summary: 'Envoi impossible', detail: r.message ?? 'Une erreur est survenue.' });
        }
      },
      error: err => {
        this.acting.set(false);
        this.toast.add({ severity: 'error', summary: 'Envoi impossible', detail: this.errorText(err) });
      }
    });
  }

  cancelPending(): void {
    this.confirmation.confirm({
      header: 'Annuler la demande',
      message: 'Voulez-vous vraiment annuler votre demande en attente ? Le cabinet en sera informé.',
      icon: 'pi pi-ban',
      acceptLabel: 'Annuler la demande',
      rejectLabel: 'Retour',
      acceptButtonStyleClass: 'btn-danger',
      accept: () => {
        this.acting.set(true);
        this.assignments.cancelPendingRequest().subscribe({
          next: r => {
            this.acting.set(false);
            if (r.success) {
              this.toast.add({ severity: 'success', summary: 'Demande annulée' });
              this.reload();
            } else {
              this.toast.add({ severity: 'error', summary: 'Annulation impossible', detail: r.message ?? 'Une erreur est survenue.' });
            }
          },
          error: err => {
            this.acting.set(false);
            this.toast.add({ severity: 'error', summary: 'Annulation impossible', detail: this.errorText(err) });
          }
        });
      }
    });
  }

  revoke(): void {
    this.confirmation.confirm({
      header: "Révoquer l'affectation",
      message: 'Voulez-vous vraiment révoquer la liaison avec ce cabinet ? Il perdra l\'accès à votre comptabilité.',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Révoquer',
      rejectLabel: 'Annuler',
      acceptButtonStyleClass: 'btn-danger',
      accept: () => {
        this.acting.set(true);
        this.assignments.revokeByCompany().subscribe({
          next: r => {
            this.acting.set(false);
            if (r.success) {
              this.toast.add({ severity: 'success', summary: 'Affectation révoquée' });
              this.reload();
            } else {
              this.toast.add({ severity: 'error', summary: 'Révocation impossible', detail: r.message ?? 'Une erreur est survenue.' });
            }
          },
          error: err => {
            this.acting.set(false);
            this.toast.add({ severity: 'error', summary: 'Révocation impossible', detail: this.errorText(err) });
          }
        });
      }
    });
  }

  private reload(): void {
    this.loading.set(true);
    this.assignments.getCompanyCurrent().subscribe({
      next: r => {
        this.current.set(r.success ? r.data : null);
        this.loadHistory();
      },
      error: () => {
        this.current.set(null);
        this.loadHistory();
      }
    });
  }

  private loadHistory(): void {
    this.assignments.getCompanyHistory().subscribe({
      next: r => {
        this.history.set(r.success ? r.data : []);
        this.loading.set(false);
      },
      error: () => {
        this.history.set([]);
        this.loading.set(false);
      }
    });
  }

  private errorText(err: unknown): string {
    const msg = (err as { error?: { message?: string } })?.error?.message;
    return msg ?? 'Une erreur est survenue. Veuillez réessayer.';
  }
}
