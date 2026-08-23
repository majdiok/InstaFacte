import {
  ChangeDetectionStrategy,
  Component,
  Input,
  OnChanges,
  SimpleChanges,
  inject,
  signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';

import { PlatformTenantModalSettingsService } from '@core/services/platform-tenant-modal-settings.service';
import type { TenantModalSettingsDto } from '@core/models/platform.models';
import { FtSkeletonComponent } from '@core/ui/skeleton/ft-skeleton.component';
import { ModalEndpointFieldsComponent } from '../ai-settings/modal-endpoint-fields.component';

/**
 * Onglet Configuration IA (Modal / Kimi) sur la fiche entreprise.
 * Hérite de la plateforme tant qu'aucun override n'est enregistré.
 */
@Component({
  selector: 'app-tenant-modal-settings-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    DialogModule,
    FtSkeletonComponent,
    ModalEndpointFieldsComponent
  ],
  template: `
    <p class="intro">
      Endpoint Modal (Kimi) propre à cette entreprise. Sans override, l'entreprise utilise
      la configuration plateforme. Désactiver Modal ici le bloque pour ce tenant uniquement,
      sans repli. Le modèle Assistant / Import / Studio reste global.
      L'app Modal ciblée doit exposer le même identifiant (ex. moonshotai/Kimi-K3).
    </p>

    @if (loading()) {
      <ft-skeleton kind="line" count="4" />
    } @else if (error()) {
      <p class="error">{{ error() }}</p>
    } @else {
      @if (data(); as d) {
      <section class="card">
      <div class="status" [attr.data-kind]="statusKind()">
        <strong>{{ statusTitle() }}</strong>
        <span>{{ statusDetail() }}</span>
      </div>

      @if (!editing()) {
        <div class="platform-summary">
          <h3>Configuration plateforme actuelle</h3>
          <dl>
            <dt>État</dt>
            <dd>{{ d.platform.isEnabled ? 'Activé' : 'Désactivé' }}</dd>
            <dt>Nom</dt>
            <dd>{{ d.platform.displayName || '—' }}</dd>
            <dt>URL</dt>
            <dd><code>{{ d.platform.baseUrl || d.defaultBaseUrl || '—' }}</code></dd>
            <dt>Token</dt>
            <dd>
              @if (d.platform.isApiKeyConfigured) {
                Configuré (…{{ d.platform.apiKeyLast4 }})
              } @else {
                Non configuré
              }
            </dd>
          </dl>
          <p-button
            label="Personnaliser pour cette entreprise"
            icon="pi pi-pencil"
            (onClick)="startCustom()" />
        </div>
      } @else {
        @if (disabledWithGlobalModal()) {
          <p class="warn">
            Le modèle plateforme est Modal, mais Modal est désactivé pour cette entreprise.
            L'assistant échouera jusqu'à réactivation ou retour à la configuration plateforme.
          </p>
        }
        <app-modal-endpoint-fields
          idPrefix="tenant-modal"
          [(enabled)]="modalEnabled"
          [(displayName)]="modalDisplayName"
          [(baseUrl)]="modalBaseUrl"
          [(tokenId)]="modalTokenId"
          [(tokenSecret)]="modalTokenSecret"
          [apiKeyConfigured]="modalApiKeyConfigured"
          [apiKeyLast4]="modalApiKeyLast4"
          [urlPlaceholder]="urlPlaceholder()"
          [warning]="tokenWarning()" />
        <div class="actions">
          <p-button
            label="Enregistrer"
            icon="pi pi-check"
            severity="primary"
            [disabled]="busy()"
            [loading]="busy()"
            (onClick)="save()" />
          @if (d.hasOverride) {
            <p-button
              label="Revenir à la configuration plateforme"
              icon="pi pi-undo"
              [outlined]="true"
              severity="secondary"
              [disabled]="busy()"
              (onClick)="confirmVisible.set(true)" />
          } @else {
            <p-button
              label="Annuler"
              [outlined]="true"
              [disabled]="busy()"
              (onClick)="cancelCustom()" />
          }
        </div>
      }
      </section>
      }
    }

    <p-dialog
      header="Revenir à la plateforme ?"
      [visible]="confirmVisible()"
      (visibleChange)="confirmVisible.set($event)"
      [modal]="true"
      [draggable]="false"
      [style]="{ width: 'min(440px, 94vw)' }">
      <p>
        L'endpoint Modal dédié sera supprimé. Cette entreprise réutilisera la configuration
        partagée (Configuration IA globale).
      </p>
      <ng-template pTemplate="footer">
        <p-button label="Annuler" [text]="true" (onClick)="confirmVisible.set(false)" />
        <p-button
          label="Confirmer"
          icon="pi pi-check"
          severity="danger"
          [loading]="busy()"
          (onClick)="revertToPlatform()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      :host {
        display: block;
        padding-top: 0.6rem;
        max-width: 620px;
        margin: 0 auto;
      }
      .intro {
        color: var(--ft-text-muted, #8b949e);
        font-size: 0.88rem;
        line-height: 1.5;
        margin: 0 0 1rem;
      }
      .card {
        background: var(--ft-surface-2, var(--ft-surface));
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius, 8px);
        padding: 1.1rem;
      }
      .status {
        display: flex;
        flex-direction: column;
        gap: 0.2rem;
        padding: 0.7rem 0.9rem;
        border-radius: var(--ft-radius, 8px);
        border: 1px solid var(--ft-border);
        margin-bottom: 1rem;
        font-size: 0.88rem;
      }
      .status strong {
        color: var(--ft-text, #e6edf3);
      }
      .status span {
        color: var(--ft-text-muted, #8b949e);
        line-height: 1.45;
      }
      .status[data-kind='inherit'] {
        background: var(--ft-surface-2, #161b22);
        border-color: var(--ft-border, #30363d);
      }
      .status[data-kind='inherit'] span {
        color: var(--ft-text-subtle, #9da7b3);
      }
      .status[data-kind='custom'] {
        border-color: var(--ft-accent-border, var(--ft-border));
        background: var(--ft-accent-muted, transparent);
      }
      .status[data-kind='disabled'] {
        border-color: var(--ft-warning);
      }
      .platform-summary h3 {
        font-size: 0.9rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--ft-text-muted);
        margin: 0 0 0.6rem;
      }
      dl {
        display: grid;
        grid-template-columns: 6rem 1fr;
        gap: 0.35rem 0.8rem;
        margin: 0 0 1rem;
        font-size: 0.88rem;
      }
      dt { color: var(--ft-text-muted); }
      dd { margin: 0; }
      code { font-size: 0.8rem; word-break: break-all; }
      .actions {
        display: flex;
        flex-wrap: wrap;
        gap: 0.5rem;
        margin-top: 1rem;
      }
      .error {
        color: var(--ft-danger-text, #f85149);
        background: var(--ft-danger-surface, rgba(248, 81, 73, 0.14));
        border: 1px solid var(--ft-danger-border, rgba(248, 81, 73, 0.4));
        border-radius: var(--ft-radius, 8px);
        padding: 0.6rem 0.85rem;
      }
      .warn {
        margin: 0 0 0.85rem;
        font-size: 0.85rem;
        color: var(--ft-warning);
        line-height: 1.5;
      }
    `
  ]
})
export class TenantModalSettingsTabComponent implements OnChanges {
  @Input({ required: true }) tenantId!: string;

