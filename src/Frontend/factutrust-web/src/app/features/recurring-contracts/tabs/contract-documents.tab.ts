import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TooltipModule } from 'primeng/tooltip';
import { ButtonComponent } from '@shared/components/button/button.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';

/**
 * Onglet « Documents » : placeholder phase 2 (aucun appel réseau).
 * Le backend documents n'existe pas encore — on colle à la maquette avec un état vide explicite.
 */
@Component({
  selector: 'app-contract-documents-tab',
  standalone: true,
  imports: [CommonModule, TooltipModule, ButtonComponent, EmptyStateComponent],
  template: `
    <app-empty-state
      icon="pi-folder-open"
      title="Aucun document"
      description="Le dépôt de documents contractuels arrive en phase 2."
      [showAction]="false">
    </app-empty-state>
    <div class="center">
      <app-button
        variant="outline"
        size="sm"
        icon="pi-plus"
        [disabled]="true"
        pTooltip="Disponible prochainement">
        Ajouter un document
      </app-button>
    </div>
  `,
  styles: [`
    .center { display: flex; justify-content: center; margin-top: calc(-1 * var(--spacing-6)); }
  `]
})
export class ContractDocumentsTabComponent {}
