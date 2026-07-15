using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

public sealed class FirmAssignmentService : IFirmAssignmentService
{
    private readonly MasterDbContext _masterContext;

    public FirmAssignmentService(MasterDbContext masterContext)
    {
        _masterContext = masterContext;
    }

    public async Task<IReadOnlyList<AccountingFirmDirectoryItemDto>> SearchDirectoryAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = from profile in _masterContext.AccountingFirmProfiles.AsNoTracking()
                    join tenant in _masterContext.Tenants.AsNoTracking() on profile.TenantId equals tenant.Id
                    where profile.IsPublicInDirectory && tenant.IsActive && tenant.Kind == TenantKind.AccountingFirm
                    select new { profile, tenant };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.profile.DisplayName.ToLower().Contains(term) ||
                x.profile.City.ToLower().Contains(term) ||
                x.profile.Governorate.ToLower().Contains(term));
        }

        return await query
            .OrderBy(x => x.profile.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AccountingFirmDirectoryItemDto
            {
                FirmTenantId = x.profile.TenantId,
                DisplayName = x.profile.DisplayName,
                City = x.profile.City,
                Governorate = x.profile.Governorate,
                Description = x.profile.Description,
                ProfessionalRegistrationNumber = x.profile.ProfessionalRegistrationNumber
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<FirmClientAssignmentDto?> GetCompanyCurrentAssignmentAsync(
        Guid companyTenantId, CancellationToken cancellationToken = default)
    {
        var assignment = await _masterContext.FirmClientAssignments.AsNoTracking()
            .Where(a => a.CompanyTenantId == companyTenantId &&
                (a.Status == FirmAssignmentStatus.PendingFirmApproval || a.Status == FirmAssignmentStatus.Active))
            .OrderByDescending(a => a.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return assignment is null ? null : await MapAssignmentAsync(assignment, cancellationToken);
    }

    public async Task<IReadOnlyList<FirmClientAssignmentDto>> GetCompanyAssignmentHistoryAsync(
        Guid companyTenantId, CancellationToken cancellationToken = default)
    {
        var assignments = await _masterContext.FirmClientAssignments.AsNoTracking()
            .Where(a => a.CompanyTenantId == companyTenantId)
            .OrderByDescending(a => a.RequestedAt)
            .ToListAsync(cancellationToken);

        var result = new List<FirmClientAssignmentDto>();
        foreach (var a in assignments)
            result.Add(await MapAssignmentAsync(a, cancellationToken));
        return result;
    }

    public async Task<Result<FirmClientAssignmentDto>> RequestAssignmentAsync(
        Guid companyTenantId, Guid requestedByUserId, RequestFirmAssignmentDto dto, CancellationToken cancellationToken = default)
    {
        var company = await _masterContext.Tenants.FindAsync([companyTenantId], cancellationToken);
        if (company is null || !company.IsActive || company.Kind != TenantKind.Company)
            return Result.Failure<FirmClientAssignmentDto>(Error.Validation("Company", "Société invalide"));

        var firm = await _masterContext.Tenants.FindAsync([dto.FirmTenantId], cancellationToken);
        if (firm is null || !firm.IsActive || firm.Kind != TenantKind.AccountingFirm)
            return Result.Failure<FirmClientAssignmentDto>(Error.Validation("Firm", "Cabinet comptable invalide"));

        var hasOpen = await _masterContext.FirmClientAssignments
            .AnyAsync(a => a.CompanyTenantId == companyTenantId &&
                (a.Status == FirmAssignmentStatus.PendingFirmApproval || a.Status == FirmAssignmentStatus.Active),
                cancellationToken);
        if (hasOpen)
            return Result.Failure<FirmClientAssignmentDto>(Error.Validation("Assignment", "Une demande ou affectation est déjà en cours"));

        var createResult = FirmClientAssignment.Request(companyTenantId, dto.FirmTenantId, requestedByUserId, dto.Notes);
        if (createResult.IsFailure)
            return Result.Failure<FirmClientAssignmentDto>(createResult.Error);

        _masterContext.FirmClientAssignments.Add(createResult.Value);
        await _masterContext.SaveChangesAsync(cancellationToken);

        var mapped = await MapAssignmentAsync(createResult.Value, cancellationToken);
        return Result.Success(mapped);
    }

    public async Task<Result> RevokeByCompanyAsync(
        Guid companyTenantId, Guid revokedByUserId, CancellationToken cancellationToken = default)
    {
        var assignment = await _masterContext.FirmClientAssignments
            .FirstOrDefaultAsync(a => a.CompanyTenantId == companyTenantId && a.Status == FirmAssignmentStatus.Active, cancellationToken);

        if (assignment is null)
            return Result.Failure(Error.Validation("Assignment", "Aucune affectation active"));

        var revokeResult = assignment.RevokeByCompany(revokedByUserId);
        if (revokeResult.IsFailure)
            return revokeResult;

        await _masterContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<IReadOnlyList<FirmClientAssignmentDto>> GetIncomingInvitationsAsync(
        Guid firmTenantId, CancellationToken cancellationToken = default)
    {
        var assignments = await _masterContext.FirmClientAssignments.AsNoTracking()
            .Where(a => a.FirmTenantId == firmTenantId && a.Status == FirmAssignmentStatus.PendingFirmApproval)
            .OrderByDescending(a => a.RequestedAt)
            .ToListAsync(cancellationToken);

        var result = new List<FirmClientAssignmentDto>();
        foreach (var a in assignments)
            result.Add(await MapAssignmentAsync(a, cancellationToken));
        return result;
    }

    public async Task<IReadOnlyList<FirmClientDossierDto>> GetActiveClientsAsync(
        Guid firmTenantId, CancellationToken cancellationToken = default)
    {
        return await (
            from a in _masterContext.FirmClientAssignments.AsNoTracking()
            join t in _masterContext.Tenants.AsNoTracking() on a.CompanyTenantId equals t.Id
            where a.FirmTenantId == firmTenantId && a.Status == FirmAssignmentStatus.Active
            orderby t.CompanyName
            select new FirmClientDossierDto
            {
                AssignmentId = a.Id,
                CompanyTenantId = a.CompanyTenantId,
                CompanyName = t.CompanyName,
                ActiveSince = a.RespondedAt ?? a.RequestedAt
            }).ToListAsync(cancellationToken);
    }

    public async Task<Result> AcceptAssignmentAsync(
        Guid firmTenantId, Guid assignmentId, Guid respondedByUserId, CancellationToken cancellationToken = default)
    {
        var assignment = await _masterContext.FirmClientAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.FirmTenantId == firmTenantId, cancellationToken);

        if (assignment is null)
            return Result.Failure(Error.NotFound("Assignment", assignmentId));

        var acceptResult = assignment.Accept(respondedByUserId);
        if (acceptResult.IsFailure)
            return acceptResult;

        await _masterContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RejectAssignmentAsync(
        Guid firmTenantId, Guid assignmentId, Guid respondedByUserId, CancellationToken cancellationToken = default)
    {
        var assignment = await _masterContext.FirmClientAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.FirmTenantId == firmTenantId, cancellationToken);

        if (assignment is null)
            return Result.Failure(Error.NotFound("Assignment", assignmentId));

        var rejectResult = assignment.Reject(respondedByUserId);
        if (rejectResult.IsFailure)
            return rejectResult;

        await _masterContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RevokeByFirmAsync(
        Guid firmTenantId, Guid assignmentId, Guid revokedByUserId, CancellationToken cancellationToken = default)
    {
        var assignment = await _masterContext.FirmClientAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.FirmTenantId == firmTenantId, cancellationToken);

        if (assignment is null)
            return Result.Failure(Error.NotFound("Assignment", assignmentId));

        var revokeResult = assignment.RevokeByFirm(revokedByUserId);
        if (revokeResult.IsFailure)
            return revokeResult;

        await _masterContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<bool> HasActiveAssignmentAsync(
        Guid firmTenantId, Guid companyTenantId, CancellationToken cancellationToken = default)
    {
        return await _masterContext.FirmClientAssignments.AsNoTracking()
            .AnyAsync(a =>
                a.FirmTenantId == firmTenantId &&
                a.CompanyTenantId == companyTenantId &&
                a.Status == FirmAssignmentStatus.Active,
                cancellationToken);
    }

    public async Task<AccountingFirmProfileDto?> GetFirmProfileAsync(
        Guid firmTenantId, CancellationToken cancellationToken = default)
    {
        var profile = await _masterContext.AccountingFirmProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == firmTenantId, cancellationToken);

        return profile is null ? null : MapProfile(profile);
    }

    public async Task<Result> UpdateFirmProfileAsync(
        Guid firmTenantId, UpdateAccountingFirmProfileDto dto, CancellationToken cancellationToken = default)
    {
        var profile = await _masterContext.AccountingFirmProfiles
            .FirstOrDefaultAsync(p => p.TenantId == firmTenantId, cancellationToken);

        if (profile is null)
            return Result.Failure(Error.Validation("Profile", "Profil cabinet introuvable"));

        profile.Update(
            dto.DisplayName,
            dto.City,
            dto.Governorate,
            dto.ContactEmail,
            dto.ContactPhone,
            dto.Description,
            dto.ProfessionalRegistrationNumber,
            dto.IsPublicInDirectory);

        await _masterContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<FirmClientAssignmentDto> MapAssignmentAsync(
        FirmClientAssignment assignment, CancellationToken cancellationToken)
    {
        var company = await _masterContext.Tenants.AsNoTracking()
            .FirstAsync(t => t.Id == assignment.CompanyTenantId, cancellationToken);

        var firmProfile = await _masterContext.AccountingFirmProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == assignment.FirmTenantId, cancellationToken);

        var firmName = firmProfile?.DisplayName
            ?? (await _masterContext.Tenants.AsNoTracking().FirstAsync(t => t.Id == assignment.FirmTenantId, cancellationToken)).CompanyName;

        return new FirmClientAssignmentDto
        {
            Id = assignment.Id,
            CompanyTenantId = assignment.CompanyTenantId,
            CompanyName = company.CompanyName,
            FirmTenantId = assignment.FirmTenantId,
            FirmDisplayName = firmName,
            Status = assignment.Status,
            StatusDisplay = assignment.Status.ToDisplayString(),
            RequestedAt = assignment.RequestedAt,
            RespondedAt = assignment.RespondedAt,
            RevokedAt = assignment.RevokedAt,
            Notes = assignment.Notes
        };
    }

    private static AccountingFirmProfileDto MapProfile(AccountingFirmProfile profile) => new()
    {
        TenantId = profile.TenantId,
        DisplayName = profile.DisplayName,
        Description = profile.Description,
        City = profile.City,
        Governorate = profile.Governorate,
        IsPublicInDirectory = profile.IsPublicInDirectory,
        ProfessionalRegistrationNumber = profile.ProfessionalRegistrationNumber,
        ContactEmail = profile.ContactEmail,
        ContactPhone = profile.ContactPhone
    };
}
