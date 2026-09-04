/**
 * Frozen copy of the backend canonical catalog snapshot.
 *
 * Source of truth: `src/Backend/FactuTrust.API/Resources/sector-catalog.snapshot.json`
 * (relative to the repository root). The content below was copied *verbatim* (same
 * segments/domains/modules/moduleDependencies/catalogVersion data, decoded from the
 * backend file's JSON-escaped Unicode into plain UTF-8 for readability) on 2026-09-01
 * when the backend agent first checked that file in. The per-module
 * `availableOnFreePlan` flag (true for core/standard modules, false for the 4 premium
 * modules AI/Forecasting/Studio/Payroll) was added alongside the backend's Free-plan
 * gating change so this fixture mirrors the backend snapshot's updated module entries;
 * `sector-catalog-parity.spec.ts` pins the frontend `PREMIUM_MODULE_IDS` constant
 * against the fixture's `availableOnFreePlan: false` set.
 *
 * Why a copy instead of a live import: the Angular workspace's TypeScript project
 * (`tsconfig.app.json`) restricts `rootDir` to `src/Frontend/factutrust-web/src`, and the
 * Karma/webpack test bundle only resolves modules reachable from that root — a file under
 * `src/Backend/...` is outside the tree entirely and cannot be `import`ed or `require`d from
 * a spec here. `sector-catalog-parity.spec.ts` therefore diffs the frontend's static catalog
 * (`registration-catalog.ts`) against *this* fixture, not against the backend file directly.
 *
 * Keeping this copy honest is a two-part contract:
 *  1. This Jasmine/Karma spec (`sector-catalog-parity.spec.ts`) pins the frontend static
 *     catalog against the data below.
 *  2. `scripts/ci/check-sector-catalog-parity.mjs`, exécuté par le job « Web Angular » de la CI
 *     AVANT `ng test`, compare les données ci-dessous au vrai fichier backend. Une dérive de
 *     recopie entre les deux arbres casse donc la CI au lieu de passer inaperçue. Lancez-le
 *     localement (`node scripts/ci/check-sector-catalog-parity.mjs`) après toute mise à jour
 *     de ce fichier.
 *
 * Do NOT hand-edit the values below independently of the backend file. If the backend
 * snapshot changes, regenerate this file from it (and update `registration-catalog.ts` to
 * match, if the change reflects a real static-catalog update rather than a Phase 2 remote-only
 * addition).
 */
import type { SectorCatalogDto } from './registration-catalog';

