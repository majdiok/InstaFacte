# Exonération / déduction IRPP SMIG

> ⚠️ **Avertissement de conformité (R-12 / CAL-006, vérifié 2026-08-31)** : la base légale du mode
> `SmigPortion` (crédit mensuel d'IRPP appliqué à **tous** les salariés) n'a **pas pu être
> vérifiée** dans les textes. Le mode reste fonctionnel pour préserver les bulletins des tenants
> qui l'ont déjà activé, mais **n'est plus recommandé**. La règle légale vérifiable distincte est
> la **déduction annuelle de 500 TND** pour les salariés rémunérés au SMIG/SMAG (voir §3).
> Le changement de libellé UI (« recommandé » → avertissement explicite) est un item frontend
> différé — voir `/code/.plans/audit/phase3-deferred.md`.

## 1. Base légale

### Mode `SmigPortion` (portion SMIG exonérée)

Aucune base légale vérifiée n'a été retrouvée pour le mécanisme tel que codé (crédit
`taux × min(net imposable ; SMIG)` versé à tout salarié, y compris ceux gagnant bien plus que le
SMIG). La référence « article 21 du code de l'IRPP (LF 2019) » figurait antérieurement dans cette
documentation **sans source corroborée** ; elle est **retirée** en attendant une confirmation
fiscaliste. À défaut, ce mode doit être considéré comme une convention de calcul métier héritée
du mockup, pas comme une disposition légale.

### Déduction annuelle SMIG/SMAG (règle vérifiable)

Une **déduction supplémentaire de 500 TND/an** de la base imposable est prévue pour les salariés
rémunérés au niveau du SMIG/SMAG (art. 26 CIR — profiscal.com, paie-tunisie.com, vérifié 2026-08).
C'est une **déduction de la base imposable** (et non un crédit d'impôt) : elle réduit le net
imposable annuel avant application du barème IRPP.

> ⏳ **À confirmer par un fiscaliste (Q2 du plan)** : le libellé exact de l'éligibilité (seuil
> strictement inférieur au SMIG, ou inférieur ou égal ; prise en compte du SMAG agricole) et le
> texte précis (art. 26 vs autre). Les fonctions pures sont déjà exposées (voir §3) et prêtes à
> être câblées dès confirmation.

## 2. Activation

**RH & Paie → Paramètres paie → Conformité → Exonération IRPP SMIG**

- Désactivée par défaut (`SmigIrppExemptionMode = None`) : les exercices existants conservent
  exactement leur comportement (règle cardinale).
- Le SMIG mensuel (`MonthlySmig`) doit être positif pour activer un mode.

## 3. Modes disponibles

### `SmigAnnualDeduction` — Déduction annuelle 500 TND (règle légale vérifiable)

> ⏳ **Mode non encore câblé dans le moteur** : les fonctions de calcul pures existent dans
> `SmigIrppExemptionCalculator` (`SmigAnnualDeductionAmount`, `IsEligibleForSmigAnnualDeduction`,
> `ComputeMonthlySmigAnnualDeductionEffect`) mais la valeur d'énumération
> `SmigIrppExemptionMode.SmigAnnualDeduction` et son branchement dans `PayrollCalculator.cs` /
> `IrppRegularizationCalculator.cs` sont **différés** (hors périmètre WS-3 — voir
> `/code/.plans/audit/phase3-deferred.md`).

Mécanisme prévu (à câbler) :

```
éligibilité (par mois) : rémunération mensuelle totale imposable ≤ SMIG mensuel
effet mensuel  = 500 / 12 = 41,667 TND  (déduction de la base imposable, arrondi 3 déc.)
régularisation = forfait annuel exact de 500 TND  (pas la somme des arrondis mensuels)
```

- L'éligibilité se juge sur la **rémunération mensuelle totale imposable** (net imposable du
  mois, salaire de base + primes + avantages), **pas** sur le seul salaire de base contractuel.
