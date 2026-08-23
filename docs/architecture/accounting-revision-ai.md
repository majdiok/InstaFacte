# Réviseur IA — contrôles continus et dossier de révision

> Audience : développeurs backend / frontend, support N3, QA.
>
> Périmètre : élargissement du moteur de contrôles comptables et révision de portefeuille cabinet.

---

## 1. Principe

L'expert-comptable arrive en fin de mois sur un dossier « sale ». Le coût n'est pas la saisie —
déjà automatisée — mais la **chasse aux anomalies**, puis leur mise en forme en note de travail.

Le module répond en deux temps, et la frontière entre les deux est **non négociable** :

| Étage | Ce qu'il produit | Qui le produit |
|---|---|---|
| **Détection** | Anomalies : règle, sévérité, compte, période, montant, pièces | Code déterministe, reproductible |
| **Rédaction** | Note de travail, question client, action retenue | Modèle de langage, borné |

**Le modèle ne détecte rien et ne produit aucun chiffre.** Montants, comptes, dates, sévérités et
comptages sont réinjectés par le code après la génération. C'est la généralisation de la règle déjà
appliquée à la [trésorerie prévisionnelle](treasury-cash-forecast.md) : *le chiffre est calculé par
du code, le mot est écrit par le modèle.*

---

## 2. Drapeaux

`src/Backend/FactuTrust.API/appsettings.json` → `Features:AccountingFirms` :

```jsonc
"FirmRevisionEnabled": true,           // interrupteur maître — 503 sur /api/firm/revision
"FirmRevisionSweepEnabled": true,      // balayage nocturne du portefeuille
"FirmRevisionAiEnabled": true,         // rédaction par le modèle ; OFF = notes déterministes
"FirmRevisionAiTimeoutSeconds": 60,
"FirmRevisionMaxAnomaliesInPrompt": 40
```

**`FirmRevisionEnabled` est la garde de fonctionnalité, et la seule.** Elle commande à la fois les
503 du contrôleur et la délivrance des permissions `firm:revision:*` dans le profil cabinet
(`FirmGovernanceNativeAccess`). Côté front, la présence de `firm:revision:view` suffit donc à
décider de l'entrée de menu comme de l'accès à la route : il n'y a pas de second drapeau à
synchroniser.

Front : `FirmFeatureFlagsService` → `firmRevision` (défaut `true`, `localStorage`
`ft.firm.featureFlags`). Ce n'est **pas** la garde de fonctionnalité mais un interrupteur local de
secours : l'éteindre retire l'entrée de menu (`filterFirmRevisionNav`) *et* ferme la route, les
deux ensemble, pour ne jamais laisser un lien mort.

> `appsettings.Production.json` n'override aucun de ces drapeaux : la production hérite donc des
> valeurs ci-dessus, balayage nocturne compris (Hangfire, 9 h UTC, une connexion SQL par dossier).

Le moteur de contrôle lui-même reste piloté par `Accounting:AccountingAuditDashboardEnabled`,
`AccountingAuditPersistenceEnabled` et `AccountingAuditSchedulingEnabled`.

---

## 3. Schéma

Migration `20260816120000_AddAccountingRevisionNote_Tenant` — strictement additive.

| Objet | Rôle |
|---|---|
| `AccountingControlRuns.EvaluatedRuleCount` | Dénominateur du taux de conformité |
| `AccountingRevisionNotes` | Le dossier de révision rédigé d'un contrôle (une note par contrôle) |

Script idempotent :
[`docs/runbooks/sql/AddAccountingRevisionNote_Tenant.idempotent.sql`](../runbooks/sql/AddAccountingRevisionNote_Tenant.idempotent.sql).

> Les entrées de la note vivent en JSON dans `ItemsJson`, pas dans une table : elles sont toujours
> lues en bloc avec leur note, jamais interrogées ni triées individuellement.

---

## 4. Correctifs du socle livrés avec le module

Cinq défauts du moteur d'audit rendaient l'élargissement du catalogue inexploitable.

