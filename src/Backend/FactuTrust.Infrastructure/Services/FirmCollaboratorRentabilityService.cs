using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.FirmGovernance;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Marge sur coût direct d'un collaborateur : CA produit − coût employeur − quote-part de structure.
/// </summary>
/// <remarks>
/// Le CA est la quote-part des honoraires de chaque dossier, répartie au prorata des heures
/// réellement saisies. Le modèle antérieur créditait la totalité des honoraires au comptable
/// nominalement assigné, ce qui rendait la marge par collaborateur non représentative du travail
/// fourni. Les charges IT et d'exploitation ne sont plus calculées : aucune source de données
/// fiable n'existe pour les alimenter, et le solde est nommé « marge sur coût direct » plutôt que
/// « rentabilité » pour ne pas le faire passer pour un résultat net.
/// </remarks>
public sealed class FirmCollaboratorRentabilityService : IFirmCollaboratorRentabilityService
{
    private readonly MasterDbContext _master;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<FirmCollaboratorRentabilityService> _logger;

    public FirmCollaboratorRentabilityService(
        MasterDbContext master,
        UserManager<ApplicationUser> userManager,
        ILogger<FirmCollaboratorRentabilityService> logger)
    {
        _master = master;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<FirmCollaboratorRentabilityListDto> ListAsync(
        Guid firmTenantId,
        int? year,
        Guid? collaboratorUserId,
        CancellationToken cancellationToken = default)
    {
        var q = _master.Set<FirmCollaboratorRentability>().AsNoTracking()
            .Include(r => r.Lines)
            .Where(r => r.FirmTenantId == firmTenantId);
        if (year.HasValue)
            q = q.Where(r => r.Year == year.Value);
        if (collaboratorUserId.HasValue)
            q = q.Where(r => r.CollaboratorUserId == collaboratorUserId.Value);

        var rows = await q.OrderByDescending(r => r.Year).ThenBy(r => r.CollaboratorDisplayName).ToListAsync(cancellationToken);
        var items = rows.Select(MapListItem).ToList();
        var totals = new FirmCollaboratorRentabilityListItemDto
        {
            Id = Guid.Empty,
            CollaboratorUserId = Guid.Empty,
            CollaboratorName = "Total",
            Year = year ?? 0,
            AttachedCollaboratorsCount = items.Sum(i => i.AttachedCollaboratorsCount),
            CompaniesCount = items.Sum(i => i.CompaniesCount),
            TotalRevenue = items.Sum(i => i.TotalRevenue),
            PayrollCost = items.Sum(i => i.PayrollCost),
            AdminPayrollCharge = items.Sum(i => i.AdminPayrollCharge),
            ItManagementCharge = items.Sum(i => i.ItManagementCharge),
            OperatingCharge = items.Sum(i => i.OperatingCharge),
            ClientDebitBalance = items.Sum(i => i.ClientDebitBalance),
            ClientCreditBalance = items.Sum(i => i.ClientCreditBalance),
            Rentability = items.Sum(i => i.Rentability),
            CollectedRentability = items.Sum(i => i.CollectedRentability),
            RecoveryRatePercent = ComputeRecoveryRate(
                items.Sum(i => i.TotalRevenue), items.Sum(i => i.ClientDebitBalance))
        };

        var warnings = await BuildTotalsWarningsAsync(rows, cancellationToken);
        return new FirmCollaboratorRentabilityListDto { Items = items, Totals = totals, Warnings = warnings };
    }

    /// <summary>
    /// Signale les additions trompeuses plutôt que de les laisser passer en silence.
    /// </summary>
    /// <remarks>
    /// Les charges admin, IT et exploitation sont réparties par manager et multipliées par son
    /// effectif rattaché. Si un manager et l'un de ses rattachés ont chacun un snapshot sur
    /// l'exercice, la ligne « Totaux » compte deux fois les mêmes charges.
    /// </remarks>
    private async Task<IReadOnlyList<string>> BuildTotalsWarningsAsync(
        IReadOnlyList<FirmCollaboratorRentability> rows, CancellationToken cancellationToken)
    {
        if (rows.Count < 2)
            return Array.Empty<string>();

        var warnings = new List<string>();
        var byYear = rows.GroupBy(r => r.Year);

        foreach (var yearGroup in byYear)
        {
            var userIds = yearGroup.Select(r => r.CollaboratorUserId).Distinct().ToList();
            if (userIds.Count < 2)
                continue;

            var overlapping = await _master.FirmCollaboratorLinks.AsNoTracking()
                .Where(l => userIds.Contains(l.ParentUserId) && userIds.Contains(l.ChildUserId))
                .Select(l => l.ParentUserId)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (overlapping.Count == 0)
                continue;

            var names = yearGroup
                .Where(r => overlapping.Contains(r.CollaboratorUserId))
                .Select(r => r.CollaboratorDisplayName)
                .Distinct()
                .ToList();

            warnings.Add(
                $"Exercice {yearGroup.Key} : {string.Join(", ", names)} et leurs rattachés ont chacun un snapshot. "
                + "Les charges admin, IT et exploitation sont comptées plusieurs fois dans la ligne Totaux.");
        }

        return warnings;
    }

    private static decimal ComputeRecoveryRate(decimal totalRevenue, decimal clientDebitBalance)
    {
        if (totalRevenue <= 0)
            return 0m;
        var collected = totalRevenue - clientDebitBalance;
        return MillimeRounding.Round(Math.Clamp(collected / totalRevenue * 100m, 0m, 100m));
    }

    public async Task<FirmCollaboratorRentabilityDetailDto?> GetByIdAsync(
        Guid firmTenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _master.Set<FirmCollaboratorRentability>()
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == id && r.FirmTenantId == firmTenantId, cancellationToken);
        if (entity is null) return null;

        var prefill = await BuildLivePrefillAsync(firmTenantId, entity.CollaboratorUserId, entity.Year, cancellationToken);
        return MergeSnapshotWithLive(entity, prefill);
    }

