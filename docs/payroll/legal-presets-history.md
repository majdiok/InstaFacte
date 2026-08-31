# Historique des presets légaux paie (LF 2020–2026)

> Source de vérité des valeurs codées dans
> [`PayrollLegalPresets.cs`](../../src/Backend/FactuTrust.Domain/Services/Payroll/PayrollLegalPresets.cs).
> Valeurs vérifiées le 2026-08-31 sur les sources publiques citées ci-dessous pour la correction
> Phase 3 / WS-3 du plan d'audit SCE. Chaque ligne du tableau renvoie à la citation JORT/décret/note
> commune qui la justifie.

## Règle cardinale

Les presets ne s'appliquent qu'**au seed initial d'un exercice** ou à un **rechargement explicite**
(bouton **Recharger défauts LF** dans *RH & Paie → Paramètres paie*). Les
`PayrollYearParameters` déjà persistés pour un exercice ne sont **jamais réécrits
automatiquement** — un tenant qui a ajusté un taux ou ses tranches de saisie conserve ses valeurs.
C'est ce qui garantit la non-régression des exercices en production et la traçabilité de tout
changement de paramètre.

Exposé via :
- `GET /api/payroll/settings/parameters/{year}/preset`
- bouton **Recharger défauts LF** (Paramètres paie)

---

## 1. CNSS — Régime des salariés non agricoles (RSNA)

| Exercice | Taux salarié | Taux employeur | Entrée en vigueur | Source |
|----------|-------------:|---------------:|-------------------|--------|
| 2020 | 9,18 % | 16,57 % | taux pré-2019 maintenus | — |
| 2021 | 9,18 % | 16,57 % | — | — |
| 2022 | 9,18 % | 16,57 % | — | — |
| 2023 | 9,18 % | 16,57 % | — | — |
| 2024 | 9,18 % | 16,57 % | — | — |
| 2025 | **9,68 %** | **17,07 %** | **1er janvier 2025** | **LF 2025** |
| 2026 | **9,68 %** | **17,07 %** | idem (report) | **LF 2025** |

- **Hausse de 0,50 pt** (salarié 9,18 → 9,68 ; employeur 16,57 → 17,07) entrée en vigueur au
  **1er janvier 2025** par la Loi de Finances 2025.
- Sources web vérifiées 2026-08 : smartpaie.tn (« taux CNSS 2026 »), paie-tunisie.com,
  jurisitetunisie.com, efacturetn.com.
- L'ancien preset appliquait 9,18/16,57 à **tous** les exercices y compris 2025-2026 (CAL-001).
  Corrigé en paramétrant le taux par exercice.

## 2. CNSS — Régime des salariés agricoles (RSA)

| Exercice | Taux salarié | Taux employeur | Source |
|----------|-------------:|---------------:|--------|
| 2020–2026 (const.) | **4,57 %** | **7,72 %** | en vigueur depuis le **01/07/2011** |

- Taux distinct du RSNA, constant sur toute la période 2020-2026 (CAL-002). L'ancien preset
  reprenait par défaut les taux RSNA pour le régime agricole.
- **Limite de modélisation** : le régime agricole réel calcule la cotisation sur une **assiette
  forfaitaire SMAG** (salaire minimum agricole garanti, par jour), et non sur la masse salariale
  réelle. Le moteur applique ici le taux RSA à la masse salariale comme pour le RSNA
  (simplification) — le calcul SMAG forfaitaire réel n'est pas modélisé. À documenter pour un
  client agricole si besoin.

## 3. SMIG mensuel (régime 48 h)

| Exercice | SMIG mensuel (TND) | Décret | Entrée en vigueur |
|----------|-------------------:|--------|-------------------|
| 2020 | 429,312 | décret n° 2020-1069 du 30/12/2020 | 01/10/2020 |
| 2021 | 429,312 | (report) | — |
| 2022 | 459,264 | décret n° 2022-769 du 19/10/2022 | 01/10/2022 |
| 2023 | 459,264 | (report) | — |
| 2024 | 491,504 | décrets n° 2024-419 / 2024-420 du 09/07/2024 | 01/05/2024 |
| 2025 | 528,320 | (mêmes décrets 2024-419/420) | 01/01/2025 |
| 2026 | **554,736** | décret n° 2026-67 du 30/04/2026, JORT n° 44 | **01/01/2026** |

- Le SMIG sert de référence à l'exonération IRPP SMIG (art. 21 / déduction annuelle 500 TND) et
  au barème de saisie sur salaire conventionnel (tranches indexées sur le SMIG).
- L'ancien preset mettait **528,320** pour 2026 (CAL-004) ; la valeur légale au 01/01/2026 est
  **554,736** (décret 2026-67). Corrigé.

## 4. Contribution de solidarité sociale (CSS)

| Exercice | Taux salarié | Source |
|----------|-------------:|--------|
| 2020 | **1 %** | taux de droit commun (avant LF 2023) |
| 2021 | **1 %** | idem |
| 2022 | **1 %** | idem |
| 2023 | **0,5 %** | **LF 2023** (ramène le taux à 0,5 %) |
| 2024 | 0,5 % | (report) |
| 2025 | 0,5 % | (report) |
| 2026 | 0,5 % | **note commune 01-2026** (maintien à 0,5 %) |

- Seuil d'exonération annuelle : **5 000 TND** de net imposable cumulé (constant sur la période).
- La LF 2023 a ramené le taux de 1 % à **0,5 %** ; la **note commune 01-2026** confirme le
  maintien à 0,5 % pour 2026, malgré des rumeurs de retour à 1 % non retenues (confirmé par la
  presse du 14/01/2026).
