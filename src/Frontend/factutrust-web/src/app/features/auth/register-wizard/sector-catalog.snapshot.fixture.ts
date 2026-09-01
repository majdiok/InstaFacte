/**
 * Frozen copy of the backend canonical catalog snapshot.
 *
 * Source of truth: `src/Backend/FactuTrust.API/Resources/sector-catalog.snapshot.json`
 * (relative to the repository root). The content below was copied *verbatim* (same
 * segments/domains/modules/moduleDependencies/catalogVersion data, decoded from the
 * backend file's JSON-escaped Unicode into plain UTF-8 for readability) on 2026-09-01
 * when the backend agent first checked that file in.
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
 *  2. A separate CI step (outside `ng test`, run once per build from the repo root) is
 *     expected to byte-diff this fixture's data against the real backend snapshot file so a
 *     silent copy/paste drift between the two trees cannot slip through unnoticed. If that CI
 *     step does not exist yet, updating this fixture whenever the backend snapshot changes is
 *     a manual step — do not let the two drift apart.
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
    { code: 'immobilier', labelFr: 'Immobilier', sortOrder: 5, additionalModuleIds: [] },
    { code: 'energie-environnement', labelFr: 'Énergie & Environnement', sortOrder: 6, additionalModuleIds: [] },
    { code: 'communication-marketing', labelFr: 'Communication & Marketing', sortOrder: 7, additionalModuleIds: [9] },
    { code: 'artisanat', labelFr: 'Artisanat', sortOrder: 8, additionalModuleIds: [] },
    { code: 'autre', labelFr: 'Autre domaine', sortOrder: 9, additionalModuleIds: [] }
  ],
  modules: [
    { id: 0, code: 'Clients', labelFr: 'Clients', isCore: true },
    { id: 1, code: 'Products', labelFr: 'Produits et services', isCore: true },
    { id: 2, code: 'Sales', labelFr: 'Ventes (factures)', isCore: true },
    { id: 3, code: 'Treasury', labelFr: 'Trésorerie (paiements)', isCore: true },
    { id: 4, code: 'Reports', labelFr: 'Rapports', isCore: true },
    { id: 5, code: 'Administration', labelFr: 'Paramètres et utilisateurs', isCore: true },
    { id: 6, code: 'Purchases', labelFr: 'Achats', isCore: false },
    { id: 7, code: 'Stock', labelFr: 'Stock', isCore: false },
    { id: 8, code: 'Accounting', labelFr: 'Comptabilité', isCore: false },
    { id: 9, code: 'CRM', labelFr: 'CRM Commercial', isCore: false },
    { id: 10, code: 'Fiscal', labelFr: 'Fiscal / TEJ', isCore: false },
    { id: 11, code: 'AI', labelFr: 'Assistant IA', isCore: false },
    { id: 12, code: 'Forecasting', labelFr: 'Prévisions IA', isCore: false },
    { id: 13, code: 'Studio', labelFr: 'Studio (low-code)', isCore: false },
    { id: 14, code: 'Payroll', labelFr: 'RH & Paie', isCore: false },
    { id: 16, code: 'Projects', labelFr: 'Projets', isCore: false },
    { id: 17, code: 'RecurringContracts', labelFr: 'Contrats récurrents', isCore: false }
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