| Défaut | Conséquence avant correctif |
|---|---|
| Auto-résolution sans filtre d'exercice ni de module | Un run filtré `payroll` fermait en « Corrigée » les anomalies TVA et celles des autres exercices |
| Taux de conformité divisé par la taille du catalogue | Passer de 15 à 22 règles aurait fait **bondir** le taux de tous les tenants, et rendu `ComplianceRateDeltaVsPriorYear` faux |
| `AnomalyCount` recopiant `WarningCount` | Vignette « anomalies » fausse sur le tableau de bord |
| `ExportPdfAsync` renvoyant du texte brut en `application/pdf` | Fichier non ouvrable |
| `AccountingAuditScheduledJob` ne lançant jamais le moteur | **Aucun contrôle continu réel** malgré les planifications enregistrées |

> ⚠️ **À la mise en production**, le job `accounting-audit-schedules` (7 h UTC) exécutera pour de bon
> les planifications déjà enregistrées dans `AccountingControlSchedules`.

---

## 5. Contrat d'une règle

`IAccountingAuditRule` — quatre invariants, tous verrouillés par `AuditRuleContractTests` :

1. **Constructeur sans paramètre.** Une règle lit uniquement par `ctx.Db`. Injecter un repository la
   ferait lire le tenant **ambiant** — celui du cabinet lors d'un balayage de portefeuille — donc
   les données d'un autre dossier. C'est ce qui a motivé la réécriture de `DepreciationAuditRule`.
2. **Code unique**, `ModuleCode` présent dans `AccountingAuditModuleCatalog`.
3. **Enregistrement DI** aux deux endroits (type concret et `IAccountingAuditRule`). Sans lui, la
   règle n'est jamais évaluée — en silence.
4. **Discriminant d'empreinte** dès que la règle émet plus d'une anomalie par compte et par période :
   `SingleGroup(..., discriminator)` avec un identifiant métier **stable**. `Fingerprint` porte un
   index unique ; sans discriminant, la seconde anomalie écrase la première.

> Sans discriminant, l'empreinte reste **strictement identique** à l'historique : la changer aurait
> auto-résolu puis recréé toutes les anomalies en base, perdant statut et affectation.

### Évaluateur cron

`CronOccurrenceEvaluator` — sous-ensemble standard à cinq champs plus les macros `@daily`,
`@weekly`… Hangfire 1.8 internalise Cronos : aucun analyseur cron public n'est accessible depuis les
paquets déjà référencés. Sémantique : « existe-t-il une occurrence dans `]dernière exécution,
maintenant]` », robuste au fait que le déclencheur est quotidien.

---

## 6. Catalogue des règles

Sévérité : **B** bloquant · **W** avertissement · **I** information.

### Famille Comptable

| Code | Sév. | Détection |
|---|---|---|
| `entry-vat-vs-document` | W | Somme des lignes 4366x/4367x ≠ TVA du document, tolérance 1 millime |
| `thirdparty-account-mismatch` | W | Client hors 411, fournisseur hors 401, compte collectif sans tiers — trois lots séparés |
| `lettering-orphan` | W | Membre pointant une ligne disparue, groupe sur ≥ 2 comptes, groupe non partiel déséquilibré |
| `reversal-missing` | B | Marque d'extourne sans contre-passation ; facture annulée jamais extournée |

### Famille Documentaire

| Code | Sév. | Détection |
|---|---|---|
| `supplier-invoice-no-proof` | B | Aucune pièce, ni sur le reçu d'achat ni sur l'écriture |
| `supplier-invoice-duplicate` | B / W | **Strict** : même fournisseur + numéro normalisé (`FA-00123` ≡ `fa 123`). **Approché** : même montant au millime, ≤ 5 j, numéros différents |
| `purchase-price-drift` | W | Prix facturé vs ligne de commande, seuil réglable par `AccountingControlRuleSetting.DecimalThreshold` (défaut 10 %) |

### Famille Trésorerie

| Code | Sév. | Détection |
|---|---|---|
| `cash-negative` | B | Solde **cumulé chronologique** des comptes 54x passant sous zéro. Le solde d'ouverture compte ; les banques sont exclues (elles peuvent être à découvert) |
| `cash-in-without-invoice` | W | Entrée de trésorerie sans document source, sans tiers rattaché et sans contrepartie client lettrée. Origines connues (paie, effets, dépôts, virements internes 58x) exclues |

> **Z-caisse vs théorique :** le POS persiste désormais une vacation (`CashRegisterSession`) et un rapport Z immuable (`ZReport`). Le réviseur IA ne compare pas encore le Z au théorique caisse — cette règle santé reste hors périmètre.

### Famille Fiscale

