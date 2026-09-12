import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TagModule } from 'primeng/tag';
import { STUDIO_AI_LABELS } from '../studio-ai-labels';
import { StudioSpecEntity, StudioSystemSpec } from '../studio-ai.models';

/** Un rapport prêt à afficher : toutes les puces sont déjà résolues en libellés de champ. */
interface StudioAiReportView {
  entityRef: string;
  entityName: string;
  displayName: string;
  groupBy: string[];
  measures: string[];
  columns: string[];
  filters: string[];
  sort: string[];
}

/**
 * Onglet « Rapports » (lecture, P1a) : une carte par table possédant un `report`, avec les
 * regroupements, mesures, colonnes, filtres et tris exprimés avec les libellés des champs.
 *
 * Les clés techniques (`nb_jours`) sont remplacées par les libellés (« Nombre de jours ») : l'écran
 * sert à faire valider la proposition par un utilisateur métier. L'édition des puces est P1b.
 */
@Component({
  selector: 'app-studio-ai-reports-tab',
  standalone: true,
  imports: [TagModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './studio-ai-preview.scss',
  template: `
    @if (!reports().length) {
      <p class="sai-hint">{{ labels.noReports }}</p>
    } @else {
      @for (report of reports(); track report.entityRef) {
        <div class="sai-panel sai-body sai-block">
          <div class="sai-block__head">
            <i class="fa-solid fa-chart-column" aria-hidden="true"></i>
            <span>{{ report.displayName }}</span>
            <span class="sai-list__meta">{{ report.entityName }}</span>
          </div>
          @for (group of groupsOf(report); track group.title) {
            @if (group.values.length) {
              <div class="sai-block" style="margin-bottom: var(--spacing-2, 0.5rem)">
                <p class="sai-hint">{{ group.title }}</p>
                <div class="sai-tags">
                  @for (value of group.values; track value) {
                    <p-tag severity="secondary" [value]="value" />
                  }
                </div>
              </div>
            }
          }
        </div>
      }
    }
  `
})
export class StudioAiReportsTabComponent {
  readonly spec = input.required<StudioSystemSpec>();
  /** Réservé à P1b : l'onglet reste en lecture seule en P1a. */
  readonly editable = input(false);

  readonly labels = STUDIO_AI_LABELS.preview;

  readonly reports = computed<StudioAiReportView[]>(() =>
    this.spec()
      .entities.filter(e => !!e.report)
      .map(entity => this.toView(entity))
  );

  groupsOf(report: StudioAiReportView): { title: string; values: string[] }[] {
    return [
      { title: this.labels.groupedBy, values: report.groupBy },
      { title: this.labels.measures, values: report.measures },
      { title: this.labels.columns, values: report.columns },
      { title: this.labels.filters, values: report.filters },
      { title: this.labels.sort, values: report.sort }
    ];
  }

  private toView(entity: StudioSpecEntity): StudioAiReportView {
    const report = entity.report!;
    const label = (key: string | undefined): string =>
      (key ? entity.fields.find(f => f.key === key)?.label ?? key : '—');
    return {
      entityRef: entity.ref,
      entityName: entity.displayNamePlural || entity.displayName,
      displayName: report.displayName || entity.displayName,
      groupBy: (report.groupBy ?? []).map(label),
      measures: (report.measures ?? []).map(m => (m.field ? `${m.fn} (${label(m.field)})` : m.fn)),
      columns: (report.columns ?? []).map(label),
      filters: (report.filters ?? []).map(f => `${label(f.field)} ${f.op} ${this.value(f.value)}`),
      sort: (report.sort ?? []).map(s => `${label(s.field)} ${s.dir === 'desc' ? '↓' : '↑'}`)
    };
  }

  private value(raw: unknown): string {
    if (raw === null || raw === undefined) return '—';
    if (typeof raw === 'object') return JSON.stringify(raw);
    return String(raw);
  }
}
