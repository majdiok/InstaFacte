using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

public sealed class FirmInternalPayrollProvisioningService : IFirmInternalPayrollProvisioningService
{
    /// <summary>Salaire de base retenu quand la configuration n'en impose aucun.</summary>
    /// <remarks>Valeur historiquement codée en dur : conservée comme repli pour ne rien changer.</remarks>
    private const decimal FallbackProvisionBaseSalary = 1_500m;

    /// <summary>Taux accident du travail retenu quand l'exercice n'en renseigne aucun.</summary>
    private const decimal FallbackWorkAccidentRate = 0.4m;

    private readonly MasterDbContext _master;
    private readonly FirmTenantPayrollAccessor _payrollAccess;
    private readonly IFirmCollaboratorCostService _costs;
    private readonly FirmGovernanceOptions _options;
    private readonly ILogger<FirmInternalPayrollProvisioningService> _logger;

    public FirmInternalPayrollProvisioningService(
        MasterDbContext master,
        FirmTenantPayrollAccessor payrollAccess,
        IFirmCollaboratorCostService costs,
        IOptions<FirmGovernanceOptions> options,
        ILogger<FirmInternalPayrollProvisioningService> logger)
    {
        _master = master;
        _payrollAccess = payrollAccess;
        _costs = costs;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FirmPayrollProvisioningStatusDto> GetProvisioningStatusAsync(
        Guid firmTenantId,
        CancellationToken cancellationToken = default)
    {
        var enabled = FirmGovernanceNativeAccess.IsInternalPayrollEnabled(_options);
        var collaborators = await LoadCollaboratorRowsAsync(firmTenantId, cancellationToken);
        var activePayroll = 0;

        if (enabled)
        {
            await using var tenant = await _payrollAccess.OpenAsync(firmTenantId, cancellationToken);
            if (tenant is not null)
            {
                activePayroll = await tenant.Set<Employee>().AsNoTracking()
                    .CountAsync(e => e.IsActive, cancellationToken);
                collaborators = await EnrichWithPayrollMatchesAsync(tenant, collaborators, cancellationToken);
                collaborators = await EnrichWithIdentityCompletenessAsync(tenant, collaborators, cancellationToken);
            }
        }

        return new FirmPayrollProvisioningStatusDto
        {
            InternalPayrollEnabled = enabled,
            AutoProvisionOnCollaboratorCreate =
                FirmGovernanceNativeAccess.ShouldAutoProvisionPayrollOnCollaboratorCreate(_options),
            ActivePayrollEmployees = activePayroll,
            Collaborators = collaborators,
            CollaboratorsWithIncompleteIdentity = collaborators.Count(c => c.MissingPayrollIdentifiers.Count > 0)
        };
    }

    /// <summary>
    /// Signale les mentions obligatoires absentes des salariés liés (N° CNSS, CIN).
    /// </summary>
    /// <remarks>
    /// Le cabinet découvrait ces manques au moment de la déclaration sociale, une fois la paie
    /// calculée. Les remonter dès l'écran de provisionnement évite d'avoir à rouvrir un cycle.
    /// </remarks>
    private static async Task<List<FirmPayrollCollaboratorProvisioningRowDto>> EnrichWithIdentityCompletenessAsync(
        TenantDbContext tenant,
        List<FirmPayrollCollaboratorProvisioningRowDto> rows,
        CancellationToken cancellationToken)
    {
        var linkedIds = rows
            .Where(r => r.PayrollEmployeeId.HasValue)
            .Select(r => r.PayrollEmployeeId!.Value)
            .Distinct()
            .ToList();
        if (linkedIds.Count == 0)
            return rows;

        var identities = await tenant.Set<Employee>().AsNoTracking()
            .Where(e => linkedIds.Contains(e.Id))
            .Select(e => new { e.Id, e.CnssNumber, e.Cin })
            .ToListAsync(cancellationToken);
        var byId = identities.ToDictionary(e => e.Id);

        return rows.Select(row =>
        {
            if (row.PayrollEmployeeId is not Guid id || !byId.TryGetValue(id, out var identity))
                return row;

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(identity.CnssNumber))
                missing.Add("N° CNSS");
            if (string.IsNullOrWhiteSpace(identity.Cin))
                missing.Add("CIN");

            return missing.Count == 0 ? row : row with { MissingPayrollIdentifiers = missing };
        }).ToList();
    }

