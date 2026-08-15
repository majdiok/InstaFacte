using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Audit;
using FactuTrust.Application.Features.Accounting.Audit.Narrative;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.AccountingAudit;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.AccountingAudit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.FirmRevision;

/// <inheritdoc cref="IFirmRevisionService"/>
/// <remarks>
/// <para>
/// <b>Fan-out en trois temps, dans cet ordre impérativement</b> — copié de
/// <see cref="FirmPortfolioReadService"/>, dont le patron est éprouvé :
/// </para>
/// <list type="number">
///   <item>lectures master SÉQUENTIELLES (dossiers autorisés, collaborateur affecté) ;</item>
///   <item>résolution des chaînes de connexion SÉQUENTIELLE — <c>ITenantService</c> s'appuie sur le
///         <c>MasterDbContext</c> scoped en cas de défaut de cache, qui n'admet pas deux opérations
///         concurrentes ;</item>
///   <item>lectures dossiers EN PARALLÈLE borné, chacune sur son propre contexte isolé.</item>
/// </list>
/// <para>
/// L'échec d'un dossier ne fait jamais tomber le balayage : il est marqué <c>ReadFailed</c> et
/// compté dans <see cref="FirmFanOutHealthDto"/>. Un chef de mission préfère une vue partielle
/// signalée à une page d'erreur.
/// </para>
/// </remarks>
public sealed class FirmRevisionService : IFirmRevisionService
{
    /// <summary>Anomalies détaillées remontées par dossier. Garde-fou contre un dossier pathologique.</summary>
    private const int MaxAnomaliesPerDossier = 500;

    private static readonly int BlockingSeverity = (int)PreClosingSeverity.Blocking;
    private static readonly int WarningSeverity = (int)PreClosingSeverity.Warning;
    private static readonly int InfoSeverity = (int)PreClosingSeverity.Info;

    private readonly MasterDbContext _master;
    private readonly ITenantService _tenantService;
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly IFirmDossierAccessService _dossierAccess;
    private readonly IAccountingAuditEngine _engine;
    private readonly IRevisionNoteGenerator _noteGenerator;
    private readonly IPdfService _pdfService;
    private readonly AccountingFirmsOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FirmRevisionService> _logger;

    public FirmRevisionService(
        MasterDbContext master,
        ITenantService tenantService,
        ITenantDbContextFactory contextFactory,
        IFirmDossierAccessService dossierAccess,
        IAccountingAuditEngine engine,
        IRevisionNoteGenerator noteGenerator,
        IPdfService pdfService,
        IOptions<AccountingFirmsOptions> options,
        TimeProvider timeProvider,
        ILogger<FirmRevisionService> logger)
    {
        _master = master;
        _tenantService = tenantService;
        _contextFactory = contextFactory;
        _dossierAccess = dossierAccess;
        _engine = engine;
        _noteGenerator = noteGenerator;
        _pdfService = pdfService;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // ── Lectures ──────────────────────────────────────────────────────────────────────────

    public async Task<Result<FirmRevisionOverviewDto>> GetOverviewAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        int fiscalYear,
        CancellationToken cancellationToken = default)
    {
        var guard = Validate(firmTenantId, fiscalYear);
        if (guard is not null) return Result.Failure<FirmRevisionOverviewDto>(guard);

        var snapshots = await CollectAsync(firmTenantId, scope, fiscalYear, cancellationToken);
        var rows = snapshots.Select(ToRow).OrderByDescending(r => r.RiskScore)
            .ThenByDescending(r => r.ImpactAmount)
            .ToList();

        var allAnomalies = snapshots.SelectMany(s => s.Anomalies).ToList();

        return Result.Success(new FirmRevisionOverviewDto
        {
            FiscalYear = fiscalYear,
            DossiersCount = snapshots.Count,
            DossiersWithAnomaliesCount = snapshots.Count(s => !s.ReadFailed && s.Anomalies.Count > 0),
            DossiersNeverScannedCount = snapshots.Count(s => !s.ReadFailed && s.NeverScanned),
            BlockingCount = allAnomalies.Count(a => a.Severity == BlockingSeverity),
            WarningCount = allAnomalies.Count(a => a.Severity == WarningSeverity),
            InfoCount = allAnomalies.Count(a => a.Severity == InfoSeverity),
            TotalAnomalies = allAnomalies.Count,
            TotalImpactAmount = SumImpact(allAnomalies),
            GeneratedAt = _timeProvider.GetUtcNow().UtcDateTime,
            Dossiers = rows,
            ByFamily = BuildFamilySlices(allAnomalies),
            FanOut = BuildFanOut(snapshots)
        });
    }

