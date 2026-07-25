using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

public sealed class FirmDossierAccessService : IFirmDossierAccessService
{
    public const string NotAssignedMessage = "Vous n'êtes pas affecté à ce dossier client.";
    public const string InactiveAssignmentMessage = "Affectation cabinet inactive ou expirée.";

    private readonly MasterDbContext _master;

    public FirmDossierAccessService(MasterDbContext master)
    {
        _master = master;
    }

    public async Task<bool> CanAccessClientDossierAsync(
        Guid firmTenantId,
        FirmDossierAccessScope scope,
        Guid companyTenantId,
        CancellationToken cancellationToken = default)
    {
        if (firmTenantId == Guid.Empty || companyTenantId == Guid.Empty || scope.UserId == Guid.Empty)
            return false;

        if (scope.IsFirmManager)
        {
            return await _master.FirmClientAssignments.AsNoTracking()
                .AnyAsync(a =>
                    a.FirmTenantId == firmTenantId &&
                    a.CompanyTenantId == companyTenantId &&
                    a.Status == FirmAssignmentStatus.Active,
                    cancellationToken);
        }

        if (!scope.IsFirmAccountant)
            return false;

        return await (
            from a in _master.FirmClientAssignments.AsNoTracking()
            join p in _master.PermanentFiles.AsNoTracking()
                on a.Id equals p.FirmClientAssignmentId
            where a.FirmTenantId == firmTenantId
                  && a.CompanyTenantId == companyTenantId
                  && a.Status == FirmAssignmentStatus.Active
                  && p.FirmTenantId == firmTenantId
                  && p.AssignedAccountantUserId == scope.UserId
            select a.Id).AnyAsync(cancellationToken);
    }

    public async Task<bool> CanAccessAssignmentAsync(
        Guid firmTenantId,
        FirmDossierAccessScope scope,
        Guid firmClientAssignmentId,
        CancellationToken cancellationToken = default)
    {
        if (firmTenantId == Guid.Empty || firmClientAssignmentId == Guid.Empty || scope.UserId == Guid.Empty)
            return false;

        if (scope.IsFirmManager)
        {
            return await _master.FirmClientAssignments.AsNoTracking()
                .AnyAsync(a =>
                    a.Id == firmClientAssignmentId &&
                    a.FirmTenantId == firmTenantId &&
                    a.Status == FirmAssignmentStatus.Active,
                    cancellationToken);
        }

        if (!scope.IsFirmAccountant)
            return false;

        return await (
            from a in _master.FirmClientAssignments.AsNoTracking()
            join p in _master.PermanentFiles.AsNoTracking()
                on a.Id equals p.FirmClientAssignmentId
            where a.Id == firmClientAssignmentId
                  && a.FirmTenantId == firmTenantId
                  && a.Status == FirmAssignmentStatus.Active
                  && p.FirmTenantId == firmTenantId
                  && p.AssignedAccountantUserId == scope.UserId
            select a.Id).AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>?> GetAccessibleCompanyTenantIdsAsync(
        Guid firmTenantId,
        FirmDossierAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        if (!scope.RequiresAccountantAssignmentFilter)
            return null;

        var ids = await (
            from a in _master.FirmClientAssignments.AsNoTracking()
            join p in _master.PermanentFiles.AsNoTracking()
                on a.Id equals p.FirmClientAssignmentId
            where a.FirmTenantId == firmTenantId
                  && a.Status == FirmAssignmentStatus.Active
                  && p.FirmTenantId == firmTenantId
                  && p.AssignedAccountantUserId == scope.UserId
            select a.CompanyTenantId).ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    public async Task<IReadOnlySet<Guid>?> GetAccessibleAssignmentIdsAsync(
        Guid firmTenantId,
        FirmDossierAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        if (!scope.RequiresAccountantAssignmentFilter)
            return null;

        var ids = await (
            from a in _master.FirmClientAssignments.AsNoTracking()
            join p in _master.PermanentFiles.AsNoTracking()
                on a.Id equals p.FirmClientAssignmentId
            where a.FirmTenantId == firmTenantId
                  && a.Status == FirmAssignmentStatus.Active
                  && p.FirmTenantId == firmTenantId
                  && p.AssignedAccountantUserId == scope.UserId
            select a.Id).ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }
}
