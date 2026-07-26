# Analyse des écarts ERP — FactuTrust

**Version :** 1.0  
**Date :** 23 mai 2026  
**Périmètre :** codebase `FactuTrust` (.NET 8 + Angular 17, multi-tenant SaaS, marché Tunisie)  
**Objectif :** identifier les modules et fonctionnalités manquants pour atteindre le statut d’ERP crédible, avec matrice priorisation/effort et benchmark concurrentiel.

---

## 1. Synthèse exécutive

FactuTrust est aujourd’hui une **plateforme de gestion commerciale avancée** (PGI partiel) avec une couverture remarquable du cycle **vente → achat → stock → trésorerie → comptabilité**, enrichie par CRM, fiscal TEJ, IA et prévisions.

| Indicateur | Valeur |
|---|---|
| Maturité ERP globale (PME commerce TN) | **3,0 / 5** |
| Maturité ERP (PME industrielle) | **2,0 / 5** |
| Maturité ERP (groupe multi-sociétés) | **1,5 / 5** |
| Modules licenciés implémentés | **13** (`AppModule`) |
| Modules hors catalogue implémentés | POS, Virtual Street, Backoffice SaaS |
| Effort estimé Phase 1 (ERP commercial TN) | **~420 j/h** |
| Effort estimé ERP complet mid-market | **~2 800 j/h** (24–36 mois, équipe 4–6 devs) |

**Recommandation stratégique :** viser d’abord le positionnement **« ERP commercial intelligent Tunisie »** (Phase 1), puis verticaliser (services, industrie, groupe) plutôt qu’un ERP généraliste monolithique immédiat.

---

## 2. Méthodologie

### 2.1 Référentiel ERP

Analyse basée sur les piliers standards ERP (Gartner / APICS / modèle Odoo Enterprise) :

1. Finance & comptabilité  
2. Trésorerie & contrôle de gestion  
3. Ventes & distribution  
4. Achats & approvisionnement  
5. Stock & logistique (WMS)  
6. Production & MRP  
7. RH & paie (HCM)  
8. CRM & marketing  
9. Projets & PSA  
10. Immobilisations  
11. BI & consolidation  
12. Intégrations & gouvernance  

### 2.2 Statuts utilisés dans les matrices

| Statut | Signification |
|---|---|
| ✅ Complet | Production-grade, backend + frontend + persistance |
| ⚠️ Partiel | Existe mais incomplet, stub, ou non scalable |
| ❌ Absent | Aucune entité, API ou écran |
| 🔜 Prévu code | Entités/migrations sans logique métier |

### 2.3 Priorités

| Priorité | Critère |
|---|---|
| **P0** | Bloquant pour label « ERP » sur segment cible principal |
| **P1** | Différenciateur marché / conformité réglementaire |
| **P2** | Confort enterprise / scalabilité |
| **P3** | Nice-to-have / verticalisation |

### 2.4 Estimation effort

Efforts en **jours/homme (j/h)** pour une équipe senior connaissant le codebase (backend Clean Architecture + Angular). Inclut conception, dev, tests, doc utilisateur minimale. **Non inclus :** certification, audit externe, infra cloud.

---

## 3. État des lieux — modules existants

### 3.1 Modules officiels (`AppModule`)

| Module | Backend | Frontend | Persistance | Maturité |
|---|---|---|---|---|
| Clients | ✅ | ✅ | Tenant DB | Production |
| Produits | ✅ | ✅ | Tenant DB | Production |
| Ventes (devis, BL, factures) | ✅ | ✅ | Tenant DB | Production |
| Trésorerie | ✅ | ✅ | Tenant DB | Production |
| Rapports | ✅ | ✅ | Agrégations | Production |
| Administration | ✅ | ✅ | Master + Tenant | Production |
| Achats | ✅ | ✅ | Tenant DB | Production |
| Stock | ✅ | ✅ | Tenant DB | Production |
| Comptabilité | ✅ | ✅ (13 écrans) | Tenant DB | Production |
| CRM | ✅ | ✅ | Tenant DB | Fonctionnel |
| Fiscal / TEJ | ✅ | ✅ | Tenant DB | Production (TN) |
| Assistant IA | ✅ | ✅ | Tenant DB | Avancé |
| Prévisions IA | ✅ | ✅ | Tenant DB | V2 réappro cutover |

