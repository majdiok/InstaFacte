import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { CardModule } from 'primeng/card';
import { MessageModule } from 'primeng/message';
import { PlatformAuthService } from '@core/services/platform-auth.service';
import { isTwoFactorChallenge } from '@core/models/platform.models';

/**
 * Lot B2 — Login en deux étapes :
 *  1. Email + mot de passe → si 2FA actif côté serveur, renvoie un ticket court (5 min).
 *  2. Saisie du code TOTP (ou recovery code) → finalise l'authentification.
 */
@Component({
  selector: 'app-platform-login',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    PasswordModule,
    CardModule,
    MessageModule
  ],
  template: `
    <div class="login-wrap">
      <p-card styleClass="login-card" header="InstaFact — Plateforme" [subheader]="subheader()">
        @if (step() === 'credentials') {
          <form [formGroup]="form" (ngSubmit)="submit()" aria-describedby="login-error">
            <div class="field">
              <label for="email">Email</label>
              <input
                id="email"
                type="email"
                pInputText
                formControlName="email"
                autocomplete="username"
                class="w-full"
                aria-required="true" />
            </div>
            <div class="field">
              <label for="password">Mot de passe</label>
              <p-password
                inputId="password"
                formControlName="password"
                [feedback]="false"
                [toggleMask]="true"
                styleClass="w-full"
                inputStyleClass="w-full"
                autocomplete="current-password"
                aria-required="true" />
            </div>
            @if (errorMessage()) {
              <p-message
                id="login-error"
                severity="error"
                [text]="errorMessage()!"
                styleClass="w-full mb-3"
                role="alert" />
            }
            <p-button
              type="submit"
              label="Se connecter"
              icon="pi pi-sign-in"
              [loading]="loading()"
              [disabled]="form.invalid" />
          </form>
        } @else {
          <form (ngSubmit)="submitTwoFactor()" aria-describedby="2fa-error">
            <p class="hint">
              <i class="pi pi-shield" aria-hidden="true"></i>
              Saisissez le code à 6 chiffres affiché sur votre application d'authentification,
              ou un code de récupération si vous n'avez pas accès à votre appareil.
            </p>
            <div class="field">
              <label for="totp">Code à 6 chiffres ou code de récupération</label>
              <input
                id="totp"
                type="text"
                pInputText
                [(ngModel)]="totpCode"
                name="totp"
                inputmode="numeric"
                autocomplete="one-time-code"
                class="w-full"
                placeholder="123456 ou XXXX-XXXXXX"
                maxlength="20" />
            </div>
            @if (errorMessage()) {
              <p-message
                id="2fa-error"
                severity="error"
                [text]="errorMessage()!"
                styleClass="w-full mb-3"
                role="alert" />
            }
            <div class="actions-row">
              <p-button
                type="button"
                label="Retour"
                icon="pi pi-arrow-left"
                [text]="true"
                severity="secondary"
                (onClick)="resetToCredentials()" />
              <p-button
                type="submit"
                label="Vérifier"
                icon="pi pi-check"
                [loading]="loading()"
                [disabled]="totpCode.length < 6" />
            </div>
          </form>
        }
      </p-card>
    </div>
  `,
  styles: [
    `
      .login-wrap {
        min-height: 100vh;
        display: flex;
        align-items: center;
        justify-content: center;
        padding: 1.5rem;
        background:
          radial-gradient(ellipse 120% 80% at 50% -20%, rgba(88, 166, 255, 0.22) 0%, transparent 55%),
          radial-gradient(ellipse at bottom, #0d1117 0%, #010409 100%);
      }
      .field {
        margin-bottom: 1rem;
      }
      .field label {
        display: block;
        margin-bottom: 0.35rem;
        font-weight: 600;
        font-size: 0.85rem;
        color: var(--ft-text-muted, #8b949e);
      }
      .hint {
        margin: 0 0 1rem;
        padding: 0.6rem 0.8rem;
        background: var(--ft-info-surface);
        border: 1px solid var(--ft-info-border);
        border-radius: var(--ft-radius);
        font-size: 0.85rem;
        line-height: 1.45;
        color: var(--ft-text);
        display: flex;
        gap: 0.5rem;
        align-items: flex-start;
      }
      .hint .pi {
        color: var(--ft-info-text);
        margin-top: 0.1rem;
      }
      .actions-row {
        display: flex;
        gap: 0.5rem;
        justify-content: flex-end;
      }
      .w-full {
        width: 100%;
      }
      :host ::ng-deep .login-card.p-card {
        width: min(100%, 420px);
        border-radius: var(--ft-radius, 10px);
        border: 1px solid var(--ft-border, #30363d);
        box-shadow: 0 24px 48px rgba(0, 0, 0, 0.45);
      }
      :host ::ng-deep .login-card .p-card-title {
        font-size: 1.2rem;
      }
      :host ::ng-deep .login-card .p-card-subtitle {
        color: var(--ft-text-muted, #8b949e);
      }
    `
  ]
})
export class PlatformLoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(PlatformAuthService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly step = signal<'credentials' | 'two-factor'>('credentials');
  readonly subheader = computed(() =>
    this.step() === 'two-factor' ? 'Vérification 2FA' : 'Connexion opérateur'
  );

  protected totpCode = '';
  private twoFactorTicket: string | null = null;

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(1)]]
  });

  submit(): void {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.errorMessage.set(null);
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (!res.success || !res.data) {
          this.errorMessage.set(res.message ?? res.errors?.join(' ') ?? 'Connexion impossible');
          return;
        }
        if (isTwoFactorChallenge(res.data)) {
          this.twoFactorTicket = res.data.ticket;
          this.totpCode = '';
          this.step.set('two-factor');
        } else {
          void this.router.navigate(['/tenants']);
        }
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Email ou mot de passe incorrect');
      }
    });
  }

  submitTwoFactor(): void {
    if (!this.twoFactorTicket || this.totpCode.length < 6) return;
    this.loading.set(true);
    this.errorMessage.set(null);
    this.auth.verifyTwoFactor(this.twoFactorTicket, this.totpCode.trim()).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success && res.data) {
          void this.router.navigate(['/tenants']);
        } else {
          this.errorMessage.set(res.message ?? 'Code invalide ou expiré');
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Code invalide ou expiré');
      }
    });
  }

  resetToCredentials(): void {
    this.twoFactorTicket = null;
    this.totpCode = '';
    this.errorMessage.set(null);
    this.step.set('credentials');
  }
}
