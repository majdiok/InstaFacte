# Plan de Test - Correction des Boutons de Modal

## Date de Test
**À compléter lors de l'exécution des tests**

## Objectif
Valider que les boutons de la modal de confirmation sont désormais cliquables et que toutes les autres modales continuent de fonctionner correctement.

## Environnement de Test
- **Navigateur:** Chrome/Edge/Firefox
- **Version Angular:** 17.0.0
- **Résolution:** Desktop (1920x1080), Tablet (768x1024), Mobile (375x667)

---

## Tests Prioritaires

### ✅ Test 1: Modal de Confirmation d'Émission de Facture (CAS PRINCIPAL)

**Chemin:** `/invoices/new` → Étape 6 → "Valider et émettre"

**Étapes:**
1. Démarrer le serveur: `npm start`
2. Se connecter à l'application
3. Naviguer vers "Factures" → "Nouvelle facture"
4. Compléter toutes les étapes (1-6)
5. À l'étape 6 (Aperçu), cliquer sur "Valider et émettre"
6. **VÉRIFIER:** La modal "CONFIRMER L'ÉMISSION" apparaît
7. **VÉRIFIER:** La modal est au-dessus de tout le contenu (z-index correct)
8. **TEST CRITIQUE:** Cliquer sur "Annuler" → La modal doit se fermer
9. Cliquer à nouveau sur "Valider et émettre"
10. **TEST CRITIQUE:** Cliquer sur "Émettre la facture" → La facture doit être émise

**Résultat Attendu:**
- ✓ Modal visible et centrée
- ✓ Backdrop semi-transparent visible
- ✓ Bouton "Annuler" cliquable et fonctionnel
- ✓ Bouton "Émettre la facture" cliquable et fonctionnel
- ✓ ESC ferme la modal
- ✓ Tab permet de naviguer entre les boutons

**Statut:** [ ] Réussi / [ ] Échoué

---

### ✅ Test 2: Modal d'Annulation du Wizard

**Chemin:** `/invoices/new` → Bouton "Annuler" (en haut à gauche)

**Étapes:**
1. Commencer une nouvelle facture
2. Remplir quelques champs (pour avoir isDirty = true)
3. Cliquer sur le bouton "Annuler"
4. **VÉRIFIER:** Modal "Quitter le wizard" apparaît
5. **TEST:** Cliquer sur "Rester" → Modal se ferme, reste sur la page
6. Cliquer à nouveau sur "Annuler"
7. **TEST:** Cliquer sur "Quitter" → Retour à la liste des factures

**Résultat Attendu:**
- ✓ Les deux boutons sont cliquables
- ✓ Navigation correcte selon le bouton cliqué

**Statut:** [ ] Réussi / [ ] Échoué

---

### ✅ Test 3: Modal de Suppression de Client

**Chemin:** `/clients` → Icône poubelle sur un client

**Étapes:**
1. Naviguer vers "Clients"
2. Cliquer sur l'icône de suppression d'un client
3. **VÉRIFIER:** Modal "Confirmation de suppression" apparaît
4. **TEST:** Cliquer sur "Annuler" → Modal se ferme, client non supprimé
5. Cliquer à nouveau sur l'icône de suppression
6. **TEST:** Cliquer sur "Supprimer" → Client supprimé

**Résultat Attendu:**
- ✓ Modal avec style danger (bouton rouge)
- ✓ Boutons cliquables
- ✓ Suppression effective lors de la confirmation

**Statut:** [ ] Réussi / [ ] Échoué

---

### ✅ Test 4: Modal de Suppression de Produit

**Chemin:** `/products` → Actions → Supprimer

**Étapes:**
1. Naviguer vers "Produits"
2. Cliquer sur "Supprimer" pour un produit
3. **VÉRIFIER:** Modal de confirmation apparaît
4. **TEST:** Vérifier la cliquabilité des boutons

**Résultat Attendu:**
- ✓ Boutons cliquables
- ✓ Comportement cohérent avec les autres modales

**Statut:** [ ] Réussi / [ ] Échoué

---

### ✅ Test 5: Modal de Suppression de Devis

**Chemin:** `/quotes/:id` → Bouton de suppression

**Étapes:**
1. Ouvrir un devis existant
2. Cliquer sur le bouton de suppression
3. **TEST:** Vérifier la cliquabilité des boutons

**Résultat Attendu:**
- ✓ Boutons cliquables
- ✓ Suppression effective

**Statut:** [ ] Réussi / [ ] Échoué

---

### ✅ Test 6: Modal de Suppression de Ligne (Invoice Wizard)

**Chemin:** `/invoices/new` → Étape 4 (Articles) → Supprimer une ligne

**Étapes:**
1. Créer une nouvelle facture
2. Aller à l'étape "Articles"
3. Ajouter au moins 2 lignes
4. Cliquer sur l'icône de suppression d'une ligne
5. **TEST:** Vérifier la modal de confirmation

**Résultat Attendu:**
- ✓ Modal apparaît correctement
- ✓ Boutons cliquables
- ✓ Ligne supprimée après confirmation

**Statut:** [ ] Réussi / [ ] Échoué

---

## Tests d'Accessibilité

### ✅ Test 7: Navigation Clavier

**Pour chaque modal:**

1. **Tab:** Doit naviguer entre les éléments (bouton fermer → bouton annuler → bouton confirmer)
2. **Shift+Tab:** Navigation arrière
3. **Enter:** Active le bouton ayant le focus
4. **ESC:** Ferme la modal (équivalent à "Annuler")
5. **Focus visible:** Un outline bleu doit être visible autour de l'élément focusé