### 3.2 Modules hors catalogue

| Module | Statut | Commentaire |
|---|---|---|
| POS | ⚠️ Partiel | UI complète ; API session in-memory (`PosSessionController`) |
| Virtual Street | ✅ | Vitrine 3D + commandes ; hors `AppModule` |
| Backoffice plateforme | ✅ | Billing, dunning, plans, migrations, 2FA admin |
| ChannelGateway | 🔜 Prévu | Entités DB ; orchestrateur vide ; pas de source Node |

### 3.3 Atouts techniques différenciants

- Clean Architecture + CQRS + domain events  
- Multi-tenant (Master DB + Tenant DB isolée)  
- Écritures comptables automatiques (factures, paiements, caisse, achats)  
- Audit chain réparable (`FactuTrust.AuditChainRepair`)  
- Licences par module + grants utilisateur  
- IA tool-calling métier + OCR factures + export PowerPoint  
- Forecasting statistique + réappro V2 avec auto-PO  
- Conformité TEJ (retenue à la source, export XML)  

---

## 4. Matrice des écarts détaillée

### 4.1 Finance & Comptabilité

| Fonctionnalité | Statut | Priorité | Effort (j/h) | Phase |
|---|---|---|---|---|
| Plan comptable | ✅ | — | — | — |
| Journal / Grand livre / Balance | ✅ | — | — | — |
| Grand livre général multi-comptes + récap. par racine | ✅ | — | — | — |
| Balance détaillée + balance par période (12 mois) | ✅ | — | — | — |
| Journaux : centralisateur / récapitulation / totaux | ✅ | — | — | — |
| Balance générale / Compte de résultat | ✅ | — | — | — |
| Lettrage | ✅ | — | — | — |
| Balance âgée clients/fournisseurs | ✅ | — | — | — |
| Déclaration TVA | ✅ | — | — | — |
| Clôture période / exercice / à-nouveaux (auxiliarisés) | ✅ | — | — | — |
| Export FEC | ✅ | — | — | — |
| Modèles d’écritures | ✅ | — | — | — |
| Écritures auto (ventes/achats/paiements) | ✅ | — | — | — |
| Budgets & contrôle de gestion | ✅ | — | — | — |
| Immobilisations & amortissements | ✅ | — | — | — |
| Emprunts & tableau d’amortissement (annuité / capital constants) | ✅ | — | — | — |
| Liasse fiscale tunisienne (états NCT + résultat fiscal) | ✅ | — | — | — |
| Comptabilité analytique (centres de coûts) | ❌ | P1 | 45 | 1 |
| Trésorerie prévisionnelle (cash flow) | ❌ | P1 | 35 | 1 |
| Multi-devises + réévaluation | ❌ | P2 | 55 | 2 |
| Consolidation multi-sociétés | ❌ | P2 | 80 | 4 |
| Intercompagnie automatique | ❌ | P2 | 50 | 4 |
| Provisions ( créances douteuses, stock ) | ❌ | P2 | 40 | 2 |
| Notes de frais → compta | ❌ | P2 | 35 | 2 |
| Livre d’inventaire légal (PDF figé + provisions détaillées) | ✅ | — | — | — |
| État de rapprochement bancaire imprimable | ✅ | — | — | — |
| Contrôle d’intégrité comptable (lecture seule) | ✅ | — | — | — |
| Import étendu (plan comptable / plan tiers / balance d’ouverture) | ✅ | — | — | — |
| Export d’archive de dossier (ZIP, lecture seule) | ✅ | — | — | — |
| Modifications de masse des brouillons (journal/date/libellé + suppression) | ✅ | — | — | — |
| Annexes NCT éditables (surcharge des notes auto) | ❌ | P2 | 15 | 2 |
| Migration de dossier avec mapping de comptes | ❌ | P2 | 10 | 2 |
| Restauration de dossier + mass-edit du validé + extension tiers | ❌ | P3 | 60 | 4 |

