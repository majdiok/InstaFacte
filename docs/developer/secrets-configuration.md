# Configuration des secrets (JWT, signature électronique)

Certains paramètres sensibles ne doivent **jamais** être commités dans
`appsettings.json` / `appsettings.Development.json`. Le démarrage de l'API
échoue volontairement (`InvalidOperationException`, message en français) si
l'un d'eux est absent, trop court, ou correspond à une ancienne clé
compromise.

## `JwtSettings:SecretKey`

- Utilisée pour signer/valider les JWT (`Program.cs`).
- Doit faire **au moins 32 octets** (256 bits).
- `appsettings.json` et `appsettings.Development.json` ne contiennent **pas**
  de valeur : à configurer localement via `dotnet user-secrets`, ou en
  production via la variable d'environnement `JwtSettings__SecretKey`.
- Une liste d'empreintes SHA-256 de clés historiquement commitées dans le
  dépôt (donc compromises) est bloquée au démarrage, quelle que soit leur
  provenance (`compromisedLegacyJwtKeySha256Values` dans `Program.cs`). Si
  vous générez une nouvelle clé après une fuite, ajoutez le hash SHA-256 de
  l'ancienne clé à cette liste (pas la clé elle-même).

## `SignatureSettings:SecretKey`

- Utilisée par `SignatureService` (HMAC-SHA256) pour signer électroniquement
  les factures (`SignInvoiceCommand`).
- Même contrainte de taille (≥ 32 octets) et même absence volontaire de
  valeur dans les fichiers `appsettings*.json` commités.
- À configurer via `dotnet user-secrets` en développement, ou
  `SignatureSettings__SecretKey` en production.

## Configurer les secrets localement (développement)

Depuis `src/Backend/FactuTrust.API` :

```bash
dotnet user-secrets init
dotnet user-secrets set "JwtSettings:SecretKey" "<valeur aléatoire ≥ 32 octets>"
dotnet user-secrets set "SignatureSettings:SecretKey" "<valeur aléatoire ≥ 32 octets>"
```

Générer une valeur aléatoire, par exemple :

```bash
openssl rand -base64 48
```

## En production

Définir les variables d'environnement (le double underscore `__` correspond
à `:` dans la configuration .NET) :

```bash
export JwtSettings__SecretKey="<valeur aléatoire ≥ 32 octets>"
export SignatureSettings__SecretKey="<valeur aléatoire ≥ 32 octets>"
```

## Rotation d'une clé compromise

Si une clé a été commitée par erreur (comme l'ancienne `JwtSettings:SecretKey`
retirée d'`appsettings.Development.json`) :

1. Générer et déployer une nouvelle clé dans **tous** les environnements où
   l'ancienne a pu être utilisée (le simple retrait du fichier ne suffit pas :
   la valeur reste dans l'historique git).
2. Ajouter le hash SHA-256 de l'ancienne clé à la liste de blocage
   correspondante (`Program.cs` pour le JWT) pour empêcher sa réutilisation.
3. Noter que la rotation invalide les JWT déjà émis (les utilisateurs
   devront se reconnecter).