| Code | Sév. | Détection |
|---|---|---|
| `withholding-missing-on-fees` | B | Facture d'un fournisseur relevant d'honoraires / commissions / loyers / non-résidents, au-delà du seuil, sans retenue |
| `fodec-missing` | W | FODEC porté par la facture mais absent du compte 4477 de son écriture |
| `vat-period-not-closed` | W | Déclaration restée `Draft` ou `Submitted` au-delà de son échéance + délai de grâce (`IntThreshold`, défaut 15 j) |

### Famille Paie

| Code | Sév. | Détection |
|---|---|---|
| `payroll-cnss-regime-mismatch` | B | Régime du **contrat en vigueur au mois du bulletin** vs CNSS réellement retenue : exonéré avec retenue, soumis sans retenue, ou taux RSA/RSNA inversés |
| `payroll-dependent-no-proof` | W / B | Compteur de parents à charge sans déclaration nominative (W) ; même CIN parent réclamé par deux salariés (B) |
| `payroll-overtime-out-of-regime` | W | Taux incompatible avec le régime hebdomadaire — rejoue `OvertimeRatePercentExtensions.IsValid` |
| `payroll-below-smig` | B | Brut **reconstitué à temps plein** sous le SMIG de l'exercice. Le prorata est neutralisé, sans quoi chaque embauche en cours de mois serait signalée |

### Famille Fraude douce

Schémas inhabituels que les contrôles de conformité laissent passer parce que chaque pièce, prise
isolément, est régulière. **Aucune de ces règles n'affirme la fraude** : un contrôle interne
défaillant produit exactement les mêmes signaux qu'une malversation. Le vocabulaire des libellés
reste factuel.

| Code | Sév. | Détection |
|---|---|---|
| `threshold-structuring` | W | Répétition de montants dans une bande de 10 % sous le seuil de RS, chez un même fournisseur, au-delà de 3 occurrences. Une facture isolée sous le seuil n'a rien d'anormal |
| `supplier-created-then-paid` | W | Premier règlement dans le jour suivant la création du tiers : aucun contrôle intermédiaire n'a pu s'exercer |
| `self-validation` | W | `ValidatedBy == CreatedBy` sur une écriture **manuelle**. Les écritures automatiques sont exclues — la confusion y est structurelle |
| `off-hours-entry` | I | Saisie nocturne ou week-end, horodatage UTC ramené en heure de Tunisie. Informative à dessein |
| `backdated-entry` | W | Écart date d'opération / date de saisie au-delà d'un seuil réglable (défaut 60 j) |

### Le principe d'abstention

Quatre règles **se taisent** plutôt que deviner : sans `Rs7TtcThresholdTnd`, sans
`PayrollYearParameters`, sans SMIG paramétré, sans bon de commande de référence. Un seuil deviné
produirait des anomalies fausses sur un exercice entier — et une règle qui crie faux est désactivée
dans la semaine. Chacun de ces cas est couvert par un test.

---

## 7. Impact chiffré — `AuditAmountSemantics`

`AccountingAnomaly.Amount` est un simple décimal, rempli au gré de chaque règle : tantôt un vrai
enjeu financier, tantôt zéro faute de mieux, tantôt une somme débit + crédit qui compte deux fois le
même flux.

**Seuls les codes déclarés dans `AuditAmountSemantics` entrent dans un total.** Les autres sont
présentés « non chiffrable » — une information honnête, pas une lacune. Ajouter un code à cette
liste est une décision comptable : son montant doit répondre à « combien coûte cette anomalie si
elle n'est pas corrigée ? ».

---

## 8. Balayage de portefeuille cabinet

`IFirmRevisionService` — fan-out en **trois temps, dans cet ordre impérativement**, calqué sur
`FirmPortfolioReadService` :

1. **ACL** — `IFirmDossierAccessService.GetAccessibleCompanyTenantIdsAsync`, contrat tri-état :
   `null` = aucun filtre, ensemble vide = aucun dossier.
2. **Chaînes de connexion, séquentiellement** — `ITenantService` s'appuie sur le `MasterDbContext`
   scoped, qui n'admet pas deux opérations concurrentes.
3. **Lectures dossiers en parallèle borné** — `FirmAgentMaxParallelDossiers`, clampé `[1, 16]`,
   chacune sur `CreateIsolatedContext(connectionString)`.

L'échec d'un dossier **ne fait jamais tomber le balayage** : `ReadFailed`, compté dans
`FirmFanOutHealthDto`. L'écran affiche un bandeau « compteurs partiels ».

