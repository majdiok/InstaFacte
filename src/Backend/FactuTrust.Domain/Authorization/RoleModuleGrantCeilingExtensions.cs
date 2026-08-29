using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Authorization;

/// <summary>
/// Extra permission keys allowed on top of <see cref="UserRoleExtensions.GetPermissions"/> when
/// per-user <c>UserModuleGrants</c> exist. Keeps legacy users (no grant rows) unchanged while letting
/// admins extend specific roles with selected modules (hybrid "widened ceiling" model, plan §5.3).
/// </summary>
public static class RoleModuleGrantCeilingExtensions
{
    /// <summary>
    /// Roles whose grant ceiling is unconditionally EMPTY on every module — never the base set,
    /// never an exception. <see cref="UserRole.Client"/> access is granted from the client record
    /// (<c>ClientPortalStaffRules</c>), never configurable here. <see cref="UserRole.FirmManager"/>
    /// and <see cref="UserRole.FirmAccountant"/> use a delegated catalog (<see cref="DelegatedPermissionCatalog"/>)
    /// whose keys happen to overlap module keys — a naive base ∩ module computation would wrongly
    /// declare them grantable.
    /// </summary>
    private static readonly HashSet<UserRole> ExcludedRoles = new()
    {
        UserRole.Client,
        UserRole.FirmManager,
        UserRole.FirmAccountant
    };

    /// <summary>
    /// Permission keys that must never appear in a non-admin role's grant ceiling delta
    /// (i.e. in <c>GetGrantCeiling(role, module) \ base(role)</c>): user management, platform
    /// settings, accounting close. Combined with the "*:delete" suffix rule in
    /// <see cref="IsForbiddenCeilingDeltaKey"/>.
    /// </summary>
    private static readonly HashSet<string> ForbiddenCeilingDeltaKeys = new(StringComparer.Ordinal)
    {
        Permissions.Users.Create,
        Permissions.Users.Read,
        Permissions.Users.Update,
        Permissions.Users.Delete,
        Permissions.Settings.Read,
        Permissions.Settings.Update,
        Permissions.Accounting.Close
    };

    /// <summary>
    /// The single deliberate, documented, historical exception to "never a destructive key in a
    /// delta": Warehouse × Clients has granted the full Clients module (incl. <c>clients:delete</c>)
    /// since before this remediation. Preserved for non-regression; allow-listed explicitly wherever
    /// the forbidden-delta rule is enforced (validation, tests).
    /// </summary>
    public static bool IsHistoricWarehouseClientsException(UserRole role, AppModule module) =>
        role == UserRole.Warehouse && module == AppModule.Clients;

    /// <summary>True for roles whose grant ceiling is always the empty set (see <see cref="ExcludedRoles"/>).</summary>
    public static bool IsExcludedFromModuleGrants(UserRole role) => ExcludedRoles.Contains(role);

    /// <summary>
    /// A permission key that a non-admin role's grant delta may never contain: any of
    /// <see cref="ForbiddenCeilingDeltaKeys"/>, or any key ending in <c>:delete</c> (destructive).
    /// </summary>
    public static bool IsForbiddenCeilingDeltaKey(string permissionKey) =>
        ForbiddenCeilingDeltaKeys.Contains(permissionKey) ||
        permissionKey.EndsWith(":delete", StringComparison.Ordinal);

    /// <summary>Exposes the explicit forbidden-key set (excluding the generic "*:delete" rule) for tests/tooling.</summary>
    public static IReadOnlySet<string> GetForbiddenCeilingDelta() => ForbiddenCeilingDeltaKeys;

    /// <summary>
    /// Adds grant ceiling keys for <paramref name="role"/> for each module marked enabled in <paramref name="moduleGrants"/>.
    /// Strict activation (anti-regression): an extension applies only when the module is
    /// explicitly present in the grants with <c>IsEnabled=true</c>. A module absent from grants —
    /// treated as "enabled" for the base union via <paramref name="defaultMissingModuleToEnabled"/> —
    /// never activates an extension. The single preserved exception is the historic
    /// (Warehouse, Clients) pair, which keeps its legacy "absent = enabled" semantics.
    /// </summary>
    public static void AddExtensionsForEnabledModules(
        UserRole role,
        IReadOnlyDictionary<AppModule, bool> moduleGrants,
        bool defaultMissingModuleToEnabled,
        HashSet<string> ceiling)
    {
        foreach (var module in Enum.GetValues<AppModule>())
        {
            bool enabledForExtension;
            if (IsHistoricWarehouseClientsException(role, module))
            {
                // Legacy behavior preserved: an absent module still counts as enabled here.
                enabledForExtension = !moduleGrants.TryGetValue(module, out var legacyFlag)
                    ? defaultMissingModuleToEnabled
                    : legacyFlag;
            }
            else
            {
                enabledForExtension = moduleGrants.TryGetValue(module, out var flag) && flag;
            }

            if (!enabledForExtension)
                continue;

            foreach (var key in GetAdditionalKeysForGrant(role, module))
                ceiling.Add(key);
        }
    }

