import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BadgeModule } from 'primeng/badge';
import { ButtonModule } from 'primeng/button';
import { SkeletonModule } from 'primeng/skeleton';
import { TooltipModule } from 'primeng/tooltip';
import { STUDIO_WORKFLOW_LABELS } from './studio-workflow-labels';
import { StudioWorkflowStatusTagComponent } from './studio-workflow-status-tag.component';
import { WorkflowInstanceDto, isOpenInstance } from './studio-workflows.models';
import { StudioWorkflowsService } from './studio-workflows.service';

/**
 * Libellés FR locaux absents de `STUDIO_WORKFLOW_LABELS` (4.4a1, non modifié dans cette
 * tranche — même motif que `DESIGNER_LABELS` du concepteur, 4.4e1) : `L.instances.title` et
 * `L.instances.loadError`, cités par l'annexe, n'existent pas — titre remplacé par
 * `L.instances.recent`, erreur par le libellé local ci-dessous.
 */
const PANEL_LABELS = {
  loadError: 'Chargement des instances impossible.',
  retry: 'Réessayer'
} as const;

/**
 * Panneau « Instances récentes » — colonne 3 du concepteur de workflow (4.4e2, maquette
 * `d44-workflows-designer.html`) : les 20 dernières instances (`listInstances(id, 20)`),
 * rechargées quand `workflowId`/`refreshToken` changent (le concepteur incrémente le jeton
 * après chaque enregistrement — pas de minuteur d'auto-rafraîchissement). Le clic sur une
 * ligne émet `open` (le concepteur pose `?instance=<id>` dans l'URL, D20 ; le drawer de
 * détail arrive en 4.4f). Le DTO n'expose pas de libellé d'enregistrement (D-44-24) :
 * identifiant tronqué + lien « Ouvrir la fiche » vers la route gardée `records/:key/:id`.
 */
