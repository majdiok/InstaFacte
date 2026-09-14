import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TableLazyLoadEvent } from 'primeng/table';
import { MessageService } from 'primeng/api';
import { DynamicTableComponent, DynamicRow } from '@shared/studio-runtime/dynamic-table.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { CustomField } from '@shared/studio-runtime/studio-runtime.models';
import { StudioRecordViewsService } from './studio-record-views.service';
import { CustomRecordViewDto, RecordViewRunResultDto } from './studio-record-views.models';
import { STUDIO_RUNTIME_LABELS } from '../shared/studio-runtime-labels';

/**
 * Exécute une vue enregistrée (`POST /run`) et affiche le résultat.
 * Mode Liste : rendu via `app-dynamic-table` (mêmes colonnes/pagination que la vue « Liste »
 * historique). Modes Kanban / Calendrier : bandeau « Bientôt » (corps réel en 2.5b).
 */
@Component({
  selector: 'app-studio-record-view-runner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, DynamicTableComponent, EmptyStateComponent],
  template: `
    @switch (view()?.mode) {
      @case ('List') {
        @if (error()) {
          <app-empty-state
            icon="pi-exclamation-triangle"
            [title]="labels.views.error"
            [showAction]="true"
            actionLabel="Réessayer"
            (actionClick)="run()" />
        } @else {
          @if (result()?.truncated) {
            <div class="runner-banner">
              <i class="fa-solid fa-circle-info"></i>
              <span>{{ labels.views.truncatedGeneric }}</span>
            </div>
          }
          <app-dynamic-table
            [entityKey]="entityKey()"
            [allFields]="allFields()"
            [columns]="columns()"
            [columnPicker]="false"
            [value]="rows()"
            [total]="result()?.total ?? 0"
            [pageSize]="pageSize()"
            [loading]="loading()"
            [showActions]="showActions()"
            (lazyLoad)="onLazy($event)"
            (editRow)="editRow.emit($event)"
            (deleteRow)="deleteRow.emit($event)" />
        }
      }
      @case ('Kanban') {
        <div class="runner-soon"><i class="fa-solid fa-table-columns"></i> Vue Kanban — {{ labels.views.soon }}</div>
      }
      @case ('Calendar') {
        <div class="runner-soon"><i class="fa-solid fa-calendar-days"></i> Vue Calendrier — {{ labels.views.soon }}</div>
      }
    }
  `,
  styleUrl: '../shared/studio-layout.scss'
})
export class StudioRecordViewRunnerComponent {
  private readonly viewsService = inject(StudioRecordViewsService);
  private readonly toast = inject(MessageService);

  readonly entityKey = input('');
  readonly view = input<CustomRecordViewDto | null>(null);
  readonly allFields = input<CustomField[]>([]);
  readonly showActions = input(true);

  readonly editRow = output<DynamicRow>();
  readonly deleteRow = output<DynamicRow>();

  protected readonly labels = STUDIO_RUNTIME_LABELS;

  protected readonly loading = signal(false);
  protected readonly error = signal(false);
  protected readonly result = signal<RecordViewRunResultDto | null>(null);

  private page = 1;
  protected readonly pageSize = signal(25);
  // Écho de montage de `p-table` (lazy+paginator) : évite un second POST /run identique (même
  // schéma que `StudioRecordListComponent.onLazy`).
  private fetched = false;

  protected readonly rows = computed<DynamicRow[]>(() => this.result()?.items ?? []);

  protected readonly columns = computed<CustomField[]>(() => {
    const v = this.view();
    const fields = this.allFields();
    if (!v) return fields;
    const byKey = new Map(fields.map(f => [f.key, f]));
    return v.definition.columns
      .filter(c => !c.hidden)
      .map(c => byKey.get(c.fieldKey))
      .filter((f): f is CustomField => !!f);
  });

  constructor() {
    effect(() => {
      const v = this.view();
      const key = this.entityKey();
      if (!v || !key) {
        this.result.set(null);
        return;
      }
      this.page = 1;
      this.pageSize.set(v.definition.pageSize || 25);
      this.fetched = false;
      // Kanban/Calendrier : panneau « Bientôt » (2.5b) — pas d'appel réseau tant que non consommé.
      if (v.mode === 'List') this.run();
      else this.result.set(null);
    });
  }

  onLazy(event: TableLazyLoadEvent): void {
    const first = event.first ?? 0;
    const rows = event.rows ?? this.pageSize();
    const nextPage = Math.floor(first / rows) + 1;
    // Ignore l'écho de montage de la table une fois le premier chargement effectué.
    if (this.fetched && nextPage === this.page && rows === this.pageSize()) return;
    this.pageSize.set(rows);
    this.page = nextPage;
    this.run();
  }

  run(): void {
    const v = this.view();
    const key = this.entityKey();
    if (!v || !key) return;
    this.fetched = true;
    this.loading.set(true);
    this.error.set(false);
    this.viewsService.runRecordView(key, v.id, { page: this.page, pageSize: this.pageSize() }).subscribe({
      next: res => {
        this.loading.set(false);
        if (res.success) this.result.set(res.data);
        else this.error.set(true);
      },
      error: () => {
        this.loading.set(false);
        this.error.set(true);
        this.toast.add({ severity: 'error', summary: 'Erreur', detail: this.labels.views.error });
      }
    });
  }
}
