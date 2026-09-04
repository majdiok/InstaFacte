# Migration du plan comptable vers la NCT 01 (`nct01-v1`)

Le plan actuel (hybride PCG français, ~215 comptes) est remplacé au **boot tenant** par le catalogue NCT 01 + overlay métier. Les écritures stockent un **numéro string** : toutes les colonnes `*AccountNumber*` sont réécrites, y compris sur les périodes clôturées.

> **Voir aussi — renumérotation des comptes trop longs.** Un second passage,
> `ChartAccountDigitCompactionService`, s'exécute juste après ce remap au boot tenant : il ramène
> sous 8 chiffres les comptes qui les dépassent (auxiliaires salariés hérités du matricule, en
> 10 chiffres). Il partage les primitives de réécriture de ce remap mais garde son **propre**
> inventaire de colonnes, plus complet de six paires, et sa propre allow-list anti-injection.
> Contrairement à ce remap, il est **réversible** — sa carte est injective vers des numéros libres,
> et `ChartOfAccountCompactionLogs` en conserve l'inverse. Pré-contrôle :
> `docs/runbooks/sql/CompactOverlongAccountNumbers.readonly.sql`.

Le remap n’est **pas idempotent** sur les cycles (`421↔425`, `231↔232`). L’idempotence repose uniquement sur `ChartOfAccountRemapLogs.MapVersion = nct01-v1`.

## Pré-checks

1. **Backup** de chaque base tenant (restore = rollback sûr).
2. Compter les lignes avant migration :

```sql
SELECT '4477' AS N, COUNT(*) FROM JournalEntryLines WHERE AccountNumber LIKE N'4477%'
UNION ALL SELECT '421', COUNT(*) FROM JournalEntryLines WHERE AccountNumber LIKE N'421%'
UNION ALL SELECT '211', COUNT(*) FROM JournalEntryLines WHERE AccountNumber LIKE N'211%'
UNION ALL SELECT '412', COUNT(*) FROM JournalEntryLines WHERE AccountNumber LIKE N'412%'
UNION ALL SELECT 'coa', COUNT(*) FROM ChartOfAccounts;
```

3. Noter le FODEC du mois en cours (somme `Invoices.FodecAmount` vs journal `4477`).
4. Vérifier que la balance générale est équilibrée (total débit = total crédit).

## Déploiement

1. Déployer l’API (JSON embarqués `nct01-coa-catalog.json` / `coa-remap-v1.json`).
2. Au premier request tenant, `TenantMigrationGuard` applique :
   - la migration EF `20260817140000_MigrateChartOfAccountsToNct01_Tenant` (table `ChartOfAccountRemapLogs`) ;
   - puis `Nct01ChartMigrationService` **avant** le seed des comptes de RS.
3. Un échec du remap bloque le tenant (HTTP 503), comme les autres migrations.

## Post-checks

```sql
-- Idempotence posée
SELECT MapVersion, AppliedAt, AccountCount FROM ChartOfAccountRemapLogs WHERE MapVersion = N'nct01-v1';

-- FODEC overlay
SELECT AccountNumber, Label, IsSystem, IsActive, ParentAccountNumber
FROM ChartOfAccounts WHERE AccountNumber IN (N'43652', N'4371', N'4320', N'228', N'425');

-- Plus aucune ligne sur les anciens numéros opérationnels
SELECT AccountNumber, COUNT(*) FROM JournalEntryLines
WHERE AccountNumber IN (N'4477', N'4478', N'6371', N'412')
   OR AccountNumber LIKE N'4456%'
GROUP BY AccountNumber;

-- Balance toujours équilibrée
SELECT SUM(DebitAmount), SUM(CreditAmount) FROM JournalEntryLines;
```

Contrôles métier :

- `43652` existe, `IsSystem = 1`, parent `4365`.
- Aucune ligne `4477` ; le FODEC du mois = somme des factures (agrégat document, pas le n° de compte).
- Paie : net sur `425` / `425xxxx`, avances `421`, État `432`, CNSS `453`. TFP/FOPROLOS restent agrégés `647`+`432` (pas `6611`/`6612`).
- Terrains : soldes sous `221*` dans la liasse corporelle, pas sous `21` (incorporelles NCT).

## Rollback

- **Restore DB** : méthode par défaut dès qu’une écriture NCT-only a été saisie après le remap (`43652`, `228`, `425xxxx`, etc.).
- **Carte inverse** (`coa-remap-v1.json` inversé) : uniquement si **aucune** nouvelle pièce n’a été comptabilisée sur des numéros NCT-only. Les cycles `421↔425` et `231↔232` rendent un second passage de `Rewrite` destructeur. Ne jamais relancer le service sans vider `ChartOfAccountRemapLogs` **et** sans restore.

Pour forcer un rejeu après restore :

```sql
DELETE FROM ChartOfAccountRemapLogs WHERE MapVersion = N'nct01-v1';
```

Puis redémarrer l’API / invalider le cache `TenantMigrationGuard`.

## Hors périmètre

- Ne pas poster TFP/FOPROLOS sur `6611`/`6612`.
- Ne pas réécrire la seed historique `20260326233329_AddAccountingModule_Tenant`.
- Ne pas modifier les journaux `JV` / `JA` / `JB` / `JC` / `JOD` / `JAN`.
