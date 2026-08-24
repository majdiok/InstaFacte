# Espace client (portail)

L’espace client permet à un contact d’un client CRM de consulter **ses** factures, paiements et relevé, et de télécharger les PDF — sans accéder à l’ERP.

## Activer

1. Paramètres → Mon entreprise → activer **Espace client**.
2. Fiche client → onglet **Espace client** → inviter un e-mail.
3. Le contact reçoit un lien (valable 7 jours) pour choisir un mot de passe.
4. Après connexion, il arrive sur `/portal` (pas le tableau de bord staff).

## Règles

- Invitation uniquement depuis la fiche client (pas depuis Paramètres → Utilisateurs).
- Un e-mail ne peut pas être à la fois staff et portail, ni portail de deux entreprises.
- Les contacts portail **ne comptent pas** dans le quota d’utilisateurs staff.
- Les brouillons de facture ne sont pas visibles.
- Le paiement en ligne n’est pas disponible dans cette version.

## Connexion orpheline

Un compte déjà créé avec le rôle Client **sans** fiche client liée ne peut pas se connecter. Invitez-le depuis la fiche client.