**Sous-total gaps compta/finance restants : ~340 j/h** _(budgets, immobilisations, liasse fiscale
livrés ; grand livre général, balances détaillée/par période, récapitulatifs de journaux, état de
rapprochement, livre d’inventaire, contrôle d’intégrité, import étendu, export ZIP de dossier,
modifications de masse des brouillons et emprunts/tableau d’amortissement ajoutés en 2026-07 — cf.
[audit de couverture menu Éditions/Maintenance](../../src/Backend/FactuTrust.Application/Features/Accounting))._

---

### 4.2 Trésorerie & Paiements

| Fonctionnalité | Statut | Priorité | Effort (j/h) | Phase |
|---|---|---|---|---|
| Suivi paiements clients/fournisseurs | ✅ | — | — | — |
| Caisse & dépenses | ✅ | — | — | — |
| Comptes bancaires (réf. TN) | ✅ | — | — | — |
| Dépôts bancaires | ✅ | — | — | — |
| Rapprochement bancaire | ✅ | — | — | — |
| Plafonds de crédit client & blocage auto | ❌ | P1 | 25 | 1 |
| Paiement en ligne client (portail) | ❌ | P1 | 40 | 1 |
| Prélèvements / virements SEPA automatisés | ❌ | P3 | 45 | 3 |
| Affacturage / escompte | ❌ | P3 | 30 | 3 |
| Multi-devises trésorerie | ❌ | P2 | 30 | 2 |

**Sous-total gaps trésorerie : ~170 j/h**

---

### 4.3 Ventes & Distribution

| Fonctionnalité | Statut | Priorité | Effort (j/h) | Phase |
|---|---|---|---|---|
| Devis → BL → Facture → Avoir | ✅ | — | — | — |
| Wizard facturation + brouillons | ✅ | — | — | — |
| Signature électronique | ✅ | — | — | — |
| PDF / Email | ✅ | — | — | — |
| Import facture IA (OCR) | ✅ | — | — | — |
| Modèles de devis (CRM) | ✅ | — | — | — |
| Champs TTN sur facture | ⚠️ | P0 | 80 | 1 |
| Soumission TTN / El Fatoora bout-en-bout | ❌ | P0 | (inclus ci-dessus) | 1 |
| Import factures achat depuis TTN | ❌ | P1 | 40 | 1 |
| Archivage probatoire 10 ans | ❌ | P1 | 35 | 1 |
| Listes de prix par client/segment | ❌ | P0 | 40 | 1 |
| Remises cascades / grilles quantité | ❌ | P1 | 35 | 1 |
| Variantes produit (taille, couleur) | ❌ | P1 | 50 | 1 |
| Kits / bundles | ❌ | P2 | 30 | 2 |
| Contrats récurrents / abonnements B2B | ❌ | P2 | 55 | 2 |
| Portail client (consultation, paiement) | ❌ | P1 | 50 | 1 |
| POS enterprise (caisses, clôtures Z) | ⚠️ | P0 | 65 | 1 |
| Conformité caisse enregistreuse TN | ❌ | P1 | 45 | 1 |
| Mode offline POS | ❌ | P2 | 40 | 2 |

**Sous-total gaps ventes : ~565 j/h**

---

### 4.4 Achats & Approvisionnement

