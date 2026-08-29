using System.Reflection;
using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.API.Controllers;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// T28 — revue de complétude des politiques d'autorisation des endpoints liasse
/// (<c>AccountingController</c>) : GET = <c>AccountingRead</c> ; POST fiscal-result =
/// <c>AccountingCreate</c> ; POST finalize = <c>FirmDelegatedContext</c> (+ défense en profondeur
/// handler) ; PUT/DELETE overrides et PUT paramètres = <c>AccountingCreate</c> ; endpoints T8
/// (GET diagnostic = <c>AccountingRead</c>, POST repair = <c>AccountingCreate</c>).
///
/// Les tests d'intégration HTTP complets (<c>WebApplicationFactory</c>) ne démarrent pas dans cet
/// environnement (Hangfire/non-configuré) — on valide donc (1) le câblage exhaustif des politiques
/// par réflexion sur les attributs <c>[Authorize(Policy = …)]</c> de chaque endpoint, et (2) la
/// décision d'autorisation réelle via les handlers (<c>PermissionAuthorizationHandler</c>,
/// <c>FirmDelegatedContextAuthorizationHandler</c>) : un verbe par endpoint représentatif, 401
/// (anonyme) / 403 (authentifié sans permission) / 200 (contrôle positif avec permission).
/// </summary>
public sealed class LiasseAuthorizationPolicyTests
{
    // ── Câblage exhaustif : chaque endpoint liasse porte la politique attendue ───────────────

    public static TheoryData<string, string> LiasseEndpointPolicies => new()
    {
        // GET (lecture) → AccountingRead
        { nameof(AccountingController.GetNctStatements), PermissionPolicies.AccountingRead },
        { nameof(AccountingController.GetFiscalAdjustmentCatalog), PermissionPolicies.AccountingRead },
        { nameof(AccountingController.GetFiscalResult), PermissionPolicies.AccountingRead },
        { nameof(AccountingController.ExportFiscalResult), PermissionPolicies.AccountingRead },
        { nameof(AccountingController.ExportConsolidatedLiasse), PermissionPolicies.AccountingRead },
        { nameof(AccountingController.GetNctNoteCatalog), PermissionPolicies.AccountingRead },
        { nameof(AccountingController.GetNctNoteOverrides), PermissionPolicies.AccountingRead },
        { nameof(AccountingController.GetInventoryBook), PermissionPolicies.AccountingRead },
        { nameof(AccountingController.ExportInventoryBook), PermissionPolicies.AccountingRead },
        { nameof(AccountingController.GetIncomeTaxParameters), PermissionPolicies.AccountingRead },
        // T8 — diagnostic à-nouveaux (lecture seule)
        { nameof(AccountingController.DiagnoseOpeningEntries), PermissionPolicies.AccountingRead },

        // POST fiscal-result (upsert brouillon) → AccountingCreate
        { nameof(AccountingController.UpsertFiscalResult), PermissionPolicies.AccountingCreate },
        // PUT/DELETE overrides → AccountingCreate
        { nameof(AccountingController.UpsertNctNoteOverride), PermissionPolicies.AccountingCreate },
        { nameof(AccountingController.DeleteNctNoteOverride), PermissionPolicies.AccountingCreate },
        // PUT paramètres fiscaux → AccountingCreate
        { nameof(AccountingController.UpdateIncomeTaxParameters), PermissionPolicies.AccountingCreate },
        // T8 — réparation à-nouveaux (mutation)
        { nameof(AccountingController.RepairOpeningEntries), PermissionPolicies.AccountingCreate },

        // POST finalize → FirmDelegatedContext (cabinet en mode dossier client délégué)
        { nameof(AccountingController.FinalizeFiscalResult), PermissionPolicies.FirmDelegatedContext },
    };

    [Theory]
    [MemberData(nameof(LiasseEndpointPolicies))]
    public void Liasse_endpoint_has_expected_authorize_policy(string methodName, string expectedPolicy)
    {
        var policy = GetAuthorizePolicy(methodName);
        Assert.Equal(expectedPolicy, policy);
    }

    // ── Décision d'autorisation par verbe (401 / 403 / 200) ─────────────────────────────────