    public Task<Result<FirmPayrollProvisionResultDto>> ProvisionFromCollaboratorsAsync(
        Guid firmTenantId,
        bool isManager,
        CancellationToken cancellationToken = default) =>
        ProvisionCoreAsync(firmTenantId, isManager, null, null, cancellationToken);

    public Task<Result<FirmPayrollProvisionResultDto>> ProvisionCollaboratorAsync(
        Guid firmTenantId,
        bool isManager,
        Guid collaboratorUserId,
        FirmCollaboratorPayrollOnboardingDto? onboarding = null,
        CancellationToken cancellationToken = default) =>
        ProvisionCoreAsync(firmTenantId, isManager, collaboratorUserId, onboarding, cancellationToken);

    private async Task<Result<FirmPayrollProvisionResultDto>> ProvisionCoreAsync(
        Guid firmTenantId,
        bool isManager,
        Guid? singleCollaboratorId,
        FirmCollaboratorPayrollOnboardingDto? collaboratorOnboarding,
        CancellationToken cancellationToken)
    {
        if (!isManager)
            return Result.Failure<FirmPayrollProvisionResultDto>(
                Error.Forbidden("Seul un manager peut provisionner la paie interne."));

        if (!FirmGovernanceNativeAccess.IsInternalPayrollEnabled(_options))
            return Result.Failure<FirmPayrollProvisionResultDto>(
                Error.Validation("Feature", "La paie interne du cabinet n'est pas activée."));

        var kind = await _master.Tenants.AsNoTracking()
            .Where(t => t.Id == firmTenantId)
            .Select(t => t.Kind)
            .FirstOrDefaultAsync(cancellationToken);
        if (kind != TenantKind.AccountingFirm)
            return Result.Failure<FirmPayrollProvisionResultDto>(
                Error.Validation("Tenant", "Le tenant n'est pas un cabinet comptable."));

        await using var tenant = await _payrollAccess.OpenAsync(firmTenantId, cancellationToken);
        if (tenant is null)
            return Result.Failure<FirmPayrollProvisionResultDto>(
                Error.Validation("Payroll", "Aucune base de paie n'est rattachée au cabinet."));

        var rows = await LoadCollaboratorRowsAsync(firmTenantId, cancellationToken);
        if (singleCollaboratorId.HasValue)
            rows = rows.Where(r => r.CollaboratorUserId == singleCollaboratorId.Value).ToList();

        var created = 0;
        var linked = 0;
        var skipped = 0;
        var messages = new List<string>();

        var employeesByEmail = await LoadEmployeesByNormalizedEmailAsync(tenant, cancellationToken);
        var linkedEmployeeIds = await (
            from p in _master.FirmCollaboratorProfiles.AsNoTracking()
            join u in _master.Users.AsNoTracking() on p.UserId equals u.Id
            where u.TenantId == firmTenantId && p.PayrollEmployeeId != null
            select p.PayrollEmployeeId!.Value).ToListAsync(cancellationToken);
        var linkedSet = new HashSet<Guid>(linkedEmployeeIds);
        var workAccidentRate = await ResolveWorkAccidentRateAsync(firmTenantId, cancellationToken);
        var baseSalary = ResolveProvisionBaseSalary();
        var pendingLinks = new List<FirmPayrollEmployeeLinkRequest>();

        // ── Phase 1 : préparer, sans rien persister ──
        foreach (var row in rows)
        {
            if (row.HasPayrollEmployee && row.PayrollEmployeeId.HasValue)
            {
                skipped++;
                continue;
            }

            var normalized = NormalizeEmail(row.Email);
            if (normalized is null)
            {
                skipped++;
                messages.Add($"{row.CollaboratorName} : email manquant.");
                continue;
            }

            if (employeesByEmail.TryGetValue(normalized, out var matches) && matches.Count > 1)
            {
                skipped++;
                messages.Add($"{row.CollaboratorName} : email ambigu côté paie.");
                continue;
            }

            Guid employeeId;
            var isNewEmployee = false;
            if (employeesByEmail.TryGetValue(normalized, out var single) && single.Count == 1)
            {
                employeeId = single[0].Id;
            }
            else
            {
                Result<Employee> create;
                if (collaboratorOnboarding is not null && singleCollaboratorId == row.CollaboratorUserId)
                {
                    var user = await _master.Users.AsNoTracking()
                        .FirstAsync(u => u.Id == row.CollaboratorUserId, cancellationToken);
                    create = await CreateEmployeeWithContractFromOnboardingAsync(
                        tenant, user, collaboratorOnboarding, cancellationToken);
                }
                else
                {
                    create = await CreateEmployeeWithContractAsync(
                        tenant, row, baseSalary, workAccidentRate, cancellationToken);
                }

                if (create.IsFailure)
                {
                    skipped++;
                    messages.Add($"{row.CollaboratorName} : {create.Error.Description}");
                    continue;
                }

                var createdEmployee = create.Value;
                employeeId = createdEmployee.Id;
                employeesByEmail[normalized] = new List<Employee> { createdEmployee };
                isNewEmployee = true;
            }

            if (linkedSet.Contains(employeeId))
            {
                skipped++;
                messages.Add($"{row.CollaboratorName} : salarié paie déjà lié à un autre collaborateur.");
                continue;
            }

            linkedSet.Add(employeeId);
            pendingLinks.Add(new FirmPayrollEmployeeLinkRequest(row.CollaboratorUserId, employeeId));
            if (isNewEmployee)
                created++;
            else
                linked++;
        }

        // ── Phase 2 : persister la paie d'abord ──
        // C'est la base distante, celle qui peut réellement échouer. Tant qu'elle n'a pas accepté
        // les salariés, aucune liaison Master ne doit exister : sinon un profil pointerait vers
        // un salarié qui n'a jamais été enregistré.
        if (created > 0)
        {
            try
            {
                await tenant.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Provisionnement de la paie interne échoué pour le cabinet {TenantId} : aucune liaison n'a été posée.",
                    firmTenantId);
                return Result.Failure<FirmPayrollProvisionResultDto>(Error.Validation(
                    "Payroll",
                    "Les salariés n'ont pas pu être enregistrés dans la paie du cabinet. Aucune liaison n'a été créée."));
            }
        }