- Effet mensuel = 500 ÷ 12 = **41,667 TND** (arrondi `AwayFromZero` à 3 décimales).
  Sur 12 mois la somme des arrondis atteint **500,004 TND** (dérive d'arrondi connue) ; la
  régularisation annuelle applique donc le **forfait exact de 500 TND**, corrigeant cet écart de
  0,004 TND à l'annualisation.

### `SmigPortion` — Portion SMIG exonérée (⚠️ sans base légale vérifiée)

> ⚠️ Non recommandé — voir avertissement en tête de page. Conservé pour compatibilité des
> tenants l'ayant déjà activé.

Applicable à **tous** les salariés :

```
exemptedBase = min(net imposable mensuel ; SMIG mensuel)
exonération  = exemptedBase × taux applicable / 100
IRPP final   = max(0 ; IRPP brut − exonération)
```

Le taux applicable est :
- le taux saisi dans **Taux applicable (%)** si renseigné ;
- sinon le **premier taux non nul** du barème IRPP de l'exercice (15 % en 2025+, 26 % en 2024 et
  antérieur).

### `FullIfBelow` — Exonération totale si rémunération totale ≤ SMIG

```
si rémunération mensuelle totale imposable ≤ SMIG mensuel :
    IRPP final = 0
sinon :
    IRPP inchangé
```

- **Correction R-12 (CAL-006)** : l'éligibilité s'apprécie désormais sur la **rémunération
  mensuelle totale imposable** (`monthlyNetTaxable` / `MonthlyNetTaxable`), et **non** sur le
  seul salaire de base contractuel comme auparavant. Un salarié au SMIG de base mais avec de
  fortes primes (rémunération totale > SMIG) n'est plus exonéré à tort.
- Pour la régularisation annuelle (`ApplyOnCumul`), l'éligibilité exige que **tous** les mois
  comptés soient ≤ SMIG.

## 4. Exemple chiffré au SMIG (554,736 TND, mode SmigPortion — conservé pour référence)

| Élément | Montant |
|---------|---------|
| IRPP brut (barème) | 1,919 TND |
| Exonération (min(459,212 ; 554,736) × 15 %) | 1,919 TND (plafonnée à l'IRPP brut) |
| IRPP net retenu | **0,000 TND** |
| CSS (inchangée) | 2,296 TND |
| Gain net salarié | +1,919 TND |

> Les chiffres reflètent le SMIG 2026 (554,736 TND) et le taux CNSS 9,68 % corrigés. Les valeurs
> antérieures (SMIG 528,320, IRPP brut 2,276) correspondaient au preset non conforme d'origine.

## 5. Bulletin et traçabilité

- Ligne informative : **Exonération IRPP SMIG (art. 21)** (`PayslipLineKind.Info`).
  > ℹ️ Le libellé « art. 21 » est conservé dans le bulletin pour ne pas casser les modèles
  > existants ; il est **réévalué** dans le cadre de la confirmation fiscaliste (§1).
- Champs figés : `IrppBeforeSmigExemption`, `IrppSmigExemption` sur le bulletin.
- `Payslip.Irpp` = IRPP **net** à retenir (compte 432, déclarations).
- Agrégat cycle : `PayrollRun.TotalIrppSmigExemption`.

## 6. Régularisation annuelle

Si `EnableIrppRegularization` est activée, l'exonération est aussi appliquée sur l'IRPP dû au
**cumul annuel** :
- mode `SmigPortion` : plafond = SMIG × nombre de mois comptés ;
- mode `FullIfBelow` : exonération totale si tous les mois comptés sont ≤ SMIG ;
- mode `SmigAnnualDeduction` (à câbler) : forfait exact de 500 TND sur la base imposable annuelle.

## 7. Comptabilité

L'exonération augmente le crédit **421** (net à payer) et diminue le crédit **432** (IRPP) du
même montant. Le débit **640** (masse salariale) est inchangé.

## 8. FAQ

**La CSS est-elle exonérée ?** Non — le dispositif concerne l'IRPP uniquement.

**Faut-il recalculer les cycles ?** Oui, après activation ou changement de mode, recalculez les
cycles en brouillon/calculé.

**Quel mode choisir ?** `SmigAnnualDeduction` est la règle légale vérifiable (à câbler, en
attente de confirmation fiscaliste Q2). `FullIfBelow` est une exonération totale simple pour les
salariés strictement au SMIG. `SmigPortion` est conservé pour compatibilité mais n'a pas de base
légale vérifiée — ne pas activer sur de nouveaux tenants.

**Pourquoi `FullIfBelow` n'exonère-t-il plus un salarié au SMIG de base avec des primes ?** Parce
que l'éligibilité se juge désormais sur la rémunération mensuelle **totale** imposable (R-12),
pas sur le seul salaire de base. C'est conforme à l'esprit du dispositif (aider les salariés dont
la rémunération globale ne dépasse pas le SMIG).
