# Unicité des numéros de facture de vente (`Invoices.Number`)

## Contexte

L'exigence fiscale tunisienne impose l'**unicité du numéro de facture de vente**.
Côté fournisseurs, `SupplierInvoices.InvoiceNumber` est déjà unique (index EF,
`TenantDbContext`). Côté ventes, la migration `20260717125815_AddInvoiceSearchAndAuditIndexes_Tenant`
n'a créé qu'un index **non unique** `IX_Invoices_Number`, volontairement : une
migration créant un index UNIQUE sur une base contenant des doublons **échoue**,
et tout échec de migration tenant **bloque le tenant** au boot via
`TenantMigrationGuard` (HTTP 503 `TENANT_MIGRATION_FAILED` sur toutes les API).

La bascule vers l'index unique suit donc une procédure en trois étapes,
**dans cet ordre, sans raccourci** :

## Étape 1 — Balayage de tous les tenants

Deux chemins équivalents :

**Endpoint plateforme** (compte PlatformAdmin) :

```http
GET /api/platform/migrations/tenants/invoice-number-integrity
Authorization: Bearer <token>
```

Le rapport liste chaque tenant (`Clean` / `DuplicatesFound` / `Unreachable`),
le détail des factures en doublon, et un verdict consolidé `IsUniqueIndexSafe`.
**Fail-closed** : un tenant injoignable rend le verdict négatif — il n'est
jamais compté comme propre.

**Script DBA** (instance SQL, lecture seule) :
[`docs/runbooks/sql/ScanInvoiceNumberDuplicates_AllTenants.sql`](sql/ScanInvoiceNumberDuplicates_AllTenants.sql)

## Étape 2 — Arbitrage des doublons

Si le balayage remonte des doublons, **aucune migration ne doit être générée ni
déployée**. Produire le rapport (le JSON de l'endpoint ou la sortie du script)
et arbitrer cas par cas :

- Une facture **payée/signée** ne se supprime jamais (piste d'audit) ; la
  correction passe par une **renumérotation** de la facture la plus récente vers
  le prochain numéro libre de la séquence, tracée dans `AuditLogs`, ou par un
  avoir + refacturation selon la position du cabinet comptable.
- Les paires observées jusqu'ici relèvent de deux causes : un **rembobinage de
  séquence** (numéros ré-émis des semaines plus tard) et une **course à la
  réservation** (deux factures créées à moins d'une minute d'écart avec le même
  numéro, antérieures au verrouillage actuel de `DocumentNumberService`).

Balayage de référence (LocalDB dev, 2026-07-18) : 79 bases scannées,
226 factures, **3 bases avec doublons, 23 numéros, 46 factures concernées**
(majoritairement au statut Payée) → migration **non générée**, arbitrage requis.

## Étape 3 — Migration index UNIQUE (seulement après un balayage 100 % propre)

Quand — et seulement quand — le balayage renvoie `IsUniqueIndexSafe = true` sur
**tous** les tenants (prod incluse) :

1. Dans `TenantDbContext` (config `Invoice.Number`), remplacer l'index
   non unique par :

   ```csharp
   num.HasIndex(n => n.Value)
       .IsUnique()
       .HasDatabaseName("UX_Invoices_Number")
       .HasFilter("[Number] IS NOT NULL");
   ```

2. Générer la migration tenant (`AddUniqueInvoiceNumberIndex_Tenant`) puis
   **remplacer le corps scaffoldé** par le SQL gardé ci-dessous : la migration
   ne doit **jamais** pouvoir échouer au boot d'un tenant, même si un doublon
   est apparu entre le balayage et le déploiement.

   ```sql
   -- Up() — idempotent, ne lève JAMAIS d'erreur :
   IF EXISTS (SELECT 1 FROM [dbo].[Invoices] GROUP BY [Number] HAVING COUNT(*) > 1)
   BEGIN
       -- Doublons apparus depuis le balayage : on conserve l'index non unique.
       -- Le tenant reste visible comme non conforme via l'endpoint
       -- invoice-number-integrity (HasUniqueIndex = false) → nouvel arbitrage.
       PRINT 'FactuTrust: doublons Invoices.Number détectés — index unique UX_Invoices_Number NON créé.';
   END
   ELSE
   BEGIN
       IF NOT EXISTS (SELECT 1 FROM sys.indexes
                      WHERE name = N'UX_Invoices_Number'
                        AND object_id = OBJECT_ID(N'[dbo].[Invoices]'))
           CREATE UNIQUE NONCLUSTERED INDEX [UX_Invoices_Number]
               ON [dbo].[Invoices] ([Number])
               WHERE [Number] IS NOT NULL;

       IF EXISTS (SELECT 1 FROM sys.indexes
                  WHERE name = N'IX_Invoices_Number'
                    AND object_id = OBJECT_ID(N'[dbo].[Invoices]'))
           DROP INDEX [IX_Invoices_Number] ON [dbo].[Invoices];
   END
   ```

3. Après déploiement, relancer le balayage : tout tenant avec
   `HasUniqueIndex = false` a été sauté par la garde et doit repasser par
   l'étape 2 (le script idempotent du runbook pourra ensuite recréer l'index).

> ⚠️ **Ne pas exécuter le SQL ci-dessus tant que l'étape 1 n'est pas 100 %
> propre.** Il est reproduit ici uniquement pour que la migration du jour J
> parte d'un modèle validé.
