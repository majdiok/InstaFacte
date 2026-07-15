import { ChangeDetectionStrategy, Component, OnInit, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { MenuModule } from 'primeng/menu';
import { TooltipModule } from 'primeng/tooltip';
import { MenuItem } from 'primeng/api';
import { PlatformAuthService } from '@core/services/platform-auth.service';
import { PlatformPermissionsService } from '@core/services/platform-permissions.service';
import { PlatformPermission } from '@core/models/platform.models';
import { FtAvatarComponent } from '@core/ui/avatar/ft-avatar.component';

/**
 * Shell principal du backoffice.
 *
 * Lot A1 :
 *  - Conserve la nav 3 onglets (Entreprises / Migrations / Vitrines 3D).
 *  - Remplace le bloc utilisateur statique par un menu overlay (Profil / Préférences /
 *    Audit / Documentation / Raccourcis / Déconnexion).
 *  - Ajoute des placeholders désactivés pour la recherche globale ⌘K et la cloche
 *    notifications (activation en Lots A4/A5).
 *
 * La structure (header + nav + main) reste identique pour zéro régression visuelle
 * sur les pages enfants.
 */
@Component({
  selector: 'app-platform-shell',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    ButtonModule,
    ToastModule,
    MenuModule,
    TooltipModule,
    FtAvatarComponent
  ],
  template: `
    <p-toast position="top-right" />
    <header class="shell-header">
      <div class="brand-block">
        <img src="assets/branding/instafact-lockup.png" alt="InstaFact" class="brand-logo-img" />
        <span class="brand-badge">Plateforme</span>
      </div>
      <nav class="shell-nav" aria-label="Navigation principale">
        <a routerLink="/tenants" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: false }">
          <i class="pi pi-building" aria-hidden="true"></i>
          Entreprises
        </a>
        <a routerLink="/migrations" routerLinkActive="active">
          <i class="pi pi-database" aria-hidden="true"></i>
          Migrations
        </a>
        <a routerLink="/storefronts" routerLinkActive="active">
          <i class="pi pi-shop" aria-hidden="true"></i>
          Vitrines 3D
        </a>
        @if (canSeeAdmins()) {
          <a routerLink="/admins" routerLinkActive="active">
            <i class="pi pi-users" aria-hidden="true"></i>
            Admins
          </a>
        }
        @if (canSeePlans()) {
          <a routerLink="/plans" routerLinkActive="active">
            <i class="pi pi-tags" aria-hidden="true"></i>
            Plans
          </a>
        }
        @if (canSeeCoupons()) {
          <a routerLink="/coupons" routerLinkActive="active">
            <i class="pi pi-tag" aria-hidden="true"></i>
            Coupons
          </a>
        }
        @if (canSeeCredits()) {
          <a routerLink="/credits" routerLinkActive="active">
            <i class="pi pi-wallet" aria-hidden="true"></i>
            Crédits
          </a>
        }
        @if (canSeeInvoices()) {
          <a routerLink="/invoices" routerLinkActive="active">
            <i class="pi pi-file" aria-hidden="true"></i>
            Factures
          </a>
        }
        @if (canSeePaymentProviders()) {
          <a routerLink="/payments/providers" routerLinkActive="active">
            <i class="pi pi-credit-card" aria-hidden="true"></i>
            Paiements
          </a>
        }
        @if (canSeeDunning()) {
          <a routerLink="/dunning" routerLinkActive="active">
            <i class="pi pi-megaphone" aria-hidden="true"></i>
            Dunning
          </a>
        }
        @if (canSeeAudit()) {
          <a routerLink="/audit" routerLinkActive="active">
            <i class="pi pi-history" aria-hidden="true"></i>
            Audit
          </a>
        }
        @if (canSeeAudit()) {
          <a routerLink="/emails" routerLinkActive="active">
            <i class="pi pi-envelope" aria-hidden="true"></i>
            Emails
          </a>
        }
        @if (canSeeSecurity()) {
          <a routerLink="/security" routerLinkActive="active">
            <i class="pi pi-lock" aria-hidden="true"></i>
            Sécurité
          </a>
        }
        @if (canSeeAiConfig()) {
          <a routerLink="/ai-settings" routerLinkActive="active">
            <i class="pi pi-microchip-ai" aria-hidden="true"></i>
            Configuration IA
          </a>
        }
      </nav>
      <div class="user-block">
        <!-- Placeholder ⌘K (Lot A5) -->
        <button
          type="button"
          class="icon-btn"
          aria-label="Recherche globale (⌘K) — bientôt"
          pTooltip="Recherche globale (⌘K) — bientôt"
          tooltipPosition="bottom"
          disabled
        >
          <i class="pi pi-search" aria-hidden="true"></i>
        </button>

        <!-- Placeholder notifications (Lot A5) -->
        <button
          type="button"
          class="icon-btn"
          aria-label="Notifications — bientôt"
          pTooltip="Notifications — bientôt"
          tooltipPosition="bottom"
          disabled
        >
          <i class="pi pi-bell" aria-hidden="true"></i>
        </button>

        <!-- Menu utilisateur -->
        @if (auth.user(); as u) {
          <p-menu #userMenu [model]="userMenuItems()" [popup]="true" appendTo="body" />
          <button
            type="button"
            class="user-btn"
            (click)="userMenu.toggle($event)"
            [attr.aria-label]="'Menu de ' + u.firstName + ' ' + u.lastName"
          >
            <ft-avatar [name]="u.firstName + ' ' + u.lastName" [seed]="u.id" size="sm" />
            <span class="user-meta">
              <span class="user-name">{{ u.firstName }} {{ u.lastName }}</span>
              <span class="user-role">Plateforme Admin</span>
            </span>
            <i class="pi pi-chevron-down user-chevron" aria-hidden="true"></i>
          </button>
        }
      </div>
    </header>
    <main class="shell-main">
      <div class="shell-inner">
        <router-outlet />
      </div>
    </main>
  `,
  styles: [
    `
      .shell-header {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: 1rem 1.5rem;
        padding: 0.85rem 1.5rem;
        border-bottom: 1px solid var(--ft-border, #30363d);
        background: var(--ft-surface, #161b22);
        position: sticky;
        top: 0;
        z-index: var(--z-sticky, 100);
      }
      .brand-block {
        display: flex;
        align-items: center;
        gap: 0.5rem;
      }
      .brand-logo-img {
        max-height: 32px;
        max-width: 140px;
        width: auto;
        object-fit: contain;
      }
      .brand-badge {
        font-size: 0.7rem;
        font-weight: 600;
        text-transform: uppercase;
        letter-spacing: 0.08em;
        padding: 0.2rem 0.45rem;
        border-radius: 6px;
        background: var(--ft-accent-muted, rgba(88, 166, 255, 0.15));
        color: var(--ft-accent, #58a6ff);
      }
      .shell-nav {
        display: flex;
        flex-wrap: wrap;
        gap: 0.35rem;
        flex: 1;
        justify-content: center;
      }
      .shell-nav a {
        display: inline-flex;
        align-items: center;
        gap: 0.4rem;
        color: var(--ft-text-muted, #8b949e);
        text-decoration: none;
        padding: 0.45rem 0.75rem;
        border-radius: 8px;
        font-size: 0.9rem;
        font-weight: 500;
        transition: background 0.15s ease, color 0.15s ease;
      }
      .shell-nav a:hover {
        color: var(--ft-text, #e6edf3);
        background: rgba(240, 246, 252, 0.06);
      }
      .shell-nav a.active {
        background: var(--ft-accent-muted, rgba(88, 166, 255, 0.18));
        color: var(--ft-accent, #58a6ff);
      }
      .shell-nav a:focus-visible {
        outline: 2px solid var(--ft-accent, #58a6ff);
        outline-offset: 2px;
      }

      .user-block {
        display: flex;
        align-items: center;
        gap: 0.5rem;
      }

      .icon-btn {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 2.1rem;
        height: 2.1rem;
        border: 1px solid transparent;
        background: transparent;
        color: var(--ft-text-muted);
        border-radius: var(--ft-radius);
        cursor: pointer;
        transition: background var(--duration-fast) var(--easing-standard),
          color var(--duration-fast) var(--easing-standard),
          border-color var(--duration-fast) var(--easing-standard);
      }

      .icon-btn:hover:not(:disabled) {
        background: var(--ft-surface-3);
        color: var(--ft-text);
      }

      .icon-btn:disabled {
        opacity: 0.45;
        cursor: not-allowed;
      }

      .user-btn {
        display: inline-flex;
        align-items: center;
        gap: 0.55rem;
        padding: 0.3rem 0.6rem 0.3rem 0.4rem;
        background: transparent;
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius);
        cursor: pointer;
        color: var(--ft-text);
        transition: background var(--duration-fast) var(--easing-standard),
          border-color var(--duration-fast) var(--easing-standard);
      }

      .user-btn:hover {
        background: var(--ft-surface-3);
        border-color: var(--ft-border-strong);
      }

      .user-btn:focus-visible {
        outline: 2px solid var(--ft-accent);
        outline-offset: 2px;
      }

      .user-meta {
        display: flex;
        flex-direction: column;
        gap: 0.05rem;
        text-align: left;
        line-height: 1.15;
      }

      .user-name {
        font-size: 0.85rem;
        font-weight: 500;
        color: var(--ft-text);
      }

      .user-role {
        font-size: 0.65rem;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        color: var(--ft-text-muted);
      }

      .user-chevron {
        font-size: 0.7rem;
        color: var(--ft-text-subtle);
      }

      .shell-main {
        min-height: calc(100vh - 4rem);
        padding: 1.5rem 1.25rem 2.5rem;
      }
      .shell-inner {
        max-width: 1400px;
        margin: 0 auto;
      }

      /* Réduit la densité du user-block sur mobile */
      @media (max-width: 640px) {
        .user-meta {
          display: none;
        }
        .user-btn {
          padding: 0.3rem;
        }
      }
    `
  ]
})
export class PlatformShellComponent implements OnInit {
  readonly auth = inject(PlatformAuthService);
  private readonly permissions = inject(PlatformPermissionsService);

