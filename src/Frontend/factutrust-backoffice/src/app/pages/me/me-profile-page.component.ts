import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { MessageService } from 'primeng/api';
import { PlatformMeService } from '@core/services/platform-me.service';
import type { PlatformMeProfileDto } from '@core/models/platform.models';
import { FtPageHeaderComponent } from '@core/ui/page-header/ft-page-header.component';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';

@Component({
  selector: 'app-me-profile-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
    ButtonModule,
    InputTextModule,
    PasswordModule,
    FtPageHeaderComponent,
    FtBadgeComponent,
    FtSkeletonComponent,
    FtEmptyStateComponent
  ],
  template: `
    <ft-page-header title="Mon profil" subtitle="Informations de votre compte administrateur plateforme.">
      <ng-container ftActions>
        <p-button label="Retour" icon="pi pi-arrow-left" [outlined]="true" routerLink="/tenants" />
      </ng-container>
    </ft-page-header>

    @if (loading()) {
      <div class="card-stack">
        <ft-skeleton shape="rect" width="100%" height="4rem" />
        <ft-skeleton shape="line" />
        <ft-skeleton shape="line" />
      </div>
    } @else if (!profile()) {
      <ft-empty-state variant="error" title="Erreur" description="Impossible de charger votre profil." />
    } @else {
      <article class="profile-card">
        <header class="profile-card__head">
          <div>
            <h2>{{ profile()!.firstName }} {{ profile()!.lastName }}</h2>
            <p class="muted">{{ profile()!.email }}</p>
          </div>
          <ft-badge [tone]="profile()!.isMfaEnabled ? 'success' : 'neutral'" size="sm">
            {{ profile()!.isMfaEnabled ? '2FA activé' : '2FA désactivé' }}
          </ft-badge>
        </header>

        <dl class="meta-grid">
          <div>
            <dt>Rôles</dt>
            <dd>{{ profile()!.roles.join(', ') || '—' }}</dd>
          </div>
          <div>
            <dt>Dernière connexion</dt>
            <dd>
              @if (profile()!.lastLoginAt) {
                {{ profile()!.lastLoginAt | date: 'dd/MM/yyyy HH:mm' }}
              } @else {
                —
              }
            </dd>
          </div>
        </dl>

        <form class="form-grid" (ngSubmit)="saveProfile()">
          <h3>Identité</h3>
          <label>
            Prénom
            <input pInputText [(ngModel)]="firstName" name="firstName" required />
          </label>
          <label>
            Nom
            <input pInputText [(ngModel)]="lastName" name="lastName" required />
          </label>

          <h3>Changer le mot de passe (optionnel)</h3>
          <label>
            Mot de passe actuel
            <p-password [(ngModel)]="currentPassword" name="currentPassword" [feedback]="false" [toggleMask]="true" />
          </label>
          <label>
            Nouveau mot de passe
            <p-password [(ngModel)]="newPassword" name="newPassword" [toggleMask]="true" />
          </label>
          <label>
            Confirmer le mot de passe
            <p-password [(ngModel)]="confirmNewPassword" name="confirmNewPassword" [feedback]="false" [toggleMask]="true" />
          </label>

          <div class="form-actions">
            <p-button type="submit" label="Enregistrer" icon="pi pi-save" [loading]="saving()" />
            <p-button
              label="Gérer le 2FA"
              icon="pi pi-shield"
              [outlined]="true"
              routerLink="/me/2fa"
            />
          </div>
        </form>
      </article>
    }
  `,
  styles: [
    `
      .card-stack {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
      }
      .profile-card {
        background: var(--ft-surface);
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius);
        padding: var(--gap-lg);
        display: flex;
        flex-direction: column;
        gap: var(--gap-lg);
      }
      .profile-card__head {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        gap: 1rem;
      }
      .profile-card__head h2 {
        margin: 0;
      }
      .muted {
        color: var(--ft-text-muted);
        margin: 0.25rem 0 0;
      }
      .meta-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr));
        gap: 1rem;
        margin: 0;
      }
      .meta-grid dt {
        font-size: 0.75rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--ft-text-muted);
      }
      .meta-grid dd {
        margin: 0.2rem 0 0;
      }
      .form-grid {
        display: grid;
        gap: 0.85rem;
      }
      .form-grid h3 {
        margin: 0.5rem 0 0;
        font-size: 0.95rem;
      }
      .form-grid label {
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
        font-size: 0.85rem;
        color: var(--ft-text-muted);
      }
      .form-actions {
        display: flex;
        flex-wrap: wrap;
        gap: 0.5rem;
        margin-top: 0.5rem;
      }
    `
  ]
})
export class MeProfilePageComponent implements OnInit {
  private readonly meService = inject(PlatformMeService);
  private readonly messages = inject(MessageService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly profile = signal<PlatformMeProfileDto | null>(null);

  firstName = '';
  lastName = '';
  currentPassword = '';
  newPassword = '';
  confirmNewPassword = '';

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.meService.getProfile().subscribe({
      next: res => {
        this.loading.set(false);
        if (res.success && res.data) {
          this.profile.set(res.data);
          this.firstName = res.data.firstName;
          this.lastName = res.data.lastName;
        }
      },
      error: () => this.loading.set(false)
    });
  }

  saveProfile(): void {
    this.saving.set(true);
    this.meService
      .updateProfile({
        firstName: this.firstName.trim(),
        lastName: this.lastName.trim(),
        currentPassword: this.currentPassword || null,
        newPassword: this.newPassword || null,
        confirmNewPassword: this.confirmNewPassword || null
      })
      .subscribe({
        next: res => {
          this.saving.set(false);
          if (res.success && res.data) {
            this.profile.set(res.data);
            this.currentPassword = '';
            this.newPassword = '';
            this.confirmNewPassword = '';
            this.messages.add({ severity: 'success', summary: 'Profil', detail: res.message ?? 'Profil mis à jour.' });
          } else {
            this.messages.add({ severity: 'error', summary: 'Profil', detail: res.message ?? 'Échec de la mise à jour.' });
          }
        },
        error: err => {
          this.saving.set(false);
          const detail = err?.error?.message ?? 'Erreur lors de la mise à jour.';
          this.messages.add({ severity: 'error', summary: 'Profil', detail });
        }
      });
  }
}
