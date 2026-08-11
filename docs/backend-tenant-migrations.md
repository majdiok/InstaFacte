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

## Erreur « Invalid column name 'ApplyCnssCeilingToPayrollTaxes' » sur la fiche salarié / paramètres paie

Si la fiche salarié (`/payroll/employees/{id}`) ou les paramètres paie affichent une erreur HTTP **500** sur `GET /api/payroll/settings/parameters/{year}` :

- **Message :** `Invalid column name 'ApplyCnssCeilingToPayrollTaxes'.`
- **Cause :** la migration tenant `20260810120000_AddPayrollTaxBase_Tenant` n'a pas été appliquée sur la base du tenant alors que le code backend interroge déjà cette colonne sur `PayrollYearParameters`.

### Solution

Appliquer les migrations tenant via l'une des options de la section [Erreur HTTP 503](#erreur-http-503--tenant_migration_failed).

**Script idempotent (production / DBA) :** [`docs/runbooks/sql/AddPayrollTaxBase_Tenant.idempotent.sql`](runbooks/sql/AddPayrollTaxBase_Tenant.idempotent.sql)

**Vérification SQL :**

```sql
SELECT MigrationId FROM __EFMigrationsHistory
WHERE MigrationId LIKE '%AddPayrollTaxBase%';

SELECT COL_LENGTH('dbo.PayrollYearParameters', 'ApplyCnssCeilingToPayrollTaxes') AS ColumnExists;
```

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

## Vague 0 « Ventes & Distribution » — 5 migrations tenant (27/07/2026)

Cinq migrations **strictement additives**, livrées ensemble sur la branche
`fix/vague0-ventes-distribution`. Chacune possède un `Down()` complet et un script SQL
idempotent, et aucune ne modifie de document déjà émis.

| Migration | Objet | Script idempotent |
|---|---|---|
| `20260727100000_AddInvoiceSourceDeliveryNote_Tenant` | `Invoices.SourceDeliveryNoteId` — fin de la double déduction de stock. Backfill exhaustif depuis `DeliveryNotes.InvoiceId`. | [AddInvoiceSourceDeliveryNote_Tenant.idempotent.sql](runbooks/sql/AddInvoiceSourceDeliveryNote_Tenant.idempotent.sql) |
| `20260727110000_AddDeliveryNoteLineDiscountFodec_Tenant` | Remise et FODEC sur les lignes de BL. Aucun backfill : les BL existants gardent leurs totaux. | [AddDeliveryNoteLineDiscountFodec_Tenant.idempotent.sql](runbooks/sql/AddDeliveryNoteLineDiscountFodec_Tenant.idempotent.sql) |
| `20260727120000_AddQuoteFodecAndFiscalStamp_Tenant` | FODEC + timbre sur devis. **Aucun recalcul rétroactif.** Normalise aussi à `'TND'` les devises FODEC laissées vides par `20260713171729`. | [AddQuoteFodecAndFiscalStamp_Tenant.idempotent.sql](runbooks/sql/AddQuoteFodecAndFiscalStamp_Tenant.idempotent.sql) |
| `20260727130000_AddInvoiceLinkedInvoice_Tenant` | `Invoices.LinkedInvoiceId` — lien avoir → facture d'origine. Backfill best-effort depuis les brouillons convertis. | [AddInvoiceLinkedInvoice_Tenant.idempotent.sql](runbooks/sql/AddInvoiceLinkedInvoice_Tenant.idempotent.sql) |
| `20260727140000_AddStockMovementShortfall_Tenant` | `StockMovements.ShortfallQuantity` — traçabilité des ruptures. Comportement fonctionnel inchangé. | [AddStockMovementShortfall_Tenant.idempotent.sql](runbooks/sql/AddStockMovementShortfall_Tenant.idempotent.sql) |

### Contrôle à exécuter AVANT le déploiement

La validation de facture applique désormais les règles de conformité fiscale sur **tous** les
chemins (création directe, conversion devis, conversion BL), et plus seulement dans l'assistant.
Les factures déjà validées ne sont pas concernées — `Validate()` exige `Status = Draft`. Seuls
des **brouillons** existants pourraient devenir non validables.

Exécuter sur chaque base tenant, en lecture seule :
[`docs/runbooks/sql/CheckDraftInvoiceCompliance_Tenant.sql`](runbooks/sql/CheckDraftInvoiceCompliance_Tenant.sql)