**Le périmètre est toujours explicite.** Chaque méthode reçoit son `FirmDossierAccessScope` plutôt
que de le déduire de l'utilisateur courant : hors requête HTTP — dans le job nocturne —
`ICurrentUser` est vide et l'idiome fail-closed retomberait silencieusement sur « aucun filtre ».

### Indice de risque

`bloquants × 10 + avertissements × 3 + infos`, plancher à 50 pour un dossier **jamais contrôlé** :
l'absence d'anomalie n'y signifie pas la conformité, seulement l'ignorance.

---

## 9. API

`/api/firm/revision` — **503 partout** quand `FirmRevisionEnabled` est faux.

| Verbe & route | Policy |
|---|---|
| `GET /overview` | `FirmRevisionView` |
| `GET /dossiers/{tenantId}` | `FirmRevisionView` |
| `GET /work-queue` | `FirmRevisionView` |
| `POST /sweep` | `FirmRevisionManage` |
| `POST /dossiers/{tenantId}/note` | `FirmRevisionManage` |
| `GET /dossiers/{tenantId}/note/export` | `FirmRevisionView` |

> **La génération et l'export sont séparés à dessein.** `POST …/note` produit la note et peut
> solliciter le modèle — d'où la permission de gestion. `GET …/note/export` met en page une note
> **existante** : télécharger un PDF ne doit jamais déclencher un appel au modèle, ni à l'insu de
> l'utilisateur ni à chaque rafraîchissement. Sans note produite, l'export échoue et le dit.

### Le PDF du dossier

`PdfService.RevisionDossierLayout.cs` — portrait et non paysage, contrairement au rapport de
contrôle : c'est un document qui se lit et s'annote, pas un tableau qui se balaie.

Il porte **toujours** la mention du mode de rédaction (« assistée » / « non assistée ») et, le cas
échéant, le motif du repli. Un lecteur du dossier doit savoir d'où vient la prose qu'il signe.

Un impact absent s'imprime **« non chiffrable »**, jamais « 0,000 » : la nuance est le sens même de
la mention. Chaque note tient d'un bloc (`ShowEntire`) — une note de travail coupée entre deux pages
ne se relit pas.

Permissions `firm:revision:view` / `firm:revision:manage`, accordées sous drapeau par
`FirmGovernanceNativeAccess` : responsable **et** collaborateur consultent (l'ACL dossier restreint
ensuite chacun) ; seul le responsable déclenche un balayage, qui mobilise toutes les bases dossiers.

> **Après déploiement**, les deux plans ne se rafraîchissent pas au même rythme, et l'écart est
> visible :
>
> - le **front** recalcule ses permissions à chaque démarrage via `/auth/me`
>   (`AuthService.bootstrapRefresh`), **sans faire tourner le JWT** — un simple rechargement de page
>   suffit donc à faire apparaître l'entrée de menu ;
> - les **politiques de l'API** lisent les revendications du **jeton**, qui datent de la connexion.
>
> Entre les deux, l'écran est atteignable mais `/api/firm/revision/*` répond 403. La page le dit
> explicitement (« Vos droits ont changé depuis votre connexion »). Une **reconnexion** referme
> l'écart.

### Enregistrement DI

`IFirmRevisionService` est enregistré **inconditionnellement** : le contrôleur porte le drapeau. Un
enregistrement conditionnel ferait échouer la validation du conteneur au démarrage.

---

## 10. Écran

`src/app/features/firm/revision/` — route `/firm/revision`, sous `accountingFirmsFeatureGuard`,
`firmNativeGuard` et `firmRevisionFeatureGuard` (fail-closed : drapeau local **et**
`firm:revision:view`, sans quoi retour au tableau de bord cabinet).

**Entrée de menu** « Révision du portefeuille » (`FIRM_NATIVE_NAV`), juste après « Chef de
mission », conditionnée par `firm:revision:view` — donc invisible tant que l'API n'accorde pas le
module. Menu visible, route ouverte et action autorisée reposent ainsi sur la même condition.

Le bouton **« Lancer un balayage » n'est rendu qu'avec `firm:revision:manage`** : le collaborateur
consulte le portefeuille sans pouvoir mobiliser toutes les bases dossiers. La traduction du 403
dans `runSweep()` est conservée en défense en profondeur.

