import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output
} from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { FtDrawerComponent } from '@core/ui/drawer/ft-drawer.component';
import { FtBadgeComponent } from '@core/ui/badge/ft-badge.component';
import { FtAvatarComponent } from '@core/ui/avatar/ft-avatar.component';
import { FtEmptyStateComponent } from '@core/ui/empty-state/ft-empty-state.component';
import type { MigrationStatusResultDto } from '@core/models/platform.models';
import { MIGRATIONS_FR } from './migrations.i18n.fr';

/**
 * Drawer de détail migration pour un tenant. Affiche le statut courant
 * (Appliquées / Manquantes) + récap subscription + placeholder pour la liste
 * détaillée des migrations (Lot D4).
 *
 * Les actions Appliquer / Voir page complète sont dans le footer.
 */
@Component({
  selector: 'app-migration-detail-drawer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ButtonModule,
    FtDrawerComponent,
    FtBadgeComponent,
    FtAvatarComponent,
    FtEmptyStateComponent
  ],
  template: `
    <ft-drawer
      [visible]="visible"
      (visibleChange)="onVisibleChange($event)"
      [title]="t('drawer.title')"
      [subtitle]="row?.tenantName ?? null"
      [width]="'480px'"
    >
      @if (row) {
        <div class="qv-head">
          <ft-avatar [name]="row.tenantName" [seed]="row.tenantId" size="lg" />
          <div class="qv-head__titles">
            <h3 class="qv-head__name">{{ row.tenantName }}</h3>
            <code class="qv-head__id">{{ row.tenantId }}</code>
          </div>
        </div>

        <dl class="qv-fields">
          <div>
            <dt>{{ t('drawer.fields.plan') }}</dt>
            <dd>
              @if (row.subscriptionPlanDisplay) {
                <ft-badge [tone]="planTone()">{{ row.subscriptionPlanDisplay }}</ft-badge>
              } @else {
                —
              }
            </dd>
          </div>
          <div>
            <dt>{{ t('drawer.fields.segment') }}</dt>
            <dd>
              <ft-badge [tone]="row.isPayingSubscriber ? 'success' : 'neutral'">
                {{ row.isPayingSubscriber ? 'Abonné' : 'Non abonné' }}
              </ft-badge>
            </dd>
          </div>
          <div>
            <dt>{{ t('drawer.fields.status') }}</dt>
            <dd>
              @if (row.hasMigrationsApplied) {
                <ft-badge tone="success" [withDot]="true">{{ t('status.applied') }}</ft-badge>
              } @else {
                <ft-badge tone="warning" [withDot]="true">{{ t('status.missing') }}</ft-badge>
              }
            </dd>
          </div>
        </dl>

        <ft-empty-state
          variant="all-clear"
          title="Détail des migrations EF"
          [description]="t('drawer.placeholder.list')"
        />
      }

      <ng-container ftFooter>
        @if (row && !row.hasMigrationsApplied) {
          <p-button
            [label]="t('drawer.action.apply')"
            icon="pi pi-play"
            severity="warn"
            [loading]="busy"
            [disabled]="busy"
            (onClick)="onApplyClick()"
          />
        }
      </ng-container>
    </ft-drawer>
  `,
  styles: [
    `
      :host {
        display: contents;
      }

      .qv-head {
        display: flex;
        gap: var(--gap-md);
        align-items: center;
        margin-bottom: var(--gap-md);
        padding-bottom: var(--gap-md);
        border-bottom: 1px solid var(--ft-border);
      }

      .qv-head__titles {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
        flex: 1;
        min-width: 0;
      }

      .qv-head__name {
        margin: 0;
        font-size: 1.05rem;
        font-weight: 600;
        color: var(--ft-text);
      }

      .qv-head__id {
        font-family: ui-monospace, SFMono-Regular, monospace;
        font-size: 0.72rem;
        color: var(--ft-text-muted);
        background: var(--ft-surface-2);
        padding: 0.1rem 0.4rem;
        border-radius: var(--ft-radius-sm);
        word-break: break-all;
      }

      .qv-fields {
        margin: 0 0 var(--gap-md);
        padding: 0;
        display: flex;
        flex-direction: column;
        gap: 0.7rem;
      }

      .qv-fields > div {
        display: grid;
        grid-template-columns: 9rem 1fr;
        gap: 0.75rem;
        align-items: baseline;
      }

      .qv-fields dt {
        margin: 0;
        font-size: 0.78rem;
        color: var(--ft-text-muted);
        text-transform: uppercase;
        letter-spacing: 0.04em;
        font-weight: 500;
      }

      .qv-fields dd {
        margin: 0;
        color: var(--ft-text);
        font-size: 0.9rem;
      }
    `
  ]
})
export class MigrationDetailDrawerComponent {
  @Input() visible = false;
  @Input() row: MigrationStatusResultDto | null = null;
  @Input() busy = false;

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() applyOne = new EventEmitter<MigrationStatusResultDto>();

  protected t(key: keyof typeof MIGRATIONS_FR): string {
    return MIGRATIONS_FR[key];
  }

  protected planTone(): 'accent' | 'info' | 'neutral' {
    switch ((this.row?.subscriptionPlan ?? '').toLowerCase()) {
      case 'annual':
        return 'accent';
      case 'monthly':
        return 'info';
      default:
        return 'neutral';
    }
  }

  onVisibleChange(value: boolean): void {
    this.visible = value;
    this.visibleChange.emit(value);
  }

  onApplyClick(): void {
    if (this.row && !this.busy) {
      this.applyOne.emit(this.row);
    }
  }
}
