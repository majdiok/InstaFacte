using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Ouvre la base de paie d'un cabinet depuis un service qui travaille sur la base Master.
/// </summary>
/// <remarks>
/// <para>
/// Point de passage unique de la frontière Master → Tenant pour la gouvernance cabinet. Les
/// données du cabinet (collaborateurs, congés, coûts, rentabilité) vivent dans Master et
/// raisonnent en <c>ApplicationUser</c> ; la paie vit dans le tenant du cabinet et raisonne en
/// <c>Payroll.Employee</c>. Chaque service qui franchissait cette frontière le faisait à sa
/// façon, dont l'une construisait son <see cref="TenantDbContext"/> à la main, hors conteneur
/// et hors garde de migration.
/// </para>
/// <para>
/// Le contexte retourné est <b>isolé</b> : il ne s'enrôle jamais dans la transaction ambiante du
/// tenant courant. C'est indispensable ici, où l'appelant est déjà dans une unité de travail
/// Master et où la base ouverte est celle d'un <i>autre</i> périmètre.
/// </para>
/// </remarks>
public sealed class FirmTenantPayrollAccessor
{
    private readonly ITenantService _tenantService;
    private readonly ITenantDbContextFactory _contextFactory;

    public FirmTenantPayrollAccessor(ITenantService tenantService, ITenantDbContextFactory contextFactory)
    {
        _tenantService = tenantService;
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Ouvre la base de paie du cabinet, ou <c>null</c> si aucune base ne lui est rattachée.
    /// </summary>
    /// <remarks>
    /// Le contexte retourné est à la charge de l'appelant (<c>await using</c>). Les erreurs
    /// d'ouverture ne sont pas capturées ici : chaque appelant décide s'il dégrade (lecture de
    /// coût, miroir de congé) ou s'il échoue (provisionnement).
    /// </remarks>
    public async Task<TenantDbContext?> OpenAsync(Guid firmTenantId, CancellationToken cancellationToken = default)
    {
        var connectionString = await _tenantService.GetConnectionStringAsync(firmTenantId, cancellationToken);
        if (string.IsNullOrEmpty(connectionString))
            return null;

        return _contextFactory.CreateIsolatedContext(connectionString);
    }
}