        // ── Phase 3 : les salariés existent, on peut lier ──
        if (pendingLinks.Count > 0)
        {
            var linkResult = await _costs.LinkPayrollEmployeesAsync(
                firmTenantId,
                isManager: true,
                pendingLinks,
                FirmPayrollLinkSource.ProvisionedFromCollaborator,
                cancellationToken);

            if (linkResult.IsFailure)
                return Result.Failure<FirmPayrollProvisionResultDto>(linkResult.Error);

            var refused = pendingLinks.Count - linkResult.Value.Linked;
            if (refused > 0)
            {
                skipped += refused;
                messages.AddRange(linkResult.Value.Messages);
                // Les compteurs ne doivent pas annoncer des liaisons qui n'ont pas été posées.
                var createdShare = Math.Min(created, refused);
                created -= createdShare;
                linked -= refused - createdShare;
            }
        }

        return Result.Success(new FirmPayrollProvisionResultDto
        {
            Created = created,
            Linked = linked,
            Skipped = skipped,
            Messages = messages
        });
    }

    /// <summary>
    /// Taux accident du travail des paramètres d'exercice, ou le repli historique s'il n'est pas renseigné.
    /// </summary>
    /// <remarks>
    /// Le défaut de <c>FirmTimeSheetYearSettings</c> est 0 % (il dépend de l'activité et doit être
    /// saisi). Reprendre ce 0 tel quel changerait silencieusement le contrat provisionné, d'où le
    /// repli sur la valeur qui était jusqu'ici en dur.
    /// </remarks>
    private async Task<decimal> ResolveWorkAccidentRateAsync(Guid firmTenantId, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var configured = await _master.FirmTimeSheetYearSettings.AsNoTracking()
            .Where(s => s.FirmTenantId == firmTenantId && s.Year == year)
            .Select(s => (decimal?)s.WorkAccidentRate)
            .FirstOrDefaultAsync(cancellationToken);

        return configured is > 0m ? configured.Value : FallbackWorkAccidentRate;
    }

    private decimal ResolveProvisionBaseSalary() =>
        _options.DefaultProvisionBaseSalary > 0m
            ? _options.DefaultProvisionBaseSalary
            : FallbackProvisionBaseSalary;

    private async Task<List<FirmPayrollCollaboratorProvisioningRowDto>> LoadCollaboratorRowsAsync(
        Guid firmTenantId,
        CancellationToken cancellationToken)
    {
        return await (
            from u in _master.Users.AsNoTracking()
            join p in _master.FirmCollaboratorProfiles.AsNoTracking() on u.Id equals p.UserId into profiles
            from p in profiles.DefaultIfEmpty()
            where u.TenantId == firmTenantId && u.IsActive
            orderby u.LastName, u.FirstName
            select new FirmPayrollCollaboratorProvisioningRowDto
            {
                CollaboratorUserId = u.Id,
                CollaboratorName = (u.FirstName + " " + u.LastName).Trim(),
                Email = u.Email,
                HasPayrollEmployee = p != null && p.PayrollEmployeeId != null,
                PayrollEmployeeId = p != null ? p.PayrollEmployeeId : null,
                PayrollLinkSource = p != null ? (int)p.PayrollLinkSource : 0
            }).ToListAsync(cancellationToken);
    }

    private static async Task<List<FirmPayrollCollaboratorProvisioningRowDto>> EnrichWithPayrollMatchesAsync(
        TenantDbContext tenant,
        List<FirmPayrollCollaboratorProvisioningRowDto> rows,
        CancellationToken cancellationToken)
    {
        var employees = await tenant.Set<Employee>().AsNoTracking()
            .Where(e => e.IsActive)
            .Select(e => new { e.Id, Email = e.Email != null ? e.Email.Value : null, e.FirstName, e.LastName })
            .ToListAsync(cancellationToken);

        var byEmail = employees
            .Select(e => new { e.Id, Normalized = NormalizeEmail(e.Email), Name = $"{e.FirstName} {e.LastName}".Trim() })
            .Where(e => e.Normalized is not null)
            .GroupBy(e => e.Normalized!)
            .ToDictionary(g => g.Key, g => g.ToList());

        return rows.Select(row =>
        {
            if (row.HasPayrollEmployee)
                return row;

            var normalized = NormalizeEmail(row.Email);
            if (normalized is null)
                return row with { BlockingReason = "Email collaborateur manquant." };

            if (!byEmail.TryGetValue(normalized, out var matchList))
                return row with { BlockingReason = "Aucun salarié paie avec cet email." };

            if (matchList.Count > 1)
                return row with { BlockingReason = "Plusieurs salariés paie partagent cet email." };

            return row with
            {
                PayrollEmployeeId = matchList[0].Id,
                PayrollEmployeeName = matchList[0].Name,
                BlockingReason = null
            };
        }).ToList();
    }

    private static async Task<Result<Employee>> CreateEmployeeWithContractFromOnboardingAsync(
        TenantDbContext tenant,
        ApplicationUser user,
        FirmCollaboratorPayrollOnboardingDto onboarding,
        CancellationToken cancellationToken)
    {
        var employeeNumber = onboarding.EmployeeNumber.Trim();
        var exists = await tenant.Set<Employee>().AsNoTracking()
            .AnyAsync(e => e.EmployeeNumber == employeeNumber, cancellationToken);
        if (exists)
            return Result.Failure<Employee>(Error.Conflict("Un salarié existe déjà avec ce matricule."));

        var contractDto = onboarding.Contract;
        if (!Enum.TryParse<ContractType>(contractDto.Type, out var contractType))
            return Result.Failure<Employee>(Error.Validation("Type", "Type de contrat invalide."));
        if (!Enum.TryParse<SocialRegime>(contractDto.Regime, out var regime))
            return Result.Failure<Employee>(Error.Validation("Regime", "Régime social invalide."));
        if (!Enum.TryParse<WeeklyWorkRegime>(contractDto.WeeklyRegime, out var weeklyRegime))
            return Result.Failure<Employee>(Error.Validation("WeeklyRegime", "Régime hebdomadaire invalide."));

        var smigError = await ValidateContractSalaryAgainstSmigAsync(tenant, contractDto, cancellationToken);
        if (smigError is not null)
            return Result.Failure<Employee>(smigError);

        Domain.ValueObjects.Email? email = null;
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var emailResult = Domain.ValueObjects.Email.Create(user.Email.Trim());
            if (emailResult.IsFailure)
                return Result.Failure<Employee>(emailResult.Error);
            email = emailResult.Value;
        }

        var employeeResult = Employee.Create(
            employeeNumber,
            user.FirstName.Trim(),
            user.LastName.Trim(),
            onboarding.HireDate.Date,
            email: email);
        if (employeeResult.IsFailure)
            return Result.Failure<Employee>(employeeResult.Error);

        var employee = employeeResult.Value;
        tenant.Set<Employee>().Add(employee);

        var contractResult = EmploymentContract.CreatePublic(
            employee.Id,
            contractType,
            regime,
            contractDto.StartDate.Date,
            contractDto.BaseSalary,
            contractDto.WorkAccidentRate,
            contractDto.EndDate?.Date,
            string.IsNullOrWhiteSpace(contractDto.JobTitle) ? "Collaborateur cabinet" : contractDto.JobTitle.Trim(),
            weeklyRegime,
            contractDto.CivpStartDate?.Date,
            contractDto.CivpEndDate?.Date,
            contractDto.CivpStateGrant,
            contractDto.CivpEmployerAllowance,
            contractDto.AnetiReference);
        if (contractResult.IsFailure)
            return Result.Failure<Employee>(contractResult.Error);

        tenant.Set<EmploymentContract>().Add(contractResult.Value);
        return Result.Success(employee);
    }

    private static async Task<Error?> ValidateContractSalaryAgainstSmigAsync(
        TenantDbContext tenant,
        CreateContractDto contract,
        CancellationToken cancellationToken)
    {
        var fiscalYear = contract.StartDate.Year;
        var parameters = await tenant.Set<PayrollYearParameters>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.FiscalYear == fiscalYear, cancellationToken);
        if (parameters is { EnforceSmigOnContracts: true } && contract.BaseSalary < parameters.MonthlySmig)
        {
            return Error.Validation(
                "BaseSalary",
                $"Le salaire de base ne peut pas être inférieur au SMIG ({parameters.MonthlySmig:N3} TND).");
        }

        return null;
    }

    private static async Task<Result<Employee>> CreateEmployeeWithContractAsync(
        TenantDbContext tenant,
        FirmPayrollCollaboratorProvisioningRowDto row,
        decimal baseSalary,
        decimal workAccidentRate,
        CancellationToken cancellationToken)
    {
        var parts = row.CollaboratorName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = parts.Length > 0 ? parts[0] : row.CollaboratorName;
        var lastName = parts.Length > 1 ? parts[1] : row.CollaboratorName;

        var employeeNumber = await GenerateEmployeeNumberAsync(tenant, row.CollaboratorUserId, cancellationToken);
        Domain.ValueObjects.Email? email = null;
        if (!string.IsNullOrWhiteSpace(row.Email))
        {
            var emailResult = Domain.ValueObjects.Email.Create(row.Email.Trim());
            if (emailResult.IsFailure)
                return Result.Failure<Employee>(emailResult.Error);
            email = emailResult.Value;
        }

        var employeeResult = Employee.Create(
            employeeNumber,
            firstName,
            lastName,
            DateTime.UtcNow.Date,
            email: email);
        if (employeeResult.IsFailure)
            return Result.Failure<Employee>(employeeResult.Error);

        var employee = employeeResult.Value;
        tenant.Set<Employee>().Add(employee);

        var contractResult = EmploymentContract.CreatePublic(
            employee.Id,
            ContractType.Cdi,
            SocialRegime.Rsna,
            DateTime.UtcNow.Date,
            baseSalary,
            workAccidentRate,
            jobTitle: "Collaborateur cabinet");
        if (contractResult.IsFailure)
            return Result.Failure<Employee>(contractResult.Error);

        tenant.Set<EmploymentContract>().Add(contractResult.Value);
        return Result.Success(employee);
    }

    private static async Task<string> GenerateEmployeeNumberAsync(
        TenantDbContext tenant,
        Guid collaboratorUserId,
        CancellationToken cancellationToken)
    {
        var suffix = collaboratorUserId.ToString("N")[..8].ToUpperInvariant();
        var candidate = $"CAB-{suffix}";
        var exists = await tenant.Set<Employee>().AsNoTracking()
            .AnyAsync(e => e.EmployeeNumber == candidate, cancellationToken);
        if (!exists)
            return candidate;

        for (var i = 1; i < 100; i++)
        {
            candidate = $"CAB-{suffix}-{i}";
            exists = await tenant.Set<Employee>().AsNoTracking()
                .AnyAsync(e => e.EmployeeNumber == candidate, cancellationToken);
            if (!exists)
                return candidate;
        }

        return $"CAB-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
    }

    private static async Task<Dictionary<string, List<Employee>>> LoadEmployeesByNormalizedEmailAsync(
        TenantDbContext tenant,
        CancellationToken cancellationToken)
    {
        var employees = await tenant.Set<Employee>().AsNoTracking()
            .Where(e => e.IsActive)
            .ToListAsync(cancellationToken);

        return employees
            .Select(e => new { Employee = e, Normalized = NormalizeEmail(e.Email?.Value) })
            .Where(x => x.Normalized is not null)
            .GroupBy(x => x.Normalized!)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Employee).ToList());
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;
        return email.Trim().ToLowerInvariant();
    }
}
