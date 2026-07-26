import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TabViewModule } from 'primeng/tabview';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ImportComponent } from './import.component';
import { ReferenceImportComponent } from './reference-import.component';
import { ReferenceImportTarget } from '../services/accounting.service';

/**
 * Reprise de dossier — hub à onglets. L'onglet « Écritures » embarque l'écran d'import d'écritures
 * existant, INCHANGÉ ; les autres onglets importent les référentiels (plan comptable, plan tiers,
 * balance d'ouverture) via le composant réutilisable paramétré par cible.
 */
@Component({
  selector: 'app-import-hub',
  standalone: true,
  imports: [CommonModule, TabViewModule, PageHeaderComponent, ImportComponent, ReferenceImportComponent],
  template: `
    <app-page-header
      title="Reprise de dossier"
      subtitle="Importer écritures, plan comptable, plan tiers et balance d'ouverture depuis un fichier" />

    <p-tabView>
      <p-tabPanel header="Écritures">
        <app-accounting-import />
      </p-tabPanel>
      <p-tabPanel header="Plan comptable">
        <app-reference-import [target]="targets.ChartOfAccounts" />
      </p-tabPanel>
      <p-tabPanel header="Plan tiers">
        <app-reference-import [target]="targets.ThirdParties" />
      </p-tabPanel>
      <p-tabPanel header="Balance d'ouverture">
        <app-reference-import [target]="targets.OpeningBalance" />
      </p-tabPanel>
    </p-tabView>
  `
})
export class ImportHubComponent {
  readonly targets = ReferenceImportTarget;
}