  /** Affiche le lien /admins uniquement si l'utilisateur a la permission de lire la liste. */
  readonly canSeeAdmins = computed(() => this.permissions.has(PlatformPermission.AdminsRead));
  /** Affiche le lien /security uniquement si l'utilisateur a la permission security:read. */
  readonly canSeeSecurity = computed(() => this.permissions.has(PlatformPermission.SecurityRead));
  /** Affiche le lien /plans uniquement si l'utilisateur a la permission plans:manage. */
  readonly canSeePlans = computed(() => this.permissions.has(PlatformPermission.PlansManage));
  /** Affiche le lien /coupons uniquement si l'utilisateur a la permission coupons:manage. */
  readonly canSeeCoupons = computed(() => this.permissions.has(PlatformPermission.CouponsManage));
  /** Affiche le lien /credits uniquement si l'utilisateur a la permission credits:manage. */
  readonly canSeeCredits = computed(() => this.permissions.has(PlatformPermission.CreditsManage));
  /** Affiche le lien /invoices uniquement si l'utilisateur a la permission invoice:read. */
  readonly canSeeInvoices = computed(() => this.permissions.has(PlatformPermission.InvoiceRead));
  /** Affiche le lien /payments uniquement si l'utilisateur a la permission providers:configure. */
  readonly canSeePaymentProviders = computed(() => this.permissions.has(PlatformPermission.ProvidersConfigure));
  /** Affiche le lien /dunning uniquement si l'utilisateur a la permission invoice:issue (BillingAdmin). */
  readonly canSeeDunning = computed(() => this.permissions.has(PlatformPermission.InvoiceIssue));
  /** Affiche le lien /audit uniquement si l'utilisateur a la permission audit:read. */
  readonly canSeeAudit = computed(() => this.permissions.has(PlatformPermission.AuditRead));
  /** Affiche le lien /ai-settings uniquement si l'utilisateur a la permission ai:manage. */
  readonly canSeeAiConfig = computed(() => this.permissions.has(PlatformPermission.AiManage));

