# Exonération IRPP SMIG (article 21)

## Base légale

L'article 21 du code de l'IRPP tunisien (LF 2019) prévoit une exonération de l'IRPP sur la
partie du salaire ne dépassant pas le SMIG mensuel en vigueur.

## Activation

**RH & Paie → Paramètres paie → Conformité → Exonération IRPP SMIG (art. 21)**

- Désactivée par défaut (`SmigIrppExemptionMode = None`) : les exercices existants conservent
  exactement leur comportement.
- Le SMIG mensuel (`MonthlySmig`, défaut 528,320 TND) doit être positif pour activer un mode.

## Modes disponibles

### `SmigPortion` — Portion SMIG exonérée (recommandé, correspond au mockup métier)

Applicable à **tous** les salariés :

```
exemptedBase = min(net imposable mensuel ; SMIG mensuel)
exonération  = exemptedBase × taux applicable / 100
IRPP final   = max(0 ; IRPP brut − exonération)
```

Le taux applicable est :
- le taux saisi dans **Taux applicable (%)** si renseigné ;
- sinon le **premier taux non nul** du barème IRPP de l'exercice (15 % par défaut).

### `FullIfBelow` — Exonération totale si salaire de base ≤ SMIG

```
si salaire de base ≤ SMIG mensuel :
    IRPP final = 0
sinon :
    IRPP inchangé
```

L'éligibilité s'apprécie sur le **salaire de base du contrat**, pas sur le brut cotisable.

## Exemple chiffré au SMIG (528,320 TND, mode SmigPortion)

| Élément | Montant |
|---------|---------|
| IRPP brut (barème) | 2,276 TND |
| Exonération (min(431,838 ; 528,320) × 15 %) | 2,276 TND (plafonnée à l'IRPP brut) |
| IRPP net retenu | **0,000 TND** |
| CSS (inchangée) | 2,159 TND |
| Gain net salarié | +2,276 TND |

## Bulletin et traçabilité

- Ligne informative : **Exonération IRPP SMIG (art. 21)** (`PayslipLineKind.Info`).
- Champs figés : `IrppBeforeSmigExemption`, `IrppSmigExemption` sur le bulletin.
- `Payslip.Irpp` = IRPP **net** à retenir (compte 432, déclarations).
- Agrégat cycle : `PayrollRun.TotalIrppSmigExemption`.

## Régularisation annuelle

Si `EnableIrppRegularization` est activée, l'exonération est aussi appliquée sur l'IRPP dû
au **cumul annuel** (mode SmigPortion : plafond SMIG × nombre de mois comptés), pour éviter
un rattrapage en décembre.

## Comptabilité

L'exonération augmente le crédit **421** (net à payer) et diminue le crédit **432** (IRPP)
du même montant. Le débit **640** (masse salariale) est inchangé.

## FAQ

**La CSS est-elle exonérée ?** Non — l'article 21 concerne l'IRPP uniquement.

**Faut-il recalculer les cycles ?** Oui, après activation ou changement de mode, recalculez
les cycles en brouillon/calculé.

**Impact sur les salariés au-dessus du SMIG ?** En mode SmigPortion, la portion plafonnée au
SMIG est exonérée pour tous ; en mode FullIfBelow, aucun effet si le salaire de base dépasse
le SMIG.
