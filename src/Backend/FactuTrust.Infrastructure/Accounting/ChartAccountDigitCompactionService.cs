using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using FactuTrust.Domain.Services.Accounting;
using FactuTrust.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FactuTrust.Infrastructure.Accounting;

/// <summary>Un compte trop long et le compte conforme qui le remplace.</summary>
public sealed record ChartAccountCompactionMapping(
    string From,
    string To,
    int DigitsBefore,
    string Root,
    string? EmployeeName);

/// <summary>Plan de renumérotation d'un dossier : ce qui bougerait, et ce qui l'en empêche.</summary>
public sealed record ChartAccountCompactionPlan(
    IReadOnlyList<ChartAccountCompactionMapping> Mappings,
    IReadOnlyList<string> BlockingIssues,
    string PlanHash)
{
    public bool IsNoOp => Mappings.Count == 0 && BlockingIssues.Count == 0;
    public bool CanApply => Mappings.Count > 0 && BlockingIssues.Count == 0;
}

/// <summary>Résultat d'une passe de compaction.</summary>
public sealed record ChartAccountCompactionResult(bool Applied, int MappingCount, string PlanHash);

/// <summary>
/// Renumérote les comptes du plan comptable dont le numéro dépasse
/// <see cref="AccountNumberRules.MaxDigits"/> chiffres, et propage le nouveau numéro à toutes les
/// colonnes qui le référencent.
/// </summary>
/// <remarks>
/// <para>
/// Les comptes auxiliaires salariés hérités valent <c>425</c> + les 7 derniers chiffres du
/// matricule, soit 10 chiffres (<c>4259655554</c>). Le générateur est corrigé par ailleurs ; ce
/// service rattrape l'existant, y compris sur des écritures validées ou clôturées — décision
/// explicite du dossier, dans la continuité du remap NCT 01 qui procède déjà ainsi
/// (<c>docs/runbooks/coa-nct01-migration.md</c>).
/// </para>
/// <para>
/// <b>La réécriture ne touche que des identifiants.</b> Aucun montant, aucune date, aucun statut,
/// aucune période n'est modifié — la post-condition <c>C2</c> vérifie que la balance générale est
/// identique au centime avant de valider la transaction.
/// </para>
/// <para>
/// <b>Idempotence par la donnée, pas par un jeton de version.</b> Le service ne fait rien dès qu'il
/// ne reste aucun compte non conforme. Un jeton « déjà appliqué » supprimerait à tort une deuxième
/// passe si un compte trop long réapparaissait — restauration d'une sauvegarde antérieure, par
/// exemple. La table <c>ChartOfAccountCompactionLogs</c> n'est donc pas la condition de garde :
/// c'est la piste d'audit (« où est passé 4259655554 ? ») et le garant d'injectivité entre passes,
/// via son index unique sur le numéro cible.
/// </para>
/// </remarks>
public static class ChartAccountDigitCompactionService
{
    public const string MapVersion = "coa-digit-compaction-v1";

    private const string LogTable = "ChartOfAccountCompactionLogs";

    /// <summary>Largeur initiale du suffixe, alignée sur <c>PayrollEmployeeChartProvisioningService</c>.</summary>
    private const int InitialSuffixLength = 4;

