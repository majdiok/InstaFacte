# 13 - Studio IA

Le **Studio IA** vous permet de créer, sans coder, des tables personnalisées, leurs relations, formulaires, données de référence et rapports, simplement en décrivant votre besoin en français. L’IA prépare une **proposition** que vous vérifiez, ajustez puis validez : rien n’est créé dans votre espace avant votre validation.

Pour ouvrir l’atelier : menu **Studio** puis **Assistant IA**, ou l’URL `/studio/ai`. La fonction doit être activée par votre administrateur ; sinon un message l’indique.

---

## L’écran de l’atelier

L’atelier se compose de trois zones :

- **L’en-tête** : le titre « Studio IA », un lien vers cette documentation et, une fois une conversation démarrée, le bouton **Nouvelle demande**.
- **La colonne principale** : la zone de saisie, les cartes « Que voulez-vous créer ? », puis, quand une proposition existe, l’**aperçu** (Vue d’ensemble, Tables, Relations, Formulaires, Données, Rapports…) et le fil de la conversation.
- **Le rail droit** (à droite sur un grand écran, sous la colonne principale en dessous de 1280 px) : **Modèles de systèmes**, **Actions rapides**, **Historique** et une carte de suggestions.

---

## Décrire un besoin

1. Choisissez éventuellement une carte (**Système complet**, **Table**, **Relations**, **Formulaire**, **Données de référence**, **Rapport**…). La carte préremplit la zone de saisie et sélectionne l’onglet d’aperçu correspondant ; vous restez libre de modifier le texte.
2. Décrivez votre besoin, par exemple : *« Créer un système de gestion des congés avec employés, types de congés, demandes, validations et rapports. »*
3. Vous pouvez joindre un document (PDF, texte, CSV, image, Word, Excel) avec **Joindre un document** : son contenu est transmis à l’IA avec votre demande.
4. Cliquez sur **Générer avec l’IA** (ou appuyez sur `Entrée` ; `Maj` + `Entrée` insère un retour à la ligne).

Les cartes marquées **Bientôt** (par exemple **Page**) ne sont pas encore disponibles ; **Workflow** s’active quand votre administrateur l’a autorisé.

### Modèle avancé

Si votre administrateur a configuré un modèle plus puissant, un interrupteur **Modèle avancé** apparaît sous la zone de saisie, avec le nom du modèle. Activez-le pour les demandes complexes (systèmes à plusieurs tables, règles métier détaillées). Votre choix est mémorisé sur cet appareil.

Si le modèle avancé ne peut pas être utilisé (désactivé, non configuré, indisponible), la demande est traitée avec le modèle standard et un bandeau **« Modèle standard utilisé »** en indique la raison. Rien n’est perdu : la proposition reste exploitable.

La dictée vocale (icône micro) arrive dans une prochaine version.

---

## Vérifier et valider la proposition

Une fois la proposition reçue, l’aperçu présente les tables, champs, relations, formulaires, données initiales et rapports prévus. Vous pouvez :

- parcourir les onglets pour vérifier chaque élément ;
- **Personnaliser** la proposition (renommer, ajouter ou retirer des champs, corriger les données initiales) puis enregistrer ;
- **Valider** : les éléments sont alors créés dans votre espace et un résumé vous propose d’ouvrir le système, ses tables et ses écrans ;
- **Refuser la proposition** si elle ne convient pas : elle passe en **Annulé** dans l’historique.

Une proposition non validée **expire** au bout d’un délai (« Cette proposition expire dans … » dans l’aperçu). Vous la retrouvez dans l’historique tant qu’elle est **À valider**.

### Tables en doublon

Si une table proposée ressemble à une table déjà présente dans votre espace (même clé, même nom, ou singulier/pluriel d’un nom existant), un bandeau **« La table « … » existe déjà »** s’affiche au-dessus de l’aperçu, avec deux choix :

- **Réutiliser la table existante** : la proposition s’appuie sur votre table actuelle ; ses champs et son formulaire ne sont pas recréés.
- **Créer quand même** : la nouvelle table est renommée avec le suffixe **« (2) »** pour éviter toute confusion, puis mise en surbrillance dans l’onglet Tables.

### Envoyer un message pendant qu’une proposition attend

Vous pouvez continuer à écrire pendant qu’une proposition attend votre validation. Comme un nouvel envoi remplace la proposition en cours, une confirmation vous est demandée : **Abandonner le plan et envoyer** annule la proposition (rien n’est créé) puis envoie votre message ; **Annuler** conserve la proposition.

### Modifier une table existante

