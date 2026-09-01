import { AppModule } from '@core/models/app-module';
import {
  QUICK_ACTION_WIDGETS,
  SECTOR_KPI_WIDGETS,
  visibleQuickActionWidgets,
  visibleSectorKpiWidgets
} from './dashboard-widgets.registry';

describe('dashboard-widgets.registry (plan v1 §2.5)', () => {
  const allModules = () => true;
  const noModules = () => false;
  const allPermissions = () => true;

  describe('visibleSectorKpiWidgets', () => {
    it('Commerce · Textile : affiche ruptures/achats, pas de widget Projets', () => {
      const widgets = visibleSectorKpiWidgets('commerce', allModules, allPermissions);
      const ids = widgets.map((w) => w.widgetId);

      expect(ids).toContain('commerce-stock-ruptures');
      expect(ids).toContain('commerce-purchases-pending');
      expect(ids).not.toContain('services-btp-active-projects');
      expect(ids).not.toContain('services-btp-recurring-contracts');
      expect(ids).not.toContain('assoc-edu-membership-fees');
    });

    it('BTP & Construction : affiche le widget Projets actifs et Contrats récurrents, pas de widget Commerce', () => {
      const widgets = visibleSectorKpiWidgets('btp-construction', allModules, allPermissions);
      const ids = widgets.map((w) => w.widgetId);

      expect(ids).toContain('services-btp-active-projects');
      expect(ids).toContain('services-btp-recurring-contracts');
      expect(ids).not.toContain('commerce-stock-ruptures');
      expect(ids).not.toContain('commerce-purchases-pending');
    });

    it('Services : même variante que BTP (projets + contrats récurrents)', () => {
      const widgets = visibleSectorKpiWidgets('services', allModules, allPermissions);
      const ids = widgets.map((w) => w.widgetId);

      expect(ids).toContain('services-btp-active-projects');
      expect(ids).toContain('services-btp-recurring-contracts');
    });

    it('Association : affiche les cotisations actives, aucun widget Commerce/Projets', () => {
      const widgets = visibleSectorKpiWidgets('association', allModules, allPermissions);
      const ids = widgets.map((w) => w.widgetId);

      expect(ids).toEqual(['assoc-edu-membership-fees']);
    });

    it("Établissement éducatif : même variante qu'Association", () => {
      const widgets = visibleSectorKpiWidgets('etablissement-educatif', allModules, allPermissions);
      expect(widgets.map((w) => w.widgetId)).toEqual(['assoc-edu-membership-fees']);
    });

    it('segment absent/inconnu (entreprise) : aucun widget sectoriel spécifique', () => {
      expect(visibleSectorKpiWidgets(null, allModules, allPermissions)).toEqual([]);
      expect(visibleSectorKpiWidgets('entreprise', allModules, allPermissions)).toEqual([]);
    });

    it('un module requis désactivé masque le widget même si le segment correspond', () => {
      const widgets = visibleSectorKpiWidgets('commerce', noModules, allPermissions);
      expect(widgets).toEqual([]);
    });

    it("une permission manquante masque le widget (défense en profondeur)", () => {
      const widgets = visibleSectorKpiWidgets('commerce', allModules, () => false);
      expect(widgets).toEqual([]);
    });

    it('chaque widget déclare au moins un module requis', () => {
      for (const widget of SECTOR_KPI_WIDGETS) {
        expect(widget.requiredModules.length).toBeGreaterThan(0);
      }
    });
  });

  describe('visibleQuickActionWidgets', () => {
    it('module Stock actif ⇒ action "Entrée de stock" visible', () => {
      const widgets = visibleQuickActionWidgets(
        (mods) => mods.every((m) => m === AppModule.Stock),
        allPermissions
      );
      expect(widgets.map((w) => w.widgetId)).toContain('qa-stock-entry');
    });

    it('module CRM actif ⇒ action "Nouvelle opportunité" visible', () => {
      const widgets = visibleQuickActionWidgets(
        (mods) => mods.every((m) => m === AppModule.CRM),
        allPermissions
      );
      expect(widgets.map((w) => w.widgetId)).toContain('qa-new-opportunity');
    });

    it('aucun module actif ⇒ aucune action rapide dérivée', () => {
      expect(visibleQuickActionWidgets(noModules, allPermissions)).toEqual([]);
    });

    it('module actif mais permission manquante ⇒ action masquée', () => {
      expect(visibleQuickActionWidgets(allModules, () => false)).toEqual([]);
    });

    it('chaque action déclare une route non vide', () => {
      for (const widget of QUICK_ACTION_WIDGETS) {
        expect(widget.route.length).toBeGreaterThan(0);
      }
    });
  });
});
