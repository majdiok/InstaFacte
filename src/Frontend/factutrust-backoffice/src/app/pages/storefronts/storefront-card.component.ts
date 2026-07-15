import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
  computed,
  signal
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtAvatarComponent } from '@core/ui/avatar/ft-avatar.component';
import type { PlatformStorefrontProfileDto } from '@core/services/platform-storefront.service';
import {
  parseStorefrontWorkflowStatus,
  type StorefrontWorkflowStatus
} from '@core/storefront-workflow-status';
import {
  STOREFRONTS_FR,
  STOREFRONT_CATEGORY_LABELS,
  STOREFRONT_THEME_LABELS
} from './storefronts.i18n.fr';

/**
 * Card visuelle d'une vitrine pour le backoffice plateforme (Lot A4).
 *
 * Affiche un cover-band (gradient des couleurs de marque), un avatar (logo
 * ou initiales), le nom public, le slug en mono, l'email de contact, et des
 * badges (catégorie, thème). Les actions affichées dépendent du `status` :
 *  - `pendingReview` → Approuver (success) + Refuser (danger outlined)
 *  - `published` / `suspended` / `draft` (refusée) → Prévisualiser uniquement
 *
 * Le bouton Prévisualiser est désactivé tant que la fonctionnalité de
 * preview signée n'est pas en place (Lot D5).
 */
@Component({
  selector: 'app-storefront-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, ButtonModule, TooltipModule, FtBadgeComponent, FtAvatarComponent],
  template: `
    <article class="card" [class.card--published]="isPublished()" [class.card--rejected]="isRejected()">
      <!-- Bandeau couverture avec dégradé des couleurs de marque -->
      <div class="card__cover" [style.background]="coverGradient()">
        @if (data.publicLogoUrl) {
          <img class="card__logo" [src]="data.publicLogoUrl" [alt]="data.displayName" loading="lazy" />
        } @else {
          <ft-avatar [name]="data.displayName" [seed]="data.id" size="lg" />
        }
        <div class="card__statusPill">
          <ft-badge [tone]="statusTone()" size="sm" [withDot]="true">{{ statusLabel() }}</ft-badge>
        </div>
      </div>

      <!-- Corps -->
      <div class="card__body">
        <h3 class="card__name">{{ data.displayName }}</h3>
        <code class="card__slug">/{{ data.slug }}</code>

        <p class="card__contact">
          <i class="pi pi-envelope" aria-hidden="true"></i>
          <span>{{ data.publicContactEmail }}</span>
        </p>
        @if (data.publicContactPhone) {
          <p class="card__contact card__contact--secondary">
            <i class="pi pi-phone" aria-hidden="true"></i>
            <span>{{ data.publicContactPhone }}</span>
          </p>
        }

        <div class="card__meta">
          <ft-badge tone="info" size="sm">{{ categoryLabel() }}</ft-badge>
          <ft-badge tone="neutral" size="sm">{{ themeLabel() }}</ft-badge>
        </div>

        @if (isRejected() && data.rejectionReason) {
          <div class="card__rejection">
            <strong>{{ t('card.rejectionReason') }}</strong>
            <span>{{ data.rejectionReason }}</span>
          </div>
        }

        @if (isPublished() && data.publishedAt) {
          <p class="card__date">
            {{ t('card.publishedAt') }} {{ data.publishedAt | date: 'dd/MM/yyyy' }}
          </p>
        }
        @if (data.suspendedAt && parsedStatus() === 'suspended') {
          <p class="card__date">
            {{ t('card.suspendedAt') }} {{ data.suspendedAt | date: 'dd/MM/yyyy' }}
          </p>
        }
      </div>

      <!-- Actions -->
      <footer class="card__actions">
        <p-button
          [label]="t('card.preview')"
          icon="pi pi-eye"
          [text]="true"
          size="small"
          [disabled]="true"
          pTooltip="Prévisualisation signée — Lot D5"
          tooltipPosition="top"
        />
        @if (parsedStatus() === 'pendingReview') {
          <p-button
            [label]="t('card.reject')"
            icon="pi pi-times"
            severity="danger"
            [outlined]="true"
            size="small"
            [disabled]="busy"
            (onClick)="rejectClick.emit(data)"
          />
          <p-button
            [label]="t('card.approve')"
            icon="pi pi-check"
            severity="success"
            size="small"
            [disabled]="busy"
            [loading]="busyAction === 'approve'"
            (onClick)="approveClick.emit(data)"
          />
        }
      </footer>
    </article>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .card {
        display: flex;
        flex-direction: column;
        background: var(--ft-surface);
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius-lg);
        overflow: hidden;
        transition: transform var(--duration-normal) var(--easing-standard),
          box-shadow var(--duration-normal) var(--easing-standard),
          border-color var(--duration-normal) var(--easing-standard);
      }

      .card:hover {
        transform: translateY(-2px);
        box-shadow: var(--ft-elev-2);
        border-color: var(--ft-border-strong);
      }

      .card--published {
        border-color: var(--ft-success-border);
      }

      .card--rejected {
        border-color: var(--ft-danger-border);
      }

      /* ----- Cover band (gradient marque) ----- */
      .card__cover {
        position: relative;
        height: 6.5rem;
        display: flex;
        align-items: center;
        justify-content: center;
        padding: var(--gap-md);
      }

      .card__logo {
        max-width: 4.5rem;
        max-height: 4.5rem;
        border-radius: var(--ft-radius);
        background: var(--ft-surface);
        padding: 0.4rem;
        box-shadow: var(--ft-elev-2);
        object-fit: contain;
      }

      .card__statusPill {
        position: absolute;
        top: var(--gap-xs);
        right: var(--gap-xs);
      }

      /* ----- Body ----- */
      .card__body {
        padding: var(--gap-md);
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
        flex: 1;
      }

      .card__name {
        margin: 0;
        font-size: 1rem;
        font-weight: 600;
        color: var(--ft-text);
        line-height: 1.3;
      }

      .card__slug {
        font-family: ui-monospace, SFMono-Regular, monospace;
        font-size: 0.78rem;
        color: var(--ft-accent);
        background: var(--ft-surface-2);
        padding: 0.1rem 0.4rem;
        border-radius: var(--ft-radius-sm);
        align-self: flex-start;
      }

      .card__contact {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        margin: 0;
        color: var(--ft-text-muted);
        font-size: 0.82rem;
        word-break: break-word;
      }

      .card__contact .pi {
        font-size: 0.85rem;
        color: var(--ft-text-subtle);
      }

      .card__contact--secondary {
        font-size: 0.78rem;
      }

      .card__meta {
        display: flex;
        flex-wrap: wrap;
        gap: 0.4rem;
        margin-top: 0.25rem;
      }

      .card__rejection {
        margin-top: var(--gap-xs);
        padding: var(--gap-xs) var(--gap-sm);
        background: var(--ft-danger-surface);
        border: 1px solid var(--ft-danger-border);
        border-radius: var(--ft-radius);
        color: var(--ft-text);
        font-size: 0.82rem;
        line-height: 1.4;
        display: flex;
        flex-direction: column;
        gap: 0.2rem;
      }

      .card__rejection strong {
        color: var(--ft-danger-text);
        font-size: 0.7rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
      }

      .card__date {
        margin: 0;
        color: var(--ft-text-subtle);
        font-size: 0.78rem;
      }

      /* ----- Actions ----- */
      .card__actions {
        display: flex;
        gap: 0.4rem;
        justify-content: flex-end;
        padding: var(--gap-sm) var(--gap-md);
        border-top: 1px solid var(--ft-border);
        background: var(--ft-surface-2);
      }
    `
  ]
})
export class StorefrontCardComponent {
  @Input({ required: true }) data!: PlatformStorefrontProfileDto;
  @Input() busy = false;
  @Input() busyAction: 'approve' | 'reject' | null = null;

