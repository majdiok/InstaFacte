# Guide de Test - Création de facture

## Parcours du wizard de création (legacy 6 étapes vs simplifié 4 étapes)

Depuis la simplification UX du wizard, deux parcours coexistent derrière le feature
flag `environment.featureFlags.wizardSimplifiedFlow` :

| Flag | Étapes affichées | Cas d'usage |
| ---- | ---------------- | ----------- |
| `false` (défaut) | Type & Date → Émetteur → Client → Articles → Paiement → Validation (6) | Comportement historique, parcours de référence pour la non-régression |
| `true` | Document → Client → Facturation → Récap & Émission (4) | Parcours simplifié, valeurs par défaut intelligentes, paiement en accordion replié |

Pour basculer ponctuellement vers le parcours simplifié sans recompiler, ouvrir la
console du navigateur et exécuter :

```js
localStorage.setItem('factutrust:feature-flags-overrides', '{"wizardSimplifiedFlow":true}');
location.reload();
```

Pour revenir au parcours legacy, supprimer la clé puis recharger :

```js
localStorage.removeItem('factutrust:feature-flags-overrides');
location.reload();
```

### Sélecteurs e2e stables

Le stepper expose `data-step-key` et `data-step-index` sur chaque bouton d'étape.
Les tests Playwright doivent cibler `[data-step-key="document"]` plutôt que des
libellés FR pour rester compatibles avec les deux parcours.

### Émission (étape Validation / Récap)

L'étape finale expose **un seul bouton utilisateur** : **Émettre la facture** ou **Émettre l'avoir**.

| Type | Action « Émettre » | Appel API | Statut créé |
|------|-------------------|-----------|-------------|
| Facture standard | `POST /api/invoices` | Facture métier | `Draft` (validation/signature depuis la fiche) |
| Facture d'avoir | `POST wizard/drafts` puis `.../submit` | Facture métier | `Validated` |

Il n'y a **plus de bouton** « Enregistrer en brouillon » / « Enregistrer brouillon » dans le wizard.

### Autosave (silencieux, conservé)

Toute saisie déclenche après 2 s d'inactivité un appel silencieux
`POST /api/invoices/wizard/drafts` (brouillon wizard technique, pas une facture émise).
L'indicateur header passe de « Non enregistré » à « Progression sauvegardée à HH:mm ».
La reprise reste possible via `/invoices/new/draft/:draftId`.

### Tests de non-régression à exécuter pour chaque PR

1. Créer une facture simple (1 société, 1 client existant, 1 ligne produit) — flag à `false`.
2. Idem avec flag à `true` (parcours 4 étapes).
3. Créer une facture d'avoir liée à une facture existante.
4. Importer une facture via l'IA (prefill de toutes les étapes).
5. Pré-sélection client via `?clientId=...`.
6. Reprise d'un brouillon via `/invoices/new/draft/:draftId`.
7. Annulation avec modifications non sauvegardées (modale de confirmation).
8. Vérifier que l'indicateur « Progression sauvegardée à HH:mm » apparaît après 2 s d'inactivité.
9. Vérifier l'absence de tout bouton « Enregistrer brouillon » / « Enregistrer en brouillon » sur toutes les étapes du wizard.

---

# Guide de Test - Validation de la Correction "Owned Entity Without Owner"

## Objectif
Tester la création de factures dans l'application réelle pour valider que l'erreur "Cannot save instance of 'InvoiceLine.UnitPrice#Money' because it is an owned entity without any reference to its owner" est résolue.

## Prérequis
1. Base de données SQL Server LocalDB configurée
2. Application backend démarrée
3. Application frontend démarrée
4. Utilisateur authentifié avec un tenant configuré

## Étapes de Test

### 1. Démarrer l'Application Backend

```powershell
cd src/Backend/FactuTrust.API
dotnet run
```

L'API devrait démarrer sur :
- HTTPS: `https://localhost:7001`
- HTTP: `http://localhost:7000`
- Swagger: `https://localhost:7001/swagger`

### 2. Démarrer l'Application Frontend

```powershell
cd src/Frontend/factutrust-web
npm start
# ou
ng serve
```

L'application devrait démarrer sur `http://localhost:4200`

### 3. Scénario de Test Principal

#### 3.1. Créer une Nouvelle Facture

1. **Se connecter à l'application** avec un utilisateur valide
2. **Naviguer vers "Nouvelle facture"** (`/invoices/new`)
3. **Remplir les informations de base** :
   - Date d'émission
   - Date d'échéance
   - Client (sélectionner un client existant)
   - Référence (optionnel)

