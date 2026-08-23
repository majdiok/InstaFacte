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
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';

import { PlatformMfaService } from '@core/services/platform-mfa.service';
import type { MfaSetupDto, MfaStatusDto } from '@core/models/platform.models';

import { FtPageHeaderComponent } from '@core/ui/page-header/ft-page-header.component';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';

type WizardStep = 'idle' | 'qr-shown' | 'recovery-codes';

/**
 * Lot B2 — Page `/me/2fa` : gestion 2FA pour l'admin courant.
 *
 * États :
 *  - **idle** : affiche le statut actuel (activé/désactivé) + boutons d'action.
 *  - **qr-shown** : QR code affiché + champ de confirmation (6 chiffres).
 *  - **recovery-codes** : 10 recovery codes affichés (one-shot, à imprimer).
 *
 * Désactivation : modale séparée demandant mot de passe + code TOTP courant.
 */
@Component({
  selector: 'app-me-2fa-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    FormsModule,
    ButtonModule,
    InputTextModule,
    PasswordModule,
    DialogModule,
    RouterLink,
    FtPageHeaderComponent,
    FtBadgeComponent,
    FtSkeletonComponent,
    FtEmptyStateComponent
  ],
  template: `
    <ft-page-header
      title="Authentification 2FA"
      subtitle="Renforcez la sécurité de votre compte avec un code à usage unique généré par votre application d'authentification (Google Authenticator, Authy, 1Password…)."
    >
      <ng-container ftActions>
        <p-button label="Retour" icon="pi pi-arrow-left" [outlined]="true" routerLink="/tenants" />
      </ng-container>
    </ft-page-header>

    @if (loading()) {
      <div class="card-stack">
        <ft-skeleton shape="rect" width="100%" height="3rem" />
        <ft-skeleton shape="line" />
        <ft-skeleton shape="line" />
      </div>
    } @else if (!status()) {
      <ft-empty-state
        variant="error"
        title="Erreur"
        description="Impossible de charger l'état 2FA. Réessayez."
      />
    } @else {
      <!-- ===== Statut actuel ===== -->
      @if (step() === 'idle') {
        <article class="status-card">
          <div class="status-card__head">
            <div class="status-card__icon" [class.status-card__icon--on]="status()!.isEnabled">
              <i class="pi pi-shield" aria-hidden="true"></i>
            </div>
            <div>
              <h2>État du 2FA</h2>
              @if (status()!.isEnabled) {
                <p>
                  <ft-badge tone="success" [withDot]="true">Activé</ft-badge>
                  @if (status()!.enabledAt) {
                    <span class="muted">— depuis le {{ status()!.enabledAt | date: 'dd/MM/yyyy' }}</span>
                  }
                </p>
              } @else if (status()!.hasPendingSetup) {
                <p><ft-badge tone="warning" [withDot]="true">Configuration en cours</ft-badge></p>
              } @else {
                <p><ft-badge tone="neutral" [withDot]="true">Non activé</ft-badge></p>
              }
            </div>
          </div>

          @if (status()!.isEnabled) {
            <div class="status-card__row">
              <span class="muted">Codes de récupération restants</span>
              <strong [class.muted]="status()!.remainingRecoveryCodes > 3"
                      [class.warn]="status()!.remainingRecoveryCodes <= 3 && status()!.remainingRecoveryCodes > 0"
                      [class.danger]="status()!.remainingRecoveryCodes === 0">
                {{ status()!.remainingRecoveryCodes }} / 10
              </strong>
            </div>
          }

          @if (status()!.isLocked) {
            <div class="warn-banner" role="alert">
              <i class="pi pi-lock" aria-hidden="true"></i>
              <span>
                Trop d'échecs de vérification. Réessayez après le
                {{ status()!.lockoutUntil | date: 'dd/MM/yyyy HH:mm' }}.
              </span>
            </div>
          }

          <div class="status-card__actions">
            @if (status()!.isEnabled) {
              <p-button
                label="Désactiver le 2FA"
                icon="pi pi-times-circle"
                severity="danger"
                [outlined]="true"
                (onClick)="disableDialogOpen = true" />
              <p-button
                label="Renouveler les codes"
                icon="pi pi-refresh"
                [text]="true"
                pTooltip="Régénère un nouveau secret + de nouveaux recovery codes"
                (onClick)="startSetup()" />
            } @else {
              <p-button
                label="Activer le 2FA"
                icon="pi pi-shield"
                severity="primary"
                (onClick)="startSetup()" />
            }
          </div>
        </article>
      }

      <!-- ===== Étape QR + confirmation ===== -->
      @if (step() === 'qr-shown' && setup(); as su) {
        <article class="setup-card">
          <h2>1. Scannez le QR code</h2>
          <p class="muted">
            Avec votre application d'authentification (Google Authenticator, Authy, 1Password…),
            scannez le QR code ci-dessous. Sinon, copiez le secret manuellement.
          </p>

          <div class="qr-row">
            <img [src]="su.qrCodeDataUri" alt="QR code 2FA" class="qr" />
            <div class="qr-meta">
              <label>Secret (saisie manuelle)</label>
              <code class="secret">{{ su.secretBase32 }}</code>
              <small class="muted">Copiez-le si votre app ne supporte pas le scan.</small>
            </div>
          </div>

          <h2>2. Confirmez avec le code</h2>
          <p class="muted">
            Saisissez le code à 6 chiffres affiché par votre application pour activer le 2FA.
          </p>
          <div class="confirm-row">
            <input
              type="text"
              pInputText
              [(ngModel)]="confirmCode"
              inputmode="numeric"
              autocomplete="one-time-code"
              placeholder="123456"
              maxlength="6"
              class="code-input"
            />
            <p-button
              label="Confirmer"
              icon="pi pi-check"
              [loading]="confirming()"
              [disabled]="confirmCode.length !== 6 || confirming()"
              (onClick)="confirmSetup()" />
            <p-button
              label="Annuler"
              [text]="true"
              severity="secondary"
              (onClick)="cancelSetup()" />
          </div>
          @if (confirmError()) {
            <p class="error-line">{{ confirmError() }}</p>
          }
        </article>
      }

      <!-- ===== Recovery codes affichés (one-shot) ===== -->
      @if (step() === 'recovery-codes') {
        <article class="recovery-card">
          <h2>
            <i class="pi pi-check-circle"></i>
            2FA activé avec succès
          </h2>
          <p class="warn-text">
            <strong>⚠ Conservez ces 10 codes de récupération en lieu sûr.</strong>
            Ils permettent de vous reconnecter si vous perdez l'accès à votre application.
            Ils ne seront <u>plus jamais affichés</u> après cette page.
          </p>

          <div class="codes-grid" role="list">
            @for (code of recoveryCodes(); track code; let i = $index) {
              <code class="code-cell" role="listitem">
                {{ i + 1 }}. {{ code }}
              </code>
            }
          </div>

          <div class="recovery-card__actions">
            <p-button
              label="Imprimer"
              icon="pi pi-print"
              [outlined]="true"
              (onClick)="onPrint()" />
            <p-button
              label="Copier tous les codes"
              icon="pi pi-copy"
              [outlined]="true"
              (onClick)="onCopyCodes()" />
            <p-button
              label="J'ai conservé mes codes"
              icon="pi pi-check"
              severity="success"
              (onClick)="finishSetup()" />
          </div>
        </article>
      }
    }

    <!-- ===== Modale de désactivation ===== -->
    <p-dialog
      [(visible)]="disableDialogOpen"
      [modal]="true"
      [closable]="!disabling()"
      [draggable]="false"
      [resizable]="false"
      [style]="{ width: '28rem', maxWidth: '95vw' }"
      header="Désactiver le 2FA"
    >
      <p class="muted">
        Cette action désactive complètement le 2FA. Pour confirmer, saisissez votre mot de passe
        et le code à 6 chiffres affiché actuellement par votre application.
      </p>
      <div class="dlg-field">
        <label for="dis-pwd">Mot de passe</label>
        <p-password
          inputId="dis-pwd"
          [(ngModel)]="disablePassword"
          [feedback]="false"
          [toggleMask]="true"
          styleClass="w-full"
          inputStyleClass="w-full" />
      </div>
      <div class="dlg-field">
        <label for="dis-code">Code à 6 chiffres</label>
        <input
          id="dis-code"
          type="text"
          pInputText
          [(ngModel)]="disableCode"
          inputmode="numeric"
          maxlength="6"
          placeholder="123456"
          class="w-full" />
      </div>
      @if (disableError()) {
        <p class="error-line">{{ disableError() }}</p>
      }
      <ng-template pTemplate="footer">
        <p-button
          label="Annuler"
          [text]="true"
          severity="secondary"
          [disabled]="disabling()"
          (onClick)="closeDisableDialog()" />
        <p-button
          label="Désactiver"
          icon="pi pi-times-circle"
          severity="danger"
          [loading]="disabling()"
          [disabled]="disablePassword.length === 0 || disableCode.length !== 6 || disabling()"
          (onClick)="confirmDisable()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .card-stack {
        display: flex;
        flex-direction: column;
        gap: var(--gap-sm);
      }

      .status-card,
      .setup-card,
      .recovery-card {
        background: var(--ft-surface);
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius-lg);
        padding: var(--gap-lg);
        margin-bottom: var(--gap-md);
      }

      .recovery-card {
        border-color: var(--ft-success-border);
      }

      .status-card__head {
        display: flex;
        gap: var(--gap-md);
        align-items: center;
        margin-bottom: var(--gap-md);
      }

      .status-card__head h2 {
        margin: 0 0 0.25rem;
        font-size: 1.05rem;
        color: var(--ft-text);
      }

      .status-card__head p {
        margin: 0;
        font-size: 0.9rem;
      }

      .status-card__icon {
        width: 2.75rem;
        height: 2.75rem;
        border-radius: var(--ft-radius-pill);
        display: flex;
        align-items: center;
        justify-content: center;
        background: var(--ft-neutral-surface);
        color: var(--ft-text-muted);
        font-size: 1.25rem;
      }

      .status-card__icon--on {
        background: var(--ft-success-surface);
        color: var(--ft-success-text);
      }

      .status-card__row {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: var(--gap-sm) 0;
        border-top: 1px solid var(--ft-border-subtle);
      }

      .status-card__actions {
        display: flex;
        gap: var(--gap-sm);
        flex-wrap: wrap;
        margin-top: var(--gap-md);
      }

      .setup-card h2 {
        margin: var(--gap-md) 0 var(--gap-xs);
        font-size: 1rem;
        color: var(--ft-text);
      }

      .setup-card h2:first-child {
        margin-top: 0;
      }

      .qr-row {
        display: flex;
        gap: var(--gap-md);
        align-items: flex-start;
        margin: var(--gap-md) 0;
      }

      .qr {
        width: 12rem;
        height: 12rem;
        background: var(--ft-content-light-bg);
        padding: 0.5rem;
        border-radius: var(--ft-radius);
        flex-shrink: 0;
      }

      .qr-meta {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
        flex: 1;
        min-width: 0;
      }

      .qr-meta label {
        font-size: 0.78rem;
        color: var(--ft-text-muted);
        text-transform: uppercase;
        letter-spacing: 0.05em;
      }

      .secret {
        font-family: ui-monospace, SFMono-Regular, monospace;
        font-size: 0.92rem;
        background: var(--ft-surface-2);
        border: 1px solid var(--ft-border);
        padding: 0.4rem 0.6rem;
        border-radius: var(--ft-radius);
        color: var(--ft-accent);
        word-break: break-all;
        user-select: all;
      }

      .confirm-row {
        display: flex;
        gap: 0.5rem;
        align-items: center;
        flex-wrap: wrap;
      }

      .code-input {
        font-family: ui-monospace, SFMono-Regular, monospace;
        font-size: 1.2rem;
        letter-spacing: 0.2em;
        text-align: center;
        width: 9rem;
      }

      .recovery-card h2 {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        margin: 0 0 var(--gap-sm);
        color: var(--ft-success-text);
      }

      .warn-text {
        margin: 0 0 var(--gap-md);
        padding: var(--gap-sm) var(--gap-md);
        background: var(--ft-warning-surface);
        border: 1px solid var(--ft-warning-border);
        border-radius: var(--ft-radius);
        color: var(--ft-text);
        font-size: 0.9rem;
        line-height: 1.5;
      }

      .codes-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(11rem, 1fr));
        gap: var(--gap-xs);
        margin: var(--gap-md) 0;
      }

      .code-cell {
        font-family: ui-monospace, SFMono-Regular, monospace;
        font-size: 1rem;
        background: var(--ft-surface-2);
        border: 1px solid var(--ft-border);
        padding: 0.5rem 0.75rem;
        border-radius: var(--ft-radius);
        color: var(--ft-accent);
        letter-spacing: 0.05em;
        user-select: all;
      }

      .recovery-card__actions {
        display: flex;
        gap: var(--gap-sm);
        flex-wrap: wrap;
        justify-content: flex-end;
      }

      .warn-banner {
        margin: var(--gap-md) 0 0;
        padding: var(--gap-sm) var(--gap-md);
        background: var(--ft-danger-surface);
        border: 1px solid var(--ft-danger-border);
        border-radius: var(--ft-radius);
        color: var(--ft-text);
        font-size: 0.85rem;
        display: flex;
        gap: 0.5rem;
        align-items: center;
      }

      .warn-banner .pi {
        color: var(--ft-danger-text);
      }

      .error-line {
        margin: var(--gap-xs) 0 0;
        color: var(--ft-danger-text);
        font-size: 0.85rem;
      }

      .muted {
        color: var(--ft-text-muted);
      }

      .warn {
        color: var(--ft-warning-text);
      }

      .danger {
        color: var(--ft-danger-text);
      }

      .dlg-field {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
        margin-bottom: var(--gap-sm);
      }

      .dlg-field label {
        font-size: 0.78rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--ft-text-muted);
        font-weight: 600;
      }

      .w-full {
        width: 100%;
      }

      :host ::ng-deep .w-full input {
        width: 100%;
      }

      @media print {
        :host ::ng-deep app-platform-shell .shell-header,
        :host ::ng-deep .status-card,
        :host ::ng-deep .setup-card,
        :host ::ng-deep p-button,
        :host ::ng-deep .recovery-card__actions {
          display: none !important;
        }

        .recovery-card {
          border: 2px solid #000;
          background: var(--ft-content-light-bg);
          color: black;
        }
      }
    `
  ]
})
export class Me2faPageComponent implements OnInit {
  private readonly api = inject(PlatformMfaService);
  private readonly toast = inject(MessageService);

