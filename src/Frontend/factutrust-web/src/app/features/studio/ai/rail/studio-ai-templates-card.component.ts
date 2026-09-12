import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { SkeletonModule } from 'primeng/skeleton';
import { TooltipModule } from 'primeng/tooltip';
import { STUDIO_AI_LABELS, formatLabel } from '../studio-ai-labels';
import { StudioTemplateListItemDto } from '../studio-ai.models';

/** Nombre de modèles montrés dans le rail ; le reste vit dans la bibliothèque. */
export const RAIL_TEMPLATES_MAX = 3;

/**
 * Carte « Modèles de systèmes » du rail : les 3 premiers modèles du catalogue + « Voir tous ».
 * « Utiliser » remonte la clé à la page (qui confirme si un plan est en cours puis appelle
 * `store.createFromTemplate`). Rendue par le rail seulement si `templatesEnabled`.
 */
@Component({
  selector: 'app-studio-ai-templates-card',
  standalone: true,
  imports: [RouterLink, ButtonModule, SkeletonModule, TooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './studio-ai-rail.scss',
  template: `
    <section class="sar-card" [attr.aria-label]="labels.templates">
      <div class="sar-card__head">
        <h3 class="sar-card__title"><i class="fa-solid fa-layer-group" aria-hidden="true"></i>{{ labels.templates }}</h3>
        <a class="sar-card__link" routerLink="/studio/ai/templates">{{ labels.seeAll }}</a>
      </div>

      @if (loading()) {
        <p-skeleton width="100%" height="2.25rem" />
        <p-skeleton width="100%" height="2.25rem" />
      } @else if (error()) {
        <p class="sar-error" role="status">{{ error() }}</p>
      } @else if (!visible().length) {
        <p class="sar-empty">{{ labels.templatesEmpty }}</p>
      } @else {
        <ul class="sar-list">
          @for (tpl of visible(); track tpl.key) {
            <li class="sar-row" [attr.data-template-key]="tpl.key">
              <span class="sar-row__icon" aria-hidden="true"><i class="fa-solid fa-cubes"></i></span>
              <span class="sar-row__body">
                <span class="sar-row__title" [pTooltip]="tpl.description" tooltipPosition="left">{{ tpl.displayName }}</span>
                <span class="sar-row__meta">{{ tpl.category }} · {{ entityCount(tpl) }}</span>
              </span>
              <span class="sar-row__end">
                <p-button
                  [label]="labels.use"
                  size="small"
                  [text]="true"
                  [disabled]="busy()"
                  [attr.aria-label]="labels.use + ' : ' + tpl.displayName"
                  (onClick)="use.emit(tpl.key)" />
              </span>
            </li>
          }
        </ul>
      }
    </section>
  `
})
export class StudioAiTemplatesCardComponent {
  readonly templates = input<StudioTemplateListItemDto[]>([]);
  readonly loading = input(false);
  readonly error = input<string | null>(null);
  readonly busy = input(false);

  /** Clé du modèle à instancier. */
  readonly use = output<string>();

  protected readonly labels = STUDIO_AI_LABELS.rail;
  protected readonly visible = computed(() => this.templates().slice(0, RAIL_TEMPLATES_MAX));

  protected entityCount(tpl: StudioTemplateListItemDto): string {
    return formatLabel(STUDIO_AI_LABELS.templates.entities, { count: tpl.entityCount ?? 0 });
  }
}
