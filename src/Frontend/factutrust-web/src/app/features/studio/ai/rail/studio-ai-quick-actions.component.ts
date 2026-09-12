import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TooltipModule } from 'primeng/tooltip';
import { STUDIO_AI_LABELS } from '../studio-ai-labels';

/**
 * Carte « Actions rapides » du rail (maquette `studio-atelier-accueil-home.html`).
 *
 * Toujours présente : « Réinitialiser la conversation » (R20) fonctionne même sans capabilities.
 * Import / Dupliquer / Exporter / Partager dépendent de `systemExportEnabled` (PR 3.3) : quand la
 * capacité est absente, seul « Exporter » reste visible, désactivé avec le badge « Bientôt », pour
 * que la feuille de route reste lisible ; les autres sont masqués. « Partager avec l'équipe » est
 * toujours « Bientôt » (aucune PR ne le livre dans ce programme). Les sorties `importTemplate`,
 * `duplicate`, `exportSystem` sont câblées en PR 3.4.
 */
@Component({
  selector: 'app-studio-ai-quick-actions',
  standalone: true,
  imports: [TooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './studio-ai-rail.scss',
  template: `
    <section class="sar-card" [attr.aria-label]="labels.quickActions">
      <div class="sar-card__head">
        <h3 class="sar-card__title"><i class="fa-solid fa-bolt" aria-hidden="true"></i>{{ labels.quickActions }}</h3>
      </div>
      <ul class="sar-list">
        @if (exportEnabled()) {
          <li>
            <button type="button" class="sar-row" data-action="import" [disabled]="busy()" (click)="importTemplate.emit()">
              <span class="sar-row__icon" aria-hidden="true"><i class="fa-solid fa-file-import"></i></span>
              <span class="sar-row__body"><span class="sar-row__title">{{ labels.importTemplateJson }}</span></span>
            </button>
          </li>
          <li>
            <button type="button" class="sar-row" data-action="duplicate" [disabled]="busy()" (click)="duplicate.emit()">
              <span class="sar-row__icon" aria-hidden="true"><i class="fa-solid fa-clone"></i></span>
              <span class="sar-row__body"><span class="sar-row__title">{{ labels.duplicateSystem }}</span></span>
            </button>
          </li>
        }
        <li>
          <button
            type="button"
            class="sar-row"
            data-action="export"
            [disabled]="!exportEnabled() || busy()"
            [pTooltip]="soon"
            [tooltipDisabled]="exportEnabled()"
            tooltipPosition="left"
            (click)="exportEnabled() && exportSystem.emit()">
            <span class="sar-row__icon" aria-hidden="true"><i class="fa-solid fa-file-export"></i></span>
            <span class="sar-row__body"><span class="sar-row__title">{{ labels.exportSystem }}</span></span>
            @if (!exportEnabled()) {
              <span class="sar-row__end"><span class="sar-badge">{{ soon }}</span></span>
            }
          </button>
        </li>
        <li>
          <button type="button" class="sar-row" data-action="share" disabled [pTooltip]="soon" tooltipPosition="left">
            <span class="sar-row__icon" aria-hidden="true"><i class="fa-solid fa-user-group"></i></span>
            <span class="sar-row__body"><span class="sar-row__title">{{ labels.shareTeam }}</span></span>
            <span class="sar-row__end"><span class="sar-badge">{{ soon }}</span></span>
          </button>
        </li>
        <li>
          <button
            type="button"
            class="sar-row sar-row--reset"
            data-action="reset"
            [disabled]="busy()"
            [pTooltip]="labels.resetConfirm"
            tooltipPosition="left"
            (click)="reset.emit()">
            <span class="sar-row__icon" aria-hidden="true"><i class="fa-solid fa-rotate-left"></i></span>
            <span class="sar-row__body"><span class="sar-row__title">{{ labels.resetConversation }}</span></span>
          </button>
        </li>
      </ul>
    </section>
  `
})
export class StudioAiQuickActionsComponent {
  /** `systemExportEnabled` des capabilities (PR 3.3) ; `false` ⇒ Export « Bientôt », Import/Dupliquer masqués. */
  readonly exportEnabled = input(false);
  readonly busy = input(false);

  readonly reset = output<void>();
  readonly importTemplate = output<void>();
  readonly duplicate = output<void>();
  readonly exportSystem = output<void>();

  protected readonly labels = STUDIO_AI_LABELS.rail;
  protected readonly soon = STUDIO_AI_LABELS.soon;
}