    public async Task<Result<FirmCollaboratorRentabilityDetailDto>> GetPrefillAsync(
        Guid firmTenantId, Guid collaboratorUserId, int year, CancellationToken cancellationToken = default)
    {
        var exists = await _master.Set<FirmCollaboratorRentability>().AsNoTracking()
            .AnyAsync(r => r.FirmTenantId == firmTenantId && r.CollaboratorUserId == collaboratorUserId && r.Year == year, cancellationToken);
        if (exists)
            return Result.Failure<FirmCollaboratorRentabilityDetailDto>(
                Error.Conflict("Une rentabilité existe déjà pour ce collaborateur et cette année."));

        var detail = await BuildLivePrefillAsync(firmTenantId, collaboratorUserId, year, cancellationToken);
        return Result.Success(detail);
    }

    public async Task<Result<FirmCollaboratorRentabilityDetailDto>> SaveAsync(
        Guid firmTenantId,
        SaveFirmCollaboratorRentabilityDto dto,
        Guid? existingId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(dto.CollaboratorUserId.ToString());
        if (user is null || user.TenantId != firmTenantId)
            return Result.Failure<FirmCollaboratorRentabilityDetailDto>(Error.NotFound("User", dto.CollaboratorUserId));

        FirmCollaboratorRentability entity;
        if (existingId.HasValue)
        {
            entity = await _master.Set<FirmCollaboratorRentability>()
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.Id == existingId && r.FirmTenantId == firmTenantId, cancellationToken)
                ?? null!;
            if (entity is null)
                return Result.Failure<FirmCollaboratorRentabilityDetailDto>(Error.NotFound("Rentability", existingId.Value));
        }
        else
        {
            var duplicate = await _master.Set<FirmCollaboratorRentability>().AsNoTracking()
                .AnyAsync(r =>
                    r.FirmTenantId == firmTenantId
                    && r.CollaboratorUserId == dto.CollaboratorUserId
                    && r.Year == dto.Year, cancellationToken);
            if (duplicate)
                return Result.Failure<FirmCollaboratorRentabilityDetailDto>(
                    Error.Conflict("Une rentabilité existe déjà pour ce collaborateur et cette année."));

            var create = FirmCollaboratorRentability.Create(
                firmTenantId,
                dto.CollaboratorUserId,
                $"{user.FirstName} {user.LastName}".Trim(),
                dto.Year);
            if (create.IsFailure)
                return Result.Failure<FirmCollaboratorRentabilityDetailDto>(create.Error);
            entity = create.Value;
            _master.Set<FirmCollaboratorRentability>().Add(entity);
        }