    public async Task<Result<FirmRevisionDossierDetailDto>> GetDossierAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        Guid companyTenantId,
        int fiscalYear,
        CancellationToken cancellationToken = default)
    {
        var guard = Validate(firmTenantId, fiscalYear);
        if (guard is not null) return Result.Failure<FirmRevisionDossierDetailDto>(guard);

        // L'ACL est vérifiée sur CE dossier avant toute lecture : on ne s'appuie pas sur le fait
        // que l'appelant aurait déjà filtré.
        if (scope is { } effectiveScope)
        {
            var allowed = await _dossierAccess.CanAccessClientDossierAsync(
                firmTenantId, effectiveScope, companyTenantId, cancellationToken);
            if (!allowed)
                return Result.Failure<FirmRevisionDossierDetailDto>(
                    Error.Validation("Dossier", FirmDossierAccessService.NotAssignedMessage));
        }

        var target = await ResolveTargetAsync(firmTenantId, companyTenantId, cancellationToken);
        if (target is null)
            return Result.Failure<FirmRevisionDossierDetailDto>(
                Error.NotFound("FirmClientAssignment", companyTenantId));

        var snapshot = await ReadDossierAsync(target.Value, fiscalYear, cancellationToken);
        if (snapshot.ReadFailed)
            return Result.Failure<FirmRevisionDossierDetailDto>(
                Error.Validation("Dossier", "La base du dossier est momentanément illisible."));

        return Result.Success(new FirmRevisionDossierDetailDto
        {
            CompanyTenantId = snapshot.CompanyTenantId,
            CompanyName = snapshot.CompanyName,
            FiscalYear = fiscalYear,
            ComplianceRate = snapshot.ComplianceRate,
            LastScanAt = snapshot.LastScanAt,
            ImpactAmount = SumImpact(snapshot.Anomalies),
            Anomalies = snapshot.Anomalies.Select(ToListItem).ToList(),
            Note = snapshot.Note
        });
    }

    public async Task<Result<FirmRevisionWorkQueueDto>> GetWorkQueueAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        int fiscalYear,
        CancellationToken cancellationToken = default)
    {
        var guard = Validate(firmTenantId, fiscalYear);
        if (guard is not null) return Result.Failure<FirmRevisionWorkQueueDto>(guard);

        var snapshots = await CollectAsync(firmTenantId, scope, fiscalYear, cancellationToken);
        var rows = snapshots.Select(ToRow).ToList();

        var collaborators = rows
            .Where(r => r.AssignedAccountantUserId is not null)
            .GroupBy(r => (r.AssignedAccountantUserId, r.AssignedAccountantName))
            .Select(g => new FirmRevisionCollaboratorLoadDto
            {
                UserId = g.Key.AssignedAccountantUserId,
                Name = g.Key.AssignedAccountantName ?? "Collaborateur",
                DossiersCount = g.Count(),
                BlockingCount = g.Sum(r => r.BlockingCount),
                TotalAnomalies = g.Sum(r => r.TotalAnomalies),
                ImpactAmount = MillimeRounding.Round(g.Sum(r => r.ImpactAmount)),
                Dossiers = g.OrderByDescending(r => r.RiskScore)
                    .ThenByDescending(r => r.ImpactAmount)
                    .ToList()
            })
            // Le collaborateur le plus chargé d'abord : c'est lui qu'il faut soulager.
            .OrderByDescending(c => c.BlockingCount)
            .ThenByDescending(c => c.TotalAnomalies)
            .ToList();

        return Result.Success(new FirmRevisionWorkQueueDto
        {
            FiscalYear = fiscalYear,
            GeneratedAt = _timeProvider.GetUtcNow().UtcDateTime,
            Collaborators = collaborators,
            Unassigned = rows
                .Where(r => r.AssignedAccountantUserId is null)
                .OrderByDescending(r => r.RiskScore)
                .ToList(),
            FanOut = BuildFanOut(snapshots)
        });
    }

    public async Task<Result<FirmRevisionNoteDto>> GenerateNoteAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        Guid companyTenantId,
        int fiscalYear,
        Guid? userId,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        var guard = Validate(firmTenantId, fiscalYear);
        if (guard is not null) return Result.Failure<FirmRevisionNoteDto>(guard);

        // L'ACL est vérifiée sur CE dossier : générer une note est une lecture de son contenu.
        if (scope is { } effectiveScope)
        {
            var allowed = await _dossierAccess.CanAccessClientDossierAsync(
                firmTenantId, effectiveScope, companyTenantId, cancellationToken);
            if (!allowed)
                return Result.Failure<FirmRevisionNoteDto>(
                    Error.Validation("Dossier", FirmDossierAccessService.NotAssignedMessage));
        }

        var target = await ResolveTargetAsync(firmTenantId, companyTenantId, cancellationToken);
        if (target is null)
            return Result.Failure<FirmRevisionNoteDto>(
                Error.NotFound("FirmClientAssignment", companyTenantId));

        if (string.IsNullOrWhiteSpace(target.Value.ConnectionString))
            return Result.Failure<FirmRevisionNoteDto>(
                Error.Validation("Dossier", "La base du dossier est momentanément inaccessible."));

        return await _noteGenerator.GenerateAsync(
            target.Value.ConnectionString!, target.Value.CompanyName, fiscalYear,
            userId, userName, cancellationToken);
    }

    public async Task<Result<byte[]>> ExportNoteAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        Guid companyTenantId,
        int fiscalYear,
        CancellationToken cancellationToken = default)
    {
        var guard = Validate(firmTenantId, fiscalYear);
        if (guard is not null) return Result.Failure<byte[]>(guard);

        if (scope is { } effectiveScope)
        {
            var allowed = await _dossierAccess.CanAccessClientDossierAsync(
                firmTenantId, effectiveScope, companyTenantId, cancellationToken);
            if (!allowed)
                return Result.Failure<byte[]>(
                    Error.Validation("Dossier", FirmDossierAccessService.NotAssignedMessage));
        }

        var target = await ResolveTargetAsync(firmTenantId, companyTenantId, cancellationToken);
        if (target is null)
            return Result.Failure<byte[]>(Error.NotFound("FirmClientAssignment", companyTenantId));

        if (string.IsNullOrWhiteSpace(target.Value.ConnectionString))
            return Result.Failure<byte[]>(
                Error.Validation("Dossier", "La base du dossier est momentanément inaccessible."));

        await using var ctx = _contextFactory.CreateIsolatedContext(target.Value.ConnectionString!);

        var run = await ctx.Set<AccountingControlRun>().AsNoTracking()
            .Where(r => r.FiscalYear == fiscalYear && r.Status == ControlRunStatus.Completed)
            .OrderByDescending(r => r.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (run is null)
            return Result.Failure<byte[]>(Error.Validation(
                "Contrôle", "Aucun contrôle terminé sur cet exercice."));

        var note = await ctx.Set<AccountingRevisionNote>().AsNoTracking()
            .FirstOrDefaultAsync(n => n.RunId == run.Id, cancellationToken);

        // Le PDF met en page une note existante : il ne la produit pas au passage, sans quoi un
        // simple téléchargement déclencherait une génération — et un appel au modèle.
        if (note is null)
            return Result.Failure<byte[]>(Error.Validation(
                "Note", "Aucun dossier de révision pour ce contrôle : générez-le d'abord."));

        // Raison sociale et matricule viennent de la base du dossier, pas du nom d'affichage du
        // cabinet : c'est le document du client, il doit porter son identité fiscale.
        var company = await ctx.Companies.AsNoTracking()
            .Select(c => new { c.Name, c.VatCode })
            .FirstOrDefaultAsync(cancellationToken);

        var pdf = await _pdfService.GenerateRevisionDossierPdfAsync(
            new RevisionDossierPdfContext
            {
                Header = new AccountingReportHeader(
                    company?.Name ?? target.Value.CompanyName,
                    company?.VatCode,
                    "Dossier de révision",
                    $"Exercice {fiscalYear}"),
                Note = RevisionNoteMapper.ToDto(note),
                ComplianceRate = run.ComplianceRate,
                ControlCompletedAt = run.CompletedAt
            },
            cancellationToken);

        return Result.Success(pdf);
    }

    // ── Balayage ──────────────────────────────────────────────────────────────────────────

    public async Task<Result<FirmRevisionSweepResultDto>> SweepAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        int fiscalYear,
        Guid? triggeredByUserId,
        string? triggeredByUserName,
        CancellationToken cancellationToken = default)
    {
        var guard = Validate(firmTenantId, fiscalYear);
        if (guard is not null) return Result.Failure<FirmRevisionSweepResultDto>(guard);

        var startedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var targets = await ResolveTargetsAsync(firmTenantId, scope, cancellationToken);

        var scanned = 0;
        var failed = 0;
        var anomalies = 0;
        var gate = new SemaphoreSlim(1, 1);

        await Parallel.ForEachAsync(
            targets,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(_options.FirmAgentMaxParallelDossiers, 1, 16),
                CancellationToken = cancellationToken
            },
            async (target, ct) =>
            {
                if (string.IsNullOrWhiteSpace(target.ConnectionString))
                {
                    await BumpAsync(gate, () => failed++, ct);
                    return;
                }

                try
                {
                    var result = await _engine.RunForConnectionAsync(
                        target.ConnectionString,
                        new AccountingAuditRunRequestDto { FiscalYear = fiscalYear },
                        triggeredByUserId,
                        triggeredByUserName,
                        ct);

                    if (result.IsFailure)
                    {
                        _logger.LogWarning(
                            "Balayage de révision : échec sur le dossier {TenantId} — {Error}",
                            target.CompanyTenantId, result.Error.Description);
                        await BumpAsync(gate, () => failed++, ct);
                        return;
                    }

                    await BumpAsync(gate, () =>
                    {
                        scanned++;
                        anomalies += result.Value.TotalAnomalies;
                    }, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Un dossier en panne ne prive pas les autres de leur contrôle.
                    _logger.LogError(ex,
                        "Balayage de révision : exception sur le dossier {TenantId}", target.CompanyTenantId);
                    await BumpAsync(gate, () => failed++, ct);
                }
            });

        var completedAt = _timeProvider.GetUtcNow().UtcDateTime;
        _logger.LogInformation(
            "Balayage de révision du cabinet {FirmTenantId} terminé : {Scanned} dossier(s), {Failed} échec(s), {Anomalies} anomalie(s).",
            firmTenantId, scanned, failed, anomalies);

        return Result.Success(new FirmRevisionSweepResultDto
        {
            FiscalYear = fiscalYear,
            DossiersScanned = scanned,
            DossiersFailed = failed,
            TotalAnomalies = anomalies,
            Duration = completedAt - startedAt,
            CompletedAt = completedAt
        });
    }

    /// <summary>Incrément sérialisé : les compteurs sont partagés entre tâches parallèles.</summary>
    private static async Task BumpAsync(SemaphoreSlim gate, Action action, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try { action(); }
        finally { gate.Release(); }
    }

    // ── Collecte ──────────────────────────────────────────────────────────────────────────

    private static Error? Validate(Guid firmTenantId, int fiscalYear)
    {
        if (firmTenantId == Guid.Empty)
            return Error.Validation("Firm", "Contexte cabinet indisponible.");
        if (fiscalYear is < 2000 or > 2100)
            return Error.Validation("FiscalYear", "Exercice invalide.");
        return null;
    }

    private async Task<IReadOnlyList<DossierRevisionSnapshot>> CollectAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        int fiscalYear,
        CancellationToken cancellationToken)
    {
        var targets = await ResolveTargetsAsync(firmTenantId, scope, cancellationToken);
        if (targets.Count == 0) return Array.Empty<DossierRevisionSnapshot>();

        var snapshots = new DossierRevisionSnapshot[targets.Count];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, targets.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(_options.FirmAgentMaxParallelDossiers, 1, 16),
                CancellationToken = cancellationToken
            },
            async (index, ct) =>
            {
                snapshots[index] = await ReadDossierAsync(targets[index], fiscalYear, ct);
            });

        return snapshots;
    }

    private async Task<IReadOnlyList<DossierTarget>> ResolveTargetsAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        CancellationToken cancellationToken)
    {
        // 1. ACL. Contrat tri-état : null = aucun filtre, ensemble vide = aucun dossier.
        IReadOnlySet<Guid>? allowed = null;
        if (scope is { } effectiveScope)
        {
            allowed = await _dossierAccess.GetAccessibleCompanyTenantIdsAsync(
                firmTenantId, effectiveScope, cancellationToken);
            if (allowed is { Count: 0 }) return Array.Empty<DossierTarget>();
        }

        var query =
            from assignment in _master.FirmClientAssignments.AsNoTracking()
            join tenant in _master.Tenants.AsNoTracking() on assignment.CompanyTenantId equals tenant.Id
            where assignment.FirmTenantId == firmTenantId
                  && assignment.Status == FirmAssignmentStatus.Active
            select new { assignment.CompanyTenantId, tenant.CompanyName };

        if (allowed is not null)
            query = query.Where(d => allowed.Contains(d.CompanyTenantId));

        var dossiers = await query.OrderBy(d => d.CompanyName).ToListAsync(cancellationToken);
        if (dossiers.Count == 0) return Array.Empty<DossierTarget>();

        var accountants = await BuildAccountantMapAsync(firmTenantId, cancellationToken);

        // 2. Chaînes de connexion, SÉQUENTIELLEMENT (MasterDbContext scoped non réentrant).
        var targets = new List<DossierTarget>(dossiers.Count);
        foreach (var dossier in dossiers)
        {
            string? connectionString = null;
            try
            {
                connectionString = await _tenantService.GetConnectionStringAsync(
                    dossier.CompanyTenantId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Chaîne de connexion introuvable pour le dossier {TenantId}", dossier.CompanyTenantId);
            }

            accountants.TryGetValue(dossier.CompanyTenantId, out var accountant);
            targets.Add(new DossierTarget(
                dossier.CompanyTenantId, dossier.CompanyName, connectionString,
                accountant.UserId, accountant.Name));
        }

        return targets;
    }

    private async Task<DossierTarget?> ResolveTargetAsync(
        Guid firmTenantId, Guid companyTenantId, CancellationToken cancellationToken)
    {
        var dossier = await (
            from assignment in _master.FirmClientAssignments.AsNoTracking()
            join tenant in _master.Tenants.AsNoTracking() on assignment.CompanyTenantId equals tenant.Id
            where assignment.FirmTenantId == firmTenantId
                  && assignment.CompanyTenantId == companyTenantId
                  && assignment.Status == FirmAssignmentStatus.Active
            select new { assignment.CompanyTenantId, tenant.CompanyName }).FirstOrDefaultAsync(cancellationToken);

        if (dossier is null) return null;

        var accountants = await BuildAccountantMapAsync(firmTenantId, cancellationToken);
        accountants.TryGetValue(companyTenantId, out var accountant);

        string? connectionString = null;
        try
        {
            connectionString = await _tenantService.GetConnectionStringAsync(companyTenantId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Chaîne de connexion introuvable pour le dossier {TenantId}", companyTenantId);
        }

        return new DossierTarget(
            dossier.CompanyTenantId, dossier.CompanyName, connectionString,
            accountant.UserId, accountant.Name);
    }

    /// <summary>
    /// Collaborateur affecté par dossier. Dictionnaire construit défensivement : un doublon de
    /// dossier permanent ferait échouer <c>ToDictionaryAsync</c>, et donc tout l'écran, pour une
    /// donnée purement informative.
    /// </summary>
    private async Task<Dictionary<Guid, (Guid? UserId, string? Name)>> BuildAccountantMapAsync(
        Guid firmTenantId, CancellationToken cancellationToken)
    {
        var rows = await _master.PermanentFiles.AsNoTracking()
            .Where(p => p.FirmTenantId == firmTenantId)
            .Join(_master.FirmClientAssignments.AsNoTracking(),
                p => p.FirmClientAssignmentId,
                a => a.Id,
                (p, a) => new { a.CompanyTenantId, p.AssignedAccountantUserId, p.AssignedAccountantName })
            .ToListAsync(cancellationToken);

        var map = new Dictionary<Guid, (Guid? UserId, string? Name)>();
        foreach (var row in rows)
            map[row.CompanyTenantId] = (row.AssignedAccountantUserId, row.AssignedAccountantName);
        return map;
    }

    private async Task<DossierRevisionSnapshot> ReadDossierAsync(
        DossierTarget target, int fiscalYear, CancellationToken cancellationToken)
    {
        var failed = new DossierRevisionSnapshot
        {
            CompanyTenantId = target.CompanyTenantId,
            CompanyName = target.CompanyName,
            AssignedAccountantUserId = target.AccountantUserId,
            AssignedAccountantName = target.AccountantName,
            ReadFailed = true
        };

        if (string.IsNullOrWhiteSpace(target.ConnectionString)) return failed;

        try
        {
            // CreateIsolatedContext plutôt qu'un DbContextOptionsBuilder nu : on conserve
            // EnableRetryOnFailure, le MigrationsAssembly et la journalisation SQL.
            await using var ctx = _contextFactory.CreateIsolatedContext(target.ConnectionString);

            var lastRun = await ctx.Set<AccountingControlRun>().AsNoTracking()
                .Where(r => r.FiscalYear == fiscalYear && r.Status == ControlRunStatus.Completed)
                .OrderByDescending(r => r.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var anomalies = await ctx.Set<AccountingAnomaly>().AsNoTracking()
                .Where(a => a.Run.FiscalYear == fiscalYear
                            && a.Status != AnomalyStatus.Corrected
                            && a.Status != AnomalyStatus.Ignored)
                .OrderByDescending(a => a.Severity)
                .ThenByDescending(a => a.Amount)
                .Take(MaxAnomaliesPerDossier)
                .Select(a => new AnomalyFacts(
                    a.Id, a.RuleCode, a.ModuleCode, a.Severity, (int)a.Category,
                    a.Title, a.Description, a.AccountRef, a.Amount, (int)a.Status,
                    a.PeriodFrom, a.PeriodTo, a.DetectedAt, a.DeepLinkRoute,
                    a.AssignedToUserId, a.AssignedToUserName))
                .ToListAsync(cancellationToken);

            FirmRevisionNoteDto? note = null;
            if (lastRun is not null)
            {
                var entity = await ctx.Set<AccountingRevisionNote>().AsNoTracking()
                    .FirstOrDefaultAsync(n => n.RunId == lastRun.Id, cancellationToken);
                if (entity is not null) note = RevisionNoteMapper.ToDto(entity);
            }

            return new DossierRevisionSnapshot
            {
                CompanyTenantId = target.CompanyTenantId,
                CompanyName = target.CompanyName,
                AssignedAccountantUserId = target.AccountantUserId,
                AssignedAccountantName = target.AccountantName,
                ComplianceRate = lastRun?.ComplianceRate,
                LastScanAt = lastRun?.CompletedAt,
                NeverScanned = lastRun is null,
                Anomalies = anomalies,
                Note = note,
                ReadFailed = false
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Lecture de révision impossible pour le dossier {TenantId}", target.CompanyTenantId);
            return failed;
        }
    }

    // ── Projections ───────────────────────────────────────────────────────────────────────

    private static decimal SumImpact(IEnumerable<AnomalyFacts> anomalies) =>
        MillimeRounding.Round(
            AuditAmountSemantics.SumImpacts(anomalies.Select(a => (a.RuleCode, a.Amount))));

    private static FirmRevisionDossierRowDto ToRow(DossierRevisionSnapshot s)
    {
        var blocking = s.Anomalies.Count(a => a.Severity == BlockingSeverity);
        var warning = s.Anomalies.Count(a => a.Severity == WarningSeverity);
        var info = s.Anomalies.Count(a => a.Severity == InfoSeverity);

        return new FirmRevisionDossierRowDto
        {
            CompanyTenantId = s.CompanyTenantId,
            CompanyName = s.CompanyName,
            AssignedAccountantName = s.AssignedAccountantName,
            AssignedAccountantUserId = s.AssignedAccountantUserId,
            RiskScore = ComputeRiskScore(blocking, warning, info, s.NeverScanned, s.ReadFailed),
            BlockingCount = blocking,
            WarningCount = warning,
            InfoCount = info,
            TotalAnomalies = s.Anomalies.Count,
            ImpactAmount = SumImpact(s.Anomalies),
            ComplianceRate = s.ComplianceRate,
            LastScanAt = s.LastScanAt,
            ReadFailed = s.ReadFailed,
            NeverScanned = s.NeverScanned,
            HasRevisionNote = s.Note is not null
        };
    }

    /// <summary>
    /// Indice de risque, purement calculé. Un bloquant pèse dix avertissements : c'est ce qui
    /// empêche un dossier criblé d'informations mineures de passer devant un dossier qui ne
    /// bouclera pas.
    ///
    /// <para>Un dossier jamais balayé reçoit un plancher élevé : l'absence d'anomalie n'y signifie
    /// pas la conformité, seulement l'ignorance — et c'est précisément ce qu'il faut traiter.</para>
    /// </summary>
    private static int ComputeRiskScore(int blocking, int warning, int info, bool neverScanned, bool readFailed)
    {
        if (readFailed) return 0;
        var score = blocking * 10 + warning * 3 + info;
        return neverScanned ? Math.Max(score, 50) : score;
    }

    private static IReadOnlyList<FirmRevisionFamilySliceDto> BuildFamilySlices(
        IReadOnlyList<AnomalyFacts> anomalies) =>
        anomalies
            .GroupBy(a => a.ModuleCode, StringComparer.Ordinal)
            .Select(g => new FirmRevisionFamilySliceDto
            {
                ModuleCode = g.Key,
                Label = AccountingAuditModuleCatalog.LabelFor(g.Key) ?? g.Key,
                Count = g.Count(),
                BlockingCount = g.Count(a => a.Severity == BlockingSeverity),
                ImpactAmount = SumImpact(g)
            })
            .OrderByDescending(s => s.BlockingCount)
            .ThenByDescending(s => s.Count)
            .ToList();

    private static AccountingAnomalyListItemDto ToListItem(AnomalyFacts a) => new()
    {
        Id = a.Id,
        RuleCode = a.RuleCode,
        ModuleCode = a.ModuleCode,
        Severity = a.Severity,
        Category = a.Category,
        Title = a.Title,
        Description = a.Description,
        DetailSummary = a.Description,
        AccountRef = a.AccountRef,
        PeriodFrom = a.PeriodFrom,
        PeriodTo = a.PeriodTo,
        Amount = a.Amount,
        Status = a.Status,
        AssignedToUserId = a.AssignedToUserId,
        AssignedToUserName = a.AssignedToUserName,
        DeepLinkRoute = a.DeepLinkRoute,
        LineCount = 0,
        DetectedAt = a.DetectedAt
    };

    private static FirmFanOutHealthDto BuildFanOut(IReadOnlyList<DossierRevisionSnapshot> snapshots) => new()
    {
        DossiersRead = snapshots.Count(s => !s.ReadFailed),
        DossiersFailed = snapshots.Count(s => s.ReadFailed)
    };

    // ── Types internes ────────────────────────────────────────────────────────────────────

    private readonly record struct DossierTarget(
        Guid CompanyTenantId,
        string CompanyName,
        string? ConnectionString,
        Guid? AccountantUserId,
        string? AccountantName);

    /// <summary>Projection plate d'une anomalie : le contexte du dossier est fermé avant l'usage.</summary>
    private sealed record AnomalyFacts(
        Guid Id,
        string RuleCode,
        string ModuleCode,
        int Severity,
        int Category,
        string Title,
        string Description,
        string? AccountRef,
        decimal Amount,
        int Status,
        DateOnly? PeriodFrom,
        DateOnly? PeriodTo,
        DateTime DetectedAt,
        string? DeepLinkRoute,
        Guid? AssignedToUserId,
        string? AssignedToUserName);

    private sealed class DossierRevisionSnapshot
    {
        public Guid CompanyTenantId { get; init; }
        public string CompanyName { get; init; } = string.Empty;
        public Guid? AssignedAccountantUserId { get; init; }
        public string? AssignedAccountantName { get; init; }
        public decimal? ComplianceRate { get; init; }
        public DateTime? LastScanAt { get; init; }
        public bool NeverScanned { get; init; }
        public bool ReadFailed { get; init; }
        public IReadOnlyList<AnomalyFacts> Anomalies { get; init; } = Array.Empty<AnomalyFacts>();
        public FirmRevisionNoteDto? Note { get; init; }
    }
}
