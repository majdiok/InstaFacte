using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.FiscalSchedule;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

/// <inheritdoc cref="IFirmPortfolioReadService"/>
public sealed class FirmPortfolioReadService : IFirmPortfolioReadService
{
    /// <summary>Fenêtre « à venir » par défaut, en jours.</summary>
    private const int DefaultHorizonDays = 30;

    /// <summary>
    /// Profondeur maximale de recherche des retards, en jours. Le générateur crée 62 échéances par
    /// an et par dossier, jamais soldées tant que personne ne les marque déposées : sans plancher,
    /// un dossier ancien renverrait des centaines de lignes. Les échéances plus anciennes ne sont
    /// pas comptées — <c>TotalMatching</c> permet de le signaler.
    /// </summary>
    private const int OverdueFloorDays = 400;

    /// <summary>Garde-fou dur par dossier, pour qu'un dossier pathologique ne noie pas le fan-out.</summary>
    private const int MaxRowsPerDossier = 200;

    private readonly MasterDbContext _masterContext;
    private readonly ITenantService _tenantService;
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly IFirmDossierAccessService _dossierAccess;
    private readonly AccountingFirmsOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FirmPortfolioReadService> _logger;

    // ────────────────────────── Mémoïsation par requête (Lot 2.3) ──────────────────────────
    // Service Scoped (DependencyInjection.cs) : la mémoïsation ci-dessous vit UNIQUEMENT pour la
    // durée du scope DI (une requête / un job), jamais entre requêtes. Elle évite que 3 outils
    // firm d'un même tour (overview + health + workload, tous DefaultHorizonDays) ne relancent
    // chacun le fan-out complet CollectAsync — source d'incohérences (données différentes vues par
    // 3 outils du même tour) et de coût ×3. La clé mémoïse la Task (pas seulement le résultat) pour
    // que des appels concurrents dans le même scope PARTAGENT la même exécution en vol.
    //
    // scopeKey = "system" quand scope est null (FirmMissionBriefingJob : lecture cabinet complète,
    // une fois par tour de brief, AUCUN utilisateur) — sinon "{UserId}:{Role}". "system" est une
    // chaîne, jamais un Guid : elle ne peut jamais coïncider avec une clé utilisateur par
    // construction, donc une entrée "system" n'est jamais servie à un scope utilisateur (ni l'inverse).
    //
    // L'ACL effective (GetAccessibleCompanyTenantIdsAsync) est résolue au premier appel pour la clé
    // et reste figée pour le reste du tour : une mutation d'affectation pendant le tour ne sera vue
    // qu'au tour suivant (cohérence intra-tour privilégiée sur la fraîcheur intra-tour).
    private readonly Dictionary<(Guid FirmTenantId, string ScopeKey, int HorizonDays), Task<IReadOnlyList<DossierSnapshot>>> _collectMemo = new();
    private readonly SemaphoreSlim _collectMemoLock = new(1, 1);

    private static string BuildScopeKey(FirmDossierAccessScope? scope)
        => scope is { } effectiveScope ? $"{effectiveScope.UserId}:{effectiveScope.Role}" : "system";

    public FirmPortfolioReadService(
        MasterDbContext masterContext,
        ITenantService tenantService,
        ITenantDbContextFactory contextFactory,
        IFirmDossierAccessService dossierAccess,
        IOptions<AccountingFirmsOptions> options,
        TimeProvider timeProvider,
        ILogger<FirmPortfolioReadService> logger)
    {
        _masterContext = masterContext;
        _tenantService = tenantService;
        _contextFactory = contextFactory;
        _dossierAccess = dossierAccess;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<FirmPortfolioOverviewDto> GetOverviewAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        CancellationToken cancellationToken = default)
    {
        var today = Today();
        var snapshots = await CollectAsync(firmTenantId, scope, DefaultHorizonDays, cancellationToken);

        var deadlines = snapshots.SelectMany(s => s.Deadlines).ToList();
        var overdue = deadlines.Where(d => d.Status == FiscalScheduleStatus.Overdue).ToList();
        var within7 = deadlines.Where(d => d.Status == FiscalScheduleStatus.UpcomingWithin7Days).ToList();

        return new FirmPortfolioOverviewDto
        {
            ActiveDossiersCount = snapshots.Count,
            OverdueCount = overdue.Count,
            UpcomingWithin7DaysCount = within7.Count,
            UpcomingAfter7DaysCount = deadlines.Count(d => d.Status == FiscalScheduleStatus.UpcomingAfter7Days),
            OverdueEstimatedAmount = overdue.Sum(d => d.EstimatedAmount),
            UpcomingWithin7DaysEstimatedAmount = within7.Sum(d => d.EstimatedAmount),
            DossiersWithOverdueCount = snapshots.Count(s => s.Deadlines.Any(d => d.Status == FiscalScheduleStatus.Overdue)),
            InactiveDossiers30DaysCount = snapshots.Count(s => !s.ReadFailed && s.IsInactive30Days(today)),
            VatDraftsCount = snapshots.Sum(s => s.VatDraftsCount),
            GeneratedAt = today,
            FanOut = BuildFanOut(snapshots)
        };
    }