        var lines = new List<FirmCollaboratorRentabilityLine>
        {
            FirmCollaboratorRentabilityLine.Create(entity.Id, FirmRentabilityReference.TotalRevenue, dto.TotalRevenue),
            FirmCollaboratorRentabilityLine.Create(entity.Id, FirmRentabilityReference.PayrollCost, dto.PayrollCost),
            FirmCollaboratorRentabilityLine.Create(entity.Id, FirmRentabilityReference.AdminPayrollCharge, dto.AdminPayrollCharge),
            FirmCollaboratorRentabilityLine.Create(entity.Id, FirmRentabilityReference.ItManagementCharge, dto.ItManagementCharge),
            FirmCollaboratorRentabilityLine.Create(entity.Id, FirmRentabilityReference.OperatingCharge, dto.OperatingCharge),
            FirmCollaboratorRentabilityLine.Create(entity.Id, FirmRentabilityReference.ClientDebitBalance, dto.ClientDebitBalance),
            FirmCollaboratorRentabilityLine.Create(entity.Id, FirmRentabilityReference.ClientCreditBalance, dto.ClientCreditBalance)
        };

        if (dto.PayrollRows is { Count: > 0 })
        {
            foreach (var row in dto.PayrollRows)
            {
                lines.Add(FirmCollaboratorRentabilityLine.Create(entity.Id, FirmRentabilityReference.PayrollGross, row.GrossSalary, row.CollaboratorUserId));
                lines.Add(FirmCollaboratorRentabilityLine.Create(entity.Id, FirmRentabilityReference.EmployerContributions, row.EmployerContributions, row.CollaboratorUserId));
                lines.Add(FirmCollaboratorRentabilityLine.Create(entity.Id, FirmRentabilityReference.PayrollExtras, row.PayrollExtras, row.CollaboratorUserId));
            }
        }

        // Remove tracked old lines
        if (existingId.HasValue)
        {
            var old = entity.Lines.ToList();
            _master.Set<FirmCollaboratorRentabilityLine>().RemoveRange(old);
        }

        lines.Add(FirmCollaboratorRentabilityLine.Create(
            entity.Id, FirmRentabilityReference.PortfolioCount, dto.CompaniesCount));
        lines.Add(FirmCollaboratorRentabilityLine.Create(
            entity.Id, FirmRentabilityReference.AttachedCollaboratorsCount, dto.AttachedCollaboratorsCount));

        // ReplaceLines déclenche Recalculate() sur les lignes réellement persistées : recalculer ici
        // à partir du DTO ferait diverger la rentabilité stockée de la masse salariale affichée
        // dès que le détail de paie est renseigné sans report du total.
        entity.ReplaceLines(lines);
        await _master.SaveChangesAsync(cancellationToken);

