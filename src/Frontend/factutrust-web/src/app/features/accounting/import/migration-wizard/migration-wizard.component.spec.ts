import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { MigrationWizardComponent } from './migration-wizard.component';
import {
  AccountingService,
  MigrationAnalysisDto,
  MigrationSourceSystem,
  MigrationSuggestionOrigin,
  JournalImportFormat,
  ReferenceImportTarget
} from '../../services/accounting.service';

/**
 * Specs du wizard de migration assistée (N1) — centrées sur les helpers purs
 * (dictionnaire de colonnes, table de correspondance CSV, complétude requise)
 * et sur le repli 404 → mode dégradé. Le composant est instancié dans le
 * contexte d'injection sans rendu du template.
 */
describe('MigrationWizardComponent', () => {
  const analysisStub: MigrationAnalysisDto = {
    fileName: 'export-sage.csv',
    detectedSource: MigrationSourceSystem.SageLigne100,
    confidence: 0.9,
    format: JournalImportFormat.Csv,
    delimiter: ';',
    suggestedTarget: ReferenceImportTarget.ChartOfAccounts,
    targetConfidence: 0.8,
    headers: ['Compte', 'Intitulé'],
    sampleRowCount: 120,
    knownFormatMatched: true,
    warnings: []
  };

  function createComponent(serviceStub: Partial<AccountingService> = {}): MigrationWizardComponent {
    TestBed.configureTestingModule({
      providers: [{ provide: AccountingService, useValue: serviceStub }]
    });
    return TestBed.runInInjectionContext(() => new MigrationWizardComponent());
  }

  it('se crée avec un service injecté', () => {
    expect(createComponent()).toBeTruthy();
  });

  // ── columnMappingDict ─────────────────────────────────────────────────────

  describe('columnMappingDict', () => {
    it('construit le dictionnaire source → canonique en excluant les entrées non mappées', () => {
      const c = createComponent();
      c.columnItems.set([
        { sourceColumn: 'Compte', canonicalColumn: 'compte', confidence: 1, origin: MigrationSuggestionOrigin.Synonyme },
        { sourceColumn: 'Intitulé', canonicalColumn: 'libelle', confidence: 0.9, origin: MigrationSuggestionOrigin.Ia },
        { sourceColumn: 'Colonne inconnue', canonicalColumn: null, confidence: 0, origin: MigrationSuggestionOrigin.Synonyme }
      ]);

      expect(c.columnMappingDict()).toEqual({ Compte: 'compte', 'Intitulé': 'libelle' });
    });

    it('retourne un dictionnaire vide quand rien n\'est mappé', () => {
      const c = createComponent();
      c.columnItems.set([
        { sourceColumn: 'X', canonicalColumn: null, confidence: 0, origin: MigrationSuggestionOrigin.Synonyme }
      ]);
      expect(c.columnMappingDict()).toEqual({});
    });
  });

  // ── buildAccountMappingCsv ────────────────────────────────────────────────

  describe('buildAccountMappingCsv', () => {
    it('produit un CSV « source;cible » en excluant cibles vides et identités', async () => {
      const c = createComponent();
      c.accountItems.set([
        { sourceAccount: '411000', sourceLabel: 'Clients', targetAccount: '4111', confidence: 0.9, origin: MigrationSuggestionOrigin.Prefixe, justification: null },
        { sourceAccount: '401000', sourceLabel: 'Fournisseurs', targetAccount: '   ', confidence: 0, origin: MigrationSuggestionOrigin.Ia, justification: null },
        { sourceAccount: '701000', sourceLabel: 'Ventes', targetAccount: '701000', confidence: 1, origin: MigrationSuggestionOrigin.Identite, justification: null }
      ]);

      const file = c.buildAccountMappingCsv();
      expect(file).not.toBeNull();
      expect(file!.name).toBe('correspondance-comptes.csv');
      expect(await file!.text()).toBe('source;cible\n411000;4111\n');
    });

    it('retourne null quand aucune correspondance n\'est éditée', () => {
      const c = createComponent();
      c.accountItems.set([
        { sourceAccount: '411000', sourceLabel: null, targetAccount: null, confidence: 0, origin: MigrationSuggestionOrigin.Ia, justification: null },
        { sourceAccount: '701000', sourceLabel: null, targetAccount: '701000', confidence: 1, origin: MigrationSuggestionOrigin.Identite, justification: null }
      ]);
      expect(c.buildAccountMappingCsv()).toBeNull();
    });
  });

  // ── columnMappingComplete / requiredCanonical / canonicalOptions ──────────

  describe('complétude de la correspondance de colonnes', () => {
    it('exige compte, libelle et classe pour le plan comptable', () => {
      const c = createComponent();
      c.analysis.set(analysisStub); // suggestedTarget = ChartOfAccounts
      c.target = ReferenceImportTarget.ChartOfAccounts;
      c.columnItems.set([
        { sourceColumn: 'Compte', canonicalColumn: 'compte', confidence: 1, origin: MigrationSuggestionOrigin.Synonyme },
        { sourceColumn: 'Intitulé', canonicalColumn: 'libelle', confidence: 1, origin: MigrationSuggestionOrigin.Synonyme }
      ]);
      expect(c.columnMappingComplete()).toBeFalse(); // « classe » manquante

      c.columnItems.update(items => [...items, { sourceColumn: 'Cl', canonicalColumn: 'classe', confidence: 1, origin: MigrationSuggestionOrigin.Synonyme }]);
      expect(c.columnMappingComplete()).toBeTrue();
    });

    it('exige seulement « compte » pour la balance d\'ouverture', () => {
      const c = createComponent();
      c.analysis.set(analysisStub);
      c.target = ReferenceImportTarget.OpeningBalance;
      c.columnItems.set([
        { sourceColumn: 'Compte', canonicalColumn: 'compte', confidence: 1, origin: MigrationSuggestionOrigin.Synonyme }
      ]);
      expect(c.columnMappingComplete()).toBeTrue();
    });

    it('est fausse sans analyse préalable', () => {
      const c = createComponent();
      expect(c.columnMappingComplete()).toBeFalse();
    });

    it('propose les options canoniques adaptées à chaque cible', () => {
      const c = createComponent();
      c.target = ReferenceImportTarget.ThirdParties;
      expect(c.canonicalOptions()).toContain('nif');
      expect(c.requiredCanonical()).toContain('nom');
      c.target = ReferenceImportTarget.OpeningBalance;
      expect(c.canonicalOptions()).toEqual(['compte', 'debit', 'credit']);
    });
  });

  // ── Libellés ──────────────────────────────────────────────────────────────

  describe('libellés d\'origine et de progiciel', () => {
    it('traduit les origines de suggestion', () => {
      const c = createComponent();
      expect(c.originLabel(MigrationSuggestionOrigin.Ia)).toBe('IA');
      expect(c.originLabel(MigrationSuggestionOrigin.Identite)).toBe('Identique');
      expect(c.originLabel(MigrationSuggestionOrigin.Prefixe)).toBe('Préfixe');
    });

    it('traduit les progiciels détectés', () => {
      const c = createComponent();
      expect(c.sourceLabel(MigrationSourceSystem.SageLigne100)).toBe('Sage Ligne 100');
      expect(c.sourceLabel(MigrationSourceSystem.Inconnu)).toBe('Non reconnu');
    });
  });

  // ── accountResolvedCount ──────────────────────────────────────────────────

  describe('accountResolvedCount', () => {
    it('compte les comptes résolus sans IA (identique / préfixe)', () => {
      const c = createComponent();
      c.accountItems.set([
        { sourceAccount: '411000', sourceLabel: null, targetAccount: '411000', confidence: 1, origin: MigrationSuggestionOrigin.Identite, justification: null },
        { sourceAccount: '401000', sourceLabel: null, targetAccount: '4011', confidence: 0.9, origin: MigrationSuggestionOrigin.Prefixe, justification: null },
        { sourceAccount: '607000', sourceLabel: null, targetAccount: '6071', confidence: 0.7, origin: MigrationSuggestionOrigin.Ia, justification: null }
      ]);
      expect(c.accountResolvedCount()).toBe(2);
    });
  });

  // ── Analyse : succès et repli 404 ─────────────────────────────────────────

  describe('analyze', () => {
    it('renseigne l\'analyse et avance à l\'étape 2 en cas de succès', () => {
      const c = createComponent({
        analyzeMigrationSource: () => of({ success: true, data: analysisStub })
      });
      c.file = new File(['Compte;Intitulé\n411000;Clients\n'], 'export-sage.csv', { type: 'text/csv' });

      c.analyze();

      expect(c.analysis()).toEqual(analysisStub);
      expect(c.target).toBe(ReferenceImportTarget.ChartOfAccounts);
      expect(c.step()).toBe(1);
      expect(c.busy()).toBeFalse();
    });

    it('active le mode dégradé quand le serveur répond 404 (fonctionnalité désactivée)', () => {
      const c = createComponent({
        analyzeMigrationSource: () => throwError(() => ({ status: 404 }))
      });
      c.file = new File(['x'], 'f.csv', { type: 'text/csv' });

      c.analyze();

      expect(c.featureUnavailable()).toBeTrue();
      expect(c.busy()).toBeFalse();
      expect(c.error()).toBeNull();
    });

    it('affiche une erreur explicite pour les autres échecs', () => {
      const c = createComponent({
        analyzeMigrationSource: () => throwError(() => ({ status: 500, error: { message: 'Fichier illisible' } }))
      });
      c.file = new File(['x'], 'f.csv', { type: 'text/csv' });

      c.analyze();

      expect(c.featureUnavailable()).toBeFalse();
      expect(c.error()).toBe('Fichier illisible');
    });
  });

  // ── reset ─────────────────────────────────────────────────────────────────

  it('reset réinitialise l\'intégralité de l\'état du wizard', () => {
    const c = createComponent();
    c.step.set(4);
    c.analysis.set(analysisStub);
    c.columnItems.set([{ sourceColumn: 'A', canonicalColumn: 'compte', confidence: 1, origin: MigrationSuggestionOrigin.Ia }]);
    c.mappingWarnings.set(['w']);
    c.file = new File(['x'], 'f.csv');

    c.reset();

    expect(c.step()).toBe(0);
    expect(c.analysis()).toBeNull();
    expect(c.columnItems()).toEqual([]);
    expect(c.mappingWarnings()).toEqual([]);
    expect(c.file).toBeNull();
    expect(c.buildAccountMappingCsv()).toBeNull();
  });
});