Un résultat vide signifie qu'aucun brouillon existant ne sera bloqué. Sinon, la colonne
`MotifBlocage` indique la conduite à tenir pour chaque ligne.

---

## Erreur « Invalid column name 'CreatedBy' / 'UpdatedBy' / 'Version' » sur devis ou tarification

Si **Nouveau devis** (`POST /api/quotes`) ou les écrans pricing (`/api/pricing/price-lists`,
`promotions`, `payment-terms`) renvoient HTTP **500** avec :

- **Message :** `Invalid column name 'CreatedBy'.` / `UpdatedBy` / `Version`
- **Cause :** les tables `Promotions`, `PriceLists`, `PaymentTermTemplates`, etc. ont été créées
  sans les colonnes d'audit de `Entity` / `AggregateRoot`, alors que EF les mappe. La création de
  devis échoue dans `PromotionResolver` en lisant `Promotions`.

### Solution

Appliquer la migration tenant `20260731180000_AddPricingAuditColumns_Tenant` (redémarrage API en
dev, ou backoffice plateforme), ou le script :

[`docs/runbooks/sql/AddPricingAuditColumns_Tenant.idempotent.sql`](runbooks/sql/AddPricingAuditColumns_Tenant.idempotent.sql)

---

## Vague 1 « Socle ERP commercial » — migrations tenant

Migrations **strictement additives** de la vague 1 (commande client, conversions, tarification,
régimes TVA, code-barres). Chacune possède un `Down()` complet et un script SQL idempotent, et
aucune ne modifie de document déjà émis.

| Migration | Objet | Script idempotent |
|---|---|---|
| `20260728120000_AddSalesOrders_Tenant` | Tables `SalesOrders` / `SalesOrderLines` — l'agrégat commande client (numérotation `CDE`). Création de tables uniquement. | [AddSalesOrders_Tenant.idempotent.sql](runbooks/sql/AddSalesOrders_Tenant.idempotent.sql) |
| `20260729100000_AddProductBarcode_Tenant` | `Products.Barcode` (EAN-8/13) + index filtré **non unique**. L'unicité par tenant n'est livrée qu'après balayage des doublons. | [AddProductBarcode_Tenant.idempotent.sql](runbooks/sql/AddProductBarcode_Tenant.idempotent.sql) |
| `20260729140000_AddClientVatRegime_Tenant` | `Clients.VatRegime` (défaut 0 = Normal) + attestation de suspension (3 colonnes nullables) + index filtré. Les clients existants gardent leur comportement de facturation. | [AddClientVatRegime_Tenant.idempotent.sql](runbooks/sql/AddClientVatRegime_Tenant.idempotent.sql) |
| `20260730100000_AddPricing_Tenant` | Tables `PriceLists` / `PriceListItems` / `ClientProductPrices` (tarification, tranche 5A) + colonne `Clients.PriceListId` nullable + index. Création de tables + colonne additive. | [AddPricing_Tenant.idempotent.sql](runbooks/sql/AddPricing_Tenant.idempotent.sql) |
| `20260730160000_AddPriceListItemTiers_Tenant` | Table `PriceListItemTiers` — paliers quantitatifs (tranche 5B). Création de table uniquement ; les prix existants restent des prix de base sans palier. | [AddPriceListItemTiers_Tenant.idempotent.sql](runbooks/sql/AddPriceListItemTiers_Tenant.idempotent.sql) |
| `20260730180000_AddGlobalDiscount_Tenant` | Remise de pied (tranche 5B) : `GlobalDiscountPercent` / `GlobalDiscountAmount` sur `Invoices`, `Quotes`, `SalesOrders`, et `AllocatedGlobalDiscount` sur les trois tables de lignes. Montants à 0 par défaut ⇒ calcul inchangé sur l'existant. | [AddGlobalDiscount_Tenant.idempotent.sql](runbooks/sql/AddGlobalDiscount_Tenant.idempotent.sql) |
| `20260730200000_AddPromotionsAndPaymentTerms_Tenant` | Tables `Promotions` et `PaymentTermTemplates` (tranche 5C). Creation de tables uniquement ; le champ texte `PaymentTerms` des documents n'est pas touche. | [AddPromotionsAndPaymentTerms_Tenant.idempotent.sql](runbooks/sql/AddPromotionsAndPaymentTerms_Tenant.idempotent.sql) |
| `20260730220000_AddClientCreditTerms_Tenant` | `Clients.CreditLimit` et `Clients.DefaultPaymentTermDays` (lot 6). Deux colonnes nullables ; le plafond alimente une alerte et ne bloque rien. | [AddClientCreditTerms_Tenant.idempotent.sql](runbooks/sql/AddClientCreditTerms_Tenant.idempotent.sql) |
| `20260731180000_AddPricingAuditColumns_Tenant` | Colonnes d'audit manquantes (`CreatedBy` / `UpdatedBy` / `Version`) sur les tables pricing. Correctif du schéma livré par les migrations 5A–5C. | [AddPricingAuditColumns_Tenant.idempotent.sql](runbooks/sql/AddPricingAuditColumns_Tenant.idempotent.sql) |

