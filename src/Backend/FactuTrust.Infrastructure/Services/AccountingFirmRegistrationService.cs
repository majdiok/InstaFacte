using System.Diagnostics;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using DomainEmail = FactuTrust.Domain.ValueObjects.Email;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services;

public sealed class AccountingFirmRegistrationService : IAccountingFirmRegistrationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly MasterDbContext _masterContext;
    private readonly ITenantService _tenantService;
    private readonly ITenantAuthTokenService _tokenService;
    private readonly ILogger<AccountingFirmRegistrationService> _logger;

    public AccountingFirmRegistrationService(
        UserManager<ApplicationUser> userManager,
        MasterDbContext masterContext,
        ITenantService tenantService,
        ITenantAuthTokenService tokenService,
        ILogger<AccountingFirmRegistrationService> logger)
    {
        _userManager = userManager;
        _masterContext = masterContext;
        _tenantService = tenantService;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<AccountingFirmRegistrationResult> RegisterAsync(
        RegisterAccountingFirmDto dto,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var totalSw = Stopwatch.StartNew();

        if (dto.Password != dto.ConfirmPassword)
            return AccountingFirmRegistrationResult.ValidationFailure("Les mots de passe ne correspondent pas");

        var nifResult = NIF.Create(dto.Nif);
        if (nifResult.IsFailure)
            return AccountingFirmRegistrationResult.ValidationFailure(nifResult.Error.Description);

        var addressResult = Address.Create(dto.Street, dto.City, dto.Governorate, dto.StreetLine2, dto.PostalCode);
        if (addressResult.IsFailure)
            return AccountingFirmRegistrationResult.ValidationFailure(addressResult.Error.Description);

        var emailResult = DomainEmail.Create(dto.FirmEmail);
        if (emailResult.IsFailure)
            return AccountingFirmRegistrationResult.ValidationFailure(emailResult.Error.Description);

        var phoneResult = PhoneNumber.Create(dto.Phone);
        if (phoneResult.IsFailure)
            return AccountingFirmRegistrationResult.ValidationFailure(phoneResult.Error.Description);

        var emailCheckSw = Stopwatch.StartNew();
        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        LogStep("EmailPrecheck", emailCheckSw.ElapsedMilliseconds, null, correlationId);
        if (existingUser is not null)
            return AccountingFirmRegistrationResult.DuplicateEmailFailure();

        string? provisionedDatabaseName = null;
        Guid? tenantId = null;

        await using var transaction = await _masterContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var tenantResult = Tenant.CreateAccountingFirm(
                dto.FirmName,
                nifResult.Value,
                addressResult.Value,
                emailResult.Value,
                phoneResult.Value,
                dto.Website);

            if (tenantResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return AccountingFirmRegistrationResult.ValidationFailure(tenantResult.Error.Description);
            }

            var tenant = tenantResult.Value;
            tenantId = tenant.Id;
            provisionedDatabaseName = tenant.DatabaseName;

            var masterSw = Stopwatch.StartNew();
            _masterContext.Tenants.Add(tenant);

            var subscription = Subscription.CreateFree(tenant.Id);
            _masterContext.Subscriptions.Add(subscription);

            var profileResult = AccountingFirmProfile.Create(
                tenant.Id,
                dto.FirmName,
                dto.City,
                dto.Governorate,
                emailResult.Value,
                phoneResult.Value,
                dto.Description,
                dto.ProfessionalRegistrationNumber,
                dto.IsPublicInDirectory);

            if (profileResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return AccountingFirmRegistrationResult.ValidationFailure(profileResult.Error.Description);
            }

            _masterContext.AccountingFirmProfiles.Add(profileResult.Value);
            await _masterContext.SaveChangesAsync(cancellationToken);
            LogStep("MasterEntities", masterSw.ElapsedMilliseconds, tenant.Id, correlationId);

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                TenantId = tenant.Id,
                EmailConfirmed = true
            };

            var identitySw = Stopwatch.StartNew();
            var createResult = await _userManager.CreateAsync(user, dto.Password);
            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                var errorMessage = IdentityErrorTranslator.TranslateToFrench(createResult.Errors);
                return AccountingFirmRegistrationResult.ValidationFailure(errorMessage);
            }

            await _userManager.AddToRoleAsync(user, UserRole.FirmManager.ToString());
            LogStep("IdentityCreate", identitySw.ElapsedMilliseconds, tenant.Id, correlationId);

            var provisionSw = Stopwatch.StartNew();
            await _tenantService.CreateAccountingFirmDatabaseAsync(tenant.Id, tenant.DatabaseName, cancellationToken);
            LogStep("DatabaseProvision", provisionSw.ElapsedMilliseconds, tenant.Id, correlationId);

            await _masterContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var tokenSw = Stopwatch.StartNew();
            var tokens = await _tokenService.GenerateTokensAsync(user.Id, tenant.Id, cancellationToken: cancellationToken);
            LogStep("TokenGeneration", tokenSw.ElapsedMilliseconds, tenant.Id, correlationId);

            totalSw.Stop();
            LogStep("Total", totalSw.ElapsedMilliseconds, tenant.Id, correlationId);

            _logger.LogInformation(
                "Accounting firm {FirmName} registered by {Email}. CorrelationId={CorrelationId} DurationMs={DurationMs}",
                dto.FirmName,
                dto.Email,
                correlationId,
                totalSw.ElapsedMilliseconds);

            return AccountingFirmRegistrationResult.Ok(tokens, totalSw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            if (!string.IsNullOrEmpty(provisionedDatabaseName))
                await _tenantService.TryDropDatabaseAsync(provisionedDatabaseName, cancellationToken);

            _logger.LogError(
                ex,
                "Firm registration failed for {Email}. CorrelationId={CorrelationId}",
                dto.Email,
                correlationId);

            return AccountingFirmRegistrationResult.InternalFailure(correlationId);
        }
    }

    private void LogStep(string step, long durationMs, Guid? tenantId, string correlationId)
    {
        _logger.LogInformation(
            "FirmRegistration.Step={Step} DurationMs={DurationMs} TenantId={TenantId} CorrelationId={CorrelationId}",
            step,
            durationMs,
            tenantId,
            correlationId);
    }
}
