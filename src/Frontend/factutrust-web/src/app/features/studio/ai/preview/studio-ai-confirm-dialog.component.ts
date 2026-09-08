import { ChangeDetectionStrategy, Component, computed, effect, input, model, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { StudioAiActivePlan } from '../studio-ai-session.store';
import { STUDIO_AI_LABELS, formatLabel } from '../studio-ai-labels';
import { StudioSpecCounters, countSpec } from '../studio-ai.models';

/**
 * Dernière barrière avant écriture (plan P1 §8.1) : rien n'est créé tant que ce dialogue n'a pas
 * été validé explicitement. Il récapitule ce qui va être écrit (compteurs) et les points restés en
 * suspens, et rappelle que la création est définitive.
 *
 * Le composant ne connaît ni le store ni le service : la page décide ce que « Intégrer » déclenche.
 */
@Component({
  selector: 'app-studio-ai-confirm-dialog',
  standalone: true,
  imports: [DialogModule, ButtonModule, CheckboxModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p-dialog
      [header]="header()"
      [(visible)]="visible"
      [modal]="true"
      [draggable]="false"
      [closable]="true"
      [style]="{ width: '560px', maxWidth: '95vw' }">
      <div class="sacd">
        <p class="sacd__intro">{{ labels.confirm.intro }}</p>
        <ul class="sacd__counters">
          <li><strong>{{ counters().entities }}</strong> {{ labels.preview.tables }}</li>
          <li><strong>{{ counters().fields }}</strong> {{ labels.preview.fields }}</li>
          <li><strong>{{ counters().relations }}</strong> {{ labels.preview.relations }}</li>
          <li><strong>{{ counters().seedRecords }}</strong> {{ labels.preview.seedRecords }}</li>
        </ul>
        <p class="sacd__irreversible">
          <i class="fa-solid fa-circle-info" aria-hidden="true"></i> {{ labels.confirm.irreversible }}
        </p>
        @if (warnings().length) {
          <p class="sacd__warnings-title">{{ labels.confirm.pendingWarnings }}</p>
          <ul class="sacd__warnings">
            @for (warning of warnings(); track warning) {
              <li><i class="fa-solid fa-triangle-exclamation" aria-hidden="true"></i> {{ warning }}</li>
            }
          </ul>
        }
        <div class="sacd__ack">
          <p-checkbox
            inputId="sacd-ack"
            [binary]="true"
            [ngModel]="checked()"
            (ngModelChange)="checked.set($event)"
            [disabled]="busy()"></p-checkbox>
          <label for="sacd-ack">{{ labels.confirm.checkbox }}</label>
        </div>
      </div>
      <ng-template pTemplate="footer">
        <div class="sacd__footer">
          <button
            pButton
            type="button"
            class="p-button-text"
            icon="fa-solid fa-xmark"
            [label]="labels.confirm.cancel"
            (click)="cancel()"></button>
          <button
            pButton
            type="button"
            icon="fa-solid fa-check"
            [label]="labels.confirm.ok"
            [disabled]="busy() || !checked()"
            (click)="accept()"></button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .sacd { display: flex; flex-direction: column; gap: var(--spacing-3); }
    .sacd__intro, .sacd__irreversible, .sacd__warnings-title {
      margin: 0;
      font-size: var(--font-size-sm);
      color: var(--color-neutral-700);
    }
    .sacd__irreversible { color: var(--color-neutral-500); }
    .sacd__counters {
      display: flex;
      flex-wrap: wrap;
      gap: var(--spacing-2) var(--spacing-4);
      margin: 0;
      padding: 0;
      list-style: none;
      font-size: var(--font-size-sm);
      color: var(--color-neutral-700);
    }
    .sacd__warnings-title { font-weight: var(--font-weight-semibold); }
    .sacd__warnings {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-2);
      margin: 0;
      padding: 0;
      list-style: none;
      font-size: var(--font-size-sm);
      color: var(--color-warning-700, #b45309);
    }
    .sacd__ack {
      display: flex;
      align-items: center;
      gap: var(--spacing-2);
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-medium);
      color: var(--color-neutral-800);
    }
    .sacd__ack label { cursor: pointer; }
    .sacd__footer { display: flex; justify-content: flex-end; gap: var(--spacing-2); }  `]
})
export class StudioAiConfirmDialogComponent {
  /** Ouverture pilotée par la page (deux-sens : la croix et Échap referment aussi). */
  readonly visible = model(false);
  /** Plan en attente ; son `summary.title` donne le nom du système dans l'en-tête. */
  readonly plan = input<StudioAiActivePlan | null>(null);
  readonly counters = input<StudioSpecCounters>(countSpec(null));
  readonly warnings = input<string[]>([]);
  /** Intégration déjà lancée : évite un double clic sur « Intégrer ». */
  readonly busy = input(false);

  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  protected readonly labels = STUDIO_AI_LABELS;

  /** Case obligatoire (plan P1 §8.1) : remise à zéro à chaque ouverture du dialogue. */
  protected readonly checked = signal(false);

  constructor() {
    effect(() => {
      if (this.visible()) {
        this.checked.set(false);
      }
    });
  }

  protected readonly header = computed(() =>
    formatLabel(STUDIO_AI_LABELS.confirm.title, { name: this.plan()?.summary?.title ?? '' })
  );

  protected accept(): void {
    if (this.busy() || !this.checked()) {
      return;
    }
    this.visible.set(false);
    this.confirmed.emit();
  }

  protected cancel(): void {
    this.visible.set(false);
    this.cancelled.emit();
  }
}
