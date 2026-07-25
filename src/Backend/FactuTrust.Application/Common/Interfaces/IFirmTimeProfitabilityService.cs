using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Marge dossier = BudgetAnnuel − (Hours × PrixHoraire). Formules Décisiel SuiviFeuilleDeTemps.
/// Source: AzureFiparco BCOWEB BCO.Presentation (SuiviFeuilleDeTemps / FeuilleDeTempsRepository).
/// </summary>
public interface IFirmTimeProfitabilityService
{
    Task<FirmDossierTimeProfitabilityReportDto> GetDossierTimeProfitabilityAsync(
        Guid firmTenantId,
        string? companySearch,
        int? year,
        Guid? collaboratorUserId,
        FirmMarginSignFilter marginFilter,
        CancellationToken cancellationToken = default);

    Task<Result<FirmDossierYearBudgetDto>> UpsertYearBudgetAsync(
        Guid firmTenantId,
        Guid assignmentId,
        int year,
        decimal budgetAnnuel,
        CancellationToken cancellationToken = default);

    Task<FirmHourlyRateSettingsDto> GetHourlyRateSettingsAsync();

    Task<byte[]> ExportDossierTimeProfitabilityPdfAsync(
        Guid firmTenantId,
        string? companySearch,
        int? year,
        Guid? collaboratorUserId,
        FirmMarginSignFilter marginFilter,
        CancellationToken cancellationToken = default);
}
