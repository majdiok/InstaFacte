import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TagModule } from 'primeng/tag';
import { STUDIO_AI_LABELS } from '../studio-ai-labels';
import {
  StudioSpecEntity,
  StudioSpecRecordView,
  StudioSystemSpec,
  normalizeViewMode,
  viewDisplayName
} from '../studio-ai.models';
import { StudioAiViewMiniatureComponent } from './studio-ai-view-miniature.component';

/** Une vue prête à afficher : mode normalisé, puces déjà résolues en libellés de champ. */
export interface StudioAiViewCard {
  id: string;
  entity: StudioSpecEntity;
  entityName: string;
  view: StudioSpecRecordView;
  name: string;
  mode: 'list' | 'kanban' | 'calendar';
  modeLabel: string;
  isDefault: boolean;
  chips: string[];
  seed: Record<string, unknown>[];
}

const MODE_ICONS: Record<StudioAiViewCard['mode'], string> = {
  list: 'fa-solid fa-list',
  kanban: 'fa-solid fa-table-columns',
  calendar: 'fa-solid fa-calendar-days'
};

/**
 * Onglet « Vues » (M3, lecture) : une carte par vue enregistrée de chaque table, avec une miniature
 * statique, le mode (Liste / Kanban / Calendrier), les puces de configuration et « Par défaut ».
 *
 * Les alias français du mode (`liste`, `calendrier`) et du plan maître (`displayName`, `dateField`…)
 * sont tolérés en lecture via `normalizeViewMode` / `viewDisplayName`. L'édition est hors périmètre 3.4d.
 */
@Component({
  selector: 'app-studio-ai-views-tab',
  standalone: true,
  imports: [TagModule, StudioAiViewMiniatureComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './studio-ai-preview.scss',
  template: `
    <div class="sai-body">
      <div class="sai-block__head">
        <i class="fa-solid fa-eye" aria-hidden="true"></i>
        <span>{{ labels.title }}</span>
        <span class="sai-list__meta">{{ countLabel() }}</span>
      </div>
      @if (!cards().length) {
        <div class="sai-empty">
          <span class="sai-empty__icon" aria-hidden="true"><i class="fa-solid fa-eye-slash"></i></span>
          <strong>{{ labels.empty }}</strong>
        </div>
      } @else {
        <div class="sai-views">
          @for (card of cards(); track card.id) {
            <article class="sai-view" [attr.data-component-id]="'sai-view-' + card.mode" [attr.aria-label]="card.name">
              <app-studio-ai-view-miniature [view]="card.view" [entity]="card.entity" [seed]="card.seed" />
              <div class="sai-view__body">
                <div class="sai-view__title">
                  <i [class]="icons[card.mode]" aria-hidden="true"></i>
                  <span>{{ card.name }}</span>
                  <span class="sai-list__meta">{{ card.entityName }}</span>
                </div>
                <div class="sai-view__chips">
                  <p-tag severity="secondary" [value]="card.modeLabel" />
                  @for (chip of card.chips; track chip) {
                    <span class="sai-chip sai-chip--xs">{{ chip }}</span>
                  }
                  @if (card.isDefault) {
                    <p-tag severity="info" [value]="labels.isDefault" />
                  }
                </div>
              </div>
            </article>
          }
        </div>
      }
    </div>
  `
})
export class StudioAiViewsTabComponent {
  readonly spec = input.required<StudioSystemSpec>();
  /** Réservé à 3.4g+ : l'onglet reste en lecture seule en 3.4d. */
  readonly editable = input(false);

  readonly labels = STUDIO_AI_LABELS.views;
  readonly icons = MODE_ICONS;

  readonly cards = computed<StudioAiViewCard[]>(() => {
    const spec = this.spec();
    const cards: StudioAiViewCard[] = [];
    for (const entity of spec.entities) {
      const seed = spec.seed?.find(s => s.entityRef === entity.ref)?.records ?? [];
      (entity.views ?? []).forEach((view, index) => cards.push(this.toCard(entity, view, index, seed)));
    }
    return cards;
  });

  readonly countLabel = computed(() => this.labels.count.replace('{count}', String(this.cards().length)));

  private toCard(
    entity: StudioSpecEntity,
    view: StudioSpecRecordView,
    index: number,
    seed: Record<string, unknown>[]
  ): StudioAiViewCard {
    const mode = normalizeViewMode(view.mode);
    const label = (key: string | undefined): string =>
      (key ? entity.fields.find(f => f.key === key)?.label ?? key : '');
    const alias = (key: string): string | undefined => {
      const raw = view[key];
      return typeof raw === 'string' && raw ? raw : undefined;
    };
    const chips: string[] = [];
    const push = (title: string, value: string | undefined) => {
      if (value) chips.push(`${title} : ${value}`);
    };
    push(this.labels.groupBy, view.groupBy ? label(view.groupBy) : undefined);
    const start = view.start ?? alias('dateField');
    push(this.labels.dateField, start ? label(start) : undefined);
    const end = view.end ?? alias('endDateField');
    push(this.labels.endField, end ? label(end) : undefined);
    const title = view.title ?? alias('titleField');
    push(this.labels.titleField, title ? label(title) : undefined);
    if (view.columns?.length) push(this.labels.columns, view.columns.map(label).join(', '));
    if (view.sort?.length) {
      push(this.labels.sort, view.sort.map(s => `${label(s.field)} ${(s.desc ?? s.descending) ? '↓' : '↑'}`).join(', '));
    }
    if (view.filters?.length) push(this.labels.filters, String(view.filters.length));

    return {
      id: `${entity.ref}:${index}`,
      entity,
      entityName: entity.displayNamePlural || entity.displayName,
      view,
      name: viewDisplayName(view) || entity.displayName,
      mode,
      modeLabel: this.labels[mode],
      isDefault: view.isDefault === true,
      chips,
      seed
    };
  }
}