| Fonctionnalité | Statut | Priorité | Effort (j/h) | Phase |
|---|---|---|---|---|
| Fournisseurs | ✅ | — | — | — |
| Bons de commande + réception | ✅ | — | — | — |
| Factures fournisseurs + paiements | ✅ | — | — | — |
| PO → facture fournisseur | ✅ | — | — | — |
| Retenue TEJ sur achats | ✅ | — | — | — |
| Réappro IA → auto PO | ✅ | — | — | — |
| Workflow approbation achats multi-niveaux | ❌ | P1 | 40 | 1 |
| Appels d’offres / comparatif devis fournisseurs | ❌ | P3 | 35 | 3 |
| Contrats cadre fournisseurs | ❌ | P2 | 30 | 2 |
| Références multi-fournisseurs par produit | ❌ | P1 | 25 | 1 |
| Email facture fournisseur (TODO code) | ⚠️ | P2 | 5 | 1 |

**Sous-total gaps achats : ~135 j/h**

---

### 4.5 Stock & Logistique (WMS)

| Fonctionnalité | Statut | Priorité | Effort (j/h) | Phase |
|---|---|---|---|---|
| Multi-entrepôts | ✅ | — | — | — |
| Mouvements + CMUP | ✅ | — | — | — |
| Transferts inter-entrepôts | ✅ | — | — | — |
| Inventaire physique | ✅ | — | — | — |
| Alertes stock min/max | ✅ | — | — | — |
| Déduction auto sur facture/BL | ✅ | — | — | — |
| Traçabilité lots / numéros de série | ❌ | P0 | 60 | 1 |
| Dates de péremption | ❌ | P1 | 25 | 1 |
| Codes-barres / scan mobile | ❌ | P1 | 45 | 1 |
| Picking / packing / expédition | ❌ | P2 | 70 | 2 |
| Transport & tournées | ❌ | P3 | 50 | 3 |
| Réservation avancée (commandes en attente) | ⚠️ | P2 | 20 | 1 |
| Valorisation FIFO/LIFO (vs CMUP seul) | ❌ | P2 | 35 | 2 |
| WMS mobile (inventaire terrain) | ❌ | P2 | 40 | 2 |

**Sous-total gaps stock : ~345 j/h**

---

### 4.6 Production & MRP

| Fonctionnalité | Statut | Priorité | Effort (j/h) | Phase |
|---|---|---|---|---|
| Nomenclatures (BOM) | ❌ | P0* | 55 | 3 |
| Ordres de fabrication | ❌ | P0* | 70 | 3 |
| Gammes / postes de charge | ❌ | P1* | 50 | 3 |
| MRP (besoins nets) | ❌ | P0* | 65 | 3 |
| Coûts de revient industriels | ❌ | P1* | 45 | 3 |
| Planification atelier | ❌ | P2* | 55 | 3 |
| Contrôle qualité production | ❌ | P2* | 40 | 3 |
| OEE / suivi machine | ❌ | P3* | 35 | 3 |

*\*P0 uniquement si segment industriel ciblé.*

**Sous-total gaps production : ~415 j/h**

---

### 4.7 RH & Paie (HCM)

| Fonctionnalité | Statut | Priorité | Effort (j/h) | Phase |
|---|---|---|---|---|
| Dossier salarié | ❌ | P0* | 40 | 4 |
| Contrats de travail | ❌ | P0* | 35 | 4 |
| Pointage / temps | ❌ | P0* | 50 | 4 |
| Congés & absences | ❌ | P0* | 35 | 4 |
| Paie & bulletins | ❌ | P0* | 120 | 4 |
| CNSS / CSS / IRPP (Tunisie) | ❌ | P0* | 90 | 4 |
| Notes de frais | ❌ | P1 | 40 | 2 |
| Recrutement | ❌ | P3 | 45 | 4 |
| Évaluation / compétences | ❌ | P3 | 40 | 4 |
| Organigramme | ❌ | P2 | 20 | 4 |

**Sous-total gaps RH : ~515 j/h**

---

### 4.8 CRM & Marketing

