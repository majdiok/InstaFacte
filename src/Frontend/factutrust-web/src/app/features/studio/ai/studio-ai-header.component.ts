import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { STUDIO_AI_LABELS } from './studio-ai-labels';

/**
 * En-tête de l'atelier (maquette `studio-atelier-accueil-home.html`) : titre « Studio IA » + pastille
 * « Créer sans coder », sous-titre, lien vers la documentation et bouton « Nouvelle demande ».
 * Présentiel : `newRequest` remonte à la page, qui appelle `store.resetConversation()`.
 */
@Component({
  selector: 'app-studio-ai-header',
  standalone: true,
  imports: [RouterLink, ButtonModule, TooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="sah">
      <div class="sah__text">
        <h1 class="sah__title">
          <span class="sah__icon" aria-hidden="true"><i class="fa-solid fa-wand-magic-sparkles"></i></span>
          {{ labels.title }}
          <span class="sah__badge">{{ labels.badge }}</span>
        </h1>
        <p class="sah__subtitle">{{ labels.subtitle }}</p>
      </div>
      <div class="sah__actions">
        <a class="sah__doc" [routerLink]="labels.docLink">
          <i class="fa-solid fa-book-open" aria-hidden="true"></i>
          {{ labels.seeDocumentation }}
        </a>
        @if (showNewRequest()) {
          <p-button
            class="sah__new"
            [label]="labels.newRequest"
            icon="fa-solid fa-plus"
            severity="secondary"
            [outlined]="true"
            size="small"
            [disabled]="busy()"
            [pTooltip]="labels.resetConversationHint"
            tooltipPosition="bottom"
            (onClick)="newRequest.emit()" />
        }
      </div>
    </header>
  `,
  styles: [`
    .sah {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: var(--spacing-4, 1rem);
      flex-wrap: wrap;
    }
    .sah__text { display: flex; flex-direction: column; gap: var(--spacing-1, 0.25rem); min-width: 0; }
    .sah__title {
      display: flex;
      align-items: center;
      gap: var(--spacing-2, 0.5rem);
      margin: 0;
      font-size: var(--font-size-xl, 1.25rem);
      font-weight: var(--font-weight-semibold, 600);
      color: var(--color-neutral-900, #111827);
    }
    .sah__icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 2rem;
      height: 2rem;
      border-radius: var(--radius-md, 8px);
      background: var(--studio-promo-gradient, var(--color-primary-600));
      color: #fff;
      font-size: 0.875rem;
    }
    .sah__badge {
      font-size: 0.6875rem;
      font-weight: var(--font-weight-medium, 500);
      padding: 2px var(--spacing-2, 0.5rem);
      border-radius: 999px;
      background: var(--color-primary-50, #eef2ff);
      color: var(--color-primary-700, #4338ca);
    }
    .sah__subtitle { margin: 0; font-size: var(--font-size-sm, 0.875rem); color: var(--color-neutral-500, #6b7280); }
    .sah__actions { display: flex; align-items: center; gap: var(--spacing-3, 0.75rem); flex-wrap: wrap; }
    .sah__doc {
      display: inline-flex;
      align-items: center;
      gap: var(--spacing-1, 0.25rem);
      font-size: var(--font-size-sm, 0.875rem);
      color: var(--color-primary-600, #4f46e5);
      text-decoration: none;
    }
    .sah__doc:hover { text-decoration: underline; }
  `]
})
export class StudioAiHeaderComponent {
  /** Le bouton « Nouvelle demande » n'a de sens qu'une fois une conversation entamée. */
  readonly showNewRequest = input(false);
  readonly busy = input(false);
  readonly newRequest = output<void>();

  protected readonly labels = STUDIO_AI_LABELS.page;
}