    /// <summary>
    /// Permission keys that may apply when this role has the module enabled in grants, beyond the base role set.
    /// Table of deltas — plan §5.3. Excluded roles (<see cref="ExcludedRoles"/>) never reach here with a
    /// non-empty result because <see cref="GetGrantCeiling"/> short-circuits them to the empty set first;
    /// this switch simply has no case for them (defensive: falls through to empty regardless).
    /// </summary>
    private static IEnumerable<string> GetAdditionalKeysForGrant(UserRole role, AppModule module) =>
        (role, module) switch
        {
            // Comptable : saisie ET mise à jour de trésorerie (base : create/read seulement) ; IA.
            (UserRole.Accountant, AppModule.Treasury) => new[] { Permissions.Payments.Update },
            (UserRole.Accountant, AppModule.AI) => new[] { Permissions.AI.Chat },

            // Commercial : trésorerie (saisie + prévisionnel), stock en lecture, assistant IA.
            (UserRole.SalesRep, AppModule.Treasury) => new[]
            {
                Permissions.Payments.Create, Permissions.Payments.Update, Permissions.TreasuryForecast.View
            },
            (UserRole.SalesRep, AppModule.Stock) => new[]
            {
                Permissions.Stock.Read, Permissions.StockTransfers.Read, Permissions.StockVouchers.Read, Permissions.Inventory.Read
            },
            (UserRole.SalesRep, AppModule.AI) => new[] { Permissions.AI.Chat },

            // Responsable Commercial : identiques au Commercial.
            (UserRole.SalesManager, AppModule.Treasury) => new[]
            {
                Permissions.Payments.Create, Permissions.Payments.Update, Permissions.TreasuryForecast.View
            },
            (UserRole.SalesManager, AppModule.Stock) => new[]
            {
                Permissions.Stock.Read, Permissions.StockTransfers.Read, Permissions.StockVouchers.Read, Permissions.Inventory.Read
            },
            (UserRole.SalesManager, AppModule.AI) => new[] { Permissions.AI.Chat },

            // Magasinier : Clients (module complet — exception historique, incl. clients:delete),
            // achats en lecture, rapports.
            (UserRole.Warehouse, AppModule.Clients) => module.GetPermissionKeys(),
            (UserRole.Warehouse, AppModule.Purchases) => new[]
            {
                Permissions.Suppliers.Read, Permissions.PurchaseOrders.Read,
                Permissions.PurchaseReceipts.Read, Permissions.SupplierInvoices.Read
            },
            (UserRole.Warehouse, AppModule.Reports) => new[] { Permissions.Reports.View },

            // Acheteur : trésorerie en lecture, stock (transferts + inventaire), rapports.
            (UserRole.Purchaser, AppModule.Treasury) => new[] { Permissions.Payments.Read },
            (UserRole.Purchaser, AppModule.Stock) => new[] { Permissions.StockTransfers.Read, Permissions.Inventory.Read },
            (UserRole.Purchaser, AppModule.Reports) => new[] { Permissions.Reports.View },

            // Caissier : mise à jour trésorerie, création client au comptoir (jamais delete).
            (UserRole.Cashier, AppModule.Treasury) => new[] { Permissions.Payments.Update },
            (UserRole.Cashier, AppModule.Clients) => new[] { Permissions.Clients.Create, Permissions.Clients.Update },

            // Auditeur : lecture seule uniquement — CRM, stock, RH & paie, contrats récurrents.
            (UserRole.Auditor, AppModule.CRM) => new[] { Permissions.CRM.Read },
            (UserRole.Auditor, AppModule.Stock) => new[] { Permissions.StockTransfers.Read, Permissions.Inventory.Read },
            (UserRole.Auditor, AppModule.Payroll) => new[] { Permissions.Payroll.Read },
            (UserRole.Auditor, AppModule.RecurringContracts) => new[] { Permissions.RecurringContracts.Read },

            // Superviseur : contrats récurrents SANS Delete — les déltas non-admin n'accordent
            // jamais de permission destructive (ForbiddenCeilingDelta).
            (UserRole.Supervisor, AppModule.RecurringContracts) => new[]
            {
                Permissions.RecurringContracts.Read, Permissions.RecurringContracts.Create,
                Permissions.RecurringContracts.Update, Permissions.RecurringContracts.Manage,
                Permissions.RecurringContracts.RecordUsage, Permissions.RecurringContracts.TriggerBilling
            },

            // Développeur : lecture seule uniquement, pour construction de rapports.
            (UserRole.Developer, AppModule.Sales) => new[]
            {
                Permissions.Quotes.Read, Permissions.SalesOrders.Read,
                Permissions.DeliveryNotes.Read, Permissions.SalesReturnNotes.Read
            },
            (UserRole.Developer, AppModule.Purchases) => new[] { Permissions.Suppliers.Read, Permissions.PurchaseOrders.Read },
            (UserRole.Developer, AppModule.Stock) => new[] { Permissions.Stock.Read },
            (UserRole.Developer, AppModule.CRM) => new[] { Permissions.CRM.Read },

            _ => Array.Empty<string>()
        };

    /// <summary>
    /// The maximal set of permission keys grantable to <paramref name="role"/> for <paramref name="module"/>:
    /// <c>(base(role) ∪ GetAdditionalKeysForGrant(role, module)) ∩ GetModulePermissionUniverse(module)</c>.
    /// Excluded roles (<see cref="ExcludedRoles"/>) always return the EMPTY set for every module —
    /// never the base set, never an exception — consistent with the module-catalog and write-time
    /// validation behavior (plan §5.3).
    /// </summary>
    public static IReadOnlySet<string> GetGrantCeiling(UserRole role, AppModule module)
    {
        if (ExcludedRoles.Contains(role))
            return new HashSet<string>();

        var universe = module.GetModulePermissionUniverse();
        var ceiling = new HashSet<string>(role.GetPermissions());
        foreach (var key in GetAdditionalKeysForGrant(role, module))
            ceiling.Add(key);

        ceiling.IntersectWith(universe);
        return ceiling;
    }
}