  private readonly api = inject(PlatformTenantModalSettingsService);
  private readonly toast = inject(MessageService);

  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly data = signal<TenantModalSettingsDto | null>(null);
  protected readonly editing = signal(false);
  protected readonly confirmVisible = signal(false);

  protected modalEnabled = false;
  protected modalDisplayName = '';
  protected modalBaseUrl = '';
  protected modalTokenId = '';
  protected modalTokenSecret = '';
  protected modalApiKeyConfigured = false;
  protected modalApiKeyLast4: string | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['tenantId'] && this.tenantId) {
      this.load();
    }
  }

  protected statusKind(): 'inherit' | 'custom' | 'disabled' {
    const d = this.data();
    if (!d?.hasOverride) return 'inherit';
    return d.isEnabled ? 'custom' : 'disabled';
  }

  protected statusTitle(): string {
    switch (this.statusKind()) {
      case 'custom':
        return 'Endpoint dédié';
      case 'disabled':
        return 'Modal désactivé pour cette entreprise';
      default:
        return 'Hérite de la plateforme';
    }
  }

  protected statusDetail(): string {
    switch (this.statusKind()) {
      case 'custom':
        return 'Cette entreprise utilise son propre URL et token Modal.';
      case 'disabled':
        return 'Aucun appel Kimi ne partira pour ce tenant, même si la plateforme a Modal activé.';
      default:
        return 'Même endpoint et token que Configuration IA globale.';
    }
  }

  protected urlPlaceholder(): string {
    const d = this.data();
    return d?.defaultBaseUrl
      || d?.platform.baseUrl
      || 'https://…--ep-kimi-k3-server.us-west.modal.direct/v1';
  }

  protected tokenWarning(): string | null {
    const d = this.data();
    const usesModal = (d?.platformConfiguredModelRef ?? '').toLowerCase().startsWith('modal:');
    const hasKey = this.modalApiKeyConfigured
      || (!!this.modalTokenId.trim() && !!this.modalTokenSecret.trim());
    if (usesModal && this.modalEnabled && !hasKey) {
      return "Un modèle Modal est sélectionné au niveau plateforme mais le token n'est pas configuré pour cette entreprise.";
    }
    return null;
  }

  protected disabledWithGlobalModal(): boolean {
    const d = this.data();
    const usesModal = (d?.platformConfiguredModelRef ?? '').toLowerCase().startsWith('modal:');
    return usesModal && this.editing() && !this.modalEnabled;
  }

  protected startCustom(): void {
    const d = this.data();
    if (!d) return;
    if (!d.hasOverride) {
      this.modalEnabled = true;
      this.modalDisplayName = d.platform.displayName || d.displayName || 'Modal (Kimi)';
      this.modalBaseUrl = d.platform.baseUrl || d.defaultBaseUrl || '';
      this.modalApiKeyConfigured = false;
      this.modalApiKeyLast4 = null;
      this.modalTokenId = '';
      this.modalTokenSecret = '';
    }
    this.editing.set(true);
  }

  protected cancelCustom(): void {
    this.applyForm(this.data());
    this.editing.set(!!this.data()?.hasOverride);
  }

  protected save(): void {
    const tokenId = this.modalTokenId.trim();
    const tokenSecret = this.modalTokenSecret.trim();
    const apiKey = tokenId && tokenSecret ? `${tokenId}.${tokenSecret}` : null;
    this.busy.set(true);
    this.api.update(this.tenantId, {
      isEnabled: this.modalEnabled,
      displayName: this.modalDisplayName.trim() || null,
      baseUrl: this.modalBaseUrl.trim() || null,
      apiKey
    }).subscribe({
      next: res => {
        this.busy.set(false);
        if (res.success && res.data) {
          this.applyLoaded(res.data);
          this.toast.add({ severity: 'success', summary: 'Enregistré', detail: 'Configuration Modal de l\'entreprise mise à jour.' });
        } else {
          this.toast.add({
            severity: 'error',
            summary: 'Erreur',
            detail: res.message || res.errors?.[0] || 'Enregistrement impossible.'
          });
        }
      },
      error: err => {
        this.busy.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Erreur',
          detail: err.error?.message || err.error?.errors?.[0] || 'Enregistrement impossible.'
        });
      }
    });
  }

  protected revertToPlatform(): void {
    this.busy.set(true);
    this.api.remove(this.tenantId).subscribe({
      next: res => {
        this.busy.set(false);
        this.confirmVisible.set(false);
        if (res.success && res.data) {
          this.applyLoaded(res.data);
          this.toast.add({
            severity: 'success',
            summary: 'Rétabli',
            detail: 'Cette entreprise hérite à nouveau de la configuration plateforme.'
          });
        }
      },
      error: err => {
        this.busy.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Erreur',
          detail: err.error?.message || 'Suppression impossible.'
        });
      }
    });
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.get(this.tenantId).subscribe({
      next: res => {
        this.loading.set(false);
        if (res.success && res.data) {
          this.applyLoaded(res.data);
        } else {
          this.error.set(res.message || 'Chargement impossible.');
        }
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err.error?.message || 'Chargement impossible.');
      }
    });
  }

  private applyLoaded(d: TenantModalSettingsDto): void {
    this.data.set(d);
    this.editing.set(d.hasOverride);
    this.applyForm(d);
  }

  private applyForm(d: TenantModalSettingsDto | null): void {
    if (!d) return;
    this.modalEnabled = d.isEnabled;
    this.modalDisplayName = d.displayName ?? '';
    this.modalBaseUrl = d.baseUrl ?? '';
    this.modalApiKeyConfigured = d.isApiKeyConfigured;
    this.modalApiKeyLast4 = d.apiKeyLast4;
    this.modalTokenId = '';
    this.modalTokenSecret = '';
  }
}
