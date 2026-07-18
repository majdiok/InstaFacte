# Migrations des bases tenant (FactuTrust)

## Erreur HTTP 503 — `TENANT_MIGRATION_FAILED`

Si le frontend affiche **« Impossible d'accéder à la base de données »** avec le code `TENANT_MIGRATION_FAILED` dans la console :

- **Cause :** une migration EF Core tenant est en attente ou a échoué (`TenantMiddleware` bloque toutes les API authentifiées).
- **Diagnostic :** consulter les logs API (`Failed to ensure migrations for tenant {TenantId}`) et l'historique `__EFMigrationsHistory` sur la base du tenant.

### Solution

**Option 1 – Redémarrer l'API (développement)**  
Avec `TenantMigrations.ApplyOnStartup = true`, les migrations en attente sont ré-appliquées au démarrage. En dev, un échec bloque le démarrage pour signaler le problème immédiatement.

**Option 2 – Backoffice / API plateforme**  
Avec un compte **PlatformAdmin** :

```http
POST https://localhost:7001/api/platform/migrations/tenants/apply-migrations
Authorization: Bearer <token>
```

Pour un tenant précis :

```http
POST https://localhost:7001/api/platform/migrations/tenants/{tenantId}/apply-migrations
Authorization: Bearer <token>
```

Statut global :

```http
GET https://localhost:7001/api/platform/migrations/tenants/migrations-status
Authorization: Bearer <token>
```

**Option 3 – Ligne de commande EF (maintenance)**  

```bash
dotnet ef database update \
  --project src/Backend/FactuTrust.Infrastructure \
  --startup-project src/Backend/FactuTrust.API \
  --context TenantDbContext \
  --connection "Server=(localdb)\MSSQLLocalDB;Database=FactuTrust_Tenant_XXXX;..."
```

---

## Erreur « Invalid column name 'Reference' » sur la page Inventaire

Si la page **Inventaire** affiche une erreur de chargement du type :

- **Message :** `Invalid column name 'Reference'.`
- **Cause :** La migration qui ajoute la colonne `Reference` à la table `PhysicalInventories` n'a pas encore été appliquée sur la base de données du tenant.

Appliquer les migrations tenant via l'une des options ci-dessus.

---

## Erreur « Invalid object name 'CustomSystemDefinitions' » sur Studio AI

Si la page **Studio AI** (`/studio/ai`) ou la sidebar Studio affiche une erreur HTTP **500** sur `GET /api/studio/nav` :

- **Message :** `Invalid object name 'CustomSystemDefinitions'.` (ou `Invalid column name 'SystemId'`)
- **Cause :** la migration tenant `20260624181553_AddStudioSystems_Tenant` n'a pas été appliquée sur la base du tenant alors que le code backend la référence déjà.

### Solution

Appliquer les migrations tenant via l'une des options de la section [Erreur HTTP 503](#erreur-http-503--tenant_migration_failed).

**Script idempotent (production / DBA) :** [`docs/runbooks/sql/AddStudioSystems_Tenant.idempotent.sql`](runbooks/sql/AddStudioSystems_Tenant.idempotent.sql)

**Vérification SQL :**

```sql
SELECT MigrationId FROM __EFMigrationsHistory
WHERE MigrationId LIKE '%AddStudioSystems%';

SELECT OBJECT_ID('CustomSystemDefinitions') AS TableExists;
```

---

## Erreur « Invalid column name 'ValidatedAt' / 'ValidatedBy' » sur l'échéancier fiscal

Si la page **Échéancier fiscal** (`/accounting/fiscal-schedule`) affiche **« Chargement impossible »** avec une erreur HTTP **500** dans la console :

- **Message :** `Invalid column name 'ValidatedAt'.` et/ou `Invalid column name 'ValidatedBy'.`
- **Cause :** la migration tenant `20260710040719_AddFiscalScheduleValidatedAt_Tenant` n'a pas été appliquée sur la base du tenant alors que le code backend interroge déjà ces colonnes sur `FiscalScheduleEntries`.

### Solution

Appliquer les migrations tenant via l'une des options de la section [Erreur HTTP 503](#erreur-http-503--tenant_migration_failed).

**Script idempotent (production / DBA) :** [`docs/runbooks/sql/AddFiscalScheduleValidatedAt_Tenant.idempotent.sql`](runbooks/sql/AddFiscalScheduleValidatedAt_Tenant.idempotent.sql)

**Vérification SQL :**

```sql
SELECT MigrationId FROM __EFMigrationsHistory
WHERE MigrationId LIKE '%AddFiscalScheduleValidatedAt%';

SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'FiscalScheduleEntries'
  AND COLUMN_NAME IN ('ValidatedAt', 'ValidatedBy');
```

---

## Unicité des numéros de facture de vente (index unique différé)

L'index sur `Invoices.Number` est aujourd'hui **non unique** (migration
`20260717125815_AddInvoiceSearchAndAuditIndexes_Tenant`). L'index UNIQUE exigé
par la réglementation fiscale ne doit être livré qu'après un balayage 100 %
propre de tous les tenants — une migration unique en échec bloquerait le tenant
au boot (`TenantMigrationGuard`).

Procédure complète (balayage → arbitrage → migration gardée) :
[`docs/runbooks/invoice-number-uniqueness.md`](runbooks/invoice-number-uniqueness.md)

Balayage : `GET /api/platform/migrations/tenants/invoice-number-integrity`
(PlatformAdmin) ou [`docs/runbooks/sql/ScanInvoiceNumberDuplicates_AllTenants.sql`](runbooks/sql/ScanInvoiceNumberDuplicates_AllTenants.sql).

---

## Configuration

```json
"TenantMigrations": {
  "ApplyOnStartup": true,
  "ApplyOnlyToMissingMigrations": true
}
```

Quand `ApplyOnlyToMissingMigrations` est `true`, seuls les tenants avec **au moins une migration en attente** sont traités au démarrage (pas les tenants déjà à jour).

---

## En résumé

- En **développement**, corriger l'erreur de migration puis redémarrer l'API.
- En **production**, utiliser le backoffice plateforme ou un script de déploiement qui applique les migrations tenant avant le trafic utilisateur.
