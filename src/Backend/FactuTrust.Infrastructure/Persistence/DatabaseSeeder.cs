using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Application.Common.Logging;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Persistence;

/// <summary>
/// Seeds initial data into the database (roles, etc.).
/// </summary>
public static class DatabaseSeeder
{
    /// <summary>
    /// Ensures all required roles exist in the database.
    /// </summary>
    public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("DatabaseSeeder");

        var roles = Enum.GetValues<UserRole>();

        foreach (var role in roles)
        {
            var roleName = role.ToString();
            var roleExists = await roleManager.RoleExistsAsync(roleName);

            if (!roleExists)
            {
                var applicationRole = new ApplicationRole
                {
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant(),
                    Description = GetRoleDescription(role)
                };

                var result = await roleManager.CreateAsync(applicationRole);
                if (result.Succeeded)
                {
                    logger.LogInformation("Role '{RoleName}' created successfully", roleName);
                }
                else
                {
                    var errors = IdentityErrorTranslator.TranslateToFrench(result.Errors);
                    logger.LogError("Failed to create role '{RoleName}': {Errors}", roleName, errors);
                }
            }
            else
            {
                logger.LogDebug("Role '{RoleName}' already exists", roleName);
            }
        }

        await EnsurePlatformRoleAsync(roleManager, logger, PlatformRoles.PlatformAdmin, "Plateforme : administration globale (tenants, migrations)");
    }

    /// <summary>
    /// Seeds default Tunisian fiscal calendar rules (TVA jour 22, report week-end/fériés). Idempotent.
    /// </summary>
    public static async Task SeedFiscalCalendarRulesAsync(MasterDbContext context)
    {
        if (await context.FiscalCalendarRules.AnyAsync())
            return;

        context.FiscalCalendarRules.AddRange(
            FiscalCalendarRule.CreateDefault(
                FiscalObligationType.MonthlyDeclaration,
                dueDayOfMonth: 22,
                monthsAfterPeriod: 1,
                label: "TVA mensuelle — déclaration et paiement"),
            FiscalCalendarRule.CreateDefault(
                FiscalObligationType.QuarterlyVat,
                dueDayOfMonth: 22,
                monthsAfterPeriod: 1,
                label: "TVA trimestrielle — déclaration et paiement"));

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Creates the first platform back-office user when <c>Bootstrap:PlatformAdmin:Email</c> and <c>Password</c> are set (e.g. user secrets or environment variables).
    /// Idempotent: skips if the user already exists with role <see cref="PlatformRoles.PlatformAdmin"/> and no tenant.
    /// </summary>
    public static async Task SeedPlatformAdminAsync(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        using var scope = serviceProvider.CreateScope();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("DatabaseSeeder");

        var section = configuration.GetSection("Bootstrap:PlatformAdmin");
        var email = section["Email"]?.Trim();
        var password = section["Password"]?.Trim();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            logger.LogInformation(
                "Bootstrap plateforme ignoré : renseignez Bootstrap:PlatformAdmin:Email et Bootstrap:PlatformAdmin:Password " +
                "(dotnet user-secrets ou variables d'environnement Bootstrap__PlatformAdmin__Email / __Password) sur le projet API, puis redémarrez.");
            return;
        }

        var firstName = string.IsNullOrWhiteSpace(section["FirstName"])
            ? "Plateforme"
            : section["FirstName"]!.Trim();
        var lastName = string.IsNullOrWhiteSpace(section["LastName"])
            ? "Admin"
            : section["LastName"]!.Trim();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (existing.TenantId != Guid.Empty)
            {
                logger.LogError(
                    "Bootstrap platform admin skipped: email {Email} is already used by a tenant user (TenantId {TenantId}).",
                    LogSanitizer.MaskEmail(email), existing.TenantId);
                return;
            }

            if (await userManager.IsInRoleAsync(existing, PlatformRoles.PlatformAdmin))
            {
                logger.LogInformation("Bootstrap platform admin skipped: user {Email} already exists with PlatformAdmin role.", LogSanitizer.MaskEmail(email));
                return;
            }

            var addRole = await userManager.AddToRoleAsync(existing, PlatformRoles.PlatformAdmin);
            if (!addRole.Succeeded)
            {
                logger.LogError("Bootstrap platform admin: failed to add PlatformAdmin role to {Email}: {Errors}",
                    LogSanitizer.MaskEmail(email), IdentityErrorTranslator.TranslateToFrench(addRole.Errors));
                return;
            }

            logger.LogInformation("Bootstrap platform admin: added PlatformAdmin role to existing user {Email}.", LogSanitizer.MaskEmail(email));
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            TenantId = Guid.Empty,
            EmailConfirmed = true,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            logger.LogError("Bootstrap platform admin: failed to create user {Email}: {Errors}",
                LogSanitizer.MaskEmail(email), IdentityErrorTranslator.TranslateToFrench(createResult.Errors));
            return;
        }

        var roleResult = await userManager.AddToRoleAsync(user, PlatformRoles.PlatformAdmin);
        if (!roleResult.Succeeded)
        {
            logger.LogError("Bootstrap platform admin: user {Email} created but role assignment failed: {Errors}",
                LogSanitizer.MaskEmail(email), IdentityErrorTranslator.TranslateToFrench(roleResult.Errors));
            return;
        }

        logger.LogInformation("Bootstrap platform admin: user {Email} created with PlatformAdmin role.", LogSanitizer.MaskEmail(email));
    }

    /// <summary>
    /// In Development, logs a warning if no platform operator exists (PlatformAdmin + empty tenant), so /api/platform/auth/login is expected to fail until secrets are set.
    /// </summary>
    public static async Task WarnIfNoPlatformOperatorInDevelopmentAsync(
        IServiceProvider serviceProvider,
        IHostEnvironment environment,
        ILogger logger)
    {
        if (!environment.IsDevelopment())
            return;

        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var inRole = await userManager.GetUsersInRoleAsync(PlatformRoles.PlatformAdmin);
        var platformOperators = inRole.Count(u => u.TenantId == Guid.Empty);
        if (platformOperators > 0)
            return;

        logger.LogWarning(
            "Aucun opérateur plateforme (rôle PlatformAdmin, sans entreprise) : la connexion back-office /api/platform/auth/login échouera. " +
            "Depuis le répertoire du projet FactuTrust.API : dotnet user-secrets set \"Bootstrap:PlatformAdmin:Email\" \"votre@email\" ; " +
            "dotnet user-secrets set \"Bootstrap:PlatformAdmin:Password\" \"mot de passe conforme à la politique\" ; puis redémarrer l'API.");
    }

    private static async Task EnsurePlatformRoleAsync(
        RoleManager<ApplicationRole> roleManager,
        ILogger logger,
        string roleName,
        string description)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            logger.LogDebug("Role '{RoleName}' already exists", roleName);
            return;
        }

        var applicationRole = new ApplicationRole
        {
            Name = roleName,
            NormalizedName = roleName.ToUpperInvariant(),
            Description = description
        };

        var result = await roleManager.CreateAsync(applicationRole);
        if (result.Succeeded)
            logger.LogInformation("Role '{RoleName}' created successfully", roleName);
        else
            logger.LogError("Failed to create role '{RoleName}': {Errors}", roleName,
                IdentityErrorTranslator.TranslateToFrench(result.Errors));
    }

    private static string GetRoleDescription(UserRole role) => role switch
    {
        UserRole.Administrator => "Full access to all features and settings",
        UserRole.Accountant => "Can create, edit, and manage invoices and clients",
        UserRole.Client => "Read-only access to their own invoices and payments",
        UserRole.SalesRep => "Can manage clients, quotes, invoices, but no settings or reports",
        UserRole.SalesManager => "Can do everything a SalesRep can, plus reports and deleting sales documents",
        UserRole.Warehouse => "Can manage stock, transfers, inventory, but no access to sales or financials",
        UserRole.Purchaser => "Can manage suppliers and purchase orders/invoices",
        UserRole.Cashier => "Limited to creating invoices/payments and viewing products/stock",
        UserRole.Auditor => "Read-only access to everything",
        UserRole.Supervisor => "Full access like Administrator, except cannot manage users",
        UserRole.Developer => "Low-code Studio: designs custom tables, forms, and reports",
        UserRole.FirmManager => "Accounting firm manager: users, assignments, delegated client access",
        UserRole.FirmAccountant => "Accounting firm staff: delegated client accounting access",
        _ => string.Empty
    };
}