  ngOnInit(): void {
    // Lot B1 : charge les permissions au démarrage si pas encore fait. Idempotent.
    if (!this.permissions.isLoaded() && this.auth.isAuthenticated()) {
      this.permissions.load().subscribe({
        error: () => {
          // Silencieux : si l'API échoue, on tombe sur la nav minimale
        }
      });
    }
  }

  /**
   * Items du menu utilisateur. Construits comme `computed` pour réagir aux
   * changements de l'utilisateur (signal `auth.user`).
   *
   * Les routes `/me`, `/me/audit`, `/preferences` n'existent pas encore (Lots ultérieurs)
   * et sont marquées `disabled` ; les libellés FR sont conformes au glossaire.
   */
  readonly userMenuItems = computed<MenuItem[]>(() => {
    const u = this.auth.user();
    return [
      {
        label: u ? `${u.firstName} ${u.lastName}` : '—',
        styleClass: 'mu-header',
        items: [
          { label: u?.email ?? '', styleClass: 'mu-email', disabled: true }
        ]
      },
      { separator: true },
      {
        label: 'Mon profil',
        icon: 'pi pi-user',
        disabled: true
      },
      {
        label: 'Préférences',
        icon: 'pi pi-cog',
        disabled: true
      },
      {
        label: 'Authentification 2FA',
        icon: 'pi pi-shield',
        routerLink: '/me/2fa'
      },
      {
        label: 'Santé du système',
        icon: 'pi pi-heart',
        routerLink: '/ops/health',
        visible: this.canSeeSecurity()
      },
      {
        label: 'Mon audit',
        icon: 'pi pi-history',
        disabled: true
      },
      {
        label: 'Documentation',
        icon: 'pi pi-book',
        disabled: true
      },
      {
        label: 'Raccourcis clavier',
        icon: 'pi pi-key',
        disabled: true
      },
      { separator: true },
      {
        label: 'Déconnexion',
        icon: 'pi pi-sign-out',
        styleClass: 'mu-danger',
        command: () => this.onLogout()
      }
    ];
  });

  onLogout(): void {
    this.permissions.clear();
    this.auth.logoutAndNavigate();
  }
}