    [Fact]
    public async Task Get_Liasse_Endpoint_Enforces_AccountingRead_401_403()
    {
        var policy = GetAuthorizePolicy(nameof(AccountingController.GetFiscalResult));
        Assert.Equal(PermissionPolicies.AccountingRead, policy);
        var permission = await ResolvePermission(policy); // "accounting:read"

        // 401 — anonyme (non authentifié) → refusé.
        Assert.False(await PermissionSatisfied(permission, Anonymous()));
        // 403 — authentifié SANS accounting:read (permèse étrangère) → refusé.
        Assert.False(await PermissionSatisfied(permission, AuthenticatedWith(Permissions.Clients.Read)));
        // 200 — authentifié AVEC accounting:read → autorisé (contrôle positif).
        Assert.True(await PermissionSatisfied(permission, AuthenticatedWith(permission)));
    }

    [Fact]
    public async Task PostFiscalResult_Endpoint_Enforces_AccountingCreate_401_403()
    {
        var policy = GetAuthorizePolicy(nameof(AccountingController.UpsertFiscalResult));
        Assert.Equal(PermissionPolicies.AccountingCreate, policy);
        var permission = await ResolvePermission(policy); // "accounting:create"

        Assert.False(await PermissionSatisfied(permission, Anonymous()));                       // 401
        Assert.False(await PermissionSatisfied(permission, AuthenticatedWith(Permissions.Clients.Read))); // 403
        Assert.True(await PermissionSatisfied(permission, AuthenticatedWith(permission)));     // 200
    }

    [Fact]
    public async Task PutParameters_Endpoint_Enforces_AccountingCreate_401_403()
    {
        var policy = GetAuthorizePolicy(nameof(AccountingController.UpdateIncomeTaxParameters));
        Assert.Equal(PermissionPolicies.AccountingCreate, policy);
        var permission = await ResolvePermission(policy);

        Assert.False(await PermissionSatisfied(permission, Anonymous()));
        Assert.False(await PermissionSatisfied(permission, AuthenticatedWith(Permissions.Clients.Read)));
        Assert.True(await PermissionSatisfied(permission, AuthenticatedWith(permission)));
    }

    [Fact]
    public async Task DeleteOverride_Endpoint_Enforces_AccountingCreate_401_403()
    {
        var policy = GetAuthorizePolicy(nameof(AccountingController.DeleteNctNoteOverride));
        Assert.Equal(PermissionPolicies.AccountingCreate, policy);
        var permission = await ResolvePermission(policy);

        Assert.False(await PermissionSatisfied(permission, Anonymous()));
        Assert.False(await PermissionSatisfied(permission, AuthenticatedWith(Permissions.Clients.Read)));
        Assert.True(await PermissionSatisfied(permission, AuthenticatedWith(permission)));
    }

    [Fact]
    public async Task T8_RepairEndpoint_Enforces_AccountingCreate_401_403()
    {
        var policy = GetAuthorizePolicy(nameof(AccountingController.RepairOpeningEntries));
        Assert.Equal(PermissionPolicies.AccountingCreate, policy);
        var permission = await ResolvePermission(policy);

        Assert.False(await PermissionSatisfied(permission, Anonymous()));
        Assert.False(await PermissionSatisfied(permission, AuthenticatedWith(Permissions.Clients.Read)));
        Assert.True(await PermissionSatisfied(permission, AuthenticatedWith(permission)));
    }

    [Fact]
    public async Task T8_DiagnoseEndpoint_Enforces_AccountingRead_401_403()
    {
        var policy = GetAuthorizePolicy(nameof(AccountingController.DiagnoseOpeningEntries));
        Assert.Equal(PermissionPolicies.AccountingRead, policy);
        var permission = await ResolvePermission(policy);

        Assert.False(await PermissionSatisfied(permission, Anonymous()));
        Assert.False(await PermissionSatisfied(permission, AuthenticatedWith(Permissions.Clients.Read)));
        Assert.True(await PermissionSatisfied(permission, AuthenticatedWith(permission)));
    }