  @Output() approveClick = new EventEmitter<PlatformStorefrontProfileDto>();
  @Output() rejectClick = new EventEmitter<PlatformStorefrontProfileDto>();

  protected t(key: keyof typeof STOREFRONTS_FR): string {
    return STOREFRONTS_FR[key];
  }

  protected parsedStatus(): StorefrontWorkflowStatus | null {
    const parsed = parseStorefrontWorkflowStatus(this.data.status);
    return parsed.ok ? parsed.value : null;
  }

  protected isPublished(): boolean {
    return this.parsedStatus() === 'published';
  }

  protected isRejected(): boolean {
    // "Rejected" est un état métier dérivé : Draft + RejectionReason non null
    return this.parsedStatus() === 'draft' && !!this.data.rejectionReason;
  }

  protected statusTone(): 'success' | 'warning' | 'danger' | 'neutral' | 'info' | 'accent' {
    switch (this.parsedStatus()) {
      case 'published':
        return 'success';
      case 'pendingReview':
        return 'warning';
      case 'suspended':
        return 'danger';
      case 'draft':
        return this.isRejected() ? 'danger' : 'neutral';
      default:
        return 'neutral';
    }
  }

  protected statusLabel(): string {
    const status = this.parsedStatus();
    if (this.isRejected()) return 'Refusée';
    switch (status) {
      case 'pendingReview':
        return 'En attente';
      case 'published':
        return 'Publiée';
      case 'suspended':
        return 'Suspendue';
      case 'draft':
        return 'Brouillon';
      default:
        return 'Inconnu';
    }
  }

  protected categoryLabel(): string {
    const raw = String(this.data.category ?? '').trim();
    return STOREFRONT_CATEGORY_LABELS[raw] ?? raw ?? '—';
  }

  protected themeLabel(): string {
    const raw = String(this.data.facadeTheme ?? '').trim();
    return STOREFRONT_THEME_LABELS[raw] ?? raw ?? '—';
  }

  protected coverGradient(): string {
    const a = this.data.brandPrimaryColorHex || '#2563eb';
    const b = this.data.brandSecondaryColorHex || '#0ea5e9';
    return `linear-gradient(135deg, ${a} 0%, ${b} 100%)`;
  }
}