| Fonctionnalité | Statut | Priorité | Effort (j/h) | Phase |
|---|---|---|---|---|
| Pipeline opportunités | ✅ | — | — | — |
| Activités & rappels | ✅ | — | — | — |
| Objectifs commerciaux | ✅ | — | — | — |
| Modèles de devis | ✅ | — | — | — |
| Leads (non clients) + scoring | ❌ | P1 | 35 | 2 |
| Comptes / contacts multiples | ❌ | P1 | 30 | 2 |
| Campagnes email/SMS | ❌ | P2 | 50 | 2 |
| Segmentation RFM | ❌ | P2 | 25 | 2 |
| Service client / tickets SAV | ❌ | P1 | 55 | 2 |
| Sync email (Gmail/Outlook) | ❌ | P2 | 45 | 2 |
| Territoires & équipes commerciales | ❌ | P2 | 30 | 2 |
| Prévisions CRM (commit/best case) | ❌ | P2 | 25 | 2 |

**Sous-total gaps CRM : ~295 j/h**

---

### 4.9 Projets & PSA

| Fonctionnalité | Statut | Priorité | Effort (j/h) | Phase |
|---|---|---|---|---|
| Projets / affaires | ❌ | P0* | 50 | 2 |
| WBS / jalons | ❌ | P0* | 40 | 2 |
| Feuilles de temps | ❌ | P0* | 45 | 2 |
| Facturation forfait / régie / jalons | ❌ | P0* | 55 | 2 |
| Coûts projet vs budget | ❌ | P1* | 40 | 2 |
| Planification ressources | ❌ | P2* | 50 | 2 |

*\*P0 si segment ESN/BTP/conseil ciblé.*

**Sous-total gaps projets : ~280 j/h**

---

### 4.10 Immobilisations

| Fonctionnalité | Statut | Priorité | Effort (j/h) | Phase |
|---|---|---|---|---|
| Registre immobilisations | ❌ | P1 | 40 | 4 |
| Amortissements automatiques | ❌ | P1 | 45 | 4 |
| Cessions / mises au rebut | ❌ | P2 | 25 | 4 |
| Inventaire patrimonial | ❌ | P2 | 30 | 4 |
| Maintenance préventive liée actifs | ❌ | P3 | 40 | 4 |

**Sous-total gaps immobilisations : ~180 j/h**

---

### 4.11 BI, Consolidation & Pilotage

| Fonctionnalité | Statut | Priorité | Effort (j/h) | Phase |
|---|---|---|---|---|
| Rapports opérationnels (18+ endpoints) | ✅ | — | — | — |
| États comptables de recoupement (centralisateur, récap. GL, balances détaillée/périodique) | ✅ | — | — | — |
| Forecasting IA + ABC/XYZ | ✅ | — | — | — |
| Tableaux de bord configurables | ❌ | P1 | 50 | 2 |
| Report builder self-service | ❌ | P2 | 70 | 3 |
| Exports planifiés / alertes KPI | ❌ | P2 | 35 | 2 |
| Entrepôt de données (DWH) | ❌ | P3 | 80 | 4 |
| Consolidation groupe | ❌ | P2 | 80 | 4 |
| Benchmarking sectoriel | ❌ | P3 | 40 | 4 |

**Sous-total gaps BI : ~355 j/h**

---

### 4.12 E-commerce & Omnicanal

| Fonctionnalité | Statut | Priorité | Effort (j/h) | Phase |
|---|---|---|---|---|
| Virtual Street (vitrine 3D) | ✅ | — | — | — |
| Commandes storefront | ✅ | — | — | — |
| WhatsApp / Telegram (ChannelGateway) | 🔜 | P2 | 60 | 2 |
| Sync marketplaces (Amazon, etc.) | ❌ | P3 | 70 | 3 |
| PIM (catalogue enrichi multi-canal) | ❌ | P3 | 55 | 3 |
| Logistique e-commerce (colis, transporteurs) | ❌ | P2 | 50 | 2 |
| Retours SAV e-commerce | ❌ | P2 | 35 | 2 |

**Sous-total gaps e-commerce : ~270 j/h**

---

### 4.13 Intégrations, Gouvernance & Plateforme

