# Imputation comptable de la paie — NCT 01

Table de référence des comptes utilisés par les écritures de paie, et différences entre les deux
profils d'imputation. Elle fait foi pour le code (`PayrollJournalEntryBuilder`,
`PayrollJournalEntryAccountMap`) comme pour les tests.

Source : plan comptable général tunisien (NCT 01), complété par l'*overlay métier* du catalogue
`docs/accounting/nct01-coa-catalog.json` (comptes marqués `layer: overlay`).

## Les deux profils

| | `Legacy` | `Sce2026` |
|---|---|---|
| Statut | historique, défaut | conforme NCT 01 |
| Réglage | `PayrollAccountingSettings` du dossier, repli `Accounting:PayrollAccountProfile` | idem |
| Portée | cycles antérieurs à la date de bascule | cycles à partir de la date de bascule |

La **date de bascule est obligatoire** pour passer en `Sce2026` : sans elle, le profil s'appliquerait
aussi aux cycles déjà arrêtés, dont la réouverture puis la revalidation réécriraient l'imputation.
Elle doit être postérieure au dernier cycle validé ou clôturé — le serveur le refuse sinon.

Écran : *Paie → Paramètres → Comptabilisation de la paie*. API : `GET`/`PUT
api/payroll/settings/accounting` (`payroll:settings` + contexte cabinet délégué).

## Écriture d'engagement (validation du cycle) — journal `JOD`

### Débits

| Rubrique | `Legacy` | `Sce2026` | Libellé NCT 01 |
|---|---|---|---|
| Brut (hors rupture et avantages en nature) | 640 | 640 | Salaires et compléments de salaires |
| — dont heures supplémentaires¹ | 640 | 6401 | Heures supplémentaires |
| — dont primes¹ | 640 | 6402 | Primes |
| — dont indemnités ordinaires et autres gains¹ | 640 | 6409 | Autres compléments de salaires |
| — reliquat (prorata, absences)¹ | 640 | 6400 | Salaires |
| Avantages en nature | 640 (dans le brut) | **6404** | Avantages en nature |
| Indemnités de rupture / solde de tout compte | 641 *(overlay)* | **64602** | Indemnités de préavis et de licenciements |
| CNSS patronale + accident du travail + CSS patronale | 647 | 647 | Charges sociales légales |
| **TFP** | 647 | **6611** | TFP |
| **FOPROLOS** | 647 | **6612** | FOPROLOS |
| Charges patronales de fonds sociaux / mutuelle | 647 | 647 | Charges sociales légales |

¹ Ventilation optionnelle, activée par « Ventilation détaillée des salaires (640) ». Éteinte, le brut
reste sur le compte collectif 640, qui est un compte NCT 01 de niveau 3 parfaitement valide.

### Crédits

| Rubrique | `Legacy` | `Sce2026` | Libellé NCT 01 |
|---|---|---|---|
| Net à payer | 425 ou 425xxxxxxx (auxiliaire salarié) | idem | Personnel - rémunérations dues |
| IRPP + CSS salariés (+ régularisations) | 432 | 432 | Etat, impôts et taxes retenus à la source |
| **TFP + FOPROLOS + CSS patronale** | 432 | **437** | Autres impôts, taxes et versements assimilés |
| CNSS salarié + CNSS patronale + accident du travail | 453 | 453 | Sécurité sociale et autres organismes sociaux |
| Retenue d'avance | 421 | 421 | Personnel - avances et acomptes |
| Retenue d'échéance de prêt | 421.1 *(overlay)* | 421.1 | Prêts au personnel - retenues en cours |
| Saisie sur salaire / pension alimentaire | 427 | 427 | Personnel - oppositions |
| Retenue mutuelle (part salariale) | 428.1 *(overlay)* | 428.1 | Mutuelle / caisse complémentaire - part salariale |
| Tickets restaurant (part salariale) | 428.2 *(overlay)* | 428.2 | Tickets restaurant - part salariale |
| Compensation d'avantage en nature | 421 | **4286** | Personnel - autres charges à payer |
| Retenue non typée (« autres retenues ») | 421 | **4286** | Personnel - autres charges à payer |
| Part patronale de fonds social / mutuelle | compte du régime, sinon 4538 | idem | Organismes sociaux - charges à payer |

Le compte de compensation d'avantage en nature est **surchargeable par dossier**
(`InKindOffsetAccount`). Le défaut a quitté 4386 pour 4286 : 4386 dépend de 438
« État - charges à payer », ce qui logeait une dette envers le salarié dans la branche fiscale, où
elle ne se soldait jamais.

## Écritures de trésorerie

| Opération | Débit | Crédit | Journal |
|---|---|---|---|
| Règlement des salaires | 425xxxxxxx (ou 425 agrégé) | 5321 / 5411 | `JB` / `JC` |
| Versement CNSS | 453 | 5321 / 5411 | `JB` / `JC` |
| **Décaissement d'une avance** | **421** | 5321 / 5411 | `JB` / `JC` |
| **Décaissement d'un prêt salarié** | **421.1** | 5321 / 5411 | `JB` / `JC` |

Les deux dernières sont générées quand « Écritures de décaissement (avances/prêts) » est actif.
Éteintes, l'OD reste à saisir manuellement — et **tant qu'elle ne l'est pas, 421 et 421.1 restent
créditeurs**, puisque la retenue du bulletin les crédite sans qu'aucun débit ne les ait ouverts.