  readonly status = signal<MfaStatusDto | null>(null);
  readonly setup = signal<MfaSetupDto | null>(null);
  readonly recoveryCodes = signal<string[]>([]);
  readonly loading = signal(false);
  readonly confirming = signal(false);
  readonly disabling = signal(false);
  readonly confirmError = signal<string | null>(null);
  readonly disableError = signal<string | null>(null);

  readonly step = signal<WizardStep>('idle');

  protected confirmCode = '';
  protected disablePassword = '';
  protected disableCode = '';
  disableDialogOpen = false;

  ngOnInit(): void {
    this.loadStatus();
  }

  private loadStatus(): void {
    this.loading.set(true);
    this.api.status().subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success && res.data) {
          this.status.set(res.data);
        }
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  // ----- Setup wizard ------------------------------------------------------
  startSetup(): void {
    this.confirmError.set(null);
    this.confirmCode = '';
    this.api.startSetup().subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.setup.set(res.data);
          this.step.set('qr-shown');
        } else {
          this.toastError(res.message);
        }
      },
      error: () => this.toastError()
    });
  }

  cancelSetup(): void {
    this.setup.set(null);
    this.confirmCode = '';
    this.confirmError.set(null);
    this.step.set('idle');
    this.loadStatus();
  }

  confirmSetup(): void {
    if (this.confirmCode.length !== 6) return;
    this.confirming.set(true);
    this.confirmError.set(null);
    this.api.confirmSetup(this.confirmCode).subscribe({
      next: (res) => {
        this.confirming.set(false);
        if (res.success && res.data) {
          this.recoveryCodes.set(res.data.recoveryCodes);
          this.setup.set(null);
          this.confirmCode = '';
          this.step.set('recovery-codes');
        } else {
          this.confirmError.set(res.message ?? 'Code invalide.');
        }
      },
      error: (err) => {
        this.confirming.set(false);
        this.confirmError.set(err?.error?.message ?? 'Code invalide ou expiré.');
      }
    });
  }

  finishSetup(): void {
    this.recoveryCodes.set([]);
    this.step.set('idle');
    this.loadStatus();
    this.toast.add({ severity: 'success', summary: '2FA activé', detail: 'Vous serez invité au code à chaque connexion.' });
  }

  onPrint(): void {
    window.print();
  }

  onCopyCodes(): void {
    const text = this.recoveryCodes().join('\n');
    if (navigator.clipboard?.writeText) {
      navigator.clipboard
        .writeText(text)
        .then(() =>
          this.toast.add({ severity: 'success', summary: 'Copié', detail: 'Codes de récupération copiés.' })
        )
        .catch(() => undefined);
    }
  }

  // ----- Disable -----------------------------------------------------------
  closeDisableDialog(): void {
    this.disableDialogOpen = false;
    this.disablePassword = '';
    this.disableCode = '';
    this.disableError.set(null);
  }

  confirmDisable(): void {
    if (this.disablePassword.length === 0 || this.disableCode.length !== 6) return;
    this.disabling.set(true);
    this.disableError.set(null);
    this.api.disable(this.disablePassword, this.disableCode).subscribe({
      next: (res) => {
        this.disabling.set(false);
        if (res.success) {
          this.closeDisableDialog();
          this.toast.add({ severity: 'success', summary: '2FA désactivé', detail: '' });
          this.loadStatus();
        } else {
          this.disableError.set(res.message ?? 'Mot de passe ou code invalide.');
        }
      },
      error: (err) => {
        this.disabling.set(false);
        this.disableError.set(err?.error?.message ?? 'Mot de passe ou code invalide.');
      }
    });
  }

  private toastError(detail?: string | null): void {
    this.toast.add({
      severity: 'error',
      summary: 'Erreur',
      detail: detail ?? 'Impossible de contacter l’API'
    });
  }
}