| Fonctionnalité | Statut | Priorité | Effort (j/h) | Phase |
|---|---|---|---|---|
| RBAC + permissions granulaires | ✅ | — | — | — |
| Audit trail chaîné | ✅ | — | — | — |
| Multi-tenant SaaS + billing | ✅ | — | — | — |
| API publique partenaire (clés tenant) | ❌ | P0 | 45 | 1 |
| Webhooks sortants configurables | ❌ | P1 | 35 | 1 |
| SSO entreprise (OIDC/SAML) | ❌ | P2 | 40 | 2 |
| LDAP / Active Directory | ❌ | P2 | 30 | 2 |
| Connecteurs ERP/compta tiers | ❌ | P2 | 60 | 2 |
| EDI fournisseurs/clients | ❌ | P3 | 55 | 3 |
| GED documentaire centralisée | ❌ | P1 | 50 | 2 |
| Moteur workflow générique | ❌ | P1 | 55 | 1 |
| App mobile native (iOS/Android) | ❌ | P2 | 120 | 3 |
| Import/export CSV massif tous modules | ❌ | P1 | 40 | 1 |
| Segregation of duties avancée | ❌ | P2 | 25 | 2 |

**Sous-total gaps intégrations : ~555 j/h**

---

## 5. Synthèse effort par phase

| Phase | Objectif | Durée estimée | Effort (j/h) | Modules clés |
|---|---|---|---|---|
| **Phase 1** | ERP commercial Tunisie | 6–9 mois | **~420** | TTN, POS, tarifs, lots, analytique, budgets, API, workflow, portail |
| **Phase 2** | ERP services & distribution avancée | 9–12 mois | **~680** | Projets, CRM 360, GED, multi-devises, notes de frais, omnicanal |
| **Phase 3** | ERP industriel léger | 12–18 mois | **~415** | BOM, OF, MRP, coûts, qualité |
| **Phase 4** | ERP groupe & RH | 18–36 mois | **~695** | RH/paie TN, immobilisations, consolidation, BI avancée |
| **Total ERP complet** | Mid-market | 24–36 mois | **~2 810** | Toutes lignes P0–P2 |

### Phase 1 — Détail (priorité immédiate)

| Lot | Fonctionnalités | j/h |
|---|---|---|
| L1 — Conformité TN | TTN bout-en-bout, archivage probatoire, import achat TTN | 155 |
| L2 — Retail | POS enterprise, conformité caisse TN | 110 |
| L3 — Catalogue & prix | Listes de prix, remises, variantes, refs multi-fournisseurs | 150 |
| L4 — Stock pro | Lots/séries, péremption, codes-barres | 130 |
| L5 — Pilotage | Comptabilité analytique, budgets, cash flow, crédit client | 165 |
| L6 — Plateforme | API publique, webhooks, workflow, import/export CSV, portail client | 230 |
| **Total Phase 1** | | **~940** |

> Note : certains lots peuvent être parallélisés. Effort net Phase 1 ajusté à **~420 j/h** si on ne retient que les P0/P1 critiques ; le détail L1–L6 représente le périmètre complet Phase 1.

---

## 6. Benchmark concurrentiel

Périmètre comparé : **PME/TPE tunisienne, commerce & distribution, 5–50 utilisateurs**.

Légende : ● Complet · ◐ Partiel · ○ Absent · ★ Avantage FactuTrust