Deux onglets : **Portefeuille** (dossiers triés par risque, répartition par domaine) et **File de
travail** (ventilée par collaborateur, dossiers non affectés isolés). La logique de présentation vit
dans `firm-revision.view-model.ts`, pure et testée hors DOM.

---

## 11. Tests

| Niveau | Fichier | Objet |
|---|---|---|
| Moteur | `AccountingAudit/AuditEngineScopingTests.cs` | Périmètre exercice/module, `EvaluatedRuleCount`, `AnomalyCount` |
| Contrat | `AccountingAudit/AuditRuleContractTests.cs` | Les quatre invariants de règle, compatibilité d'empreinte |
| Clôture | `AccountingAudit/ClosingNotBlockedByAuditRulesTests.cs` | Une règle bloquante ne bloque aucune clôture |
| Cron | `AccountingAudit/CronOccurrenceEvaluatorTests.cs` | Sous-ensemble cron, macros, expressions invalides |
| Règles | `AccountingAudit/AccountingFamilyRulesTests.cs`, `DocumentaryFamilyRulesTests.cs`, `TreasuryFamilyRulesTests.cs`, `FiscalFamilyRulesTests.cs`, `PayrollFamilyRulesTests.cs` | Cas sain, cas dégradé **et** cas d'abstention pour chaque règle |
| Narratif | `AccountingAudit/RevisionDossierGuardTests.cs` | Les six garde-fous, lecture tolérante, chaîne complète du repli |
| PDF | `AccountingAudit/RevisionDossierPdfTests.cs` | Vrai fichier PDF (signature `%PDF`), dossier vide, impact non chiffrable, 60 anomalies paginées, accents |
| Front | `firm/revision/firm-revision.view-model.spec.ts` | Niveaux de risque, « non chiffrable », tris, bandeaux |
| Menu | `core/config/firm-navigation.registry.spec.ts` | Entrée après « Chef de mission », gating par `firm:revision:view`, survie au drapeau Gouvernance éteint, bascule de `filterFirmRevisionNav` |
| Menu | `core/services/app-nav.service.spec.ts` | Entrée masquée sans la permission, visible pour les deux rôles cabinet qui la portent |
| Garde | `core/guards/firm-revision.guard.spec.ts` | Responsable et collaborateur passent ; redirection sans permission ; redirection drapeau éteint |

```bash
dotnet test src/Backend/tests/FactuTrust.Infrastructure.Tests/FactuTrust.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AccountingAudit"
```

```bash
npx ng test --watch=false --browsers=ChromeHeadless --include='**/firm/revision/**/*.spec.ts'
```

> **Référence de comparaison.** Ce poste a des échecs préexistants : 3 constants côté back
> (`InvoiceRepositoryTests`, owned types `Money`), +6 si LocalDB est éteint, et des échecs front
> antérieurs à ce module. Comparer le **nombre de tests passés**, pas viser zéro.

### Piège des owned types `Money` en test

Chaque propriété `owned` doit recevoir **sa propre instance** de `Money`. Partager le même objet
entre `Product.UnitPrice` et `Product.PurchasePrice` casse le suivi EF à la persistance
(`Cannot save instance of 'Product.UnitPrice#Money'…`). Voir `SupplierInvoiceRepositoryTests`, qui
documente le même piège.

---

## 12. Hors périmètre

- **Z-caisse vs théorique (réviseur)** — la vacation POS existe (`CashRegisterSession` / `ZReport`)
  mais le réviseur IA ne croise pas encore le snapshot Z avec le journal `CashOperation`.
  `CashOperation` reste un journal d'opérations ; la clôture Z ne poste pas de deuxième écriture 5411.
- **Apprentissage** — les anomalies marquées « ignorée » ne réduisent pas encore le bruit ; cela
  suppose le journal de décision IA (S2 de la
  [feuille de route IA](../strategy/ai-differentiation-roadmap.md) §4.2).
- **Écritures correctives auto-appliquées** — le réviseur **propose** une action, il n'écrit jamais.
- **Métrologie IA** (tokens, coût par dossier) — aucun compteur n'existe dans le produit.

### Défaut connu, non corrigé

Les empreintes d'anomalie dépendent de la **culture du serveur** : `SingleGroup` sérialise les dates
via `DateOnly.ToString()`, qui n'est pas invariant. Un changement de culture serveur recréerait tout
l'historique d'anomalies. Le corriger exige une migration de données — chantier à part entière.
