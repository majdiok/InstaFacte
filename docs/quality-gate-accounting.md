# Quality Gate — module Comptabilité (FactuTrust)

Checklist à exécuter avant merge d’une PR touchant `features/accounting` ou l’API `/api/accounting/*`.

## Frontend (`src/Frontend/factutrust-web`)

```bash
npm run lint
npm run test -- --no-watch --browsers=ChromeHeadless
npm run test:e2e
npm run build:prod
```

## Backend (tests hors solution principale)

Depuis la racine des solutions backend, exécuter explicitement les projets de tests :

```bash
dotnet test src/Backend/tests/FactuTrust.API.Tests/FactuTrust.API.Tests.csproj
dotnet test src/Backend/tests/FactuTrust.Infrastructure.Tests/FactuTrust.Infrastructure.Tests.csproj
```

## Matrice must-pass

| Type de changement | Obligatoire |
|--------------------|-------------|
| UI uniquement (templates/CSS) | `lint`, `build:prod`, tests unitaires impactés |
| UI + service HTTP | + `accounting.service.spec.ts` + smoke manuel des écrans |
| API / DTO / lettrage | + `dotnet test` projets ci-dessus |
| Immobilisations / FF achats | + `fixed-assets.service.spec.ts` + tests `FixedAsset*` / `SupplierInvoiceJournalLineBuilder` backend |
| **Règle d'audit / moteur de contrôle** | + `dotnet test --filter "FullyQualifiedName~AccountingAudit"` |

### Ajouter une règle de contrôle — points de passage obligés

`AuditRuleContractTests` échoue si l'un d'eux est oublié :

1. **Constructeur sans paramètre.** Une règle lit uniquement par `ctx.Db`. Injecter un repository
   la ferait lire le tenant *ambiant* — celui du cabinet lors d'un balayage de portefeuille — donc
   les données d'un autre dossier.
2. **Code unique**, et `ModuleCode` présent dans `AccountingAuditModuleCatalog`.
3. **Enregistrement DI** dans `RegisterAccountingAuditServices`, aux deux endroits (type concret et
   `IAccountingAuditRule`). Sans lui, la règle n'est jamais évaluée — en silence.
4. **Discriminant d'empreinte** dès que la règle émet plus d'une anomalie par compte et par
   période : surcharge `SingleGroup(..., discriminator)` avec un identifiant métier *stable*
   (fournisseur, matricule, n° de pièce). `Fingerprint` porte un index unique — sans discriminant,
   la seconde anomalie écrase la première.
5. **Lien de correction** dans `AuditCorrectionLinkBuilder` (à défaut, repli générique).

## Scénarios manuels rapides (smoke)

1. Plan comptable : chargement, recherche, pagination.
2. Journal : plage de dates valide / invalide, actualiser.
3. Grand livre : compte requis, navigation depuis balance si drilldown.
4. Saisie manuelle : écriture équilibrée, message si déséquilibre.
5. Lettrage : charger écritures, sélection, lettrage (si données dispo).
6. **Immobilisations** : registre, création manuelle, mise en service (JIM), tableau CP17, export Excel.
7. **FF → immo (V2)** : BC reçu → facture avec ligne classée immo → écriture 21x/43662 → brouillon registre lié → badge liste/détail FF.
8. **Dotations** : batch exercice, export rapport dotations, TVA 43662 dans déclaration (si données).

## Feature flags (rollout progressif)

Le service `AccountingFeatureFlagsService` lit `localStorage` sous la clé `ft.accounting.featureFlags` (JSON), fusionné avec les valeurs par défaut :

- `sharedAccountingUi` (défaut `true`) — barres de filtres / bannières partagées
- `tableShellOverlay` (défaut `true`) — enveloppe table avec overlay chargement si utilisée
- `consoleAccountingErrors` (défaut `false`) — logs console des erreurs réseau côté comptabilité (support / debug)
- `fixedAssetsEnabled` (défaut `true`) — sous-module Immobilisations (registre, dotations, intégration FF)

Backend : `Features:FixedAssets:Enabled` dans `appsettings.json` (503 sur `/api/accounting/fixed-assets/*` si `false`).

Exemple (console navigateur) :

```js
localStorage.setItem('ft.accounting.featureFlags', JSON.stringify({ consoleAccountingErrors: true }));
```

## Sécurité (OWASP — rappel)

- Ne pas exposer d’erreurs techniques brutes à l’utilisateur final.
- Valider entrées côté client (UX) et serveur (autorité).
- Pas de concaténation SQL côté client ; paramètres HTTP typés.
