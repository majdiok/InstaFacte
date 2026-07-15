# Test Rapide - Création de Facture

## ✅ Backend Démarré
Le backend est en cours de démarrage en arrière-plan.

## 📋 Étapes de Test

### 1. Vérifier que le Backend est Démarré

Attendez quelques secondes, puis ouvrez dans votre navigateur :
- **Swagger UI**: https://localhost:7001/swagger
- Si Swagger s'affiche, le backend est prêt ✅

### 2. Démarrer le Frontend (dans un nouveau terminal)

```powershell
cd src/Frontend/factutrust-web
npm start
```

L'application frontend démarrera sur `http://localhost:4200`

### 3. Tester la Création de Facture

1. **Ouvrir l'application** : `http://localhost:4200`
2. **Se connecter** avec vos identifiants
3. **Naviguer vers** : `/invoices/new` ou cliquer sur "Nouvelle facture"
4. **Remplir les informations** :
   - Sélectionner un client
   - Date d'émission
   - Date d'échéance
5. **Ajouter une ligne avec produit** :
   - Cliquer sur "Ajouter une ligne"
   - Sélectionner un produit existant
   - Définir la quantité (ex: 2)
   - Optionnel : ajouter une remise (ex: 10%)
6. **Valider et soumettre** :
   - Passer à l'étape de validation/aperçu
   - Cliquer sur "Valider et créer la facture"

### 4. Vérifier le Résultat

#### ✅ SUCCÈS si :
- La facture est créée sans erreur
- Message de succès affiché
- La facture apparaît dans la liste
- **AUCUNE erreur** "Cannot save instance of 'InvoiceLine.UnitPrice#Money'..."

#### ❌ ÉCHEC si :
- L'erreur "Cannot save instance of 'InvoiceLine.UnitPrice#Money' because it is an owned entity without any reference to its owner" apparaît
- Erreur 500 ou 400 dans la console
- La facture n'est pas créée

### 5. Vérifier les Logs

Si une erreur se produit, vérifier :
- **Console du navigateur** (F12 > Console)
- **Logs backend** : `src/Backend/FactuTrust.API/logs/factutrust-YYYYMMDD.log`
- **Console du backend** (terminal où dotnet run a été exécuté)

## 🔍 Points de Vérification de la Correction

La correction devrait :
1. ✅ Attacher les `Product` au contexte avant de sauvegarder
2. ✅ Gérer les cas où le `Product` est déjà tracké
3. ✅ Permettre la sauvegarde des owned entities `Money` sans erreur

## 📝 Note

Si vous voyez l'erreur "owned entity without owner", cela signifie que la correction n'a pas fonctionné comme prévu. Dans ce cas, vérifiez :
- Que le code dans `InvoiceRepository.AddAsync()` contient bien la logique d'attachement
- Les logs backend pour plus de détails sur l'erreur