Le régime de TVA du client est un attribut du **client**, non du taux de ligne (`VatRate`
inchangé). La validation de facture (`ValidateInvoiceCommand`) refuse désormais toute TVA pour un
client dont le régime la supprime (exonéré, suspension, export) et exige, pour une suspension, une
attestation en cours de validité **à la date d'émission** de la facture. Aucun client existant
n'est concerné : ils sont tous en régime `Normal` par défaut.

La tarification (tranche 5A) introduit un **point de résolution unique** — `IPriceResolver`
(`Infrastructure/Services/Pricing/PriceResolver.cs`) — qui répond au prix HT selon la priorité
**prix négocié client → grille affectée au client → prix catalogue**. Ces tables n'alimentent que
la résolution au moment de créer une ligne : le prix reste **figé** sur les documents émis, aucun
document existant ne change de prix quand une grille bouge. Les grilles et prix négociés bornés
dans le temps (validité, activation) ne sont consultés que s'ils sont applicables à la date du
document. Sans affectation ni prix négocié, le comportement est identique à aujourd'hui (catalogue).

**Remise de pied de document (tranche 5B) — décision de traitement fiscal.** La remise réduit la
base de TVA **et** le FODEC, parce qu'elle réduit le montant HT réellement facturé : le client ne
doit pas payer de TVA sur ce qu'il ne règle pas. Techniquement, elle est **répartie sur les lignes
au prorata de leur base HT** (`GlobalDiscountAllocator`), et non soustraite du total : chaque ligne
recalcule alors son FODEC et sa TVA sur sa base réduite, si bien que la ventilation par taux reste
juste même avec des taux mêlés (7 / 13 / 19 %), et que la comptabilité, le PDF et l'export fiscal
continuent de fonctionner sans modification. Le **timbre fiscal n'est pas touché** : c'est un droit
fixe, appliqué après la remise. La somme des parts imputées vaut exactement la remise annoncée, le
résidu d'arrondi étant donné à la ligne de plus forte base.

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

## Module Honoraires cabinet (01/08/2026)

Migration tenant **additive** `20260801120000_AddHonorairesModule_Tenant` : tables
`HonorairesInvoices` / `HonorairesInvoiceLines` / `HonorairesQuotes` /
`HonorairesQuoteLines` / `HonorairesPayments` / `HonorairesAttachments`.

Aucun schéma Sales (`Invoices`, `Quotes`, `Payments`) n’est modifié.

### Solution

Appliquer les migrations tenant via l’une des options de la section [Erreur HTTP 503](#erreur-http-503--tenant_migration_failed), ou le script :

[`docs/runbooks/sql/AddHonorairesModule_Tenant.idempotent.sql`](runbooks/sql/AddHonorairesModule_Tenant.idempotent.sql)

**Après déploiement :** les utilisateurs firm doivent se **reconnecter** pour recevoir le module JWT `Honoraires` et les permissions `honoraires.*`.

**Vérification SQL :**

```sql
SELECT MigrationId FROM __EFMigrationsHistory
WHERE MigrationId LIKE '%AddHonorairesModule%';

SELECT OBJECT_ID('HonorairesInvoices') AS Invoices,
       OBJECT_ID('HonorairesQuotes') AS Quotes,
       OBJECT_ID('HonorairesPayments') AS Payments,
       OBJECT_ID('HonorairesAttachments') AS Attachments;
```

---

## En résumé

- En **développement**, corriger l'erreur de migration puis redémarrer l'API.
- En **production**, utiliser le backoffice plateforme ou un script de déploiement qui applique les migrations tenant avant le trafic utilisateur.
