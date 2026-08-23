using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Onboarding;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Constants;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ProductOnboarding;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

public sealed class ProductOnboardingService : IProductOnboardingService
{
    private readonly MasterDbContext _master;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantDbContextFactory _tenantDbFactory;
    private readonly ProductOnboardingSettings _settings;
    private readonly ILogger<ProductOnboardingService> _logger;

    public ProductOnboardingService(
        MasterDbContext master,
        ICurrentUser currentUser,
        ITenantDbContextFactory tenantDbFactory,
        IOptions<ProductOnboardingSettings> settings,
        ILogger<ProductOnboardingService> logger)
    {
        _master = master;
        _currentUser = currentUser;
        _tenantDbFactory = tenantDbFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ProductOnboardingDto> GetMineAsync(CancellationToken cancellationToken)
    {
        var user = await LoadCurrentUserAsync(cancellationToken);
        if (user is null)
            return DisabledEmpty();

        var autoIds = await CollectAutoCompletedIdsSafeAsync(user, cancellationToken);
        return MapDto(user, autoIds);
    }

    public async Task<Result<ProductOnboardingDto>> PatchMineAsync(
        PatchProductOnboardingRequest request,
        CancellationToken cancellationToken)
    {
        var user = await LoadCurrentUserAsync(cancellationToken);
        if (user is null)
            return Result.Failure<ProductOnboardingDto>(Error.Unauthorized("Aucun utilisateur authentifié."));

        if (request.Status is { } status && !Enum.IsDefined(status))
            return Result.Failure<ProductOnboardingDto>(Error.Validation("Status", "Statut d'accueil invalide."));

        var checklist = ProductOnboardingChecklistSerializer.Deserialize(user.ProductOnboardingChecklistJson);

        if (request.Status is { } nextStatus)
            user.ProductOnboardingStatus = nextStatus;

        if (request.ChecklistDismissed is { } dismissed)
            checklist = checklist with { Dismissed = dismissed };

        if (!string.IsNullOrWhiteSpace(request.ChecklistDoneId))
            checklist = ProductOnboardingChecklistSerializer.WithDoneId(checklist, request.ChecklistDoneId);

        user.ProductOnboardingChecklistJson = ProductOnboardingChecklistSerializer.Serialize(checklist);
        user.ProductOnboardingUpdatedAt = DateTime.UtcNow;
        await _master.SaveChangesAsync(cancellationToken);

        var autoIds = await CollectAutoCompletedIdsSafeAsync(user, cancellationToken);
        return Result.Success(MapDto(user, autoIds));
    }

    private async Task<ApplicationUser?> LoadCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
            return null;

        return await _master.Users.FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);
    }

    private ProductOnboardingDto MapDto(ApplicationUser user, IReadOnlyList<string> autoIds) =>
        new()
        {
            Enabled = _settings.Enabled,
            Status = user.ProductOnboardingStatus,
            Version = user.ProductOnboardingVersion,
            Checklist = ProductOnboardingChecklistSerializer.Deserialize(user.ProductOnboardingChecklistJson),
            AutoCompletedIds = autoIds
        };

    private ProductOnboardingDto DisabledEmpty() =>
        new()
        {
            Enabled = false,
            Status = ProductOnboardingStatus.Completed,
            Version = ProductOnboardingDefaults.CatalogVersion,
            Checklist = new ProductOnboardingChecklistDto { Dismissed = true },
            AutoCompletedIds = Array.Empty<string>()
        };

    private async Task<IReadOnlyList<string>> CollectAutoCompletedIdsSafeAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CollectAutoCompletedIdsAsync(user, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product onboarding auto-progress failed for user {UserId}", user.Id);
            return Array.Empty<string>();
        }
    }

    private async Task<IReadOnlyList<string>> CollectAutoCompletedIdsAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var ids = new List<string>();
        var tenant = await _master.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == user.TenantId, cancellationToken);
        if (tenant is null)
            return ids;

        var otherUsers = await _master.Users.AsNoTracking()
            .CountAsync(u => u.TenantId == user.TenantId && u.IsActive && u.Id != user.Id, cancellationToken);

        if (tenant.Kind == TenantKind.AccountingFirm)
        {
            if (otherUsers > 0)
                ids.Add(ProductOnboardingDefaults.FirmItemIds.AddCollaborator);

            var hasDossier = await _master.FirmClientAssignments.AsNoTracking()
                .AnyAsync(
                    a => a.FirmTenantId == tenant.Id
                         && (a.Status == FirmAssignmentStatus.Active
                             || a.Status == FirmAssignmentStatus.PendingFirmApproval),
                    cancellationToken);
            if (hasDossier)
                ids.Add(ProductOnboardingDefaults.FirmItemIds.ClientDossier);

            var profile = await _master.AccountingFirmProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == tenant.Id, cancellationToken);
            if (profile is not null
                && (!string.IsNullOrWhiteSpace(profile.Description)
                    || !string.IsNullOrWhiteSpace(profile.ProfessionalRegistrationNumber)))
            {
                ids.Add(ProductOnboardingDefaults.FirmItemIds.FirmSettings);
            }

            return ids;
        }

        if (otherUsers > 0)
            ids.Add(ProductOnboardingDefaults.CompanyItemIds.InviteUser);

        if (_currentUser.IsAccountingFirmDelegatedContext)
            return ids;

        await using var tenantDb = _tenantDbFactory.CreateContext();

        var companyComplete = await tenantDb.Companies.AsNoTracking()
            .AnyAsync(
                c => c.IsActive
                     && (c.LogoUrl != null
                         || c.BankName != null
                         || c.Rib != null
                         || c.CommerceRegistry != null),
                cancellationToken);
        if (companyComplete)
            ids.Add(ProductOnboardingDefaults.CompanyItemIds.CompanyProfile);

        var passengerEmail = DefaultPassengerClient.Email.Trim().ToLowerInvariant();
        var hasClient = await tenantDb.Clients.AsNoTracking()
            .AnyAsync(c => c.Email.Value != passengerEmail, cancellationToken);
        if (hasClient)
            ids.Add(ProductOnboardingDefaults.CompanyItemIds.CreateClient);

        var hasProduct = await tenantDb.Products.AsNoTracking()
            .AnyAsync(cancellationToken);
        if (hasProduct)
            ids.Add(ProductOnboardingDefaults.CompanyItemIds.CreateProduct);

        var hasInvoice = await tenantDb.Invoices.AsNoTracking()
            .AnyAsync(cancellationToken);
        if (hasInvoice)
            ids.Add(ProductOnboardingDefaults.CompanyItemIds.CreateInvoice);

        return ids;
    }
}
