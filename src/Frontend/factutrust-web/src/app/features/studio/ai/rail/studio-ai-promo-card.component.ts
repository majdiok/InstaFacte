import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { STUDIO_AI_LABELS } from '../studio-ai-labels';

/**
 * Carte promotionnelle du rail (dégradé `--studio-promo-gradient`, défini par le thème D2) :
 * « Une idée ? Laissez l'IA la réaliser ! ». Le CTA remonte un prompt d'exemple que la page pousse
 * dans le composer (`prefill`) — rien n'est envoyé sans l'utilisateur.
 */
@Component({
  selector: 'app-studio-ai-promo-card',
  standalone: true,
  imports: [ButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="sap" [attr.aria-label]="labels.promoTitle">
      <span class="sap__icon" aria-hidden="true"><i class="fa-solid fa-lightbulb"></i></span>
      <h3 class="sap__title">{{ labels.promoTitle }}</h3>
      <p class="sap__text">{{ labels.promoText }}</p>
      <p-button
        class="sap__cta"
        [label]="labels.promoCta"
        icon="fa-solid fa-arrow-right"
        iconPos="right"
        size="small"
        severity="contrast"
        [disabled]="busy()"
        (onClick)="tryPrompt.emit(labels.promoPrompt)" />
    </section>
  `,
  styles: [`
    .sap {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: var(--spacing-2, 0.5rem);
      padding: var(--spacing-4, 1rem);
      border-radius: var(--radius-lg, 12px);
      background: var(--studio-promo-gradient, linear-gradient(135deg, #4f46e5, #7c3aed));
      color: #fff;
    }
    .sap__icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 2rem;
      height: 2rem;
      border-radius: 999px;
      background: rgb(255 255 255 / 18%);
    }
    .sap__title { margin: 0; font-size: var(--font-size-base, 1rem); font-weight: var(--font-weight-semibold, 600); }
    .sap__text { margin: 0; font-size: var(--font-size-sm, 0.875rem); opacity: 0.9; }
    .sap__cta { margin-top: var(--spacing-1, 0.25rem); }
  `]
})
export class StudioAiPromoCardComponent {
  readonly busy = input(false);
  /** Prompt d'exemple à préremplir dans le composer. */
  readonly tryPrompt = output<string>();

  protected readonly labels = STUDIO_AI_LABELS.rail;
}
