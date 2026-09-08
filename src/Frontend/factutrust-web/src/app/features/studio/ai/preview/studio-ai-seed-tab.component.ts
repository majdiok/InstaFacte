import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TableModule } from 'primeng/table';
import { STUDIO_AI_LABELS, formatLabel } from '../studio-ai-labels';
import { STUDIO_SPEC_LIMITS, StudioSystemSpec } from '../studio-ai.models';

/** Un bloc de données de référence prêt à afficher (10 premières valeurs seulement). */
interface StudioAiSeedBlock {
  entityRef: string;
  entityName: string;
  count: number;
  columns: { key: string; label: string }[];
  rows: Record<string, unknown>[];
  truncated: string | null;
}

/**
 * Onglet « Données de référence » (lecture, P1a) : par table, le nombre de valeurs initiales et un
 * extrait des 10 premières lignes.
 *
 * Les colonnes sont les champs de la table réellement présents dans les enregistrements : le
 * générateur n'alimente pas toujours tous les champs, et une colonne vide n'apprend rien. La grille
 * éditable, l'import CSV et le collage depuis un tableur arrivent en P1b.
 */
@Component({
  selector: 'app-studio-ai-seed-tab',
  standalone: true,
  imports: [TableModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './studio-ai-preview.scss',
  template: `
    @if (!blocks().length) {
      <p class="sai-hint">{{ labels.noSeed }}</p>
    } @else {
      @for (block of blocks(); track block.entityRef) {
        <div class="sai-block">
          <div class="sai-block__head">
            <i class="fa-solid fa-database" aria-hidden="true"></i>
            <span>{{ labels.referenceDataOf }} {{ block.entityName }}</span>
            <span class="sai-list__meta">{{ block.count }} / {{ maxSeed }} {{ labels.seedValues }}</span>
          </div>
          <p-table [value]="block.rows" styleClass="p-datatable-sm" responsiveLayout="scroll">
            <ng-template pTemplate="header">
              <tr>
                @for (col of block.columns; track col.key) {
                  <th scope="col">{{ col.label }}</th>
                }
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-row>
              <tr>
                @for (col of block.columns; track col.key) {
                  <td>{{ cell(row, col.key) }}</td>
                }
              </tr>
            </ng-template>
          </p-table>
          @if (block.truncated) {
            <p class="sai-hint">{{ block.truncated }}</p>
          }
        </div>
      }
    }
  `
})
export class StudioAiSeedTabComponent {
  readonly spec = input.required<StudioSystemSpec>();
  /** Réservé à P1b : l'onglet reste en lecture seule en P1a. */
  readonly editable = input(false);

  readonly labels = STUDIO_AI_LABELS.preview;
  readonly maxSeed = STUDIO_SPEC_LIMITS.maxSeedRecords;

  readonly blocks = computed<StudioAiSeedBlock[]>(() => {
    const spec = this.spec();
    return (spec.seed ?? [])
      .filter(seed => (seed.records?.length ?? 0) > 0)
      .map(seed => {
        const entity = spec.entities.find(e => e.ref === seed.entityRef);
        const records = seed.records ?? [];
        const rows = records.slice(0, 10);
        const used = new Set<string>();
        for (const record of rows) {
          for (const key of Object.keys(record)) used.add(key);
        }
        const declared = (entity?.fields ?? []).filter(f => used.has(f.key)).map(f => ({ key: f.key, label: f.label }));
        const extra = [...used]
          .filter(key => !declared.some(c => c.key === key))
          .map(key => ({ key, label: key }));
        return {
          entityRef: seed.entityRef,
          entityName: entity?.displayNamePlural || entity?.displayName || seed.entityRef,
          count: records.length,
          columns: [...declared, ...extra],
          rows,
          truncated: records.length > rows.length
            ? formatLabel(STUDIO_AI_LABELS.preview.seedTruncated, { count: records.length })
            : null
        };
      });
  });

  /** Rend une valeur de seed lisible sans dépendre du type déclaré (le JSON peut être partiel). */
  cell(row: Record<string, unknown>, key: string): string {
    const value = row?.[key];
    if (value === null || value === undefined || value === '') return '—';
    if (typeof value === 'boolean') return value ? 'Oui' : 'Non';
    if (typeof value === 'object') return JSON.stringify(value);
    return String(value);
  }
}
