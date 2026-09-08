import { ChangeDetectionStrategy, Component, computed, inject, model, output, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { SkeletonModule } from 'primeng/skeleton';
import { TabsModule } from 'primeng/tabs';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { StudioAiCapabilitiesService } from '../studio-ai-capabilities.service';
import { STUDIO_AI_LABELS } from '../studio-ai-labels';
import { StudioAiSessionStore } from '../studio-ai-session.store';
import { StudioAiPreviewTab } from '../studio-ai.models';
import { counterChips } from '../studio-ai-spec.util';
import { StudioAiFormsTabComponent } from './studio-ai-forms-tab.component';
import { StudioAiMenuTabComponent } from './studio-ai-menu-tab.component';
import { StudioAiOverviewTabComponent } from './studio-ai-overview-tab.component';
import { StudioAiRelationsTabComponent } from './studio-ai-relations-tab.component';
import { StudioAiReportsTabComponent } from './studio-ai-reports-tab.component';
import { StudioAiSeedTabComponent } from './studio-ai-seed-tab.component';
import { StudioAiTablesTabComponent } from './studio-ai-tables-tab.component';

/**
 * Coquille de l'aperçu de la proposition (`/studio/ai`) : en-tête + 9 onglets (P1a, lecture seule).
 *
 * Trois états : aucun plan (placeholder), spec en cours de chargement (squelette), proposition
 * chargée (en-tête, compteurs, onglets). Le composant ne crée jamais rien : « Créer maintenant »
 * remonte `confirmRequested` pour que la page ouvre le dialogue de confirmation, seul chemin vers
 * `POST {id}/confirm`. « Modifier » est présent mais désactivé : l'édition détaillée arrive en P1b.
 */
@Component({
  selector: 'app-studio-ai-preview',
  standalone: true,
  imports: [
    ButtonModule, SkeletonModule, TabsModule, TagModule, TooltipModule,
    StudioAiOverviewTabComponent, StudioAiTablesTabComponent, StudioAiRelationsTabComponent,
    StudioAiFormsTabComponent, StudioAiSeedTabComponent, StudioAiReportsTabComponent,
    StudioAiMenuTabComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './studio-ai-preview.scss',
  template: `
    <section class="sai-panel" aria-labelledby="sai-preview-title">
      <header class="sai-head">
        <span class="sai-empty__icon" aria-hidden="true"><i class="fa-solid fa-diagram-project"></i></span>
        <div class="sai-head__text">
          <h2 class="sai-title" id="sai-preview-title">{{ headerTitle() }}</h2>
          <p class="sai-sub">{{ headerSubtitle() }}</p>
        </div>
        @if (store.plan()) {
          <div class="sai-head__actions">
            <p-button
              [label]="labels.edit"
              icon="fa-solid fa-sliders"
              [outlined]="true"
              [disabled]="true"
              [pTooltip]="labels.readOnlyHint" />
            <p-button
              [label]="labels.cancel"
              icon="fa-solid fa-xmark"
              severity="secondary"
              [text]="true"
              (onClick)="store.cancelPlan()" />
            <p-button
              [label]="labels.createNow"
              icon="fa-solid fa-check"
              severity="success"
              [disabled]="!store.canConfirm()"
              [pTooltip]="confirmTooltip()"
              (onClick)="confirmRequested.emit()" />
          </div>
        }
      </header>

      @if (!previewEnabled()) {
        <div class="sai-banner" role="status">
          <i class="fa-solid fa-circle-info" aria-hidden="true"></i>
          <span>{{ capabilityLabels.previewDisabled }}</span>
        </div>
      }
      @if (store.error(); as error) {
        <div class="sai-banner sai-banner--error" role="alert">
          <i class="fa-solid fa-circle-exclamation" aria-hidden="true"></i>
          <span>{{ error }}</span>
        </div>
      }
      @if (store.warnings().length) {
        <div class="sai-banner sai-banner--warn">
          <i class="fa-solid fa-triangle-exclamation" aria-hidden="true"></i>
          <div>
            <strong>{{ store.warnings().length }} {{ labels.warningsTitle }}</strong>
            <ul>
              @for (warning of store.warnings(); track warning) {
                <li>{{ warning }}</li>
              }
            </ul>
          </div>
        </div>
      }

      @if (!store.plan()) {
        <div class="sai-empty">
          <span class="sai-empty__icon" aria-hidden="true"><i class="fa-solid fa-wand-magic-sparkles"></i></span>
          <strong>{{ labels.emptyTitle }}</strong>
          <span>{{ labels.emptyHint }}</span>
        </div>
      } @else if (store.specLoading() || !spec()) {
        <div class="sai-body" aria-busy="true" aria-live="polite">
          <p class="sai-hint">{{ labels.loadingSpec }}</p>
          <p-skeleton width="45%" height="1.25rem" styleClass="mb-3" />
          <p-skeleton width="100%" height="2.5rem" styleClass="mb-3" />
          <p-skeleton width="100%" height="12rem" />
        </div>
      } @else {
        <div class="sai-chips">
          @for (chip of chips(); track chip.label) {
            <span class="sai-chip"><span class="sai-chip__num">{{ chip.value }}</span> {{ chip.label }}</span>
          }
        </div>

        <p-tabs [value]="activeTab()" [scrollable]="true" (valueChange)="onTabChange($event)">
          <p-tablist>
            @for (tab of tabs; track tab.id) {
              <p-tab [value]="tab.id" [disabled]="!tab.available">
                <span
                  class="sai-tab-label"
                  [class.sai-tab-label--soon]="!tab.available"
                  [pTooltip]="tab.soonTooltip ?? ''"
                  [tooltipDisabled]="tab.available"
                  tooltipPosition="bottom"
                  (click)="onTabLabelClick($event, tab.available)">
                  <i [class]="tab.icon" aria-hidden="true"></i>
                  {{ tab.label }}
                  @if (tabCount(tab.id) !== null) {
                    <span class="sai-count">{{ tabCount(tab.id) }}</span>
                  }
                  @if (!tab.available) {
                    <p-tag class="sai-soon" severity="warn" [value]="soon" />
                  }
                </span>
              </p-tab>
            }
          </p-tablist>
          <p-tabpanels>
            <p-tabpanel value="overview">
              <app-studio-ai-overview-tab
                [spec]="spec()!"
                [counters]="store.counters()"
                [warnings]="store.warnings()"
                (openEntity)="selectEntity($event)" />
            </p-tabpanel>
            <p-tabpanel value="tables">
              <app-studio-ai-tables-tab [spec]="spec()!" [selectedRef]="selectedRef()" />
            </p-tabpanel>
            <p-tabpanel value="relations">
              <app-studio-ai-relations-tab [spec]="spec()!" />
            </p-tabpanel>
            <p-tabpanel value="forms">
              <app-studio-ai-forms-tab [spec]="spec()!" />
            </p-tabpanel>
            <p-tabpanel value="seed">
              <app-studio-ai-seed-tab [spec]="spec()!" />
            </p-tabpanel>
            <p-tabpanel value="reports">
              <app-studio-ai-reports-tab [spec]="spec()!" />
            </p-tabpanel>
            <p-tabpanel value="menu">
              <app-studio-ai-menu-tab [spec]="spec()!" />
            </p-tabpanel>
          </p-tabpanels>
        </p-tabs>
      }
    </section>
  `
})
export class StudioAiPreviewComponent {
  readonly store = inject(StudioAiSessionStore);
  private readonly capabilitiesService = inject(StudioAiCapabilitiesService);

  /** Onglet actif ; la page peut le prérégler selon l'intention (« Rapport » → `reports`). */
  readonly activeTab = model<StudioAiPreviewTab>('overview');
  /** Demande d'ouverture du dialogue de confirmation (la page décide, cf. WP‑B). */
  readonly confirmRequested = output<void>();
  /** Table choisie dans la Vue d'ensemble, relayée à la page (elle synchronise son onglet). */
  readonly openEntity = output<string>();

  readonly labels = STUDIO_AI_LABELS.preview;
  readonly capabilityLabels = STUDIO_AI_LABELS.capabilities;
  readonly soon = STUDIO_AI_LABELS.soon;
  readonly tabs = STUDIO_AI_LABELS.tabs;

  /** Table à mettre en avant dans l'onglet Tables (clic dans la Vue d'ensemble). */
  readonly selectedRef = signal<string | null>(null);

  readonly spec = computed(() => this.store.draft() ?? this.store.spec());
  readonly previewEnabled = computed(() => this.capabilitiesService.capabilities().planPreviewEnabled);

  readonly headerTitle = computed(
    () => this.spec()?.system?.displayName || this.store.plan()?.summary.title || this.labels.title
  );

  readonly headerSubtitle = computed(() => {
    if (!this.store.plan()) return this.labels.emptyHint;
    const description = this.spec()?.system?.description;
    return description ? `${description} · ${this.labels.nothingCreated}` : this.labels.nothingCreated;
  });

  readonly confirmTooltip = computed(() => (this.store.canConfirm() ? '' : this.labels.integrateDisabledDirty));

  readonly chips = computed(() => counterChips(this.store.counters()));

  /** Compteur affiché dans l'onglet ; `null` = pas de compteur (Vue d'ensemble, Workflow, Pages). */
  tabCount(id: StudioAiPreviewTab): number | null {
    const counters = this.store.counters();
    switch (id) {
      case 'tables': return counters.entities;
      case 'relations': return counters.relations;
      case 'forms': return counters.forms;
      case 'seed': return counters.seedRecords;
      case 'reports': return counters.reports;
      default: return null;
    }
  }

  onTabChange(value: string | number): void {
    const next = value as StudioAiPreviewTab;
    if (!this.tabs.find(t => t.id === next)?.available) return;
    this.activeTab.set(next);
  }

  /**
   * PrimeNG pose `pointer-events: none` sur un onglet désactivé (et ses enfants), ce qui empêche le
   * tooltip « Bientôt » de s'afficher. On réactive les événements sur le libellé seul et on stoppe
   * le clic pour qu'il ne remonte pas au `p-tab` (dont `onClick` n'est pas gardé par `disabled`).
   */
  onTabLabelClick(event: Event, available: boolean): void {
    if (!available) event.stopPropagation();
  }

  /** Clic sur une table de la Vue d'ensemble : bascule sur l'onglet Tables, table sélectionnée. */
  selectEntity(ref: string): void {
    this.selectedRef.set(ref);
    this.activeTab.set('tables');
    this.openEntity.emit(ref);
  }
}
