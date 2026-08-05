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
     annualisée), plafonné à 450 TND par parent.
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

Conventions : montants `decimal(18,3)`, taux `decimal(8,4)` ; toute nouvelle colonne non
nullable porte un défaut qui préserve le comportement antérieur.

## Limites connues / évolutions envisagées

- Format officiel de télédéclaration DTS (fichier CNSS) : l'export actuel est un CSV de
  travail, pas le format de dépôt officiel.
- Déductions « parents à charge » : le plafond légal s'apprécie par parent et par foyer ;
  la règle du non-cumul entre déclarants (un seul enfant peut déclarer un parent) n'est
  pas contrôlable par l'application.
- **Export virement** : CSV standard uniquement ; formats TXT/XLS propriétaires par banque
  (MyBIATCorporate, Multivir, STB…) en phase 2 (nécessitent les modèles officiels).
- **RIB sur bulletin** : non figé à la validation — l'export lit le RIB courant du salarié.

## Retenues et avantages avancés

Fonctionnalités activables via `AccountingSettings` (désactivées par défaut) :

| Flag | Fonctionnalité |
|------|----------------|
| `PayrollSocialFundsEnabled` | Caisses / mutuelles complémentaires (retenue salariale + charge patronale) |
| `PayrollMealVouchersEnabled` | Tickets restaurant (exonération journalière paramétrable, défaut 3 TND/jour) |
| `PayrollInKindBenefitsEnabled` | Avantages en nature (véhicule, logement…) — imposables/CNSSables, non versés |
| `PayrollEmployeeLoansEnabled` | Prêts salariés sans intérêt avec échéancier mensuel |
| `PayrollGarnishmentsEnabled` | Saisies sur salaire et pensions alimentaires (barème saisissable paramétrable) |

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

## Tests

`src/Backend/tests/FactuTrust.Infrastructure.Tests/Domain/Payroll/` : golden tests chiffrés
du moteur (`PayrollCalculatorTests`), heures supplémentaires par régime
(`OvertimeAmountCalculatorTests`, `PayrollRegimeAndFamilyTests`), export virement
(`PayrollBankTransferBuilderTests`, `CsvPayrollBankTransferWriterTests`), validation des
paramètres (`Application/Payroll/PayrollParametersValidationTests`,
`PayrollBankTransferExportTests`). Frontend : specs Jasmine à côté des composants
(`payroll-amount.pipe.spec.ts`, `payroll-settings.component.spec.ts`,
`payroll-bank-transfer-dialog.component.spec.ts`, …).
