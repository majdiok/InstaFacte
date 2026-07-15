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
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import type { AuditLogDetailDto } from '@core/models/platform.models';
import { AUDIT_FR } from './audit.i18n.fr';

/**
 * Lot B3 — Modal de détail d'une entrée d'audit log.
 *
 * Affiche 3 sections :
 *  1. Identité (id, date, user, action, entité, IP)
 *  2. Modification (OldValues / NewValues en JSON formaté)
 *  3. Intégrité (PreviousHash + Hash en mono)
 */
@Component({
  selector: 'app-audit-detail-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DialogModule, ButtonModule, FtBadgeComponent],
  template: `
    <p-dialog
      [visible]="visible"
      (visibleChange)="onVisibleChange($event)"
      [modal]="true"
      [draggable]="false"
      [resizable]="false"
      [style]="{ width: '54rem', maxWidth: '95vw' }"
      [header]="t('detail.title')"
    >
      @if (data) {
        <!-- Identité -->
        <h3 class="section-title">{{ t('detail.section.identity') }}</h3>
        <dl class="grid">
          <div><dt>{{ t('detail.field.id') }}</dt><dd><code>{{ data.id }}</code></dd></div>
          <div><dt>{{ t('detail.field.createdAt') }}</dt><dd>{{ data.createdAt | date: 'dd/MM/yyyy HH:mm:ss' }}</dd></div>
          <div><dt>{{ t('detail.field.action') }}</dt><dd><ft-badge tone="accent">{{ data.action }}</ft-badge></dd></div>
          <div><dt>{{ t('detail.field.email') }}</dt><dd>{{ data.userEmail }}</dd></div>
          <div><dt>{{ t('detail.field.user') }}</dt><dd><code class="muted">{{ data.userId ?? '—' }}</code></dd></div>
          <div><dt>{{ t('detail.field.entityType') }}</dt><dd>{{ data.entityType }}</dd></div>
          <div><dt>{{ t('detail.field.entityId') }}</dt><dd><code>{{ data.entityId ?? '—' }}</code></dd></div>
          @if (data.entityLabel) {
            <div><dt>{{ t('detail.field.entityLabel') }}</dt><dd>{{ data.entityLabel }}</dd></div>
          }
          <div><dt>{{ t('detail.field.ipAddress') }}</dt><dd><code>{{ data.ipAddress }}</code></dd></div>
          @if (data.userAgent) {
            <div class="full"><dt>{{ t('detail.field.userAgent') }}</dt><dd class="ua">{{ data.userAgent }}</dd></div>
          }
        </dl>

        <!-- Payload -->
        <h3 class="section-title">{{ t('detail.section.payload') }}</h3>
        <div class="payload-grid">
          <div class="payload-col">
            <h4>{{ t('detail.oldValues') }}</h4>
            @if (formattedOld()) {
              <pre>{{ formattedOld() }}</pre>
            } @else {
              <p class="muted">{{ t('detail.noPayload') }}</p>
            }
          </div>
          <div class="payload-col">
            <h4>{{ t('detail.newValues') }}</h4>
            @if (formattedNew()) {
              <pre>{{ formattedNew() }}</pre>
            } @else {
              <p class="muted">{{ t('detail.noPayload') }}</p>
            }
          </div>
        </div>

        <!-- Intégrité -->
        <h3 class="section-title">{{ t('detail.section.integrity') }}</h3>
        <dl class="grid">
          <div class="full">
            <dt>{{ t('detail.field.previousHash') }}</dt>
            <dd><code class="hash">{{ data.previousHash }}</code></dd>
          </div>
          <div class="full">
            <dt>{{ t('detail.field.hash') }}</dt>
            <dd><code class="hash">{{ data.hash }}</code></dd>
          </div>
        </dl>
      }

      <ng-template pTemplate="footer">
        <p-button label="Fermer" [text]="true" (onClick)="onVisibleChange(false)" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      .section-title {
        margin: var(--gap-md) 0 var(--gap-xs);
        font-size: 0.92rem;
        font-weight: 600;
        color: var(--ft-text);
        text-transform: uppercase;
        letter-spacing: 0.04em;
      }

      .section-title:first-child {
        margin-top: 0;
      }

      .grid {
        margin: 0;
        padding: 0;
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 0.6rem var(--gap-md);
      }

      .grid > div {
        display: grid;
        grid-template-columns: 9rem 1fr;
        gap: 0.5rem;
        align-items: baseline;
      }

      .grid .full {
        grid-column: 1 / -1;
      }

      .grid dt {
        margin: 0;
        font-size: 0.72rem;
        color: var(--ft-text-muted);
        text-transform: uppercase;
        letter-spacing: 0.04em;
        font-weight: 500;
      }

      .grid dd {
        margin: 0;
        color: var(--ft-text);
        font-size: 0.85rem;
        word-break: break-word;
      }

      code,
      .hash {
        font-family: ui-monospace, SFMono-Regular, monospace;
        font-size: 0.78rem;
        background: var(--ft-surface-2);
        padding: 0.1rem 0.4rem;
        border-radius: var(--ft-radius-sm);
        color: var(--ft-accent);
      }

      .hash {
        word-break: break-all;
        display: inline-block;
        max-width: 100%;
      }

      .muted {
        color: var(--ft-text-muted);
      }

      .ua {
        font-size: 0.78rem;
        color: var(--ft-text-muted);
        word-break: break-word;
      }

      .payload-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: var(--gap-md);
      }

      .payload-col h4 {
        margin: 0 0 0.4rem;
        font-size: 0.78rem;
        color: var(--ft-text-muted);
        text-transform: uppercase;
        letter-spacing: 0.05em;
      }

      .payload-col pre {
        background: var(--ft-surface-2);
        border: 1px solid var(--ft-border);
        border-radius: var(--ft-radius);
        padding: var(--gap-sm);
        margin: 0;
        font-family: ui-monospace, SFMono-Regular, monospace;
        font-size: 0.78rem;
        color: var(--ft-text);
        max-height: 16rem;
        overflow: auto;
        white-space: pre-wrap;
        word-break: break-word;
      }

      @media (max-width: 720px) {
        .grid,
        .payload-grid {
          grid-template-columns: 1fr;
        }

        .grid > div {
          grid-template-columns: 1fr;
          gap: 0.2rem;
        }
      }
    `
  ]
})
export class AuditDetailDialogComponent {
  @Input() visible = false;
  @Input() data: AuditLogDetailDto | null = null;

  @Output() visibleChange = new EventEmitter<boolean>();

  protected t(key: keyof typeof AUDIT_FR): string {
    return AUDIT_FR[key];
  }

  protected readonly formattedOld = computed(() => formatJsonOrText(this.data?.oldValues ?? null));
  protected readonly formattedNew = computed(() => formatJsonOrText(this.data?.newValues ?? null));

  onVisibleChange(value: boolean): void {
    this.visible = value;
    this.visibleChange.emit(value);
  }
}

/** Tente de parser+pretty-print un JSON, sinon renvoie le texte brut. */
function formatJsonOrText(value: string | null): string | null {
  if (!value) return null;
  const trimmed = value.trim();
  if (!trimmed) return null;
  try {
    const parsed = JSON.parse(trimmed);
    return JSON.stringify(parsed, null, 2);
  } catch {
    return trimmed;
  }
}
