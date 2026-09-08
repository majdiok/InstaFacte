import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { SkeletonModule } from 'primeng/skeleton';
import { StudioAiBuilderComponent } from '../studio-ai-builder.component';
import { StudioAiCapabilitiesService } from './studio-ai-capabilities.service';
import { StudioAiPageComponent } from './studio-ai-page.component';

/**
 * Point d'entrée de `/studio/ai` : aiguille vers l'atelier (`StudioAiPageComponent`) quand le
 * workbench Studio IA est activé côté serveur, sinon vers la page legacy `StudioAiBuilderComponent`.
 *
 * La décision repose sur `GET api/ai/studio/capabilities` (mise en cache pour la session). Tant que
 * la réponse n'est pas arrivée, un squelette évite un flash legacy → atelier. Toute erreur ⇒ legacy :
 * l'utilisateur garde toujours une page fonctionnelle.
 */
@Component({
  selector: 'app-studio-ai-entry',
  standalone: true,
  imports: [SkeletonModule, StudioAiBuilderComponent, StudioAiPageComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (capabilities.loading()) {
      <div class="studio-ai-entry__skeleton" aria-busy="true" aria-live="polite">
        <p-skeleton width="40%" height="1.75rem" styleClass="mb-3" />
        <p-skeleton width="70%" height="1rem" styleClass="mb-4" />
        <p-skeleton width="100%" height="9rem" styleClass="mb-3" />
        <p-skeleton width="100%" height="12rem" />
      </div>
    } @else if (capabilities.workbenchEnabled()) {
      <app-studio-ai-page />
    } @else {
      <app-studio-ai-builder />
    }
  `,
  styles: [`
    .studio-ai-entry__skeleton {
      padding: var(--spacing-6, 1.5rem);
      display: flex;
      flex-direction: column;
      gap: var(--spacing-3, 0.75rem);
    }
  `]
})
export class StudioAiEntryComponent {
  readonly capabilities = inject(StudioAiCapabilitiesService);

  constructor() {
    this.capabilities.ensureLoaded();
  }
}
