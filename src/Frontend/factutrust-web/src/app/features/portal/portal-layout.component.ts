import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '@core/services/auth.service';
import { PortalService } from './portal.service';
import { toSignal } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-portal-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="portal-shell">
      <header class="portal-header">
        <div class="brand">
          @if (me()?.sellerLogoUrl) {
            <img [src]="me()!.sellerLogoUrl!" alt="" class="logo" />
          }
          <div>
            <div class="seller">{{ me()?.sellerName || 'Espace client' }}</div>
            <div class="client">{{ me()?.clientName }}</div>
          </div>
        </div>
        <nav class="portal-nav">
          <a routerLink="/portal" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Accueil</a>
          <a routerLink="/portal/invoices" routerLinkActive="active">Factures</a>
          <a routerLink="/portal/payments" routerLinkActive="active">Paiements</a>
          <a routerLink="/portal/statement" routerLinkActive="active">Relevé</a>
          <a routerLink="/portal/profile" routerLinkActive="active">Profil</a>
        </nav>
        <button type="button" class="logout" (click)="auth.logout()">Déconnexion</button>
      </header>
      <main class="portal-main">
        <router-outlet />
      </main>
    </div>
  `,
  styles: [`
    .portal-shell { min-height: 100vh; background: #f7f3eb; color: #1f2937; font-family: 'Segoe UI', sans-serif; }
    .portal-header { display: flex; align-items: center; gap: 1.5rem; padding: 1rem 1.5rem; background: #fffdf8; border-bottom: 1px solid #e8dcc8; }
    .brand { display: flex; align-items: center; gap: .75rem; min-width: 220px; }
    .logo { height: 40px; width: auto; }
    .seller { font-weight: 700; }
    .client { font-size: .85rem; color: #6b7280; }
    .portal-nav { display: flex; gap: 1rem; flex: 1; }
    .portal-nav a { color: #374151; text-decoration: none; padding: .35rem .15rem; }
    .portal-nav a.active { color: #b45309; border-bottom: 2px solid #b45309; }
    .logout { margin-left: auto; border: 1px solid #d6c4a8; background: white; padding: .4rem .8rem; border-radius: 6px; cursor: pointer; }
    .portal-main { padding: 1.5rem; max-width: 1100px; margin: 0 auto; }
  `]
})
export class PortalLayoutComponent {
  readonly auth = inject(AuthService);
  private readonly portal = inject(PortalService);
  readonly me = toSignal(this.portal.getMe());
}