    /// <summary>
    /// Colonnes portant un numéro de compte SCE.
    /// </summary>
    /// <remarks>
    /// Volontairement distincte de <see cref="Nct01ChartMigrationService.DistinctValuesAllowList"/> :
    /// élargir l'une ne doit jamais élargir la surface SQL brute de l'autre. Six paires manquaient à
    /// la liste NCT 01, ajoutées à des entités plus récentes que le remap — dont
    /// <c>Employees.AuxiliaryAccountNumber</c>, qui aurait laissé la fiche salarié pointer un compte
    /// disparu.
    /// </remarks>
    internal static readonly IReadOnlySet<(string Table, string Column)> ReferencingColumns =
        new HashSet<(string Table, string Column)>
        {
            ("JournalEntryLines", "AccountNumber"),
            ("LetteringGroups", "AccountNumber"),
            ("JournalEntryTemplateLines", "AccountNumber"),
            ("ThirdPartyAccountingProfiles", "CollectiveAccountNumber"),
            ("BankAccounts", "ChartOfAccountNumber"),
            ("BankStatements", "ChartOfAccountNumber"),
            ("FixedAssets", "AssetAccountNumber"),
            ("FixedAssets", "DepreciationAccountNumber"),
            ("FixedAssets", "ExpenseAccountNumber"),
            ("FixedAssets", "CreditAccountNumber"),
            ("FixedAssets", "DisposalTreasuryAccount"),
            ("FixedAssets", "DisposalReceivableAccount"),
            ("DepreciationRateCategories", "DefaultAssetAccount"),
            ("DepreciationRateCategories", "DefaultDepreciationAccount"),
            ("DepreciationRateCategories", "DefaultExpenseAccount"),
            ("SupplierInvoiceLines", "AssetAccountNumber"),
            ("Loans", "LoanAccountNumber"),
            ("Loans", "InterestAccountNumber"),
            ("Loans", "BankAccountNumber"),
            ("Payslips", "EmployeeAuxiliaryAccount"),
            ("PayslipLines", "AccountSce"),
            ("PayrollPaymentLines", "EmployeeAuxiliaryAccount"),
            ("SocialFundSchemes", "EmployeeAccountSce"),
            ("SocialFundSchemes", "EmployerAccountSce"),
            ("PayrollAccountingSettings", "InKindOffsetAccount"),
            ("Employees", "AuxiliaryAccountNumber"),
            ("AccountingAnomalies", "AccountRef"),
            ("AccountingAnomalyLines", "AccountNumber"),
        };

    /// <summary>
    /// Sonde bon marché : reste-t-il un compte de plus de
    /// <see cref="AccountNumberRules.MaxDigits"/> chiffres ?
    /// </summary>
    /// <remarks>
    /// SQL ne sait pas exprimer « nombre de chiffres » sans supposer que le point est le seul
    /// séparateur. On pré-filtre donc sur <c>LEN &gt; MaxDigits</c> — condition nécessaire, jamais
    /// manquante, puisqu'un numéro de plus de 8 chiffres fait forcément plus de 8 caractères — et la
    /// décision exacte revient à <see cref="AccountNumberRules.DigitCount"/>, côté C#.
    /// </remarks>
    public static async Task<bool> HasNonCompliantAccountsAsync(
        TenantDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await ChartRewritePrimitives.TableExistsAsync(db, "ChartOfAccounts", cancellationToken))
            return false;

