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

## DTS CNSS (déclaration trimestrielle)

Agrège les bulletins des 3 mois du trimestre, **uniquement pour les cycles Validés ou
Clôturés**. Export CSV (`;`, UTF-8 BOM) avec ligne TOTAL.

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

Conventions : montants `decimal(18,3)`, taux `decimal(8,4)` ; toute nouvelle colonne non
nullable porte un défaut qui préserve le comportement antérieur.

## Limites connues / évolutions envisagées

- **Exonération IRPP des salariés payés au SMIG** (LF 2019) : non modélisée — un salarié
  au SMIG (528,320) dégage un net imposable annuel légèrement supérieur à 5 000 TND et
  supporte donc un IRPP/CSS marginal dans le modèle actuel (~2,3 TND/mois). À traiter via
  une règle d'exonération dédiée si nécessaire.
- Format officiel de télédéclaration DTS (fichier CNSS) : l'export actuel est un CSV de
  travail, pas le format de dépôt officiel.
- Déductions « parents à charge » : le plafond légal s'apprécie par parent et par foyer ;
  la règle du non-cumul entre déclarants (un seul enfant peut déclarer un parent) n'est
  pas contrôlable par l'application.

## Tests

`src/Backend/tests/FactuTrust.Infrastructure.Tests/Domain/Payroll/` : golden tests chiffrés
du moteur (`PayrollCalculatorTests`), heures supplémentaires par régime
(`OvertimeAmountCalculatorTests`, `PayrollRegimeAndFamilyTests`), validation des paramètres
(`Application/Payroll/PayrollParametersValidationTests`). Frontend : specs Jasmine à côté
des composants (`payroll-amount.pipe.spec.ts`, `payroll-settings.component.spec.ts`, …).
