import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

import { StepMetadataComponent } from '../step-metadata/step-metadata.component';
import { StepSellerComponent } from '../step-seller/step-seller.component';

/**
 * Étape 1 (parcours simplifié) - Document
 *
 * Conteneur fusionnant la sélection du type/dates (step-metadata) et de l'émetteur
 * (step-seller) sur un seul écran. N'ajoute aucune logique métier : se contente de
 * composer les deux composants existants pour réduire le nombre d'étapes du wizard
 * de 6 à 4 (parcours feature-flagué `wizardSimplifiedFlow`).
 */
@Component({
  selector: 'app-step-document',
  standalone: true,
  imports: [CommonModule, StepMetadataComponent, StepSellerComponent],
  template: `
    <div class="step-document">
      <section class="document-section">
        <app-step-metadata></app-step-metadata>
      </section>

      <div class="section-divider" aria-hidden="true"></div>

      <section class="document-section document-section--seller">
        <app-step-seller></app-step-seller>
      </section>
    </div>
  `,
  styles: [`
    .step-document {
      display: flex;
      flex-direction: column;
      gap: var(--spacing-6, 24px);
    }

    .document-section {
      display: block;
    }

    .section-divider {
      height: 1px;
      background: var(--color-neutral-200, #e5e7eb);
      margin: var(--spacing-2, 8px) 0;
    }

    @media (max-width: 768px) {
      .step-document {
        gap: var(--spacing-4, 16px);
      }
    }
  `]
})
export class StepDocumentComponent {}