**Résultat Attendu:**
- ✓ Navigation complète au clavier possible
- ✓ Focus visible sur tous les éléments interactifs
- ✓ ESC ferme correctement la modal

**Statut:** [ ] Réussi / [ ] Échoué

---

## Tests Responsive

### ✅ Test 8: Mobile (375px)

**Étapes:**
1. Ouvrir les DevTools (F12)
2. Passer en mode mobile (iPhone SE ou similaire)
3. Répéter le Test 1 (modal d'émission de facture)

**Résultat Attendu:**
- ✓ Modal occupe 90% de la largeur
- ✓ Boutons empilés verticalement
- ✓ Boutons en pleine largeur
- ✓ Tout le contenu visible sans scroll horizontal

**Statut:** [ ] Réussi / [ ] Échoué

---

### ✅ Test 9: Tablet (768px)

**Étapes:**
1. DevTools en mode tablet (iPad Mini)
2. Répéter le Test 1

**Résultat Attendu:**
- ✓ Modal bien proportionnée
- ✓ Boutons côte à côte
- ✓ Lisibilité parfaite

**Statut:** [ ] Réussi / [ ] Échoué

---

## Tests de Non-Régression

### ✅ Test 10: Vérifier le Z-Index du Wizard

**Étapes:**
1. Ouvrir une nouvelle facture
2. **VÉRIFIER:** Le wizard s'affiche correctement (pas de problème de z-index)
3. Les boutons du wizard fonctionnent normalement
4. La navigation entre les étapes fonctionne

**Résultat Attendu:**
- ✓ Pas de régression sur l'affichage du wizard
- ✓ Les styles du wizard sont intacts

**Statut:** [ ] Réussi / [ ] Échoué

---

### ✅ Test 11: Autres Composants PrimeNG

**Étapes:**
1. Vérifier les dropdowns (sélection de client, produit, etc.)
2. Vérifier les tooltips (hover sur les icônes)
3. Vérifier les toasts (notifications)

**Résultat Attendu:**
- ✓ Tous les composants PrimeNG fonctionnent normalement
- ✓ Pas de conflit de z-index

**Statut:** [ ] Réussi / [ ] Échoué

---

## Validation Visuelle

### ✅ Test 12: Design System Cohérence

**Pour chaque modal, vérifier:**

1. **Couleurs:** Respect du design system FactuTrust
   - Bouton primaire: Bleu (#2563eb)
   - Bouton danger: Rouge (#dc2626)
   - Bouton success: Vert (#16a34a)
   - Bouton secondaire: Gris avec bordure

2. **Typographie:**
   - Header: Font-weight 600, taille appropriée
   - Message: Lisible, couleur secondaire

3. **Espacement:**
   - Padding cohérent
   - Gap entre les boutons

4. **Border-radius:**
   - Coins arrondis selon le design system

5. **Ombre:**
   - Shadow-xl visible autour de la modal

**Résultat Attendu:**
- ✓ Design cohérent avec le reste de l'application
- ✓ Apparence professionnelle et moderne

**Statut:** [ ] Réussi / [ ] Échoué

---

## Tests de Performance

### ✅ Test 13: Temps de Réponse

**Mesurer:**
1. Temps d'ouverture de la modal (< 200ms)
2. Temps de réaction au clic sur un bouton (< 100ms)
3. Temps de fermeture de la modal (< 200ms)

**Résultat Attendu:**
- ✓ Aucun lag perceptible
- ✓ Animation fluide

**Statut:** [ ] Réussi / [ ] Échoué

---

## Résumé des Tests

| Test | Description | Priorité | Statut |
|------|-------------|----------|--------|
| 1 | Émission facture | 🔴 CRITIQUE | [ ] |
| 2 | Annulation wizard | 🟠 HAUTE | [ ] |
| 3 | Suppression client | 🟠 HAUTE | [ ] |
| 4 | Suppression produit | 🟡 MOYENNE | [ ] |
| 5 | Suppression devis | 🟡 MOYENNE | [ ] |
| 6 | Suppression ligne | 🟡 MOYENNE | [ ] |
| 7 | Navigation clavier | 🟠 HAUTE | [ ] |
| 8 | Mobile responsive | 🟠 HAUTE | [ ] |
| 9 | Tablet responsive | 🟡 MOYENNE | [ ] |
| 10 | Non-régression wizard | 🟠 HAUTE | [ ] |
| 11 | Autres composants | 🟡 MOYENNE | [ ] |
| 12 | Cohérence design | 🟡 MOYENNE | [ ] |
| 13 | Performance | 🟢 BASSE | [ ] |

---

## Problèmes Identifiés

*À compléter lors des tests*

| # | Description | Sévérité | Solution Proposée |
|---|-------------|----------|-------------------|
| | | | |

---

## Validation Finale

### ✅ Checklist Avant Validation

- [ ] Tous les tests critiques (🔴) sont passés
- [ ] Tous les tests haute priorité (🟠) sont passés
- [ ] Aucune régression détectée
- [ ] Design cohérent et professionnel
- [ ] Accessibilité validée
- [ ] Responsive validé

### Signature

**Testeur:** ____________________
**Date:** ____________________
**Statut Global:** [ ] ✅ VALIDÉ / [ ] ❌ À CORRIGER

---

## Notes Additionnelles

*Ajouter toute observation ou remarque importante ici*
