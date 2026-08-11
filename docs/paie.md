# Module Paie — Règles de calcul (normes tunisiennes)

> Référence fonctionnelle du module RH & Paie. Le moteur est `PayrollCalculator`
> (`src/Backend/FactuTrust.Domain/Services/Payroll/PayrollCalculator.cs`) — fonction pure,
> tous les taux proviennent de `PayrollYearParameters` (paramétrables par exercice dans
> **RH & Paie → Paramètres paie**, une ligne par exercice, créée avec les défauts légaux).

## Ordre de calcul d'un bulletin

1. **Brut cotisable (CNSS)** = salaire de base + primes imposables & cotisables + indemnités
   cotisables non imposables + heures supplémentaires − absences non rémunérées.
   Le **brut imposable** suit la même logique avec les primes imposables non cotisables
   (matrice 4 quadrants, activable par l'option « Matrice primes »).
2. **CNSS salariale** = brut cotisable × 9,18 % (RSNA) ou taux RSA paramétré ; 0 pour les
   régimes exonérés (SIVP/CIVP).
3. **Base imposable après CNSS** = brut imposable − CNSS salariale.
4. **Frais professionnels** = 10 % de la base, plafonnés à 2 000 TND/an (166,667 TND/mois).
5. **Déductions familiales** (art. 40 code IRPP, annuelles puis ÷ 12) :
   - chef de famille : 300 TND ;
   - enfants à charge : 100 TND/enfant dans la limite de `MaxDeductibleChildren` (4) ;
   - enfants **étudiants** non boursiers < 25 ans : 1 000 TND/enfant — ils consomment le
     plafond de rang **en priorité** (déduction la plus favorable) ;
   - enfants **infirmes** : 2 000 TND/enfant, **hors plafond** de rang ;
   - **parents à charge** (0 à 2) : 5 % du revenu net annuel (base après CNSS − frais pro,
     annualisée), plafonné à 450 TND par parent. La déduction n'est appliquée que si des
     **déclarations nominatives** (CIN parent) sont renseignées et sans conflit de non-cumul
     intra-entreprise (voir ci-dessous).
6. **IRPP** : net imposable mensuel × 12 → barème progressif de l'exercice → ÷ 12.
   Barème LF 2025 par défaut : 0 % ≤ 5 000 ; 15 % ; 25 % ; 30 % ; 33 % ; 36 % ; 38 % ;
   40 % > 70 000 TND/an.
6bis. **Exonération IRPP SMIG (art. 21)** — si activée dans les paramètres de l'exercice :
   après le calcul IRPP brut, le moteur applique l'un des deux modes configurables
   (voir [Exonération IRPP SMIG](payroll/smig-irpp-exemption.md)). La CSS n'est pas impactée.
7. **CSS** = 0,5 % du net imposable annuel ÷ 12, **exonérée** si le net imposable annuel
   ne dépasse pas 5 000 TND (seuil paramétrable).
8. **Net à payer** = brut total − CNSS salariale − IRPP − CSS − autres retenues (avances…).
9. **Charges patronales** (hors net) : CNSS 16,57 % (ou RSA), accident de travail (taux
   propre au contrat, 0 si régime exonéré), **TFP** (1 % si « Secteur industriel » coché
   dans les paramètres de l'exercice, sinon 2 %), FOPROLOS 1 %.

Arrondis : 3 décimales (millimes), `MidpointRounding.AwayFromZero`, à chaque étape.

## Heures supplémentaires

Le calcul dépend du **régime hebdomadaire du contrat** (`EmploymentContract.WeeklyRegime`) :

| Régime | Diviseur mensuel | Taux légaux proposés |
|---|---|---|
| 48 h/semaine (défaut) | 208 (26 j × 8 h) | **175 %** (majoration légale 75 %), 200 % sur option |
| 40 h/semaine | 173,33 (40 × 52 ÷ 12) | **125 %** (41ᵉ–48ᵉ h), **150 %** (au-delà de 48 h) |

`Montant = (salaire de base ÷ diviseur) × heures × (taux ÷ 100)`, override manuel possible.
Le régime est résolu **côté serveur** depuis le contrat actif (création, modification,
prévisualisation). Les taux 125/150 restent acceptés partout (lignes historiques) ; 175 %
est accepté sans option pour le régime 48 h ; 175/200 ailleurs via l'option « Taux HS
étendus » de l'exercice. Les lignes existantes conservent leur montant calculé tant
qu'elles ne sont pas rééditées.

## Cycles de paie et gel des bulletins

`Brouillon → Calculé → Validé → Clôturé` (réouverture possible avant clôture).

- Les bulletins sont des **instantanés gelés** : aucune modification de paramètres, de
  contrat ou de moteur ne recalcule rétroactivement un cycle **Validé/Clôturé**.
  Les cycles Brouillon/Calculé prennent les nouveautés au prochain « Calculer ».
- La validation génère l'écriture comptable de paie et solde les avances ; la réouverture
  les dé-solde.
- Congés : acquisition de 1 jour / 26 jours travaillés, créée à la validation du cycle.

## Prorata automatique embauche / départ / suspension

Option d'exercice **`EnableAutomaticProrata`**, désactivée par défaut dans **RH & Paie →
Paramètres paie → Conformité**. Tant qu'elle est inactive, le calcul reste strictement
identique à l'historique.

### Formule (salaire de base uniquement)

- Convention : **26 jours ouvrables** par mois (lun–ven).
- Fenêtre de présence : intersection mois civil × contrat actif × date d'embauche × date de sortie.
- `Jours travaillés = 26 × (jours ouvrables présents / jours ouvrables du mois)` − suspensions non payées approuvées.
- `Retenue prorata = (salaire de base ÷ 26) × jours non rémunérés`.

Les **indemnités contractuelles récurrentes** restent au montant mensuel plein en v1. Les
**absences manuelles** (congé sans solde / injustifié) restent une ligne distincte sur le bulletin.

### Parcours utilisateur

- **Départ** : fiche salarié → « Déclarer un départ » (date, clôture contrat).
- **Suspension** : onglet Suspensions (période, type, payé/non payé, approbation).
- **Prévisualisation** : détail cycle → « Prévisualiser le prorata » avant calcul.

Les salariés partis en cours de mois restent inclus dans le cycle du mois de départ lorsque
l'option est active.

## Régularisation IRPP annuelle

Le calcul mensuel annualise l'impôt **par projection** (net imposable du mois × 12). Dès que la
rémunération varie au fil de l'année (prime, heures supplémentaires exceptionnelles, embauche ou
départ en cours d'exercice, changement de situation familiale), la somme des douze retenues
s'écarte de l'impôt réellement dû sur le revenu annuel.

La régularisation compare l'impôt dû sur le **cumul réel** à l'impôt **déjà retenu**, et porte
l'écart sur le bulletin du mois.

> **Option d'exercice `EnableIrppRegularization`, désactivée par défaut.** Tant qu'elle n'est pas
> activée dans **RH & Paie → Paramètres paie**, le calcul de paie est strictement inchangé.

### Formules

Pour un salarié, un exercice `Y` et un mois de régularisation `M` :

```
cumulNetTaxable  = Σ Payslip.MonthlyNetTaxable   m = 1..M
cumulIrppRetenu  = Σ Payslip.Irpp                m = 1..M   (IRPP mensuel pur)
irppDû           = barème(cumulNetTaxable)
irppDelta        = irppDû − cumulIrppRetenu      > 0 rappel | < 0 restitution
```

La CSS suit la même logique (0,5 % du cumul, nulle sous le seuil d'exonération).

Les mois `1..M-1` proviennent **uniquement des cycles Validés ou Clôturés** : le cumul est ainsi
stable d'un calcul à l'autre. Le mois `M` est lu sur le cycle en cours, qui doit être **Calculé**.

### Sens de l'écart — presque toujours une restitution

Le barème progressif étant **convexe**, la méthode par projection prélève toujours au moins
l'impôt réellement dû. En pratique la régularisation produit donc une **restitution** :

| Situation | Effet |
|---|---|
| Rémunération strictement constante | Écart nul (au millime d'arrondi près) |
| Prime ou heures sup. ponctuelles | Restitution — le mois concerné a été sur-taxé |
| Embauche ou départ en cours d'année | Restitution importante (voir ci-dessous) |

**Année incomplète** : le barème annuel s'applique au cumul **réel**, sans proratisation des
tranches. Un salarié embauché en octobre a un revenu annuel effectivement faible ; la restitution
qui en découle est le comportement attendu, pas une anomalie. L'écran l'affiche explicitement
(« Année incomplète : N bulletins arrêtés sur M »).

### Déclenchement et parcours

Éligibilité : mois de **décembre**, ou mois où le contrat s'achève (**solde de tout compte**).
L'ajout manuel reste possible à tout moment depuis l'écran dédié.

```
Cycle décembre : Créer → Calculer → [Générer les régularisations] → Calculer → Valider → Clôturer
```

Le premier « Calculer » produit les bulletins ; la génération lit les cumuls et propose les
montants, **visibles et ajustables** ; le second « Calculer » les porte sur les bulletins.

### Garanties

- **Idempotence** : `Payslip.Irpp` conserve le sens d'IRPP mensuel pur, la régularisation vit dans
  une colonne distincte, et le cumul ne lit jamais cette colonne. Régénérer produit exactement le
  même montant, même après réouverture et recalcul — aucun double comptage possible.
- **Ajustements préservés** : une ligne corrigée manuellement voit ses cumuls rafraîchis mais son
  montant conservé, et reste signalée « Ajusté manuellement ».
- **Verrouillage** : comme toute variable du mois, une régularisation ne peut plus être créée,
  modifiée ni supprimée dès que le cycle du mois est Validé ou Clôturé.
- **Écrêtage** : un rappel ne peut excéder le net disponible. L'IRPP est servi en priorité, la CSS
  absorbe le solde, et l'excédent est exposé en `RegularizationDeferred` (le bulletin en garde la
  trace). Les restitutions ne sont jamais écrêtées.

### Comptabilité

La régularisation transite par le **compte 432** (État, retenues à la source), agrégée avec la
retenue mensuelle. Ce bucket pouvant devenir **négatif** lorsque les restitutions l'emportent,
`PayrollJournalEntryBuilder` l'impute via `AddSigned` : au crédit s'il est positif, **au débit**
s'il est négatif. Sans cela la ligne disparaîtrait et l'écriture serait déséquilibrée en silence.

Le net du bulletin intégrant déjà la régularisation, l'export virement bancaire et le suivi des
paiements sont corrects sans traitement particulier.

### Écran

**RH & Paie → Régularisation IRPP** (permission `payroll:read` pour consulter et calculer,
`payroll:run` pour enregistrer) : sélection du salarié et de la période, bouton **Calculer**
(prévisualisation sans persistance), détail mois par mois, carte récapitulative, montants
ajustables et notes. La grille batch est disponible sur le détail d'un cycle **Calculé**.

## DTS CNSS (déclaration trimestrielle)

Agrège les bulletins des 3 mois du trimestre, **uniquement pour les cycles Validés ou
Clôturés**. Export CSV (`;`, UTF-8 BOM) avec ligne TOTAL.

## Certificats de retenue à la source (IRPP/CSS)

Disponible dans **RH & Paie → Déclarations paie → Certificats RS** (permission `payroll:declare`).

Agrège, par salarié et par exercice civil, les montants figés sur les bulletins des cycles
**Validés ou Clôturés** :

| Champ | Formule |
|---|---|
| Net imposable annuel | Σ `Payslip.MonthlyNetTaxable` |
| IRPP retenu | Σ (`Payslip.Irpp` + `Payslip.IrppRegularization`) |
| CSS retenue | Σ (`Payslip.Css` + `Payslip.CssRegularization`) |

### Exports

- **CSV récapitulatif** : tous les salariés sur une ligne (`certificats_rs_{year}.csv`).
- **ZIP** : un PDF par salarié + copie du CSV (`_recap.csv`).
- **PDF unitaire** : téléchargement depuis la grille.

### Règles

- Le **NIF employeur** est obligatoire (bloquant si absent).
- CIN/CNSS manquants : avertissement non bloquant.
- Identité salarié (CIN, adresse) lue sur la **fiche courante** au moment de la génération.
- Hors périmètre v1 : télédéclaration TEJ, persistance des certificats validés.

Endpoints : `GET api/payroll/declarations/withholding-certificates`, `…/export/csv`,
`…/export/zip`, `…/{employeeId}/pdf`.

## États de contrôle

Deux éditions en lecture seule sur les bulletins gelés (aucun recalcul), permission
`payroll:read` pour consulter, `payroll:export` pour télécharger. Formats PDF, Excel et CSV.

### Périmètre commun

- Par défaut, seuls les cycles **Validés ou Clôturés** sont retenus (même règle que la DTS).
- L'option « inclure les cycles calculés » ajoute les cycles **Calculés** ; l'état est alors
  marqué **PROVISOIRE** (bandeau à l'écran, mention sur le PDF/Excel, suffixe `_provisoire`
  sur le nom de fichier). Les cycles **Brouillon** sont toujours exclus (aucun bulletin persisté).
- Montants arrondis au millime (`MidpointRounding.AwayFromZero`) à chaque agrégation.

### Livre de paie simplifié

`RH & Paie → Livre de paie` — registre des salaires par salarié sur une **plage de mois** d'un
exercice (Année + Mois de → Mois à). Une ligne par salarié avec le cumul de ses bulletins :
brut, brut CNSSable, CNSS salariale, frais professionnels, déductions familiales, net imposable,
IRPP (+ régularisation), CSS (+ régularisation), autres retenues, net à payer. Les charges
patronales (CNSS patronale, accident de travail, TFP, FOPROLOS, CSS patronale) et le coût
employeur sont restitués en cumul de période, pas au détail salarié.

L'identité (CIN, catégorie, échelon, date d'embauche) est relue sur la fiche salarié ; le nom,
le matricule et le n° CNSS proviennent du bulletin figé. Un salarié supprimé conserve donc sa
ligne, sans les champs enrichis.

Endpoints : `GET api/payroll/reports/payroll-book` et `…/payroll-book/export?format=`.

### Journal de paie

`RH & Paie → Journal de paie` — état **mensuel** à deux vues :

- **Par salarié** : une ligne par bulletin avec toutes les rubriques, plus les charges
  patronales et le coût employeur (brut + charges) — information absente ailleurs.
- **Ventilation comptable** : l'écriture OD du cycle avec contrôle d'équilibre débit = crédit.
  Quand le cycle est comptabilisé, l'état **reprend l'écriture réellement enregistrée**
  (n° de pièce et journal rappelés) ; sinon il affiche une **simulation** construite depuis les
  totaux figés via `PayrollJournalEntryBuilder`, explicitement signalée comme telle.

Comptes de la ventilation : `640` charges de personnel (D), `647` charges sociales employeur (D),
`421` personnel — rémunérations dues (C, ventilé par compte auxiliaire si l'option est active),
`432` État — retenues et taxes sur salaires (C), `453` organismes sociaux (C), `425` personnel —
avances et acomptes (C). Les buckets nuls sont omis.

Un mois sans cycle renvoie 404 ; un cycle Brouillon ou Calculé non demandé renvoie 409 avec un
message explicite.

Endpoints : `GET api/payroll/reports/payroll-journal` et
`…/payroll-journal/export?format=&view=ByEmployee|Accounting`.

## Export virement bancaire (salaires)

Disponible depuis un cycle **Validé** ou **Clôturé** (`RH & Paie → Cycles → Export virement`),
permission `payroll:export`.

### Règles d'éligibilité

- Montant = `Payslip.NetSalary` figé (pas de recalcul).
- Inclus si net > 0 **et** RIB salarié valide (20 chiffres).
- Exclus (listés en prévisualisation) : net nul, RIB manquant/invalide, salarié introuvable.
- Le RIB est lu sur la **fiche salarié au moment de l'export** (non snapshoté sur le bulletin
  en v1). Modifier un RIB après validation change le prochain export.

### Format CSV standard (MVP)

- Séparateur `;`, UTF-8 avec BOM, montants `0.000` (invariant).
- Section méta (`# Société`, `# Compte débiteur RIB/IBAN`, `# Banque`, totaux…).
- Colonnes : `Matricule;Nom;Prénom;RIB;IBAN;Montant net;Libellé;CIN;CNSS` + ligne `TOTAL`.
- IBAN salarié dérivé du RIB (ISO 13616). Compte débiteur = compte banque entreprise
  sélectionné ou compte par défaut (optionnel).

Endpoints : `GET api/payroll/runs/{id}/bank-transfer/preview` et
`…/bank-transfer/export?format=csv`.

Architecture extensible (`PayrollBankTransferFormat` / writers) pour formats banque
propriétaires (BIAT TXT, etc.) dès que les modèles officiels sont disponibles — hors MVP.

## Lignes de bulletin — colonnes Base / Taux

Renseignées pour : Retenue CNSS, CNSS patronale, Accident de travail, TFP, FOPROLOS,
CSS (base = net imposable mensuel, taux 0,5 %), frais professionnels (base + 10 % ; si le
plafond est atteint, libellé « Frais professionnels (plafonnés) » sans couple base × taux),
Retenue IRPP (base seule — barème progressif, pas de taux unique).

## Schéma & migrations

Tables tenant (`TenantDbContext.Payroll.cs`) — migrations dans
`src/Backend/FactuTrust.Infrastructure/Migrations/Tenant/` avec runbooks SQL idempotents
dans `docs/runbooks/sql/` :

| Migration | Runbook |
|---|---|
| `20260714145906_AddPayrollModule_Tenant` | `AddPayrollModule_Tenant.idempotent.sql` |
| `20260715103807_AddPayrollOvertimeAndLeaveBalance_Tenant` | `AddPayrollOvertimeAndLeaveBalance_Tenant.idempotent.sql` |
| `20260715120000_AddPayrollComplianceOptions_Tenant` | `AddPayrollComplianceOptions_Tenant.idempotent.sql` |
| `20260716030504_AddPayrollRegimeSectorFamily_Tenant` | `AddPayrollRegimeSectorFamily_Tenant.idempotent.sql` |
| `20260805150000_AddPayrollIrppRegularization_Tenant` | `AddPayrollIrppRegularization_Tenant.idempotent.sql` |
| `20260806300000_AddPayrollSmigIrppExemption_Tenant` | `AddPayrollSmigIrppExemption.idempotent.sql` |
| `20260807010000_AddEmployeeDependentParents_Tenant` | `AddEmployeeDependentParents_Tenant.idempotent.sql` |
| `20260810120000_AddPayrollTaxBase_Tenant` | `AddPayrollTaxBase_Tenant.idempotent.sql` |

Conventions : montants `decimal(18,3)`, taux `decimal(8,4)` ; toute nouvelle colonne non
nullable porte un défaut qui préserve le comportement antérieur.

## Parents à charge — non-cumul (art. 40 IRPP)

- Saisie sur la fiche salarié : liste de 0 à 2 parents (lien Père/Mère + **CIN obligatoire**).
- Contrainte SQL : index unique filtré `IX_EmployeeDependentParents_ParentCin_Active`
  (`ParentCin` WHERE `EndDate IS NULL`).
- Enregistrement fiche : **409 Conflict** si le CIN est déjà déclaré par un autre salarié.
- Calcul de cycle : **blocage** si conflit détecté ; **avertissement**
  `ParentClaimsIncomplete` + déduction parent = 0 si compteur legacy sans CIN.
- Désactivation salarié : clôture des déclarations actives (libération du CIN).
- Périmètre : **même tenant / même employeur** uniquement.

## Bordereau CNSS mensuel (versement des cotisations)

Fonctionnalité activable via `Accounting.PayrollCnssRemittanceEnabled` (désactivée par défaut).

- **Périmètre** : CNSS salariale + CNSS patronale + accident du travail (compte **453**), aligné sur
  l'écriture d'engagement à la validation du cycle.
- **Source** : bulletins des cycles **Validés ou Clôturés** du mois.
- **Matricule employeur CNSS** : paramétré dans **Mon entreprise** (`Company.CnssEmployerNumber`).
- **Exports** : PDF et CSV de travail (non substitut au bordereau officiel CNSS).
- **Versement** : enregistrement du paiement (permission `payroll:pay`) avec écriture **débit 453 /
  crédit 5321** (ou 5411 espèces).
- **Échéance calendrier fiscal** : 15 du mois suivant (`CnssMonthlyRemittance`).
- **Réouverture cycle** : bloquée tant qu'un versement CNSS actif existe pour le mois.

## Limites connues / évolutions envisagées

- Format officiel de télédéclaration DTS (fichier CNSS) : l'export actuel est un CSV de
  travail, pas le format de dépôt officiel.
- **Parents à charge (non-cumul)** : contrôlé **intra-entreprise** via CIN parent unique
  (table `EmployeeDependentParents`, index filtré actif). Un même parent ne peut être
  déclaré que par un seul salarié du tenant ; le calcul de paie est **bloqué** en cas de
  conflit. Les fiches legacy avec compteur > 0 sans CIN reçoivent une déduction parent
  à **0** et un avertissement jusqu'à saisie nominative. Le non-cumul avec un déclarant
  **hors** de l'entreprise n'est pas contrôlable.
- **Export virement** : CSV standard uniquement ; formats TXT/XLS propriétaires par banque
  (MyBIATCorporate, Multivir, STB…) en phase 2 (nécessitent les modèles officiels).
- **RIB sur bulletin** : non figé à la validation — l'export lit le RIB courant du salarié.
- **Certificats RS** : identité salarié (CIN, adresse) lue à la génération, pas figée sur le bulletin.
- **Prorata auto** : activé par exercice ; identité et dates lues à la génération du calcul.
- **Livre de paie** : le libellé de poste (porté par le contrat, non figé sur le bulletin) n'est
  pas restitué ; catégorie et échelon de la fiche salarié en tiennent lieu.

## Retenues et avantages avancés

Fonctionnalités activables via `AccountingSettings` (désactivées par défaut) :

| Flag | Fonctionnalité |
|------|----------------|
| `PayrollSocialFundsEnabled` | Caisses / mutuelles complémentaires (retenue salariale + charge patronale) |
| `PayrollMealVouchersEnabled` | Tickets restaurant (exonération journalière paramétrable, défaut 3 TND/jour) |
| `PayrollInKindBenefitsEnabled` | Avantages en nature (véhicule, logement…) — imposables/CNSSables, non versés |
| `PayrollEmployeeLoansEnabled` | Prêts salariés sans intérêt avec échéancier mensuel |
| `PayrollGarnishmentsEnabled` | Saisies sur salaire et pensions alimentaires (barème saisissable paramétrable) |
| `PayrollStatutorySickLeaveEnabled` | Congés maladie (carence, IJ CNSS, subrogation, complément employeur) |
| `PayrollStatutoryMaternityLeaveEnabled` | Congés maternité (IJ CNSS + maintien employeur paramétrable) |
| `PayrollStatutoryPaternityLeaveEnabled` | Congés paternité (maintien intégral des jours légaux) |

### Congés statutaires (maladie, maternité, paternité)

Activables via `PayrollStatutorySickLeaveEnabled`, `PayrollStatutoryMaternityLeaveEnabled`,
`PayrollStatutoryPaternityLeaveEnabled` (désactivés par défaut).

- **Maladie** : carence paramétrable (`SickLeaveWaitingDays`, défaut 5 j), IJ CNSS à
  `SickLeaveIjRatePercent` (défaut 66,67 %), plafond annuel 180 j. La carence réduit le brut ;
  le complément employeur et la subrogation sont versés en indemnités non imposables.
- **Maternité** : durée légale `MaternityLeaveDurationDays` (défaut 60 j), maintien employeur
  `MaternityEmployerTopUpDefault` (défaut 100 %). IJ CNSS suivie via créances `CnssIjClaims`.
- **Paternité** : `PaternityLeaveDurationDays` (défaut 2 j) rémunérés intégralement.
- **Créances IJ** : table `CnssIjClaims`, API `GET/POST api/payroll/cnss-ij-claims`.
- **Migration** : `20260809120000_AddPayrollPublicHolidaysAndStatutoryFields_Tenant` (colonnes
  `LeaveRequests` + table `CnssIjClaims` + paramètres statutaires `PayrollYearParameters`).
- **Tests** : `SickLeaveCalculatorTests`, `MaternityLeaveCalculatorTests`.

### Ordre de calcul (après activation)

1. Brut cash + primes + tickets restaurant (part imposable) + heures sup. − absences
2. + Avantages en nature (imposables/CNSSables)
3. CNSS, frais pro, déductions familiales, IRPP, CSS
4. Retenues pré-impôt : avances, prêts, mutuelle, part employée tickets restaurant, compensation AEN
5. **Régularisation IRPP/CSS annuelle** (décembre et soldes de tout compte), écrêtée au net disponible
6. Net avant saisies
7. Saisies / pensions (post-impôt, plafond légal paramétrable)
8. **Net final**

La régularisation est servie **avant** les saisies : la créance de l'État prime, et la quotité
saisissable se calcule donc sur le net réellement perçu.

### Comptes SCE

| Rubrique | Compte par défaut |
|----------|-------------------|
| Avances | 425 |
| Prêts salariés | 425.1 |
| Saisies / pensions | 427 |
| Mutuelle (retenue) | 428.1 |
| Tickets restaurant (part employée) | 428.2 |
| Charges patronales mutuelle | 647 |

L'export virement CSV inclut les lignes **bénéficiaires de saisie** (avec RIB valide) en plus des salaires.

## Gouvernance cabinet / société cliente

Lorsqu'une société possède une **affectation cabinet active** (`FirmClientAssignment`), les opérations
sensibles de paie sont réservées au **cabinet comptable en mode dossier délégué** :

| Opération | Société cliente | Cabinet délégué |
|-----------|-----------------|-----------------|
| Consulter cycles, bulletins, exports | Oui | Oui |
| Gérer fiches salariés, saisir HS/primes/tickets resto | Oui | Non (consultation) |
| Créer / calculer / valider les cycles | Non | Oui |
| Régularisation IRPP (calcul, enregistrement) | Non | Oui |
| Paramètres de paie, jours fériés, primes annuelles | Non | Oui |
| Déclarations sociales et paiements salaires | Non | Oui |

**Société sans cabinet assigné** : comportement inchangé (autonomie complète sur la paie).

Implémentation : filtrage JWT (`payroll_firm_managed`), policy API `payroll:firm-operation`,
pipeline MediatR `PayrollFirmOperationBehavior`. Feature flag : `Features:Payroll:FirmExclusiveOperations`
(défaut `true`).

### Congés de la paie interne du cabinet

Quand le cabinet tient sa **propre** paie (`EnableFirmInternalPayroll`), les congés de ses
collaborateurs ne se saisissent **pas** ici. Ils viennent de *Congés & Absences*
(`/firm/governance/leaves`, base Master), qui en est la source unique, et sont reportés dans le
tenant du cabinet à l'approbation par `FirmLeavePayrollMirrorService`. L'onglet « Congés » de la
fiche salarié est donc en lecture seule en mode cabinet ; **le mode paie client est inchangé.**

Conséquences pour le moteur :

- un `LeaveRequest` miroir est un `LeaveRequest` ordinaire — il alimente `PayrollInputBuilder`
  (retenue `ReducesGross()`), `StatutoryLeavePayrollAggregator`, les IJ CNSS et `LeaveBalanceService`
  exactement comme une saisie directe ;
- **aucun recalcul** : le nombre de jours est celui arrêté côté cabinet (demi-journées comprises,
  fériés issus de `ITunisianCalendarService`). Le report ne repasse jamais par
  `PayrollWorkingDaysCounter`, sans quoi les deux modules afficheraient deux durées ;
- **les mois arrêtés restent gelés** : si un `PayrollRun` `Validated`/`Closed` couvre la période,
  le report est refusé (`BlockedFrozenPayroll`) et l'approbateur est invité à passer par une
  régularisation. L'invariant « un bulletin validé ne se recalcule jamais » prime sur
  l'automatisation.

Détail du mapping et des états : `docs/firm-leaves.md`.

### Checklist QA manuelle

1. Société autonome : créer cycle → saisir HS → calculer → valider → export PDF bulletin.
2. Société avec cabinet : saisir HS sur cycle brouillon ; vérifier impossibilité de créer/calculer.
3. Cabinet délégué : créer cycle → calculer → régularisation IRPP → paramètres → valider.
4. Révocation affectation → refresh token société → retrouver les droits paie complets.
5. Module grants custom : permissions paie toujours cohérentes après filtrage.

## Tableau de bord mensuel

`GET api/payroll/dashboard?year&month` (policy `payroll:read`) rend en un appel la vue d'ensemble
d'un mois : indicateurs et variation M-1, répartition des charges patronales et des retenues,
ventilation du brut, série des douze mois de l'exercice, salariés du cycle, derniers cycles et
échéances sociales. Consommé par `/firm/payroll` ; l'endpoint est volontairement générique et
pourra servir la paie client sans modification serveur.

**Lecture pure.** Tous les montants viennent des totaux figés sur `PayrollRun` au calcul du cycle.
Rien n'est recalculé : le faire ferait diverger l'écran des bulletins réellement émis.

Deux points méritent attention :

- **La ventilation du brut est reconstituée**, car le bulletin ne porte pas le détail
  base / primes / heures supplémentaires / avantages. Heures et primes viennent de
  `PayrollOvertimeLine` et `PayrollVariableAllowanceLine`, les avantages de `EmployeeInKindBenefit`,
  et le **salaire de base est déduit par différence** pour que les quatre postes retombent
  exactement sur `TotalGross`. Le cas limite où les éléments variables dépassent le brut (prorata,
  absences non rémunérées) est signalé par `BreakdownWarning` au lieu d'être masqué.
- **Aucune notion de jour de paie n'existe en base.** L'indicateur d'échéance affiche donc la
  prochaine obligation sociale réelle issue de `FiscalScheduleEntry` (paiement CNSS le 15 du mois
  suivant, DTS trimestrielle, retenue IRPP le 28), et non une date de paie inventée. Échéancier
  non généré ⇒ liste vide, jamais une erreur.

`GET api/payroll/runs` a été corrigé au passage : le compte de bulletins est désormais projeté en
base (`IPayrollRunRepository.ListWithPayslipCountsAsync`) au lieu de charger chaque cycle avec tous
ses bulletins **et leurs lignes** pour en compter les éléments. Le DTO de sortie est inchangé.

## Tests

`src/Backend/tests/FactuTrust.Infrastructure.Tests/Domain/Payroll/` : golden tests chiffrés
du moteur (`PayrollCalculatorTests`), heures supplémentaires par régime
(`OvertimeAmountCalculatorTests`, `PayrollRegimeAndFamilyTests`), export virement
(`PayrollBankTransferBuilderTests`, `CsvPayrollBankTransferWriterTests`), validation des
paramètres (`Application/Payroll/PayrollParametersValidationTests`,
`PayrollBankTransferExportTests`). Frontend : specs Jasmine à côté des composants
(`payroll-amount.pipe.spec.ts`, `payroll-settings.component.spec.ts`,
`payroll-bank-transfer-dialog.component.spec.ts`, …).