#### 3.2. Ajouter des Lignes avec Produits

1. **Ajouter une ligne de facture** :
   - Sélectionner un produit existant depuis le catalogue
   - Définir la quantité (ex: 2)
   - Vérifier que le prix unitaire est pré-rempli
   - Optionnellement, ajouter une remise (ex: 10%)

2. **Vérifier les calculs automatiques** :
   - Montant HT
   - Remise
   - TVA
   - Total TTC

3. **Ajouter plusieurs lignes** (test avec plusieurs produits) :
   - Répéter l'étape précédente avec différents produits
   - Vérifier que tous les calculs sont corrects

#### 3.3. Valider et Soumettre la Facture

1. **Passer à l'étape de validation** (aperçu)
2. **Vérifier toutes les informations** :
   - Totaux corrects
   - Informations client
   - Lignes de facture
   - Mentions légales
   - Informations de paiement

3. **Soumettre la facture** :
   - Cliquer sur "Valider et créer la facture"
   - **OBSERVER LE RÉSULTAT** :
     - ✅ **SUCCÈS** : La facture est créée sans erreur
     - ❌ **ÉCHEC** : Si l'erreur "Cannot save instance of 'InvoiceLine.UnitPrice#Money'..." apparaît, la correction n'a pas fonctionné

### 4. Points de Vérification

#### ✅ Test Réussi si :
- La facture est créée avec succès
- Aucune erreur dans la console du navigateur
- Aucune erreur dans les logs du backend
- La facture apparaît dans la liste des factures
- Tous les montants (UnitPrice, DiscountAmount, SubTotal, VatAmount, Total) sont correctement sauvegardés

#### ❌ Test Échoué si :
- L'erreur "Cannot save instance of 'InvoiceLine.UnitPrice#Money' because it is an owned entity without any reference to its owner" apparaît
- La facture n'est pas créée
- Des erreurs 500 ou 400 apparaissent dans la console

### 5. Vérification des Logs Backend

Pendant le test, surveiller les logs du backend pour :
- Messages d'erreur liés aux owned entities
- Messages de tracking EF Core
- Erreurs de sauvegarde

Les logs sont disponibles dans :
- Console de l'application
- Fichier : `logs/factutrust-YYYYMMDD.log`

### 6. Test avec Produits Détachés (Scénario Problématique)

Pour reproduire le scénario problématique :

1. **Créer un produit** dans le catalogue
2. **Créer une facture** avec ce produit
3. **Soumettre la facture** immédiatement

Ce scénario simule le cas où le `Product` est chargé dans un contexte différent (via `ProductRepository.GetByIdAsync()`) et doit être attaché au contexte de `InvoiceRepository.AddAsync()`.

### 7. Test de Régression

Vérifier que les fonctionnalités suivantes fonctionnent toujours :
- ✅ Création de factures avec produits
- ✅ Création de factures avec lignes personnalisées (sans produit)
- ✅ Modification de brouillons
- ✅ Calculs automatiques des totaux
- ✅ Validation des données

## Résultat Attendu

Après la correction, la création de factures devrait fonctionner **sans erreur** même lorsque :
- Les produits sont chargés depuis un contexte différent
- Plusieurs lignes avec différents produits sont ajoutées
- Les owned entities `Money` sont créées dans `InvoiceLine.Calculate()`

## En Cas d'Échec

Si l'erreur persiste :

1. **Vérifier les logs backend** pour plus de détails
2. **Vérifier que la correction est bien déployée** :
   - Le fichier `InvoiceRepository.cs` contient la logique d'attachement des `Product`
   - La méthode `AddAsync` vérifie et attache les produits avant de sauvegarder

3. **Vérifier la configuration de la base de données** :
   - Les migrations sont appliquées
   - La connexion fonctionne correctement

4. **Contacter le développeur** avec :
   - Les logs complets
   - Les étapes exactes pour reproduire l'erreur
   - Une capture d'écran de l'erreur

## Notes Techniques

La correction implémentée :
- Attache les entités `Product` référencées au contexte avant d'ajouter l'invoice
- Gère les cas où le `Product` est déjà tracké dans le contexte
- Évite les conflits de tracking EF Core

Cette correction garantit que toutes les entités référencées sont correctement trackées avant la sauvegarde, ce qui permet à EF Core de tracker correctement les owned entities `Money` dans les `InvoiceLine`.
