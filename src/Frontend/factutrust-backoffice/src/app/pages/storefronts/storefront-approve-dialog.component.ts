import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  signal
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import type { PlatformStorefrontProfileDto } from '@core/services/platform-storefront.service';
import {
  STOREFRONTS_FR,
  STOREFRONT_CATEGORY_LABELS,
  STOREFRONT_THEME_LABELS
} from './storefronts.i18n.fr';

/**
 * Modal d'approbation de vitrine (Lot A4).
 *
 * Affiche un récap (slug, nom, catégorie, thème, contacts, version d'opt-in,
 * date d'acceptation) et exige une checkbox de conformité légale obligatoire
 * avant d'activer le bouton "Publier sur Rue InstaFact".
 */
@Component({
  selector: 'app-storefront-approve-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, FormsModule, DialogModule, ButtonModule, CheckboxModule],
  template: `
    <p-dialog
      [visible]="visible"
      (visibleChange)="onVisibleChange($event)"
      [modal]="true"
      [closable]="!busy"
      [closeOnEscape]="!busy"
      [draggable]="false"
      [resizable]="false"
      [style]="{ width: '34rem', maxWidth: '95vw' }"
      [header]="t('approve.title')"
    >
      @if (data) {
        <dl class="recap">
          <div>
            <dt>{{ t('approve.recap.slug') }}</dt>
            <dd><code>/{{ data.slug }}</code></dd>
          </div>
          <div>
            <dt>{{ t('approve.recap.displayName') }}</dt>
            <dd><strong>{{ data.displayName }}</strong></dd>
          </div>
          <div>
            <dt>{{ t('approve.recap.category') }}</dt>
            <dd>{{ categoryLabel() }}</dd>
          </div>
          <div>
            <dt>{{ t('approve.recap.theme') }}</dt>
            <dd>{{ themeLabel() }}</dd>
          </div>
          <div>
            <dt>{{ t('approve.recap.email') }}</dt>
            <dd>{{ data.publicContactEmail }}</dd>
          </div>
          @if (data.publicContactPhone) {
            <div>
              <dt>{{ t('approve.recap.phone') }}</dt>
              <dd>{{ data.publicContactPhone }}</dd>
            </div>
          }
          @if (data.publicContactWhatsApp) {
            <div>
              <dt>{{ t('approve.recap.whatsApp') }}</dt>
              <dd>{{ data.publicContactWhatsApp }}</dd>
            </div>
          }
          <div class="recap__separator">{{ t('approve.recap.optIn') }}</div>
          <div>
            <dt>{{ t('approve.recap.optInVersion') }}</dt>
            <dd><code>{{ data.consentVersion }}</code></dd>
          </div>
          @if (data.consentAcceptedAt) {
            <div>
              <dt>{{ t('approve.recap.optInAcceptedAt') }}</dt>
              <dd>{{ data.consentAcceptedAt | date: 'dd/MM/yyyy HH:mm' }}</dd>
            </div>
          }
        </dl>

        <label class="consent">
          <p-checkbox
            [(ngModel)]="consentCheckedModel"
            [binary]="true"
            inputId="consent-check"
            [disabled]="busy"
          />
          <span>{{ t('approve.consent.label') }}</span>
        </label>
      }

      <ng-template pTemplate="footer">
        <p-button label="Annuler" [text]="true" severity="secondary" [disabled]="busy" (onClick)="onCancel()" />
        <p-button
          [label]="t('approve.confirmLabel')"
          icon="pi pi-check"
          severity="success"
          [disabled]="busy || !consentChecked()"
          [loading]="busy"
          (onClick)="onConfirm()"
        />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      .recap {
        margin: 0 0 var(--gap-md);
        padding: 0;
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
      }

      .recap > div {
        display: grid;
        grid-template-columns: 9rem 1fr;
        gap: 0.75rem;
        align-items: baseline;
        font-size: 0.9rem;
      }

      .recap dt {
        margin: 0;
        color: var(--ft-text-muted);
        font-size: 0.78rem;
        text-transform: uppercase;
        letter-spacing: 0.04em;
        font-weight: 500;
      }

      .recap dd {
        margin: 0;
        color: var(--ft-text);
      }

      .recap code {
        font-family: ui-monospace, SFMono-Regular, monospace;
        font-size: 0.85em;
        background: var(--ft-surface-2);
        padding: 0.1rem 0.4rem;
        border-radius: var(--ft-radius-sm);
        color: var(--ft-accent);
      }

      .recap__separator {
        display: block !important;
        grid-template-columns: none !important;
        margin-top: var(--gap-sm);
        padding-top: var(--gap-sm);
        border-top: 1px solid var(--ft-border);
        color: var(--ft-text-muted);
        font-size: 0.72rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        font-weight: 600;
      }

      .consent {
        display: flex;
        align-items: flex-start;
        gap: 0.65rem;
        padding: var(--gap-sm) var(--gap-md);
        background: var(--ft-success-surface);
        border: 1px solid var(--ft-success-border);
        border-radius: var(--ft-radius);
        color: var(--ft-text);
        font-size: 0.88rem;
        line-height: 1.45;
        cursor: pointer;
      }

      .consent span {
        flex: 1;
      }
    `
  ]
})
export class StorefrontApproveDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() data: PlatformStorefrontProfileDto | null = null;
  @Input() busy = false;

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() confirmed = new EventEmitter<PlatformStorefrontProfileDto>();
  @Output() cancelled = new EventEmitter<void>();

  private readonly consentCheckedSig = signal(false);
  protected consentChecked = this.consentCheckedSig.asReadonly();
  protected consentCheckedModel = false;

  protected t(key: keyof typeof STOREFRONTS_FR): string {
    return STOREFRONTS_FR[key];
  }

  protected categoryLabel(): string {
    const raw = String(this.data?.category ?? '').trim();
    return STOREFRONT_CATEGORY_LABELS[raw] ?? raw ?? '—';
  }

  protected themeLabel(): string {
    const raw = String(this.data?.facadeTheme ?? '').trim();
    return STOREFRONT_THEME_LABELS[raw] ?? raw ?? '—';
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && changes['visible'].currentValue === true) {
      // Reset checkbox à chaque ouverture
      this.consentCheckedModel = false;
      this.consentCheckedSig.set(false);
    }
  }

  onVisibleChange(value: boolean): void {
    this.visible = value;
    this.visibleChange.emit(value);
    if (!value) {
      this.consentCheckedModel = false;
      this.consentCheckedSig.set(false);
    }
  }

  onCancel(): void {
    this.cancelled.emit();
    this.onVisibleChange(false);
  }

  onConfirm(): void {
    // Sync model → signal pour le canConfirm
    this.consentCheckedSig.set(this.consentCheckedModel);
    if (this.consentCheckedSig() && this.data && !this.busy) {
      this.confirmed.emit(this.data);
    }
  }
}
