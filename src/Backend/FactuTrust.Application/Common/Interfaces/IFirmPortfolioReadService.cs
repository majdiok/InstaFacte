using FactuTrust.Application.Common;
using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Lecture consolidée du portefeuille d'un cabinet, au service de l'agent « Chef de mission ».
///
/// <para>
/// <b>Invariant de sécurité.</b> Ce service ne lit JAMAIS <see cref="ICurrentUser"/> : le périmètre
/// d'accès est un paramètre explicite. C'est délibéré. L'idiome fail-closed employé ailleurs
/// (<c>FirmDashboardService</c>, <c>FirmFiscalScheduleService</c>…) retombe sur « aucun filtre »
/// quand <c>ICurrentUser.IsAuthenticated</c> est faux — ce qui est exactement le cas dans un job
/// Hangfire. Un service qui déduirait son périmètre du contexte ambiant donnerait donc
/// silencieusement le portefeuille complet au brief quotidien.
/// </para>
///
/// <para>
/// <b>Invariant d'isolation.</b> Aucune méthode ne passe par <c>ITenantContext</c> : les bases
/// dossiers sont ouvertes explicitement à partir de leur chaîne de connexion. Le contexte ambiant
/// (la base du cabinet lui-même) n'est jamais muté.
/// </para>
/// </summary>
public interface IFirmPortfolioReadService
{
    /// <param name="scope">
    /// Périmètre d'accès du demandeur. <c>null</c> = aucun filtre (responsable de cabinet, ou
    /// exécution système assumée comme le brief quotidien). Un <see cref="FirmDossierAccessScope"/>
    /// de collaborateur restreint aux dossiers qui lui sont affectés.
    /// </param>
    Task<FirmPortfolioOverviewDto> GetOverviewAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        CancellationToken cancellationToken = default);

    /// <inheritdoc cref="GetOverviewAsync"/>
    Task<FirmDeadlineListDto> GetDeadlinesAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        FirmDeadlineQuery query,
        CancellationToken cancellationToken = default);

    /// <inheritdoc cref="GetOverviewAsync"/>
    Task<FirmDossierHealthListDto> GetDossierHealthAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        int topN,
        CancellationToken cancellationToken = default);

    /// <inheritdoc cref="GetOverviewAsync"/>
    Task<FirmCollaboratorWorkloadDto> GetCollaboratorWorkloadAsync(
        Guid firmTenantId,
        FirmDossierAccessScope? scope,
        CancellationToken cancellationToken = default);
}