| Capacité | FactuTrust | Odoo 17 | Sage 100 / XRT | MS Dynamics 365 BC |
|---|---|---|---|---|
| **Facturation électronique TN** | ◐ (champs TTN) | ○ (localisation tierce) | ◐ | ○ |
| **TEJ / Retenue source** | ● ★ | ○ | ◐ | ○ |
| **Comptabilité générale** | ● | ● | ● | ● |
| **Comptabilité analytique** | ○ | ● | ● | ● |
| **Multi-devises** | ○ | ● | ● | ● |
| **Ventes (devis→facture)** | ● | ● | ● | ● |
| **Achats & PO** | ● | ● | ● | ● |
| **Stock multi-entrepôts** | ● | ● | ● | ● |
| **Lots / séries** | ○ | ● | ◐ | ● |
| **POS** | ◐ | ● | ◐ | ● |
| **Production / MRP** | ○ | ● | ◐ | ● |
| **CRM pipeline** | ◐ | ● | ◐ | ● |
| **Marketing automation** | ○ | ● | ○ | ● |
| **Projets & temps** | ○ | ● | ◐ | ● |
| **RH & Paie TN** | ○ | ◐ (module communautaire) | ● (Sage Paie) | ◐ |
| **Immobilisations** | ○ | ● | ● | ● |
| **BI / Reporting** | ◐ | ● | ◐ | ● |
| **IA métier intégrée** | ● ★ | ◐ (Odoo AI récent) | ○ | ◐ (Copilot) |
| **Prévisions & réappro IA** | ● ★ | ◐ | ○ | ◐ |
| **SaaS multi-tenant natif** | ● ★ | ◐ (Odoo.sh) | ○ | ◐ |
| **E-commerce intégré** | ◐ (Virtual Street) | ● | ○ | ◐ |
| **API / écosystème** | ◐ (Swagger interne) | ● | ◐ | ● |
| **Coût PME (5 users/an)** | Abonnement SaaS | ~800–2 000 € | Licence + maintenance | ~1 500–3 000 € |
| **Time-to-value TN** | ★ Rapide | Long (localisation) | Moyen | Long |

### 6.1 Analyse comparative

**FactuTrust vs Odoo**
- Odoo couvre **plus de modules** (production, RH, projets, immobilisations) out-of-the-box.
- FactuTrust gagne sur : **TEJ natif**, **IA/prévisions**, **SaaS TN-first**, **UX premium**, **architecture multi-tenant enterprise**.
- Gap principal : profondeur modules standards Odoo (MRP, paie, website builder, marketing).

**FactuTrust vs Sage (100 / XRT)**
- Sage fort sur **compta/paie/immobilisations** en écosystème fermé.
- FactuTrust gagne sur : **modernité SaaS**, **IA**, **cycle commercial unifié**, **prix abonnement**.
- Gap : paie tunisienne certifiée, liasse fiscale complète, immobilisations.

**FactuTrust vs Dynamics 365 Business Central**
- Dynamics = ERP **international enterprise** avec écosystème Microsoft.
- FactuTrust gagne sur : **simplicité PME**, **coût**, **spécificités TN**, **time-to-value**.
- Gap : consolidation groupe, Power Platform, intégrations Microsoft, profondeur manufacturing.

### 6.2 Positionnement recommandé

```
                    SPÉCIFICITÉ TUNISIE
                           ↑
                           |
         Sage Paie ●       |       ★ FactuTrust (cible)
                           |
    ───────────────────────┼──────────────────────→ IA / SaaS / UX
                           |
         Odoo ●            |            ● Dynamics BC
                           |
                           ↓
                    COUVERTURE ERP
```

**Message produit :** *« L’ERP intelligent des entreprises commerciales tunisiennes — conforme, unifié, assisté par IA. »*

---

## 7. Matrice de décision par segment

| Segment client | FactuTrust suffit ? | Gaps bloquants | Phase requise |
|---|---|---|---|
| TPE commerce / facturation | ✅ Oui | TTN complet | Phase 1 |
| PME distribution / grossiste | ⚠️ Presque | Lots, tarifs, POS, codes-barres | Phase 1 |
| Retail multi-magasins | ⚠️ Partiel | POS enterprise, WMS light | Phase 1–2 |
| PME industrielle (fabrication) | ❌ Non | MRP, BOM, OF, coûts | Phase 3 |
| ESN / cabinet / BTP services | ❌ Non | Projets, temps, régie | Phase 2 |
| Agro / pharma (péremption) | ⚠️ Partiel | Lots, traçabilité, péremption | Phase 1 |
| Groupe multi-sociétés | ❌ Non | Consolidation, interco, RH | Phase 4 |
| Entreprise 50+ salariés | ❌ Non | RH & paie TN | Phase 4 |