## Comptes auxiliaires salariés

Chaque salarié porte un sous-compte de **425**, marqué **auxiliaire**, rattaché au collectif, et
**libellé de son nom**. Le numéro est **alloué séquentiellement** et stocké sur la fiche
(`Employee.AuxiliaryAccountNumber`) : `425` + 4 chiffres, soit `4250001`, `4250002`…

**Règle de forme (`AccountNumberRules`) : au plus 8 chiffres**, séparateurs non comptés. `421.1`
vaut 4 chiffres et reste valide ; `4259655554` en vaut 10 et est refusé. L'invariant vit dans
`ChartOfAccount.Create`, donc sur *tous* les chemins de création — y compris l'auto-création de
sous-comptes, qui contournait FluentValidation.

### Le schéma dérivé, supprimé

Jusqu'à la remédiation, une fiche sans compte alloué retombait sur une **dérivation du matricule** :
`425` + ses 7 derniers chiffres, soit **10 chiffres** (`4256854545`, `4259655554`). Deux défauts :

- le numéro **dépassait le plafond de 8 chiffres** ;
- la troncature **collisionnait** — « 1 » et « 0000001 », ou deux CIN de même queue, produisaient le
  même compte, donc une seule dette de salaire pour deux salariés — et **recopiait le matricule**,
  souvent un CIN, dans le plan comptable et dans le FEC.

La dérivation a été retirée de tous les chemins d'écriture. `PayrollEmployeeAuxiliaryAccountResolver`
subsiste en **reconnaissance de forme héritée** (`ResolveLegacy`) et ne doit plus servir à créer.
`PayrollRun.FreezeEmployeeAuxiliaryAccounts` refuse désormais un salarié sans compte au lieu d'en
inventer un ; c'est `ValidatePayrollRunCommandHandler` qui alloue ce qui manque avant de l'appeler.

Règles communes :

- le compte est **figé sur le bulletin à la validation** du cycle : changer le matricule ensuite ne
  déplace pas la dette déjà comptabilisée ;
- deux salariés aboutissant au même compte sont refusés à la création et à la validation ; la sortie
  est d'**allouer un compte explicite** à l'un des deux depuis sa fiche ;
- l'écran *Plan comptable* affiche une colonne « Auxiliaire » et permet de les masquer ; le contrôle
  d'intégrité `health-payroll-auxiliary-shape` signale ceux qui ne sont ni typés ni nommés, et
  `health-account-number-length` tout numéro de plus de 8 chiffres, où qu'il soit dans le plan.

### Renumérotation de l'existant

`ChartAccountDigitCompactionService` renumérote les comptes hors norme et propage le nouveau numéro
à **toutes** les colonnes qui le référencent — écritures brouillon *comme* validées et clôturées,
bulletins figés, lignes de règlement, groupes de lettrage, fiches salariés. Il s'applique
automatiquement au bootstrap de chaque dossier (`TenantRuntimeCatalogBootstrapper`), dans une
transaction unique, et refuse de valider si la balance générale a bougé d'un millime.

Il est gardé **par la donnée** (aucun compte hors norme ⇒ aucune action), pas par un jeton
« déjà appliqué » : un compte trop long réapparu — restauration d'une sauvegarde antérieure — est
repris à la passe suivante. La table `ChartOfAccountCompactionLogs` conserve chaque correspondance
`from → to` indéfiniment : c'est la réponse à « où est passé 4259655554 ? », et la carte inverse d'un
éventuel retour arrière.

Pré-contrôle et forensique :

```bash
docs/runbooks/sql/CompactOverlongAccountNumbers.readonly.sql
```

### Comptes hérités mal libellés

Les auxiliaires créés **avant** la migration NCT 01 portent un libellé du type
`« Personnel et comptes rattachés — 421 — 4218744456 »` sur un compte pourtant numéroté
`4258744456` : le remap a réécrit les numéros, jamais les libellés. Requalification :

```bash
docs/runbooks/sql/RequalifyPayrollAuxiliaryAccounts.idempotent.sql
```

Il ne touche que des colonnes descriptives de `ChartOfAccounts` — aucune écriture, aucun solde. Sa
détection suppose la queue numérique héritée à 7 chiffres : le passer **avant** la renumérotation,
qui raccourcit les numéros.

## Reclassement d'un cycle historique

`POST api/payroll/compliance/reclassification/{runId}` produit **une** OD de correction
Legacy → SCE pour un cycle donné (débit 6611/6612 & crédit 647 ; débit 432 & crédit 437 ;
641 → 640 + 64602 ; 421 → compte de compensation configuré).

Garde-fous :

- une seule OD de reclassement active par cycle (index unique en base) ;
- la **réouverture du cycle l'extourne** en même temps que l'OD d'origine ;
- la génération d'une OD `Sce2026` est **refusée** tant qu'un reclassement actif subsiste, faute de
  quoi les taxes sur salaires seraient comptées deux fois.

## Hors périmètre (choix assumé)

La CNSS est imputée au collectif **453**, et la retenue sur salaires au collectif **432**, plutôt
qu'aux sous-comptes 45311 et 4328. Ce sont des comptes NCT 01 de niveau 3 valides : imputer au
collectif n'est pas une erreur. Descendre d'un niveau obligerait à déplacer simultanément l'OD de
paie, le bordereau CNSS, le lettrage et le rapprochement de la déclaration mensuelle — avec un risque
de désappariement pour les dossiers à cheval sur la bascule.