@Component({
  selector: 'app-studio-workflow-instances-panel',
  standalone: true,
  imports: [DatePipe, RouterLink, BadgeModule, ButtonModule, SkeletonModule, TooltipModule, StudioWorkflowStatusTagComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wf-inst-head">
      <h3 class="wf-inst-head__title">{{ L.instances.recent }}</h3>
      @if (openCount() > 0) {
        <p-badge [value]="openCount()" severity="warn" data-testid="wf-instances-open-count" />
      }
    </div>

    @if (!workflowId()) {
      <p class="wf-inst__hint" data-testid="wf-instances-unsaved">{{ L.designer.unsavedInstances }}</p>
    } @else if (loading()) {
      <div class="wf-inst-skel" data-testid="wf-instances-loading">
        <p-skeleton height="2.75rem" />
        <p-skeleton height="2.75rem" />
        <p-skeleton height="2.75rem" />
      </div>
    } @else if (error()) {
      <div class="wf-inst__error" data-testid="wf-instances-error">
        <p class="wf-inst__hint">{{ panelLabels.loadError }}</p>
        <button pButton type="button" size="small" [outlined]="true" [label]="panelLabels.retry"
          data-testid="wf-instances-retry" (click)="retry()"></button>
      </div>
    } @else if (!instances().length) {
      <p class="wf-inst__hint" data-testid="wf-instances-empty">{{ L.instances.empty }}</p>
    } @else {
      <ul class="wf-inst" data-testid="wf-instances-list">
        @for (i of instances(); track i.id) {
          <li class="wf-inst__item">
            <button type="button" class="wf-inst__row" (click)="open.emit(i)" [attr.data-testid]="'wf-instance-' + i.id">
              <app-studio-workflow-status-tag [status]="i.status" [compact]="true" />
              <span class="wf-inst__body">
                <span class="wf-inst__rec" [title]="i.recordId">{{ shortId(i.recordId) }}</span>
                <span class="wf-inst__dates">
                  {{ i.startedAt | date:'dd/MM HH:mm' }}
                  @if (i.dueAt) {
                    · {{ L.instances.dueAt }} {{ i.dueAt | date:'dd/MM HH:mm' }}
                  }
                  @if (i.error) {
                    <i class="fa-solid fa-circle-exclamation wf-inst__err" [pTooltip]="i.error" aria-hidden="true"></i>
                  }
                </span>
              </span>
            </button>
            @if (entityKey(); as entityKey) {
              <a class="wf-inst__open" [routerLink]="['/studio/records', entityKey, i.recordId]"
                [attr.data-testid]="'wf-instance-open-' + i.id">{{ L.approvals.openRecord }}</a>
            }
          </li>
        }
      </ul>
    }
  `,
  styles: [`
    .wf-inst-head { display: flex; align-items: center; gap: var(--spacing-2, 0.5rem); }
    .wf-inst-head__title { margin: 0; font-size: 0.9375rem; font-weight: 600; }
    .wf-inst__hint { margin: 0; color: var(--color-neutral-500, #64748b); font-size: var(--font-size-sm, 0.8125rem); }
    .wf-inst-skel { display: flex; flex-direction: column; gap: var(--spacing-2, 0.5rem); }
    .wf-inst__error { display: flex; flex-direction: column; align-items: flex-start; gap: var(--spacing-2, 0.5rem); }
    .wf-inst { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: .25rem; }
    .wf-inst__item { display: flex; flex-direction: column; }
    .wf-inst__row { display: grid; grid-template-columns: auto 1fr; gap: .5rem; width: 100%; text-align: left; background: none; border: 1px solid transparent; border-radius: var(--radius-md); padding: .4rem .5rem; cursor: pointer; font: inherit; color: inherit; }
    .wf-inst__row:hover { border-color: var(--color-border-subtle); }
    .wf-inst__body { display: flex; flex-direction: column; gap: 1px; min-width: 0; }
    .wf-inst__rec { font-weight: 600; font-size: 0.8125rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .wf-inst__dates { display: inline-flex; align-items: center; gap: .25rem; font-size: 0.75rem; color: var(--color-neutral-500, #64748b); }
    .wf-inst__err { color: var(--red-500, #ef4444); }
    .wf-inst__open { align-self: flex-start; margin: 0 .5rem .25rem 2rem; font-size: 0.75rem; }
  `]
})
export class StudioWorkflowInstancesPanelComponent {
  private readonly workflowsSvc = inject(StudioWorkflowsService);

  readonly workflowId = input.required<string | null>();
  /** Clé de la table (pour le lien « Ouvrir la fiche ») — le concepteur la connaît via `entity().key`. */
  readonly entityKey = input<string | null>(null);
  readonly refreshToken = input(0);
  readonly open = output<WorkflowInstanceDto>();

  protected readonly L = STUDIO_WORKFLOW_LABELS;
  protected readonly panelLabels = PANEL_LABELS;
  protected readonly instances = signal<WorkflowInstanceDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal(false);
  /** Instances ouvertes (badge d'en-tête) : running / waiting / waiting_approval. */
  protected readonly openCount = computed(() => this.instances().filter(i => isOpenInstance(i.status)).length);

  /** Dernier couple (workflowId, refreshToken) chargé — garde anti double-déclenchement de l'effect. */
  private lastKey: string | null = null;

  constructor() {
    effect(() => {
      const id = this.workflowId();
      const token = this.refreshToken();
      const key = `${id ?? ''}|${token}`;
      if (key === this.lastKey) return;
      this.lastKey = key;
      untracked(() => this.load(id));
    });
  }

  private load(id: string | null): void {
    if (!id) { this.instances.set([]); return; }
    this.loading.set(true);
    this.error.set(false);
    // GET de conception sans skipErrorUi (§0.5) : l'intercepteur global affiche le toast,
    // le panneau affiche un état d'erreur discret.
    this.workflowsSvc.listInstances(id, 20).subscribe({
      next: r => { this.instances.set(r.data ?? []); this.loading.set(false); },
      error: () => { this.error.set(true); this.loading.set(false); }
    });
  }

  /** Bouton « Réessayer » de l'état d'erreur. */
  protected retry(): void {
    this.load(this.workflowId());
  }

  /** Identifiant d'enregistrement tronqué (pas de libellé dans le DTO — D-44-24) ; le `title` porte l'identifiant complet. */
  protected shortId(recordId: string): string {
    return recordId.length > 8 ? recordId.slice(0, 8) + '…' : recordId;
  }
}