export const SECTOR_CATALOG_SNAPSHOT_FIXTURE: SectorCatalogDto & { catalogVersion: string } = {
  segments: [
    {
      code: 'entreprise',
      labelFr: 'Entreprise',
      descriptionFr: 'Sociétés commerciales et de services aux entreprises.',
      iconKey: 'briefcase',
      sortOrder: 0,
      coreModuleIds: [5, 0, 1, 2, 3, 4],
      recommendedModuleIds: [6, 7, 8, 9, 10],
      defaultWarehouseName: 'Entrepôt Principal',
      domainCodes: [
        'technologie-informatique',
        'alimentation-agroalimentaire',
        'sante-paramedical',
        'textile-habillement',
        'transport-logistique',
        'immobilier',
        'energie-environnement',
        'communication-marketing',
        'artisanat',
        'autre'
      ]
    },
    {
      code: 'commerce',
      labelFr: 'Commerce',
      descriptionFr: 'Négoce et distribution.',
      iconKey: 'shopping-cart',
      sortOrder: 1,
      coreModuleIds: [5, 0, 1, 2, 3, 4],
      recommendedModuleIds: [6, 7, 10],
      defaultWarehouseName: 'Magasin principal',
      domainCodes: [
        'alimentation-agroalimentaire',
        'textile-habillement',
        'technologie-informatique',
        'sante-paramedical',
        'artisanat',
        'autre'
      ]
    },
    {
      code: 'services',
      labelFr: 'Prestations de services',
      descriptionFr: 'Services et conseils.',
      iconKey: 'handshake',
      sortOrder: 2,
      coreModuleIds: [5, 0, 1, 2, 3, 4],
      recommendedModuleIds: [9, 16, 17, 10],
      defaultWarehouseName: 'Entrepôt Principal',
      domainCodes: [
        'technologie-informatique',
        'communication-marketing',
        'sante-paramedical',
        'transport-logistique',
        'immobilier',
        'autre'
      ]
    },
    {
      code: 'btp-construction',
      labelFr: 'BTP & Construction',
      descriptionFr: 'Bâtiment et travaux publics.',
      iconKey: 'hard-hat',
      sortOrder: 3,
      coreModuleIds: [5, 0, 1, 2, 3, 4],
      recommendedModuleIds: [6, 7, 16, 10],
      defaultWarehouseName: 'Dépôt chantier',
      domainCodes: ['immobilier', 'energie-environnement', 'artisanat', 'autre']
    },
    {
      code: 'association',
      labelFr: 'Association',
      descriptionFr: 'Organismes à but non lucratif.',
      iconKey: 'heart-handshake',
      sortOrder: 4,
      coreModuleIds: [5, 0, 1, 2, 3, 4],
      recommendedModuleIds: [8, 10],
      defaultWarehouseName: 'Entrepôt Principal',
      domainCodes: [
        'sante-paramedical',
        'energie-environnement',
        'communication-marketing',
        'artisanat',
        'autre'
      ]
    },
    {
      code: 'etablissement-educatif',
      labelFr: 'Établissement éducatif',
      descriptionFr: 'Écoles, universités, centres de formation.',
      iconKey: 'graduation-cap',
      sortOrder: 5,
      coreModuleIds: [5, 0, 1, 2, 3, 4],
      recommendedModuleIds: [17, 8, 10],
      defaultWarehouseName: 'Entrepôt Principal',
      domainCodes: [
        'technologie-informatique',
        'sante-paramedical',
        'artisanat',
        'communication-marketing',
        'autre'
      ]
    }
  ],
  domains: [
    { code: 'technologie-informatique', labelFr: 'Technologie & Informatique', sortOrder: 0, additionalModuleIds: [16, 17] },
    { code: 'alimentation-agroalimentaire', labelFr: 'Alimentation & Agroalimentaire', sortOrder: 1, additionalModuleIds: [7, 6] },
    { code: 'sante-paramedical', labelFr: 'Santé & Paramédical', sortOrder: 2, additionalModuleIds: [9] },
    { code: 'textile-habillement', labelFr: 'Textile & Habillement', sortOrder: 3, additionalModuleIds: [7] },
    { code: 'transport-logistique', labelFr: 'Transport & Logistique', sortOrder: 4, additionalModuleIds: [7] },
    { code: 'immobilier', labelFr: 'Immobilier', sortOrder: 5, additionalModuleIds: [16, 17] },
    { code: 'energie-environnement', labelFr: 'Énergie & Environnement', sortOrder: 6, additionalModuleIds: [16, 6] },
    { code: 'communication-marketing', labelFr: 'Communication & Marketing', sortOrder: 7, additionalModuleIds: [9] },
    { code: 'artisanat', labelFr: 'Artisanat', sortOrder: 8, additionalModuleIds: [7, 6] },
    { code: 'autre', labelFr: 'Autre domaine', sortOrder: 9, additionalModuleIds: [] }
  ],
  modules: [
    { id: 0, code: 'Clients', labelFr: 'Clients', isCore: true, availableOnFreePlan: true },
    { id: 1, code: 'Products', labelFr: 'Produits et services', isCore: true, availableOnFreePlan: true },
    { id: 2, code: 'Sales', labelFr: 'Ventes (factures)', isCore: true, availableOnFreePlan: true },
    { id: 3, code: 'Treasury', labelFr: 'Trésorerie (paiements)', isCore: true, availableOnFreePlan: true },
    { id: 4, code: 'Reports', labelFr: 'Rapports', isCore: true, availableOnFreePlan: true },
    { id: 5, code: 'Administration', labelFr: 'Paramètres et utilisateurs', isCore: true, availableOnFreePlan: true },
    { id: 6, code: 'Purchases', labelFr: 'Achats', isCore: false, availableOnFreePlan: true },
    { id: 7, code: 'Stock', labelFr: 'Stock', isCore: false, availableOnFreePlan: true },
    { id: 8, code: 'Accounting', labelFr: 'Comptabilité', isCore: false, availableOnFreePlan: true },
    { id: 9, code: 'CRM', labelFr: 'CRM Commercial', isCore: false, availableOnFreePlan: true },
    { id: 10, code: 'Fiscal', labelFr: 'Fiscal / TEJ', isCore: false, availableOnFreePlan: true },
    { id: 11, code: 'AI', labelFr: 'Assistant IA', isCore: false, availableOnFreePlan: false },
    { id: 12, code: 'Forecasting', labelFr: 'Prévisions IA', isCore: false, availableOnFreePlan: false },
    { id: 13, code: 'Studio', labelFr: 'Studio (low-code)', isCore: false, availableOnFreePlan: false },
    { id: 14, code: 'Payroll', labelFr: 'RH & Paie', isCore: false, availableOnFreePlan: false },
    { id: 16, code: 'Projects', labelFr: 'Projets', isCore: false, availableOnFreePlan: true },
    { id: 17, code: 'RecurringContracts', labelFr: 'Contrats récurrents', isCore: false, availableOnFreePlan: true }
  ],
  moduleDependencies: [],
  suggestedTaxRegimes: [
    {
      segmentCode: 'entreprise',
      regime: 0,
      noteFr: "Régime réel : comptabilité complète et facturation TVA, recommandé pour les sociétés d'entreprise."
    },
    {
      segmentCode: 'commerce',
      regime: 0,
      noteFr:
        "Régime réel : généralement adapté au commerce dès lors que le chiffre d'affaires dépasse les seuils du forfait."
    },
    {
      segmentCode: 'commerce',
      regime: 1,
      noteFr:
        'Régime forfaitaire : simplifié, possible pour les petits commerces sous les seuils légaux.'
    },
    {
      segmentCode: 'services',
      regime: 0,
      noteFr:
        'Régime réel : recommandé pour les prestations de services dès lors que les seuils du forfait sont dépassés.'
    },
    {
      segmentCode: 'services',
      regime: 1,
      noteFr:
        'Régime forfaitaire : simplifié, possible pour les petites prestations de services sous les seuils légaux.'
    },
    {
      segmentCode: 'btp-construction',
      regime: 0,
      noteFr:
        'Régime réel : obligatoire pour le BTP — comptabilité de chantier et TVA récupérable.'
    },
    {
      segmentCode: 'association',
      regime: 2,
      noteFr:
        'Exonération de TVA : la plupart des associations à but non lucratif sont exonérées.'
    },
    {
      segmentCode: 'etablissement-educatif',
      regime: 2,
      noteFr:
        "Exonération de TVA : applicable aux établissements d'enseignement remplissant les conditions légales."
    },
    {
      segmentCode: 'etablissement-educatif',
      regime: 0,
      noteFr:
        "Régime réel : possible pour les activités commerciales annexes d'un établissement éducatif."
    }
  ],
  catalogVersion: 'static:0'
};