    [Fact]
    public async Task Finalize_Endpoint_Enforces_FirmDelegatedContext_401_403()
    {
        var policy = GetAuthorizePolicy(nameof(AccountingController.FinalizeFiscalResult));
        Assert.Equal(PermissionPolicies.FirmDelegatedContext, policy);

        // 401 — anonyme.
        Assert.False(await DelegatedContextSatisfied(Anonymous()));
        // 403 — entreprise native (hors cabinet) → refusé.
        Assert.False(await DelegatedContextSatisfied(FirmUser("company", "native")));
        // 403 — cabinet en mode natif (pas de dossier client délégué) → refusé.
        Assert.False(await DelegatedContextSatisfied(FirmUser("accountingFirm", "native")));
        // 403 — cabinet délégué SANS identifiant de tenant client → refusé.
        Assert.False(await DelegatedContextSatisfied(FirmUser("accountingFirm", "delegated")));
        // 200 — cabinet en mode dossier client délégué + tenant client → autorisé.
        Assert.True(await DelegatedContextSatisfied(
            FirmUser("accountingFirm", "delegated", Guid.NewGuid().ToString())));

        // Défense en profondeur : le handler de finalisation re-vérifie IsAccountingFirmDelegatedContext
        // et rend Error.Forbidden (→ 403) même si la policy était contournée — couvert par
        // FiscalResultFinalizationTests.Finalize_NotDelegatedContext_ReturnsForbidden.
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Lit la politique <c>[Authorize(Policy = …)]</c> (non vide) d'un endpoint liasse.</summary>
    private static string GetAuthorizePolicy(string methodName)
    {
        var methods = typeof(AccountingController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == methodName)
            .ToList();
        Assert.NotEmpty(methods);

        // Le contrôleur porte aussi un [Authorize] au niveau classe (Policy = null) : on ne retient
        // que les attributs à politique explicite (au niveau méthode).
        var policies = methods
            .SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>())
            .Where(a => !string.IsNullOrEmpty(a.Policy))
            .Select(a => a.Policy)
            .Distinct()
            .ToList();
        Assert.Single(policies);
        return policies[0];
    }

    /// <summary>Résout la permission concrète portée par une politique <c>perm:…</c>.</summary>
    private static async Task<string> ResolvePermission(string policyName)
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));
        var policy = await provider.GetPolicyAsync(policyName);
        Assert.NotNull(policy);
        return policy!.Requirements.OfType<PermissionRequirement>().Single().Permission;
    }

    private static Task<bool> PermissionSatisfied(string permission, ClaimsPrincipal user)
    {
        var handler = new PermissionAuthorizationHandler();
        var ctx = new AuthorizationHandlerContext(
            new IAuthorizationRequirement[] { new PermissionRequirement(permission) }, user, resource: null);
        return handler.HandleAsync(ctx).ContinueWith(_ => ctx.HasSucceeded, TaskScheduler.Default);
    }

    private static Task<bool> DelegatedContextSatisfied(ClaimsPrincipal user)
    {
        var handler = new FirmDelegatedContextAuthorizationHandler();
        var ctx = new AuthorizationHandlerContext(
            new IAuthorizationRequirement[] { new FirmDelegatedContextRequirement() }, user, resource: null);
        return handler.HandleAsync(ctx).ContinueWith(_ => ctx.HasSucceeded, TaskScheduler.Default);
    }

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal AuthenticatedWith(params string[] permissions)
    {
        var id = new ClaimsIdentity("Bearer");
        foreach (var p in permissions)
            id.AddClaim(new Claim(AuthClaimTypes.Permission, p));
        return new ClaimsPrincipal(id);
    }

    private static ClaimsPrincipal FirmUser(string tenantKind, string accessMode, string? contextTenantId = null)
    {
        var id = new ClaimsIdentity("Bearer");
        id.AddClaim(new Claim(AuthClaimTypes.TenantKind, tenantKind));
        id.AddClaim(new Claim(AuthClaimTypes.AccessMode, accessMode));
        if (contextTenantId is not null)
            id.AddClaim(new Claim(AuthClaimTypes.ContextTenantId, contextTenantId));
        return new ClaimsPrincipal(id);
    }
}