L’assistant sait aussi retoucher une table déjà créée : ajouter, modifier ou retirer un champ, renommer la table, réorganiser le formulaire, ajouter un état, **réordonner les champs**, **changer le type d’un champ**, **relier la table à une autre** (relation simple ou plusieurs-à-plusieurs), **rattacher la table à un système** ou **proposer une vue enregistrée** (liste, kanban, calendrier). Demandez par exemple « ajoute un champ Motif de refus sur la table Contrats » ou « propose un kanban par statut pour les interventions » : l’aperçu détaille chaque modification avant validation, et la confirmation les applique réellement (chaque étape du suivi indique si elle a été appliquée, ignorée ou refusée — par exemple une relation plusieurs-à-plusieurs est signalée « ignorée » tant que votre administrateur ne l’a pas autorisée).

Le **changement de type** est encadré pour protéger vos données :

- une conversion **sans perte** (par exemple montant → nombre décimal, ou date → texte) est appliquée directement ; quand le champ perd un affichage spécialisé (calendrier, liste de choix…), l’aperçu vous le signale ;
- une conversion **qui exige une table vide** (par exemple texte → nombre) est refusée tant que la table contient des enregistrements : le message vous indique combien d’enregistrements bloquent et vous invite à vider la table ou à créer un nouveau champ à la place ;
- certains types ne peuvent jamais remplacer un champ existant (formule, pièce jointe, signature…) : l’aperçu affiche alors l’étape **en erreur** avec la raison, et l’application la refusera proprement.

Les **automatisations** demandées à l’assistant sont signalées « non appliquées » dans l’aperçu : elles arrivent dans une prochaine version et sont simplement ignorées à la validation.

---

## Le rail : modèles, actions rapides, historique

### Modèles de systèmes

Des systèmes prêts à l’emploi (CRM, gestion de stock, RH…) à adapter à votre activité. **Utiliser** prépare directement une proposition à partir du modèle, sans passer par l’IA : vous la vérifiez et la validez comme n’importe quelle proposition. **Voir tous** ouvre la **Bibliothèque de modèles** (`/studio/ai/templates`), organisée par catégorie ; **Utiliser ce modèle** vous ramène dans l’atelier avec la proposition prête.

Si une proposition est déjà affichée, une confirmation **« Remplacer la proposition en cours ? »** vous est demandée.

### Actions rapides

- **Réinitialiser la conversation** : annule toutes vos propositions en attente (elles restent visibles dans l’historique, en **Annulé**), efface la conversation et repart à zéro. Une confirmation est demandée et un message indique le nombre de propositions annulées.
- **Importer un modèle (JSON)**, **Dupliquer un système**, **Exporter le système (JSON)** : disponibles lorsque l’export de systèmes est activé par votre administrateur ; sinon **Exporter** est marqué **Bientôt**.
- **Partager avec l’équipe** : à venir.

### Historique des générations

Les cinq dernières générations, avec leur statut : **À valider**, **En cours**, **Terminé**, **Échec**, **Annulé**, **Expiré**. Cliquez sur une proposition **À valider** pour la reprendre dans l’atelier. **Voir tout** ouvre **Mes projets** (`/studio/ai/projects`) : toutes vos générations, 20 par page, filtrables par statut et par genre, avec **Reprendre** (propositions à valider) et **Ouvrir le système** (systèmes créés).

Chaque ligne indique aussi les compteurs de la proposition (tables, relations, vues) et, en cas d’échec, la raison. **Aperçu** affiche le détail structuré d’une génération (tables, champs, relations, formulaires, données de départ, avertissements) tel qu’il a été calculé, sans rien modifier. **Rejouer** — proposé sur les générations **Terminé**, **Échec**, **Annulé** ou **Expiré** — recrée une nouvelle proposition **À valider** à partir de la même demande, revérifiée contre l’existant (doublons signalés à nouveau) : la génération d’origine est conservée telle quelle, et rien n’est appliqué tant que vous n’avez pas validé la nouvelle proposition.

---

## Questions fréquentes

**Rien n’est créé tant que je n’ai pas validé ?**
Exact. L’IA ne fait que proposer ; la création n’a lieu qu’au clic sur **Valider**, et un résumé vous indique précisément ce qui a été créé.

**Pourquoi le rail ne montre-t-il pas les modèles ou l’historique ?**
Ces cartes dépendent des fonctions activées par votre administrateur (bibliothèque de modèles, aperçu des propositions). **Réinitialiser la conversation** reste toujours disponible.

**Pourquoi le Studio a-t-il une couleur différente du reste de l’application ?**
Le Studio utilise une teinte indigo pour distinguer clairement l’espace de conception (tables, formulaires, rapports, IA) de l’espace de gestion quotidienne, qui garde le bleu FactuTrust.

**Un message « Ce plan est introuvable ou a expiré » s’affiche quand je reprends une proposition.**
La proposition a dépassé son délai de validité ou a déjà été validée/annulée. Refaites la demande depuis l’atelier ; l’historique conserve la trace de l’ancienne.
