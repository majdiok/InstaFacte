import { ChangeDetectionStrategy, Component, computed, effect, input, signal } from '@angular/core';
import { STUDIO_AI_LABELS } from '../studio-ai-labels';
import {
  STUDIO_SPEC_LIMITS,
  StudioSpecEntity,
  StudioSpecField,
  StudioSystemSpec,
  relationTargetName
} from '../studio-ai.models';

/**
 * Onglet « Tables » (lecture, P1a) : liste des tables à gauche, grille des champs de la table
 * sélectionnée à droite (Libellé · Clé · Type · Requis · Unique · Détails).
 *
 * Table HTML plutôt que `p-table` : la grille est dense et purement descriptive, ce qui évite le
 * poids de style d'un `p-datatable` (budget `anyComponentStyle`). L'éditeur (glisser-déposer,
 * `p-select` de type, puces d'options éditables) arrive en P1b via `editable`.
 */
@Component({
  selector: 'app-studio-ai-tables-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './studio-ai-preview.scss',
  template: `
    @if (!entities().length) {
      <p class="sai-hint">{{ labels.noEntities }}</p>
    } @else {
      <div class="sai-grid" style="grid-template-columns: 15rem minmax(0, 1fr)">
        <ul class="sai-list">
          @for (entity of entities(); track entity.ref) {
            <li>
              <button
                type="button"
                class="sai-list__item"
                [class.sai-list__item--active]="entity.ref === activeRef()"
                [class.sai-list__item--highlight]="entity.ref === highlightedRef()"
                [attr.aria-pressed]="entity.ref === activeRef()"
                (click)="select(entity.ref)">
                <i [class]="entity.icon || 'fa-solid fa-table'" aria-hidden="true"></i>
                <span>
                  <span [class.sai-highlight]="entity.ref === highlightedRef()">{{ entity.displayName }}</span>
                  @if (entity.existingKey) {
                    <span class="sai-code sai-existing" style="display: block">↳ {{ entity.existingKey }}</span>
                  }
                  <span class="sai-code" style="display: block">{{ entity.ref }}</span>
                </span>
                <span class="sai-list__meta">{{ entity.fields.length }} {{ labels.fields }}</span>
              </button>
            </li>
          }
        </ul>

        @if (activeEntity(); as entity) {
          <div>
            <div class="sai-block__head">
              <i [class]="entity.icon || 'fa-solid fa-table'" aria-hidden="true"></i>
              <span [class.sai-highlight]="entity.ref === highlightedRef()">{{ entity.displayName }}</span>
              <span class="sai-code">{{ entity.ref }}</span>
              <span class="sai-list__meta">{{ entity.fields.length }} / {{ maxFields }} {{ labels.fields }}</span>
            </div>
            @if (entity.description) {
              <p class="sai-hint">{{ entity.description }}</p>
            }
            <div class="sai-table__scroll">
              <table class="sai-table">
                <thead>
                  <tr>
                    <th scope="col">{{ labels.colLabel }}</th>
                    <th scope="col">{{ labels.colKey }}</th>
                    <th scope="col">{{ labels.colType }}</th>
                    <th scope="col">{{ labels.colRequired }}</th>
                    <th scope="col">{{ labels.colUnique }}</th>
                    <th scope="col">{{ labels.colDetails }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (field of entity.fields; track field.key) {
                    <tr>
                      <td>{{ field.label }}</td>
                      <td class="sai-code">{{ field.key }}</td>
                      <td>{{ typeLabel(field) }}</td>
                      <td>
                        @if (field.required) {
                          <i class="fa-solid fa-check sai-ok" [attr.aria-label]="labels.colRequired"></i>
                        } @else {
                          <span class="sai-dash" aria-hidden="true">—</span>
                        }
                      </td>
                      <td>
                        @if (field.unique) {
                          <i class="fa-solid fa-check sai-ok" [attr.aria-label]="labels.colUnique"></i>
                        } @else {
                          <span class="sai-dash" aria-hidden="true">—</span>
                        }
                      </td>
                      <td class="sai-hint">{{ details(field) }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }
      </div>
    }
  `
})
export class StudioAiTablesTabComponent {
  readonly spec = input.required<StudioSystemSpec>();
  /** `ref` de la table à déplier (envoyée par l'onglet Vue d'ensemble via la page). */
  readonly selectedRef = input<string | null>(null);
  /** Réservé à P1b : l'onglet reste en lecture seule en P1a. */
  readonly editable = input(false);
  /** `ref` de la table renommée « (2) » après « Créer quand même » (R21) : libellé en surbrillance. */
  readonly highlightedRef = input<string | null>(null);

  readonly labels = STUDIO_AI_LABELS.preview;
  readonly maxFields = STUDIO_SPEC_LIMITS.maxFields;

  private readonly clicked = signal<string | null>(null);

  readonly entities = computed(() => this.spec().entities);
  readonly activeRef = computed(() => {
    const entities = this.entities();
    const wanted = this.clicked() ?? this.selectedRef();
    if (wanted && entities.some(e => e.ref === wanted)) return wanted;
    return entities[0]?.ref ?? null;
  });
  readonly activeEntity = computed<StudioSpecEntity | null>(
    () => this.entities().find(e => e.ref === this.activeRef()) ?? null
  );

  constructor() {
    // Une nouvelle demande d'ouverture (clic dans la Vue d'ensemble) reprend la main sur la
    // sélection locale de l'utilisateur.
    effect(() => {
      this.selectedRef();
      this.clicked.set(null);
    });
  }

  select(ref: string): void {
    this.clicked.set(ref);
  }

  typeLabel(field: StudioSpecField): string {
    return STUDIO_AI_LABELS.fieldTypes[field.type] ?? field.type;
  }

  /** Colonne « Détails » : options d'une liste, cible d'une relation, ou réglage de configuration. */
  details(field: StudioSpecField): string {
    if (field.type === 'relation') {
      const target = relationTargetName(this.spec(), field.relationTo);
      return target.erp
        ? `→ ${target.name} (${STUDIO_AI_LABELS.preview.erpBadge})`
        : `→ ${target.name || '—'}`;
    }
    if (field.options?.length) {
      return field.options.map(o => o.label || o.value).join(' · ');
    }
    const config = field.config;
    if (config?.currency) return String(config.currency);
    if (typeof config?.max === 'number') return `max ${config.max}`;
    if (config?.format) return String(config.format);
    return '—';
  }
}