    public async Task<FirmDeadlineListDto> GetDeadlinesAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        FirmDeadlineQuery query,
        CancellationToken cancellationToken = default)
    {
        var today = Today();
        var horizon = query.OnlyOverdue ? 0 : Math.Clamp(query.WithinDays ?? DefaultHorizonDays, 1, 365);
        var topN = Math.Clamp(query.TopN, 1, 50);

        var snapshots = await CollectAsync(firmTenantId, scope, horizon, cancellationToken);

        var matching = snapshots
            .SelectMany(s => s.Deadlines)
            .Where(d => !query.OnlyOverdue || d.Status == FiscalScheduleStatus.Overdue)
            .Where(d => query.ObligationType is null || d.ObligationType == query.ObligationType.Value)
            .Where(d => query.CompanyTenantId is null || d.CompanyTenantId == query.CompanyTenantId.Value)
            .Where(d => query.ResponsibleUserId is null || d.ResponsibleUserId == query.ResponsibleUserId.Value)
            .ToList();

        var items = matching
            .OrderBy(d => d.DueDate)
            .ThenBy(d => d.CompanyName, StringComparer.OrdinalIgnoreCase)
            .Take(topN)
            .Select(d => ToRow(d, today))
            .ToList();

        return new FirmDeadlineListDto
        {
            Items = items,
            TotalMatching = matching.Count,
            GeneratedAt = today,
            FanOut = BuildFanOut(snapshots)
        };
    }

    public async Task<FirmDossierHealthListDto> GetDossierHealthAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        int topN,
        CancellationToken cancellationToken = default)
    {
        var today = Today();
        var bounded = Math.Clamp(topN, 1, 50);
        var snapshots = await CollectAsync(firmTenantId, scope, DefaultHorizonDays, cancellationToken);

        var rows = snapshots
            .Select(s =>
            {
                var overdue = s.Deadlines.Where(d => d.Status == FiscalScheduleStatus.Overdue).ToList();
                var inactive = !s.ReadFailed && s.IsInactive30Days(today);
                var within7 = s.Deadlines.Count(d => d.Status == FiscalScheduleStatus.UpcomingWithin7Days);

                return new FirmDossierHealthRowDto
                {
                    CompanyTenantId = s.CompanyTenantId,
                    CompanyName = s.CompanyName,
                    RiskScore = ComputeRiskScore(overdue.Count, within7, inactive, s.VatDraftsCount, s.ReadFailed),
                    OverdueCount = overdue.Count,
                    OverdueEstimatedAmount = overdue.Sum(d => d.EstimatedAmount),
                    UpcomingWithin7DaysCount = within7,
                    LastJournalEntryDate = s.LastJournalEntryDate,
                    IsInactive30Days = inactive,
                    VatDraftsCount = s.VatDraftsCount,
                    AssignedAccountantName = s.AssignedAccountantName,
                    ReadFailed = s.ReadFailed
                };
            })
            .OrderByDescending(r => r.RiskScore)
            .ThenByDescending(r => r.OverdueEstimatedAmount)
            .ThenBy(r => r.CompanyName, StringComparer.OrdinalIgnoreCase)
            .Take(bounded)
            .ToList();

        return new FirmDossierHealthListDto
        {
            Items = rows,
            TotalDossiers = snapshots.Count,
            GeneratedAt = today,
            FanOut = BuildFanOut(snapshots)
        };
    }

    public async Task<FirmCollaboratorWorkloadDto> GetCollaboratorWorkloadAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        CancellationToken cancellationToken = default)
    {
        var today = Today();
        var snapshots = await CollectAsync(firmTenantId, scope, DefaultHorizonDays, cancellationToken);
        var deadlines = snapshots.SelectMany(s => s.Deadlines).ToList();

        var rows = deadlines
            .Where(d => d.ResponsibleUserId.HasValue)
            .GroupBy(d => d.ResponsibleUserId!.Value)
            .Select(g => new FirmCollaboratorWorkloadRowDto
            {
                CollaboratorUserId = g.Key,
                CollaboratorName = g.Select(d => d.ResponsibleName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                                   ?? "Collaborateur inconnu",
                DossiersCount = g.Select(d => d.CompanyTenantId).Distinct().Count(),
                OverdueCount = g.Count(d => d.Status == FiscalScheduleStatus.Overdue),
                UpcomingWithin7DaysCount = g.Count(d => d.Status == FiscalScheduleStatus.UpcomingWithin7Days),
                OverdueEstimatedAmount = g.Where(d => d.Status == FiscalScheduleStatus.Overdue).Sum(d => d.EstimatedAmount)
            })
            .OrderByDescending(r => r.OverdueCount)
            .ThenByDescending(r => r.UpcomingWithin7DaysCount)
            .ThenBy(r => r.CollaboratorName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new FirmCollaboratorWorkloadDto
        {
            Items = rows,
            UnassignedDeadlinesCount = deadlines.Count(d => !d.ResponsibleUserId.HasValue),
            GeneratedAt = today,
            FanOut = BuildFanOut(snapshots)
        };
    }

    // ────────────────────────────── Collecte ──────────────────────────────

    /// <summary>
    /// Point d'entrée mémoïsé par requête (Lot 2.3) de <see cref="CollectCoreAsync"/> : partage le
    /// fan-out entre outils d'un même tour (même dossier tenant + même scope + même horizon). Ne
    /// verrouille que la lecture/écriture de la table de mémoïsation — le fan-out lui-même s'exécute
    /// sans verrou, et les appelants concurrents attendent la MÊME <see cref="Task"/> (pas seulement
    /// le même résultat), ce qui garantit une exécution unique même sous concurrence.
    /// </summary>
    private async Task<IReadOnlyList<DossierSnapshot>> CollectAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        int horizonDays,
        CancellationToken cancellationToken)
    {
        var key = (firmTenantId, BuildScopeKey(scope), horizonDays);

        Task<IReadOnlyList<DossierSnapshot>> task;
        await _collectMemoLock.WaitAsync(cancellationToken);
        try
        {
            if (!_collectMemo.TryGetValue(key, out task!))
            {
                task = CollectCoreAsync(firmTenantId, scope, horizonDays, cancellationToken);
                _collectMemo[key] = task;
            }
        }
        finally
        {
            _collectMemoLock.Release();
        }

        return await task;
    }

    /// <summary>
    /// Fan-out en trois temps, dans cet ordre impérativement :
    /// <list type="number">
    ///   <item>lectures master SÉQUENTIELLES (dossiers autorisés, gestionnaire affecté) ;</item>
    ///   <item>résolution des chaînes de connexion SÉQUENTIELLE — <c>ITenantService</c> s'appuie sur
    ///         le <c>MasterDbContext</c> scoped en cas de défaut de cache, qui n'admet pas deux
    ///         opérations concurrentes ;</item>
    ///   <item>lectures dossiers EN PARALLÈLE borné, chacune sur son propre contexte isolé.</item>
    /// </list>
    /// </summary>
    private async Task<IReadOnlyList<DossierSnapshot>> CollectCoreAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        int horizonDays,
        CancellationToken cancellationToken)
    {
        // 1. ACL. Contrat tri-état : null = aucun filtre, ensemble vide = aucun dossier.
        IReadOnlySet<Guid>? allowedCompanyIds = null;
        if (scope is { } effectiveScope)
        {
            allowedCompanyIds = await _dossierAccess.GetAccessibleCompanyTenantIdsAsync(
                firmTenantId, effectiveScope, cancellationToken);
            if (allowedCompanyIds is { Count: 0 })
                return Array.Empty<DossierSnapshot>();
        }

        var dossiersQuery =
            from assignment in _masterContext.FirmClientAssignments.AsNoTracking()
            join tenant in _masterContext.Tenants.AsNoTracking() on assignment.CompanyTenantId equals tenant.Id
            where assignment.FirmTenantId == firmTenantId && assignment.Status == FirmAssignmentStatus.Active
            select new { assignment.CompanyTenantId, tenant.CompanyName };

        if (allowedCompanyIds is not null)
            dossiersQuery = dossiersQuery.Where(d => allowedCompanyIds.Contains(d.CompanyTenantId));

        var dossiers = await dossiersQuery
            .OrderBy(d => d.CompanyName)
            .ToListAsync(cancellationToken);

        if (dossiers.Count == 0)
            return Array.Empty<DossierSnapshot>();

        var accountantRows = await _masterContext.PermanentFiles.AsNoTracking()
            .Where(p => p.FirmTenantId == firmTenantId)
            .Join(_masterContext.FirmClientAssignments.AsNoTracking(),
                p => p.FirmClientAssignmentId,
                a => a.Id,
                (p, a) => new { a.CompanyTenantId, p.AssignedAccountantName })
            .ToListAsync(cancellationToken);

        // Dictionnaire construit défensivement : un doublon de dossier permanent ferait échouer
        // ToDictionaryAsync, et donc l'outil entier, pour une donnée purement informative.
        var accountantByCompany = new Dictionary<Guid, string?>();
        foreach (var row in accountantRows)
            accountantByCompany[row.CompanyTenantId] = row.AssignedAccountantName;

        // 2. Chaînes de connexion, séquentiellement (cf. remarque sur le MasterDbContext scoped).
        var targets = new List<(Guid CompanyTenantId, string CompanyName, string? ConnectionString)>(dossiers.Count);
        foreach (var dossier in dossiers)
        {
            string? connectionString = null;
            try
            {
                connectionString = await _tenantService.GetConnectionStringAsync(dossier.CompanyTenantId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Chaîne de connexion introuvable pour le dossier {TenantId}", dossier.CompanyTenantId);
            }

            targets.Add((dossier.CompanyTenantId, dossier.CompanyName, connectionString));
        }

        // 3. Lectures dossiers en parallèle borné, résultats indexés (ordre déterministe).
        var today = Today();
        var snapshots = new DossierSnapshot[targets.Count];
        var parallelism = Math.Clamp(_options.FirmAgentMaxParallelDossiers, 1, 16);

        await Parallel.ForEachAsync(
            Enumerable.Range(0, targets.Count),
            new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = cancellationToken },
            async (index, ct) =>
            {
                var target = targets[index];
                accountantByCompany.TryGetValue(target.CompanyTenantId, out var accountantName);
                snapshots[index] = await ReadDossierAsync(target, accountantName, today, horizonDays, ct);
            });

        return snapshots;
    }

    private async Task<DossierSnapshot> ReadDossierAsync(
        (Guid CompanyTenantId, string CompanyName, string? ConnectionString) target,
        string? accountantName,
        DateTime today,
        int horizonDays,
        CancellationToken cancellationToken)
    {
        var failed = new DossierSnapshot
        {
            CompanyTenantId = target.CompanyTenantId,
            CompanyName = target.CompanyName,
            AssignedAccountantName = accountantName,
            ReadFailed = true
        };

        if (string.IsNullOrWhiteSpace(target.ConnectionString))
            return failed;

        try
        {
            // CreateIsolatedContext(connectionString) plutôt qu'un DbContextOptionsBuilder nu :
            // on conserve EnableRetryOnFailure, le MigrationsAssembly et la journalisation SQL.
            await using var ctx = _contextFactory.CreateIsolatedContext(target.ConnectionString);

            var floor = today.AddDays(-OverdueFloorDays);
            var ceiling = today.AddDays(horizonDays);

            // Le filtre SQL reproduit exactement la précédence de FiscalScheduleEntry.ResolveStatus :
            // écarter annulé / payé / validé / déposé ne laisse que Overdue et Upcoming*.
            var entries = await ctx.FiscalScheduleEntries.AsNoTracking()
                .Where(e => !e.IsCancelled
                            && e.PaymentDate == null
                            && e.ValidatedAt == null
                            && e.DepositDate == null
                            && e.DueDate >= floor
                            && e.DueDate <= ceiling)
                .OrderBy(e => e.DueDate)
                .Take(MaxRowsPerDossier)
                .Select(e => new DeadlineSnapshot
                {
                    Id = e.Id,
                    CompanyTenantId = target.CompanyTenantId,
                    CompanyName = target.CompanyName,
                    ObligationLabel = e.ObligationLabel,
                    ObligationType = e.ObligationType,
                    DueDate = e.DueDate,
                    EstimatedAmount = e.EstimatedAmount,
                    ResponsibleUserId = e.ResponsibleUserId,
                    ResponsibleName = e.ResponsibleName,
                    LastReminderAt = e.LastReminderAt
                })
                .ToListAsync(cancellationToken);

            var lastEntry = await ctx.JournalEntries.AsNoTracking()
                .MaxAsync(e => (DateTime?)e.EntryDate, cancellationToken);

            var vatDrafts = await ctx.VatDeclarations.AsNoTracking()
                .CountAsync(v => v.Status == VatDeclarationStatus.Draft, cancellationToken);

            foreach (var entry in entries)
                entry.Status = ResolveActionableStatus(entry.DueDate, today);

            return new DossierSnapshot
            {
                CompanyTenantId = target.CompanyTenantId,
                CompanyName = target.CompanyName,
                AssignedAccountantName = accountantName,
                LastJournalEntryDate = lastEntry,
                VatDraftsCount = vatDrafts,
                Deadlines = entries,
                ReadFailed = false
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lecture du dossier {TenantId} en échec pour l'agent cabinet", target.CompanyTenantId);
            return failed;
        }
    }

    // ────────────────────────────── Projections ──────────────────────────────

    /// <summary>
    /// Statut des seules échéances actionnables. Les états soldés (déposé / payé / validé / annulé)
    /// ont déjà été écartés en SQL, donc seule la date d'échéance discrimine encore.
    /// </summary>
    private static FiscalScheduleStatus ResolveActionableStatus(DateTime dueDate, DateTime today) =>
        dueDate.Date < today ? FiscalScheduleStatus.Overdue
        : dueDate.Date <= today.AddDays(7) ? FiscalScheduleStatus.UpcomingWithin7Days
        : FiscalScheduleStatus.UpcomingAfter7Days;

    /// <summary>
    /// Score composite volontairement simple et lisible : un retard pèse le plus lourd, l'inactivité
    /// comptable ensuite. L'objectif est un ORDRE défendable, pas une mesure de risque absolue.
    /// </summary>
    private static int ComputeRiskScore(int overdueCount, int within7Count, bool inactive, int vatDrafts, bool readFailed)
    {
        if (readFailed)
            return -1; // Rien à affirmer sur ce dossier : il sort du classement par le bas.

        return (overdueCount * 10)
               + (within7Count * 3)
               + (inactive ? 15 : 0)
               + Math.Min(vatDrafts, 5);
    }

    private static FirmDeadlineRowDto ToRow(DeadlineSnapshot d, DateTime today) => new()
    {
        Id = d.Id,
        CompanyTenantId = d.CompanyTenantId,
        CompanyName = d.CompanyName,
        ObligationLabel = d.ObligationLabel,
        ObligationType = d.ObligationType,
        DueDate = d.DueDate,
        DaysUntilDue = (int)(d.DueDate.Date - today).TotalDays,
        Status = d.Status,
        StatusDisplay = FiscalScheduleMappings.GetStatusDisplay(d.Status),
        EstimatedAmount = d.EstimatedAmount,
        ResponsibleUserId = d.ResponsibleUserId,
        ResponsibleName = d.ResponsibleName,
        LastReminderAt = d.LastReminderAt
    };

    private static FirmFanOutHealthDto BuildFanOut(IReadOnlyList<DossierSnapshot> snapshots) => new()
    {
        DossiersRead = snapshots.Count(s => !s.ReadFailed),
        DossiersFailed = snapshots.Count(s => s.ReadFailed)
    };

    private DateTime Today() => _timeProvider.GetLocalNow().DateTime.Date;

    // ────────────────────────────── Instantanés internes ──────────────────────────────

    private sealed class DossierSnapshot
    {
        public Guid CompanyTenantId { get; init; }
        public string CompanyName { get; init; } = null!;
        public string? AssignedAccountantName { get; init; }
        public DateTime? LastJournalEntryDate { get; init; }
        public int VatDraftsCount { get; init; }
        public IReadOnlyList<DeadlineSnapshot> Deadlines { get; init; } = Array.Empty<DeadlineSnapshot>();
        public bool ReadFailed { get; init; }

        public bool IsInactive30Days(DateTime today) =>
            LastJournalEntryDate is null || LastJournalEntryDate < today.AddDays(-30);
    }

    private sealed class DeadlineSnapshot
    {
        public Guid Id { get; init; }
        public Guid CompanyTenantId { get; init; }
        public string CompanyName { get; init; } = null!;
        public string ObligationLabel { get; init; } = null!;
        public FiscalObligationType ObligationType { get; init; }
        public DateTime DueDate { get; init; }
        public decimal EstimatedAmount { get; init; }
        public Guid? ResponsibleUserId { get; init; }
        public string? ResponsibleName { get; init; }
        public DateTime? LastReminderAt { get; init; }
        public FiscalScheduleStatus Status { get; set; }
    }
}
