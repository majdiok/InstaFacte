import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TabsModule } from 'primeng/tabs';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ImportComponent } from './import.component';
import { ReferenceImportComponent } from './reference-import.component';
import { DossierExportComponent } from './dossier-export.component';
import { MigrationWizardComponent } from './migration-wizard/migration-wizard.component';
import { ReferenceImportTarget } from '../services/accounting.service';

/**
 * Reprise de dossier — hub à onglets. L'onglet « Écritures » embarque l'écran d'import d'écritures
 * existant, INCHANGÉ ; les autres onglets importent les référentiels (plan comptable, plan tiers,
 * balance d'ouverture) via le composant réutilisable paramétré par cible. L'onglet
 * « Migration assistée » (N1) pilote le wizard IA d'analyse de fichiers sources (Sage, EBP, …) ;
 * en mode dégradé (fonctionnalité désactivée côté serveur), il propose le parcours manuel classique.
 */
@Component({
  selector: 'app-import-hub',
  standalone: true,
  imports: [CommonModule, TabsModule, PageHeaderComponent, ImportComponent, ReferenceImportComponent, DossierExportComponent, MigrationWizardComponent],
  template: `
    <app-page-header
      title="Reprise de dossier"
      subtitle="Importer écritures / référentiels, ou exporter l'archive du dossier" />

    <p-tabs [lazy]="true">
      <p-tablist>
        <p-tab [value]="0">Écritures</p-tab>
        <p-tab [value]="1">Plan comptable</p-tab>
        <p-tab [value]="2">Plan tiers</p-tab>
        <p-tab [value]="3">Balance d'ouverture</p-tab>
        <p-tab [value]="4">Export dossier</p-tab>
        <p-tab [value]="5">Migration assistée</p-tab>
      </p-tablist>
      <p-tabpanels>
        <p-tabpanel [value]="0">
          <app-accounting-import />
        </p-tabpanel>
        <p-tabpanel [value]="1">
          <app-reference-import [target]="targets.ChartOfAccounts" />
        </p-tabpanel>
        <p-tabpanel [value]="2">
          <app-reference-import [target]="targets.ThirdParties" />
        </p-tabpanel>
        <p-tabpanel [value]="3">
          <app-reference-import [target]="targets.OpeningBalance" />
        </p-tabpanel>
        <p-tabpanel [value]="4">
          <app-dossier-export />
        </p-tabpanel>
        <p-tabpanel [value]="5">
          <app-migration-wizard />
        </p-tabpanel>
      </p-tabpanels>
    </p-tabs>
  `
})
export class ImportHubComponent {
  readonly targets = ReferenceImportTarget;
}