---

## 8. Modules à finaliser (dette produit interne)

Ces éléments **existent partiellement** dans le codebase et doivent être complétés avant d’ajouter de nouveaux modules :

| Élément | Fichier / zone | Action |
|---|---|---|
| POS session in-memory | `PosSessionController.cs` | Persistance EF/Redis, modèle caisse |
| ChannelGateway | `src/ChannelGateway/` | Implémenter source Node + webhooks |
| MultiLevelApproval réappro | `ForecastingOptions.cs` | Activer + UI workflow |
| Email facture fournisseur | `CreateSupplierInvoiceFromPOCommand.cs` | TODO ligne 139 |
| Documentation utilisateur | `docs/utilisateur/` | Chapitres compta, CRM, IA, TEJ, POS |
| Route CRM dupliquée | `app.routes.ts` | Corriger doublon |
| Tests Domain/Application | Solution backend | Ajouter projets de tests |
| Storefront captcha | `StorefrontCaptchaValidator.cs` | Renforcer si production |

---

## 9. Architecture cible — nouveaux modules ERP

Pour chaque module majeur absent, l’extension recommandée respecte l’architecture existante :

```
FactuTrust.Domain/
  Entities/{Module}/
  Enums/
  Events/
FactuTrust.Application/
  Features/{Module}/Commands|Queries/
FactuTrust.Infrastructure/
  Persistence/Configurations/
  Services/{Module}/
FactuTrust.API/
  Controllers/{Module}Controller.cs
src/Frontend/factutrust-web/
  features/{module}/
```

Nouveaux `AppModule` suggérés (Phase 2–4) :

| AppModule | Phase | Dépendances |
|---|---|---|
| `Projects` | 2 | Ventes, Comptabilité analytique |
| `FixedAssets` | 4 | Comptabilité |
| `HumanResources` | 4 | Administration, Comptabilité |
| `Manufacturing` | 3 | Stock, Achats, Produits (BOM) |
| `Integrations` | 1 | Administration |

---

## 10. Risques & prérequis

| Risque | Mitigation |
|---|---|
| Dilution produit (trop de modules) | Verticalisation par phase, feature flags |
| Conformité TTN évolutive | Module fiscal isolé, veille réglementaire |
| Paie TN complexe | Partenariat expert paie ou acquisition composant |
| Dette POS / ChannelGateway | Traiter Phase 1 avant nouveaux modules |
| Scalabilité multi-instance | Redis, outbox pattern (déjà amorcé storefront) |

---

## 11. Prochaines étapes recommandées

1. **Valider le segment cible Phase 1** (PME commerce/distribution TN).  
2. **Prioriser le lot TTN** (soumission, statuts, archivage) — différenciateur n°1.  
3. **Lancer l’API publique v1** — prérequis écosystème et intégrations.  
4. **Roadmap produit formelle** — transformer ce document en epics Linear/Jira.  
5. **Compléter la documentation utilisateur** — modules avancés déjà livrés.  

---

## 12. Références codebase

| Élément | Emplacement |
|---|---|
| Modules licenciés | `src/Backend/FactuTrust.Domain/Enums/AppModule.cs` |
| Catalogue features | `src/Backend/FactuTrust.Domain/Authorization/ModuleFeatureCatalog.cs` |
| API comptabilité | `src/Backend/FactuTrust.API/Controllers/AccountingController.cs` |
| POS (stub) | `src/Backend/FactuTrust.API/Controllers/PosSessionController.cs` |
| Options réappro | `src/Backend/FactuTrust.Application/Configuration/ForecastingOptions.cs` |
| Entités tenant | `src/Backend/FactuTrust.Infrastructure/Persistence/TenantDbContext.cs` |
| Features frontend | `src/Frontend/factutrust-web/src/app/features/` |
| Quality gate compta | `docs/quality-gate-accounting.md` |

---

*Document généré à partir de l’analyse du codebase FactuTrust — mai 2026.*