- L'ancien preset codait **0,5 % en dur pour tous les exercices** (CAL-005), omettant le 1 %
  légal de 2020-2022. Corrigé en paramétrant le taux par exercice.

## 5. Barème IRPP annuel

### 2020–2024 : barème légal 5 tranches (legacy)

| Tranche (net imposable annuel, TND) | Taux |
|-------------------------------------|-----:|
| 0 – 5 000 | 0 % |
| 5 000 – 20 000 | 26 % |
| 20 000 – 30 000 | 28 % |
| 30 000 – 50 000 | 32 % |
| > 50 000 | 35 % |

- En vigueur jusqu'au **31/12/2024**. Source : finances.gov.tn (barème IRPP antérieur à 2025).
- **CAL-003** : l'ancien preset utilisait `Irpp2024Brackets`, un barème hybride à 6 tranches
  (0/15/25/30/33/36/38) qui **ne correspond à aucun texte publié**. Sur un net annuel de
  19 796 TND il produisait 2 219,400 TND d'IRPP au lieu des **3 846,960 TND** légaux
  ((19 796 − 5 000) × 26 %). Barème supprimé ; 2024 utilise désormais le 5-tranches légal.

### 2025–2026 : barème 8 tranches (LF 2025)

| Tranche (net imposable annuel, TND) | Taux |
|-------------------------------------|-----:|
| 0 – 5 000 | 0 % |
| 5 000 – 10 000 | 15 % |
| 10 000 – 20 000 | **25 %** |
| 20 000 – 30 000 | 30 % |
| 30 000 – 40 000 | 33 % |
| 40 000 – 50 000 | 36 % |
| 50 000 – 70 000 | 38 % |
| > 70 000 | 40 % |

- Entré en vigueur au **1er janvier 2025** (LF 2025, note commune 03-2025).
- **Point critique** : la tranche 10 000–20 000 est à **25 %** (et non 30 %). Une tranche
  intermédiaire à 30 % serait une erreur de calcul d'environ 144 TND/an sur un salaire mensuel
  de 2 000 TND (IRPP 408,587 au lieu de **264,100**). Vérifié par
  `PayrollLegalPresetsOfficialFiguresTests`.

## 6. Saisie sur salaire (cession des rémunérations)

Deux barèmes coexistent :

### 6a. Convention simplifiée indexée sur le SMIG (exercices présetés 2020-2026)

| Borne inférieure (net mensuel) | Fraction saisissable |
|-------------------------------:|----------------------:|
| 0 | 0 (insaisissable sous le SMIG) |
| 1 × SMIG | 0,333 (1/3) |
| 2 × SMIG | 0,666 (2/3) |

- Convention **historique de l'application** (pas le barème légal strict). Désormais paramétrée
  **avec le SMIG propre à l'exercice** (ex. 554,736 pour 2026) au lieu d'une liste statique
  partagée par toutes les années (R-11 / CAL-004).
- **Conservée volontairement** pour ne pas modifier le comportement des tenants déjà seedés sur
  ces exercices (R-26 : les tranches déjà en base restent inchangées quoi qu'il arrive).

### 6b. Barème légal art. 354 du Code de procédure civile et commerciale (CPCC)

| Borne inférieure (net mensuel, TND) | Borne annuelle (TND) | Fraction saisissable |
|------------------------------------:|---------------------:|----------------------:|
| 0 | 0 | 1/20 |
| 25 | 300 | 1/10 |
| 50 | 600 | 1/5 |
| 75 | 900 | 1/4 |
| 100 | 1 200 | 1/3 |
| 125 | 1 500 | 2/3 |
| 250 | 3 000 | 1 (au-delà, sans limitation) |

- Barème **légal strict**, indépendant du SMIG (contrairement à 6a). Bornes annuelles ÷ 12.
- Utilisé par `PayrollLegalPresets.Resolve()` pour les **exercices non explicitement présetés**
  (années futures non couvertes par une loi de finances) — au lieu d'hériter la convention SMIG
  de l'exercice précédent.
- **R-26** : le barème art. 354 CPCC n'est **pas** appliqué rétroactivement aux exercices déjà
  seedés ; les tranches paramétrées par un tenant (`ReplaceGarnishmentBrackets`) restent
  inchangées. La migration vers le barème légal pour les exercices existants est un choix tenant,
  pas un changement automatique.

## 7. Autres paramètres (constants 2020-2026)

| Paramètre | Valeur | Source / remarque |
|-----------|-------:|-------------------|
| Frais professionnels | 10 %, plafond 2 000 TND/an | droit commun IRPP |
| Déduction chef de famille | 300 TND/an | — |
| Déduction par enfant | 100 TND/an (max 4) | — |
| Enfant étudiant | 1 000 TND/an | — |
| Enfant handicapé | 2 000 TND/an | — |
| Déduction parents | 5 %, plafond 450 TND/an | — |
| TFP industrie | 1 % | — |
| TFP autres | 2 % | — |
| FOPROLOS | 1 % | — |
| CSS employeur | 0 % (2026) / sinon non applicable | profil Sce2026 |

## Notes de mise à jour

Pour ajouter un exercice (ex. 2027 après publication de la LF 2027) :
1. Ajouter une entrée dans `PayrollLegalPresets.Presets` avec les taux/barèmes officiels.
2. Citer les sources (JORT/décret/note commune) dans ce fichier.
3. Ajouter un test pinnant les valeurs officielles dans
   `PayrollLegalPresetsOfficialFiguresTests.cs`.
4. Mettre à jour la table SMIG si un nouveau décret est publié.
