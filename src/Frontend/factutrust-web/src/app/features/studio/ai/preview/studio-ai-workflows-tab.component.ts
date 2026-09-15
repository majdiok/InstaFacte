import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { STUDIO_AI_LABELS } from '../studio-ai-labels';
import { StudioSystemSpec } from '../studio-ai.models';

/** Workflow lu de façon défensive (`spec.workflows[]` ou `entity.workflow`), forme non contractuelle avant 4.x. */
export interface StudioAiWorkflowRow {
  id: string;
  name: string;
  entityName: string;
  steps: number | null;
}

/**
 * Onglet « Workflows » (M4, P4) : état vide tant que la spec n'en contient pas ; rendu générique
 * (nom, table, nombre d'étapes) si `spec.workflows[]` ou `entity.workflow` sont présents.
 * La timeline réelle arrive avec le programme 4.4.
 */
@Component({
  selector: 'app-studio-ai-workflows-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './studio-ai-preview.scss',
  template: `
    <div class="sai-body">
      @if (!workflows().length) {
        <div class="sai-empty" data-component-id="sai-workflows-empty">
          <span class="sai-empty__icon" aria-hidden="true"><i class="fa-solid fa-route"></i></span>
          <strong>{{ labels.emptyTitle }}</strong>
          <span>{{ labels.emptyHint }}</span>
        </div>
      } @else {
        <ul class="sai-list">
          @for (workflow of workflows(); track workflow.id) {
            <li class="sai-list__item sai-list__item--static">
              <i class="fa-solid fa-route" aria-hidden="true"></i>
              <span>{{ workflow.name }}</span>
              <span class="sai-code">{{ workflow.entityName }}</span>
              @if (workflow.steps !== null) {
                <span class="sai-list__meta">{{ stepsLabel(workflow.steps) }}</span>
              }
            </li>
          }
        </ul>
      }
    </div>
  `
})
export class StudioAiWorkflowsTabComponent {
  readonly spec = input.required<StudioSystemSpec>();

  readonly labels = STUDIO_AI_LABELS.workflows;

  stepsLabel(count: number): string {
    return this.labels.steps.replace('{count}', String(count));
  }

  readonly workflows = computed<StudioAiWorkflowRow[]>(() => {
    const spec = this.spec();
    const rows: StudioAiWorkflowRow[] = [];
    const root = spec['workflows'];
    if (Array.isArray(root)) {
      root.forEach((raw, index) => {
        const row = this.toRow(raw, `root:${index}`, spec);
        if (row) rows.push(row);
      });
    }
    for (const entity of spec.entities) {
      const row = this.toRow(entity['workflow'], `entity:${entity.ref}`, spec, entity.displayName);
      if (row) rows.push(row);
    }
    return rows;
  });

  private toRow(raw: unknown, id: string, spec: StudioSystemSpec, fallbackEntity = ''): StudioAiWorkflowRow | null {
    if (!raw || typeof raw !== 'object') return null;
    const record = raw as Record<string, unknown>;
    const name = this.str(record['name']) ?? this.str(record['displayName']) ?? this.str(record['key']) ?? this.labels.title;
    const entityRef = this.str(record['entity']) ?? this.str(record['entityRef']) ?? this.str(record['table']);
    const entity = entityRef ? spec.entities.find(e => e.ref === entityRef) : undefined;
    const steps = record['steps'] ?? record['states'] ?? record['statuses'];
    return {
      id,
      name,
      entityName: entity?.displayName ?? entityRef ?? fallbackEntity,
      steps: Array.isArray(steps) ? steps.length : null
    };
  }

  private str(raw: unknown): string | undefined {
    return typeof raw === 'string' && raw.trim() ? raw.trim() : undefined;
  }
}