        var detail = await GetByIdAsync(firmTenantId, entity.Id, cancellationToken);
        return Result.Success(detail!);
    }

    public async Task<Result> DeleteAsync(Guid firmTenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _master.Set<FirmCollaboratorRentability>()
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == id && r.FirmTenantId == firmTenantId, cancellationToken);
        if (entity is null)
            return Result.Failure(Error.NotFound("Rentability", id));

        _master.Set<FirmCollaboratorRentabilityLine>().RemoveRange(entity.Lines);
        _master.Set<FirmCollaboratorRentability>().Remove(entity);
        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<DuplicateFirmRentabilityResultDto>> DuplicateAsync(
        Guid firmTenantId,
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var sources = await _master.Set<FirmCollaboratorRentability>()
            .Include(r => r.Lines)
            .Where(r => r.FirmTenantId == firmTenantId && ids.Contains(r.Id))
            .ToListAsync(cancellationToken);

        var duplicated = 0;
        var skipped = 0;
        foreach (var src in sources)
        {
            var targetYear = src.Year + 1;
            var exists = await _master.Set<FirmCollaboratorRentability>().AsNoTracking()
                .AnyAsync(r =>
                    r.FirmTenantId == firmTenantId
                    && r.CollaboratorUserId == src.CollaboratorUserId
                    && r.Year == targetYear, cancellationToken);
            if (exists)
            {
                skipped++;
                continue;
            }

            var live = await BuildLivePrefillAsync(firmTenantId, src.CollaboratorUserId, targetYear, cancellationToken);
            var create = FirmCollaboratorRentability.Create(
                firmTenantId, src.CollaboratorUserId, src.CollaboratorDisplayName, targetYear);
            if (create.IsFailure) continue;

            var entity = create.Value;
            var admin = src.GetTotal(FirmRentabilityReference.AdminPayrollCharge);
            var it = src.GetTotal(FirmRentabilityReference.ItManagementCharge);
            var expl = src.GetTotal(FirmRentabilityReference.OperatingCharge);
            var lines = new List<FirmCollaboratorRentabilityLine>
            {
                FirmCollaboratorRentabilityLine.Create(entity.Id, FirmRentabilityReference.TotalRevenue, live.CalculatedTotalRevenue),
                FirmCollaboratorRentabilityLine.Create(entity.Id, FirmRentabilityReference.AdminPayrollCharge, admin),
                FirmCollaboratorRentabilityLine.Create(entity.Id, FirmRentabilityReference.ItManagementCharge, it),
                FirmCollaboratorRentabilityLine.Create(entity.Id, FirmRentabilityReference.OperatingCharge, expl),
                FirmCollaboratorRentabilityLine.Create(entity.Id, FirmRentabilityReference.ClientDebitBalance, live.ClientDebitBalance),
                FirmCollaboratorRentabilityLine.Create(entity.Id, FirmRentabilityReference.ClientCreditBalance, live.ClientCreditBalance),
                FirmCollaboratorRentabilityLine.Create(entity.Id, FirmRentabilityReference.PortfolioCount, live.Portfolio.Count),
                FirmCollaboratorRentabilityLine.Create(
                    entity.Id,
                    FirmRentabilityReference.AttachedCollaboratorsCount,
                    src.GetTotal(FirmRentabilityReference.AttachedCollaboratorsCount) is var attached && attached > 0
                        ? attached
                        : live.AttachedCollaboratorsCount)
            };

            // Le détail par collaborateur est reporté avec son rattachement : ne copier que
            // l'agrégat perdrait la ventilation et ferait diverger la masse salariale du snapshot.
            var payrollDetails = src.Lines
                .Where(l => l.ReferenceCode is FirmRentabilityReference.PayrollGross
                    or FirmRentabilityReference.EmployerContributions
                    or FirmRentabilityReference.PayrollExtras)
                .ToList();

            if (payrollDetails.Count > 0)
            {
                lines.AddRange(payrollDetails.Select(l => FirmCollaboratorRentabilityLine.Create(
                    entity.Id, l.ReferenceCode, l.Value, l.LineCollaboratorUserId)));
            }
            else
            {
                lines.Add(FirmCollaboratorRentabilityLine.Create(
                    entity.Id, FirmRentabilityReference.PayrollCost, src.GetPayrollCost()));
            }

            entity.ReplaceLines(lines);
            _master.Set<FirmCollaboratorRentability>().Add(entity);
            duplicated++;
        }

        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success(new DuplicateFirmRentabilityResultDto { Duplicated = duplicated, Skipped = skipped });
    }

    /// <summary>
    /// Recalcule toutes les marges enregistrées selon le modèle de marge sur coût direct.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Déclenché explicitement par un manager, jamais au démarrage ni depuis une migration : un
    /// recalcul massif qui modifie des chiffres déjà communiqués ne doit pas s'exécuter à l'insu de
    /// l'utilisateur.
    /// </para>
    /// <para>
    /// <b>Garde-fou</b> : un exercice sans aucune feuille de temps produirait un chiffre d'affaires
    /// nul et écraserait des snapshots valides. Ces exercices sont laissés intacts et remontés avec
    /// leur motif plutôt que recalculés.
    /// </para>
    /// </remarks>
    public async Task<Result<FirmRentabilityRecalculationResultDto>> RecalculateAllAsync(
        Guid firmTenantId,
        bool isManager,
        CancellationToken cancellationToken = default)
    {
        if (!isManager)
            return Result.Failure<FirmRentabilityRecalculationResultDto>(
                Error.Forbidden("Seul un manager peut recalculer les marges."));

        var snapshots = await _master.Set<FirmCollaboratorRentability>()
            .Include(r => r.Lines)
            .Where(r => r.FirmTenantId == firmTenantId)
            .ToListAsync(cancellationToken);
        if (snapshots.Count == 0)
            return Result.Success(new FirmRentabilityRecalculationResultDto());

        var recalculated = 0;
        var skipped = new List<FirmRentabilitySkippedSnapshotDto>();

        // Une passe par exercice : la répartition est identique pour tous les collaborateurs
        // d'une même année.
        foreach (var yearGroup in snapshots.GroupBy(r => r.Year).OrderBy(g => g.Key))
        {
            var allocation = await BuildYearAllocationAsync(firmTenantId, yearGroup.Key, cancellationToken);

            if (!allocation.HasTimeSheets)
            {
                skipped.AddRange(yearGroup.Select(s => new FirmRentabilitySkippedSnapshotDto
                {
                    Id = s.Id,
                    CollaboratorName = s.CollaboratorDisplayName,
                    Year = s.Year,
                    Reason = "Aucune feuille de temps sur cet exercice — snapshot laissé intact."
                }));
                continue;
            }

            foreach (var snapshot in yearGroup)
            {
                var live = await BuildLivePrefillAsync(
                    firmTenantId, snapshot.CollaboratorUserId, snapshot.Year, cancellationToken);

                // Impérativement avant les SetLine : chacun déclenche Recalculate(), et la valeur
                // d'origine serait perdue si on la capturait après.
                snapshot.MarkRecalculated();

                snapshot.SetLine(FirmRentabilityReference.TotalRevenue, live.TotalRevenue);
                snapshot.SetLine(FirmRentabilityReference.AdminPayrollCharge, live.AdminPayrollCharge);
                // Le modèle ne produit plus ces deux charges : on les remet à zéro pour que la
                // marge stockée corresponde exactement à ce que l'écran affiche.
                snapshot.SetLine(FirmRentabilityReference.ItManagementCharge, 0m);
                snapshot.SetLine(FirmRentabilityReference.OperatingCharge, 0m);
                snapshot.SetLine(FirmRentabilityReference.PortfolioCount, live.Portfolio.Count);
                recalculated++;
            }
        }

        await _master.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Recalcul des marges du cabinet {TenantId} : {Recalculated} snapshot(s) recalculé(s), {Skipped} ignoré(s)",
            firmTenantId, recalculated, skipped.Count);

        return Result.Success(new FirmRentabilityRecalculationResultDto
        {
            Recalculated = recalculated,
            Skipped = skipped
        });
    }

    /// <summary>Quote-part d'honoraires d'un dossier, ventilée par collaborateur.</summary>
    private sealed record DossierAllocation(
        Guid AssignmentId,
        string CompanyName,
        decimal Budget,
        decimal TotalHours,
        IReadOnlyDictionary<Guid, decimal> HoursByCollaborator,
        IReadOnlyDictionary<Guid, decimal> RevenueByCollaborator);

    /// <summary>Photographie d'un exercice : qui a produit quoi, et qui absorbe quelle structure.</summary>
    private sealed record FirmYearAllocation(
        IReadOnlyList<DossierAllocation> Dossiers,
        IReadOnlyDictionary<Guid, decimal> SupportShareByCollaborator,
        decimal SupportTotal,
        bool HasTimeSheets);

    /// <summary>
    /// Calcule, pour tout le cabinet et un exercice, la répartition des honoraires et de la structure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Calcul à l'échelle du cabinet et non du seul collaborateur : le dénominateur d'une quote-part
    /// est le total des heures passées sur le dossier <b>par tout le monde</b>, et la répartition de
    /// la structure suppose de connaître l'ensemble des productifs. Une vue partielle produirait des
    /// quote-parts fausses.
    /// </para>
    /// <para>
    /// Le résultat est réutilisé tel quel par le recalcul global, qui ne le calcule donc qu'une fois
    /// par exercice.
    /// </para>
    /// </remarks>
    private async Task<FirmYearAllocation> BuildYearAllocationAsync(
        Guid firmTenantId, int year, CancellationToken cancellationToken)
    {
        // Un dossier archivé, à mission résiliée ou dont l'affectation n'est plus active ne produit
        // plus d'honoraires : l'y inclure gonflerait artificiellement le chiffre d'affaires.
        var activeAssignmentIds = await _master.FirmClientAssignments.AsNoTracking()
            .Where(a => a.FirmTenantId == firmTenantId && a.Status == FirmAssignmentStatus.Active)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var files = await _master.PermanentFiles.AsNoTracking()
            .Where(p => p.FirmTenantId == firmTenantId
                        && p.Status != PermanentFileStatus.Archived
                        && !p.MissionResigned
                        && activeAssignmentIds.Contains(p.FirmClientAssignmentId))
            .Select(p => new
            {
                p.FirmClientAssignmentId,
                p.CompanyName,
                p.AnnualFeeAmount,
                p.AssignedAccountantUserId
            })
            .ToListAsync(cancellationToken);

        var eligibleAssignmentIds = files.Select(f => f.FirmClientAssignmentId).ToList();

        var timeRows = await _master.FirmTimeSheetEntries.AsNoTracking()
            .Where(t => t.FirmTenantId == firmTenantId
                        && t.WorkDate.Year == year
                        && t.FirmClientAssignmentId != null
                        && eligibleAssignmentIds.Contains(t.FirmClientAssignmentId.Value))
            .Select(t => new { AssignmentId = t.FirmClientAssignmentId!.Value, t.UserId, t.Hours })
            .ToListAsync(cancellationToken);

        var yearBudgets = await _master.Set<FirmDossierYearBudget>().AsNoTracking()
            .Where(b => b.FirmTenantId == firmTenantId
                        && b.Year == year
                        && eligibleAssignmentIds.Contains(b.FirmClientAssignmentId))
            .ToListAsync(cancellationToken);

        var dossiers = new List<DossierAllocation>();
        foreach (var group in timeRows.GroupBy(t => t.AssignmentId))
        {
            var file = files.First(f => f.FirmClientAssignmentId == group.Key);
            var budget = yearBudgets.FirstOrDefault(b => b.FirmClientAssignmentId == group.Key)?.BudgetAnnuel
                         ?? file.AnnualFeeAmount
                         ?? 0m;

            var hoursByCollaborator = group
                .GroupBy(t => t.UserId)
                .ToDictionary(g => g.Key, g => g.Sum(t => t.Hours));

            dossiers.Add(new DossierAllocation(
                group.Key,
                file.CompanyName ?? "—",
                budget,
                hoursByCollaborator.Values.Sum(),
                hoursByCollaborator,
                CollaboratorMarginCalculator.AllocateDossierRevenue(budget, hoursByCollaborator)));
        }

        // Est productif tout collaborateur ayant produit des heures sur un dossier client, ou
        // détenant un dossier. Le second critère protège celui qui gère un portefeuille sans avoir
        // saisi ses temps : il ne doit pas basculer en support et voir son coût réparti sur autrui.
        var productiveIds = timeRows.Select(t => t.UserId)
            .Concat(files.Where(f => f.AssignedAccountantUserId.HasValue)
                         .Select(f => f.AssignedAccountantUserId!.Value))
            .Distinct()
            .ToHashSet();

        var activeUserIds = await _master.Users.AsNoTracking()
            .Where(u => u.TenantId == firmTenantId && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var costs = await _master.FirmCollaboratorYearCosts.AsNoTracking()
            .Where(c => c.FirmTenantId == firmTenantId && c.Year == year)
            .ToListAsync(cancellationToken);

        decimal CostOf(Guid userId) =>
            costs.FirstOrDefault(c => c.CollaboratorUserId == userId)?.TotalEmployerCost ?? 0m;

        var supportTotal = MillimeRounding.Round(
            activeUserIds.Where(id => !productiveIds.Contains(id)).Sum(CostOf));

        var directCostByProductive = activeUserIds
            .Where(productiveIds.Contains)
            .ToDictionary(id => id, CostOf);

        return new FirmYearAllocation(
            dossiers,
            CollaboratorMarginCalculator.AllocateSupportCost(supportTotal, directCostByProductive),
            supportTotal,
            timeRows.Count > 0);
    }

    private async Task<FirmCollaboratorRentabilityDetailDto> BuildLivePrefillAsync(
        Guid firmTenantId, Guid collaboratorUserId, int year, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(collaboratorUserId.ToString());
        var name = user is null ? "Collaborateur" : $"{user.FirstName} {user.LastName}".Trim();

        var childIds = await _master.FirmCollaboratorLinks.AsNoTracking()
            .Where(l => l.ParentUserId == collaboratorUserId)
            .Select(l => l.ChildUserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var portfolioUserIds = childIds.Append(collaboratorUserId).Distinct().ToList();
        var headcount = portfolioUserIds.Count;

        var allocation = await BuildYearAllocationAsync(firmTenantId, year, cancellationToken);

        // Le snapshot couvre le collaborateur et ses rattachés : on additionne leurs quote-parts.
        var portfolio = new List<FirmRentabilityPortfolioRowDto>();
        decimal ca = 0;
        foreach (var dossier in allocation.Dossiers)
        {
            var share = MillimeRounding.Round(
                portfolioUserIds.Sum(id => dossier.RevenueByCollaborator.GetValueOrDefault(id)));
            if (share == 0)
                continue;

            var portfolioHours = portfolioUserIds.Sum(id => dossier.HoursByCollaborator.GetValueOrDefault(id));
            ca += share;
            portfolio.Add(new FirmRentabilityPortfolioRowDto
            {
                FirmClientAssignmentId = dossier.AssignmentId,
                CompanyName = dossier.CompanyName,
                CollaboratorName = name,
                AnnualFeeHt = dossier.Budget,
                PortfolioHours = portfolioHours,
                TotalDossierHours = dossier.TotalHours,
                RevenueShare = share
            });
        }
        ca = MillimeRounding.Round(ca);

        var supportShare = MillimeRounding.Round(
            portfolioUserIds.Sum(id => allocation.SupportShareByCollaborator.GetValueOrDefault(id)));

        // Coût employeur réel de chaque membre du portefeuille : saisi par le cabinet ou importé
        // de sa paie.
        var costs = await _master.FirmCollaboratorYearCosts.AsNoTracking()
            .Where(c => c.FirmTenantId == firmTenantId
                        && c.Year == year
                        && portfolioUserIds.Contains(c.CollaboratorUserId))
            .ToListAsync(cancellationToken);

        var payrollRows = new List<FirmRentabilityPayrollRowDto>();
        foreach (var uid in portfolioUserIds)
        {
            var member = uid == collaboratorUserId ? user : await _userManager.FindByIdAsync(uid.ToString());
            var display = member is null
                ? "Collaborateur rattaché"
                : $"{member.FirstName} {member.LastName}".Trim();
            var cost = costs.FirstOrDefault(c => c.CollaboratorUserId == uid);

            payrollRows.Add(new FirmRentabilityPayrollRowDto
            {
                CollaboratorUserId = uid,
                CollaboratorName = string.IsNullOrWhiteSpace(display) ? "Collaborateur rattaché" : display,
                GrossSalary = cost?.GrossAnnualSalary ?? 0m,
                EmployerContributions = cost?.EmployerContributions ?? 0m,
                PayrollExtras = cost?.PayrollExtras ?? 0m
            });
        }

        var calculatedPayrollCost = MillimeRounding.Round(costs.Sum(c => c.TotalEmployerCost));

        return new FirmCollaboratorRentabilityDetailDto
        {
            CollaboratorUserId = collaboratorUserId,
            CollaboratorName = name,
            Year = year,
            AttachedCollaboratorsCount = headcount,
            TotalRevenue = ca,
            CalculatedTotalRevenue = ca,
            PayrollCost = calculatedPayrollCost,
            CalculatedPayrollCost = calculatedPayrollCost,
            AdminPayrollCharge = supportShare,
            CalculatedSupportShare = supportShare,
            ItManagementCharge = 0,
            OperatingCharge = 0,
            ClientDebitBalance = 0,
            ClientCreditBalance = 0,
            Rentability = CollaboratorMarginCalculator.ComputeMargin(ca, calculatedPayrollCost, supportShare),
            Portfolio = portfolio,
            PayrollRows = payrollRows
        };
    }

    private static FirmCollaboratorRentabilityDetailDto MergeSnapshotWithLive(
        FirmCollaboratorRentability entity,
        FirmCollaboratorRentabilityDetailDto live)
    {
        var payrollRows = entity.Lines
            .Where(l => l.LineCollaboratorUserId.HasValue
                        && l.ReferenceCode is FirmRentabilityReference.PayrollGross
                            or FirmRentabilityReference.EmployerContributions
                            or FirmRentabilityReference.PayrollExtras)
            .GroupBy(l => l.LineCollaboratorUserId!.Value)
            .Select(g => new FirmRentabilityPayrollRowDto
            {
                CollaboratorUserId = g.Key,
                CollaboratorName = live.PayrollRows.FirstOrDefault(p => p.CollaboratorUserId == g.Key)?.CollaboratorName ?? "—",
                GrossSalary = g.Where(x => x.ReferenceCode == FirmRentabilityReference.PayrollGross).Sum(x => x.Value),
                EmployerContributions = g.Where(x => x.ReferenceCode == FirmRentabilityReference.EmployerContributions).Sum(x => x.Value),
                PayrollExtras = g.Where(x => x.ReferenceCode == FirmRentabilityReference.PayrollExtras).Sum(x => x.Value)
            })
            .ToList();

        return live with
        {
            Id = entity.Id,
            TotalRevenue = entity.GetTotal(FirmRentabilityReference.TotalRevenue),
            // Règle unique partagée avec Recalculate() et la projection de liste.
            PayrollCost = entity.GetPayrollCost(),
            AdminPayrollCharge = entity.GetTotal(FirmRentabilityReference.AdminPayrollCharge),
            ItManagementCharge = entity.GetTotal(FirmRentabilityReference.ItManagementCharge),
            OperatingCharge = entity.GetTotal(FirmRentabilityReference.OperatingCharge),
            ClientDebitBalance = entity.GetTotal(FirmRentabilityReference.ClientDebitBalance),
            ClientCreditBalance = entity.GetTotal(FirmRentabilityReference.ClientCreditBalance),
            Rentability = entity.Rentability,
            PayrollRows = payrollRows.Count > 0 ? payrollRows : live.PayrollRows
        };
    }

    private static FirmCollaboratorRentabilityListItemDto MapListItem(FirmCollaboratorRentability r) => new()
    {
        Id = r.Id,
        CollaboratorUserId = r.CollaboratorUserId,
        CollaboratorName = r.CollaboratorDisplayName,
        Year = r.Year,
        AttachedCollaboratorsCount = (int)r.GetTotal(FirmRentabilityReference.AttachedCollaboratorsCount) is var ac && ac > 0
            ? ac
            : Math.Max(1, r.Lines.Where(l => l.LineCollaboratorUserId.HasValue).Select(l => l.LineCollaboratorUserId).Distinct().Count()),
        CompaniesCount = (int)r.GetTotal(FirmRentabilityReference.PortfolioCount),
        TotalRevenue = r.GetTotal(FirmRentabilityReference.TotalRevenue),
        // Même règle que Recalculate() : le détail prime sur l'agrégat, sans quoi cette colonne
        // pourrait afficher une masse salariale absente du calcul de la rentabilité.
        PayrollCost = r.GetPayrollCost(),
        AdminPayrollCharge = r.GetTotal(FirmRentabilityReference.AdminPayrollCharge),
        ItManagementCharge = r.GetTotal(FirmRentabilityReference.ItManagementCharge),
        OperatingCharge = r.GetTotal(FirmRentabilityReference.OperatingCharge),
        ClientDebitBalance = r.GetTotal(FirmRentabilityReference.ClientDebitBalance),
        ClientCreditBalance = r.GetTotal(FirmRentabilityReference.ClientCreditBalance),
        Rentability = r.Rentability,
        CollectedRentability = r.GetCollectedRentability(),
        RecoveryRatePercent = ComputeRecoveryRate(
            r.GetTotal(FirmRentabilityReference.TotalRevenue),
            r.GetTotal(FirmRentabilityReference.ClientDebitBalance))
    };

}