        var candidates = await LoadCandidateNumbersAsync(db, cancellationToken);
        return candidates.Count > 0;
    }

    /// <summary>
    /// Construit le plan sans rien écrire ni ouvrir de transaction — utilisable tel quel en
    /// production comme pré-contrôle.
    /// </summary>
    public static async Task<ChartAccountCompactionPlan> BuildPlanAsync(
        TenantDbContext db, CancellationToken cancellationToken = default)
    {
        var issues = new List<string>();
        var mappings = new List<ChartAccountCompactionMapping>();

        if (!await ChartRewritePrimitives.TableExistsAsync(db, "ChartOfAccounts", cancellationToken))
            return new ChartAccountCompactionPlan(mappings, issues, ComputeHash(mappings));

        var all = await db.ChartOfAccounts.AsNoTracking()
            .Select(a => new { a.AccountNumber, a.IsSystem })
            .ToListAsync(cancellationToken);

        var everyNumber = all.Select(a => a.AccountNumber).ToHashSet(StringComparer.Ordinal);
        var systemNumbers = all.Where(a => a.IsSystem).Select(a => a.AccountNumber)
            .ToHashSet(StringComparer.Ordinal);

        var sources = all
            .Select(a => a.AccountNumber)
            .Where(n => AccountNumberRules.DigitCount(n) > AccountNumberRules.MaxDigits)
            // Ordre ordinal et non CreatedAt/Id : c'est la seule clé stable entre le plan à blanc et
            // l'application, sur une valeur par ailleurs unique par construction.
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        if (sources.Count == 0)
            return new ChartAccountCompactionPlan(mappings, issues, ComputeHash(mappings));

        var names = await LoadEmployeeNamesAsync(db, sources, cancellationToken);

        // Le vivier de cibles exclut TOUS les numéros existants, y compris les sources : la carte
        // n'est donc jamais une permutation, et aucune réécriture ne peut entrer en collision, même
        // transitoirement.
        var taken = new HashSet<string>(everyNumber, StringComparer.Ordinal);
        var previousTargets = await LoadPreviousTargetsAsync(db, cancellationToken);
        taken.UnionWith(previousTargets);

        foreach (var source in sources)
        {
            // P2 — un compte système trop long signale un plan corrompu : c'est un humain qu'il faut.
            if (systemNumbers.Contains(source))
            {
                issues.Add($"Le compte {source} est marqué système : renumérotation refusée, "
                           + "le catalogue NCT 01 ne contient aucun numéro de cette longueur.");
                continue;
            }

            // P1 — renuméroter un compte qui a des enfants briserait l'invariant « le numéro commence
            // par le compte parent » (ChartOfAccount.Create) pour chacun d'eux.
            var children = everyNumber
                .Where(n => n.Length > source.Length && n.StartsWith(source, StringComparison.Ordinal))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            if (children.Count > 0)
            {
                issues.Add($"Le compte {source} a {children.Count} sous-compte(s) (dont "
                           + $"{children[0]}) : renumérotation refusée, ils cesseraient de commencer "
                           + "par leur compte parent.");
                continue;
            }

            // P6 — un numéro déjà compacté qui réapparaît : ne pas le compacter une seconde fois en
            // silence, l'ancienne correspondance deviendrait ambiguë.
            if (previousTargets.Count > 0 && await WasAlreadyCompactedAsync(db, source, cancellationToken))
            {
                issues.Add($"Le compte {source} figure déjà comme source dans {LogTable} : "
                           + "il a été renuméroté puis est réapparu. Intervention manuelle requise.");
                continue;
            }

            var root = ResolveRoot(source, everyNumber);
            if (root is null)
            {
                issues.Add($"Aucun compte parent conforme sous {source} : impossible de déterminer "
                           + "une racine de renumérotation.");
                continue;
            }

            var target = AllocateTarget(root, taken);
            if (target is null)
            {
                issues.Add($"Plus aucun numéro libre sous {root} dans la limite de "
                           + $"{AccountNumberRules.MaxDigits} chiffres.");
                continue;
            }

            // P5 — contrôle de ceinture : les deux colonnes EmployeeAuxiliaryAccount sont en
            // nvarchar(10). Une cible fait au plus 8 caractères, mais on l'affirme plutôt que de s'y fier.
            if (target.Length > 10)
            {
                issues.Add($"La cible {target} dépasse 10 caractères : elle ne tiendrait pas dans "
                           + "Payslips.EmployeeAuxiliaryAccount.");
                continue;
            }

            taken.Add(target);
            names.TryGetValue(source, out var employeeName);
            mappings.Add(new ChartAccountCompactionMapping(
                source, target, AccountNumberRules.DigitCount(source), root, employeeName));
        }

        return new ChartAccountCompactionPlan(mappings, issues, ComputeHash(mappings));
    }

    /// <summary>
    /// Applique la renumérotation dans une transaction unique. Sans compte non conforme, ne fait
    /// rien et n'ouvre aucune transaction.
    /// </summary>
    /// <param name="expectedPlanHash">
    /// Empreinte du plan validé par l'opérateur. Fournie, elle est recalculée sous verrou et toute
    /// divergence annule l'opération : ce qui a été relu est ce qui s'exécute.
    /// </param>
    public static async Task<ChartAccountCompactionResult> EnsureCompactedAsync(
        TenantDbContext db,
        string? expectedPlanHash = null,
        string appliedBy = "system",
        CancellationToken cancellationToken = default)
    {
        if (!await ChartRewritePrimitives.TableExistsAsync(db, "ChartOfAccounts", cancellationToken))
            return new ChartAccountCompactionResult(false, 0, ComputeHash([]));

        if (!await HasNonCompliantAccountsAsync(db, cancellationToken))
            return new ChartAccountCompactionResult(false, 0, ComputeHash([]));

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireApplockAsync(db, cancellationToken);
            await EnsureLogTableAsync(db, cancellationToken);

            // Re-sonde sous verrou : une autre instance a pu compacter entre-temps.
            if (!await HasNonCompliantAccountsAsync(db, cancellationToken))
            {
                await tx.CommitAsync(cancellationToken);
                return new ChartAccountCompactionResult(false, 0, ComputeHash([]));
            }

            var plan = await BuildPlanAsync(db, cancellationToken);

            if (plan.BlockingIssues.Count > 0)
            {
                throw new InvalidOperationException(
                    "Renumérotation des comptes trop longs impossible : "
                    + string.Join(" | ", plan.BlockingIssues));
            }

            if (expectedPlanHash is not null
                && !string.Equals(expectedPlanHash, plan.PlanHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Le plan de renumérotation a changé depuis sa relecture "
                    + $"(attendu {expectedPlanHash}, calculé {plan.PlanHash}) : opération annulée.");
            }

            var (debitBefore, creditBefore) = await ReadTrialBalanceAsync(db, cancellationToken);
            var linesBefore = await CountLinesByAccountAsync(db, plan.Mappings.Select(m => m.From), cancellationToken);

            var batchId = Guid.NewGuid();
            var appliedAt = DateTime.UtcNow;

            // Les index uniques sur FromAccountNumber et ToAccountNumber tranchent ici, AVANT que la
            // moindre donnée ne bouge : deux salariés ne peuvent pas se retrouver sur un même compte.
            await InsertLogRowsAsync(db, plan.Mappings, batchId, appliedAt, appliedBy, cancellationToken);

            var map = plan.Mappings.ToDictionary(m => m.From, m => m.To, StringComparer.Ordinal);
            string Rewrite(string value) => map.TryGetValue(value, out var to) ? to : value;

            foreach (var (table, column) in ReferencingColumns.OrderBy(c => c.Table, StringComparer.Ordinal)
                         .ThenBy(c => c.Column, StringComparer.Ordinal))
            {
                if (!await ChartRewritePrimitives.TableExistsAsync(db, table, cancellationToken)
                    || !await ChartRewritePrimitives.ColumnExistsAsync(db, table, column, cancellationToken))
                {
                    continue;
                }

                var originals = await ChartRewritePrimitives.DistinctValuesAsync(
                    db, table, column, ReferencingColumns, nameof(ChartAccountDigitCompactionService), cancellationToken);
                await ChartRewritePrimitives.CaseRewriteAsync(db, table, column, originals, Rewrite, cancellationToken);
            }

            var parents = await db.ChartOfAccounts.AsNoTracking()
                .Select(a => a.ParentAccountNumber).ToListAsync(cancellationToken);
            var affectations = await db.ChartOfAccounts.AsNoTracking()
                .Select(a => a.AffectationAccountNumber).ToListAsync(cancellationToken);
            var numbers = await db.ChartOfAccounts.AsNoTracking()
                .Select(a => a.AccountNumber).ToListAsync(cancellationToken);

            await ChartRewritePrimitives.CaseRewriteAsync(
                db, "ChartOfAccounts", "ParentAccountNumber", parents, Rewrite, cancellationToken);
            await ChartRewritePrimitives.CaseRewriteAsync(
                db, "ChartOfAccounts", "AffectationAccountNumber", affectations, Rewrite, cancellationToken);
            await ChartRewritePrimitives.TwoPhaseUniqueRewriteAsync(
                db, "ChartOfAccounts", "AccountNumber", numbers, Rewrite, cancellationToken);

            await ChartRewritePrimitives.RecomputeLevelAndClassAsync(db, cancellationToken);
            db.ChangeTracker.Clear();

            await StampAuditAsync(db, plan.Mappings.Select(m => m.To).ToList(), appliedAt, cancellationToken);
            await ChartRewritePrimitives.NormalizeAutoGeneratedLabelsAsync(db, cancellationToken);

            await AssertPostConditionsAsync(db, plan, debitBefore, creditBefore, linesBefore, cancellationToken);

            await tx.CommitAsync(cancellationToken);
            return new ChartAccountCompactionResult(true, plan.Mappings.Count, plan.PlanHash);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Recopie sur la fiche salarié le compte auxiliaire figé sur ses bulletins, quand elle n'en
    /// porte pas.
    /// </summary>
    /// <remarks>
    /// Sans cette reprise, un salarié dont le compte n'existait que par dérivation du matricule se
    /// verrait attribuer un <b>second</b> compte à la validation du cycle suivant, une fois la
    /// dérivation supprimée : sa dette de salaire se scinderait entre deux comptes et le lettrage du
    /// règlement deviendrait arbitraire. Volontairement indépendant de la compaction : un dossier
    /// sans compte trop long a lui aussi des fiches sans compte alloué.
    /// </remarks>
    public static async Task<int> BackfillEmployeeAuxiliaryAccountsAsync(
        TenantDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await ChartRewritePrimitives.TableExistsAsync(db, "Employees", cancellationToken)
            || !await ChartRewritePrimitives.TableExistsAsync(db, "Payslips", cancellationToken)
            || !await ChartRewritePrimitives.ColumnExistsAsync(db, "Employees", "AuxiliaryAccountNumber", cancellationToken)
            || !await ChartRewritePrimitives.ColumnExistsAsync(db, "Payslips", "EmployeeAuxiliaryAccount", cancellationToken))
        {
            return 0;
        }

        // Un compte revendiqué par deux salariés (collision héritée de la troncature du matricule)
        // est écarté : on préfère laisser la fiche vide, et l'allocation explicite tranchera.
        return await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            WITH latest AS (
                SELECT p.[EmployeeId],
                       p.[EmployeeAuxiliaryAccount] AS Account,
                       ROW_NUMBER() OVER (PARTITION BY p.[EmployeeId] ORDER BY p.[Year] DESC, p.[Month] DESC) AS rn
                FROM [Payslips] p
                WHERE p.[EmployeeAuxiliaryAccount] IS NOT NULL AND p.[EmployeeAuxiliaryAccount] <> N''
            ),
            unambiguous AS (
                SELECT Account FROM latest WHERE rn = 1
                GROUP BY Account HAVING COUNT(DISTINCT [EmployeeId]) = 1
            )
            UPDATE e
            SET e.[AuxiliaryAccountNumber] = l.Account,
                e.[UpdatedAt] = {DateTime.UtcNow},
                e.[UpdatedBy] = {MapVersion}
            FROM [Employees] e
            INNER JOIN latest l ON l.[EmployeeId] = e.[Id] AND l.rn = 1
            INNER JOIN unambiguous u ON u.Account = l.Account
            WHERE e.[AuxiliaryAccountNumber] IS NULL
              AND NOT EXISTS (
                  SELECT 1 FROM [Employees] e2
                  WHERE e2.[Id] <> e.[Id] AND e2.[AuxiliaryAccountNumber] = l.Account)
            """,
            cancellationToken);
    }

    // ── Plan ──────────────────────────────────────────────────────────────────────────────────

    private static async Task<List<string>> LoadCandidateNumbersAsync(
        TenantDbContext db, CancellationToken ct)
    {
        var maxDigits = AccountNumberRules.MaxDigits;
        var candidates = await db.ChartOfAccounts.AsNoTracking()
            .Where(a => a.AccountNumber.Length > maxDigits)
            .Select(a => a.AccountNumber)
            .ToListAsync(ct);

        return candidates
            .Where(n => AccountNumberRules.DigitCount(n) > AccountNumberRules.MaxDigits)
            .ToList();
    }

    /// <summary>
    /// Racine de renumérotation : le compte conforme le plus long qui préfixe la source. Le plus
    /// long, pour que le compte reste rattaché au même collectif — <c>425</c> et non <c>42</c>.
    /// </summary>
    private static string? ResolveRoot(string source, IReadOnlySet<string> everyNumber)
    {
        string? best = null;
        foreach (var candidate in everyNumber)
        {
            if (candidate.Length >= source.Length)
                continue;
            if (!source.StartsWith(candidate, StringComparison.Ordinal))
                continue;
            if (AccountNumberRules.DigitCount(candidate) > AccountNumberRules.MaxDigits)
                continue;
            if (best is null || candidate.Length > best.Length)
                best = candidate;
        }

        return best;
    }

    /// <summary>
    /// Premier numéro libre sous <paramref name="root"/>, en élargissant le suffixe tant que le
    /// budget de chiffres le permet.
    /// </summary>
    /// <remarks>
    /// Même forme que <c>PayrollEmployeeChartProvisioningService.AllocateNextNumber</c> : les
    /// comptes compactés atterrissent dans le même espace de numérotation que les comptes alloués,
    /// si bien que l'allocateur enchaîne naturellement sur le premier suffixe encore libre, sans
    /// aucune coordination entre les deux chemins.
    /// </remarks>
    private static string? AllocateTarget(string root, IReadOnlySet<string> taken)
    {
        var budget = AccountNumberRules.MaxDigits - AccountNumberRules.DigitCount(root);
        if (budget < 1)
            return null;

        // On vise la largeur de l'allocateur (4 chiffres) quand le budget le permet, pour partager
        // son espace de numérotation ; sous une racine plus longue — 436711 ne laisse que 2 chiffres —
        // on prend ce qui reste plutôt que de déclarer le vivier saturé à tort.
        for (var width = Math.Min(InitialSuffixLength, budget); width <= budget; width++)
        {
            var format = new string('0', width);
            var max = (int)Math.Pow(10, width) - 1;
            for (var i = 1; i <= max; i++)
            {
                var candidate = root + i.ToString(format, CultureInfo.InvariantCulture);
                if (!taken.Contains(candidate))
                    return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Nom du salarié porté par un compte, pour le rapport d'opération uniquement — jamais pour
    /// décider d'une cible. Même rapprochement structurel que le runbook de requalification.
    /// </summary>
    private static async Task<Dictionary<string, string?>> LoadEmployeeNamesAsync(
        TenantDbContext db, IReadOnlyList<string> accounts, CancellationToken ct)
    {
        var names = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (!await ChartRewritePrimitives.TableExistsAsync(db, "Payslips", ct))
            return names;

        var rows = await db.Payslips.AsNoTracking()
            .Where(p => p.EmployeeAuxiliaryAccount != null && accounts.Contains(p.EmployeeAuxiliaryAccount))
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
            .Select(p => new { p.EmployeeAuxiliaryAccount, p.EmployeeName })
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            if (row.EmployeeAuxiliaryAccount is not null)
                names.TryAdd(row.EmployeeAuxiliaryAccount, row.EmployeeName);
        }

        return names;
    }

    private static string ComputeHash(IReadOnlyList<ChartAccountCompactionMapping> mappings)
    {
        var payload = string.Join('\n', mappings.Select(m => $"{m.From}>{m.To}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // ── Journal des correspondances ───────────────────────────────────────────────────────────

    private static Task AcquireApplockAsync(TenantDbContext db, CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync(
            """
            DECLARE @lockResult INT;
            EXEC @lockResult = sp_getapplock
                @Resource = 'coa-digit-compaction-v1',
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction';
            IF @lockResult < 0
                THROW 51000, 'ChartAccountDigitCompactionService: unable to acquire coa-digit-compaction-v1 applock.', 1;
            """,
            ct);

    /// <summary>
    /// Crée la table de correspondances si la migration EF n'a pas encore été appliquée : le
    /// bootstrap d'un tenant peut précéder ses migrations selon le chemin emprunté.
    /// </summary>
    internal static Task EnsureLogTableAsync(TenantDbContext db, CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'dbo.ChartOfAccountCompactionLogs', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ChartOfAccountCompactionLogs] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [BatchId] uniqueidentifier NOT NULL,
                    [MapVersion] nvarchar(32) NOT NULL,
                    [FromAccountNumber] nvarchar(32) NOT NULL,
                    [ToAccountNumber] nvarchar(32) NOT NULL,
                    [RootAccountNumber] nvarchar(32) NOT NULL,
                    [DigitsBefore] int NOT NULL,
                    [EmployeeName] nvarchar(200) NULL,
                    [AppliedAt] datetime2 NOT NULL,
                    [AppliedBy] nvarchar(128) NOT NULL
                );
                CREATE UNIQUE INDEX [IX_ChartOfAccountCompactionLogs_From]
                    ON [dbo].[ChartOfAccountCompactionLogs] ([FromAccountNumber]);
                CREATE UNIQUE INDEX [IX_ChartOfAccountCompactionLogs_To]
                    ON [dbo].[ChartOfAccountCompactionLogs] ([ToAccountNumber]);
                CREATE INDEX [IX_ChartOfAccountCompactionLogs_Batch]
                    ON [dbo].[ChartOfAccountCompactionLogs] ([BatchId]);
            END
            """,
            ct);

    private static async Task<HashSet<string>> LoadPreviousTargetsAsync(TenantDbContext db, CancellationToken ct)
    {
        if (!await ChartRewritePrimitives.TableExistsAsync(db, LogTable, ct))
            return new HashSet<string>(StringComparer.Ordinal);

        var values = await ChartRewritePrimitives.DistinctValuesAsync(
            db, LogTable, "ToAccountNumber", LedgerColumns, nameof(ChartAccountDigitCompactionService), ct);

        return values.Where(v => v is not null).Cast<string>().ToHashSet(StringComparer.Ordinal);
    }

    private static async Task<bool> WasAlreadyCompactedAsync(TenantDbContext db, string source, CancellationToken ct)
    {
        if (!await ChartRewritePrimitives.TableExistsAsync(db, LogTable, ct))
            return false;

        var values = await ChartRewritePrimitives.DistinctValuesAsync(
            db, LogTable, "FromAccountNumber", LedgerColumns, nameof(ChartAccountDigitCompactionService), ct);

        return values.Any(v => string.Equals(v, source, StringComparison.Ordinal));
    }

    /// <summary>Allow-list dédiée aux lectures du journal de correspondances.</summary>
    private static readonly IReadOnlySet<(string Table, string Column)> LedgerColumns =
        new HashSet<(string Table, string Column)>
        {
            (LogTable, "FromAccountNumber"),
            (LogTable, "ToAccountNumber"),
        };

    private static async Task InsertLogRowsAsync(
        TenantDbContext db,
        IReadOnlyList<ChartAccountCompactionMapping> mappings,
        Guid batchId,
        DateTime appliedAt,
        string appliedBy,
        CancellationToken ct)
    {
        foreach (var mapping in mappings)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO [dbo].[ChartOfAccountCompactionLogs]
                    ([Id], [BatchId], [MapVersion], [FromAccountNumber], [ToAccountNumber],
                     [RootAccountNumber], [DigitsBefore], [EmployeeName], [AppliedAt], [AppliedBy])
                VALUES ({Guid.NewGuid()}, {batchId}, {MapVersion}, {mapping.From}, {mapping.To},
                        {mapping.Root}, {mapping.DigitsBefore}, {mapping.EmployeeName}, {appliedAt}, {appliedBy})
                """,
                ct);
        }
    }

    /// <summary>
    /// Marque les lignes déplacées. C'est ce qui permet à un contrôle de distinguer « ligne touchée
    /// par la renumérotation » de « écriture close modifiée à la main ».
    /// </summary>
    private static async Task StampAuditAsync(
        TenantDbContext db, IReadOnlyList<string> targets, DateTime appliedAt, CancellationToken ct)
    {
        if (targets.Count == 0)
            return;

        await db.Database.ExecuteSqlRawAsync(
            "UPDATE [ChartOfAccounts] SET [UpdatedAt] = {0}, [UpdatedBy] = {1} WHERE [AccountNumber] IN ("
            + string.Join(", ", targets.Select((_, i) => "{" + (i + 2) + "}")) + ")",
            new object[] { appliedAt, MapVersion }.Concat(targets).ToArray(),
            ct);

        if (await ChartRewritePrimitives.TableExistsAsync(db, "JournalEntryLines", ct))
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE [JournalEntryLines] SET [UpdatedAt] = {0}, [UpdatedBy] = {1} WHERE [AccountNumber] IN ("
                + string.Join(", ", targets.Select((_, i) => "{" + (i + 2) + "}")) + ")",
                new object[] { appliedAt, MapVersion }.Concat(targets).ToArray(),
                ct);
        }
    }

    // ── Post-conditions ───────────────────────────────────────────────────────────────────────

    private static async Task<(decimal Debit, decimal Credit)> ReadTrialBalanceAsync(
        TenantDbContext db, CancellationToken ct)
    {
        if (!await ChartRewritePrimitives.TableExistsAsync(db, "JournalEntryLines", ct))
            return (0m, 0m);

        // Lecture en SQL brut : DebitAmount/CreditAmount sont des types possedes (Money) dont seule
        // la sous-propriete Amount est une colonne decimale.
        var debit = await ScalarDecimalAsync(db, "SELECT ISNULL(SUM([DebitAmount]), 0) FROM [JournalEntryLines]", ct);
        var credit = await ScalarDecimalAsync(db, "SELECT ISNULL(SUM([CreditAmount]), 0) FROM [JournalEntryLines]", ct);
        return (debit, credit);
    }

    private static async Task<decimal> ScalarDecimalAsync(TenantDbContext db, string sql, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await db.Database.OpenConnectionAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (db.Database.CurrentTransaction is not null)
            cmd.Transaction = db.Database.CurrentTransaction.GetDbTransaction();

        var value = await cmd.ExecuteScalarAsync(ct);
        return value is null or DBNull ? 0m : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    private static async Task<Dictionary<string, int>> CountLinesByAccountAsync(
        TenantDbContext db, IEnumerable<string> accounts, CancellationToken ct)
    {
        var wanted = accounts.ToList();
        if (wanted.Count == 0 || !await ChartRewritePrimitives.TableExistsAsync(db, "JournalEntryLines", ct))
            return new Dictionary<string, int>(StringComparer.Ordinal);

        return await db.JournalEntryLines.AsNoTracking()
            .Where(l => wanted.Contains(l.AccountNumber))
            .GroupBy(l => l.AccountNumber)
            .Select(g => new { Account = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Account, x => x.Count, StringComparer.Ordinal, ct);
    }

    /// <summary>
    /// Vérifie, avant de valider la transaction, que la renumérotation n'a rien fait d'autre que
    /// renommer. Tout écart annule l'intégralité de l'opération.
    /// </summary>
    private static async Task AssertPostConditionsAsync(
        TenantDbContext db,
        ChartAccountCompactionPlan plan,
        decimal debitBefore,
        decimal creditBefore,
        IReadOnlyDictionary<string, int> linesBefore,
        CancellationToken ct)
    {
        // C1 — plus aucun compte non conforme.
        var remaining = await LoadCandidateNumbersAsync(db, ct);
        if (remaining.Count > 0)
        {
            throw new InvalidOperationException(
                $"Post-condition C1 : {remaining.Count} compte(s) dépassent toujours "
                + $"{AccountNumberRules.MaxDigits} chiffres (dont {remaining[0]}).");
        }

        // C2 — balance générale identique au centime.
        var (debitAfter, creditAfter) = await ReadTrialBalanceAsync(db, ct);
        if (debitAfter != debitBefore || creditAfter != creditBefore)
        {
            throw new InvalidOperationException(
                $"Post-condition C2 : la balance a bougé (débit {debitBefore} → {debitAfter}, "
                + $"crédit {creditBefore} → {creditAfter}).");
        }

        // C3 — chaque compte déplacé porte exactement les lignes qu'il portait.
        var linesAfter = await CountLinesByAccountAsync(db, plan.Mappings.Select(m => m.To), ct);
        foreach (var mapping in plan.Mappings)
        {
            linesBefore.TryGetValue(mapping.From, out var before);
            linesAfter.TryGetValue(mapping.To, out var after);
            if (before != after)
            {
                throw new InvalidOperationException(
                    $"Post-condition C3 : {mapping.From} portait {before} ligne(s), "
                    + $"{mapping.To} en porte {after}.");
            }
        }

        // C4 — un groupe de lettrage reste mono-compte.
        if (await ChartRewritePrimitives.TableExistsAsync(db, "LetteringGroupMembers", ct))
        {
            var straddling = await db.LetteringGroups.AsNoTracking()
                .Where(g => g.Members
                    .Join(db.JournalEntryLines, m => m.JournalEntryLineId, l => l.Id, (m, l) => l.AccountNumber)
                    .Distinct().Count() > 1)
                .CountAsync(ct);

            if (straddling > 0)
            {
                throw new InvalidOperationException(
                    $"Post-condition C4 : {straddling} groupe(s) de lettrage couvrent plusieurs comptes.");
            }
        }

        // C5 — aucune fusion de comptes.
        var total = await db.ChartOfAccounts.AsNoTracking().CountAsync(ct);
        var distinct = await db.ChartOfAccounts.AsNoTracking()
            .Select(a => a.AccountNumber).Distinct().CountAsync(ct);
        if (total != distinct)
        {
            throw new InvalidOperationException(
                $"Post-condition C5 : {total - distinct} compte(s) en doublon après renumérotation.");
        }
    }
}
