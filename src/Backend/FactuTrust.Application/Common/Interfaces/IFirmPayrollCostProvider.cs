namespace FactuTrust.Application.Common.Interfaces;

/// <summary>Coût employeur annuel d'un salarié du cabinet, agrégé depuis ses bulletins.</summary>
/// <param name="PayrollEmployeeId">Identifiant du salarié dans la paie du cabinet.</param>
/// <param name="EmployeeName">Nom tel qu'il figure sur les bulletins.</param>
/// <param name="GrossAnnualSalary">Cumul des bruts de l'exercice.</param>
/// <param name="EmployerContributions">CNSS patronale, accident du travail, TFP et FOPROLOS cumulés.</param>
/// <param name="PayslipCount">Nombre de bulletins agrégés, pour détecter une année incomplète.</param>
public sealed record FirmPayrollEmployeeCostDto(
    Guid PayrollEmployeeId,
    string EmployeeName,
    decimal GrossAnnualSalary,
    decimal EmployerContributions,
    int PayslipCount)
{
    public decimal TotalEmployerCost => GrossAnnualSalary + EmployerContributions;
}

/// <summary>Résultat d'un import, y compris lorsqu'il n'a rien pu produire.</summary>
/// <param name="IsAvailable">Faux si la paie du cabinet n'est pas exploitable.</param>
/// <param name="UnavailableReason">Motif à afficher lorsque l'import n'a pas abouti.</param>
public sealed record FirmPayrollCostSnapshotDto(
    bool IsAvailable,
    string? UnavailableReason,
    IReadOnlyList<FirmPayrollEmployeeCostDto> Employees)
{
    public static FirmPayrollCostSnapshotDto Unavailable(string reason) =>
        new(false, reason, Array.Empty<FirmPayrollEmployeeCostDto>());

    public static FirmPayrollCostSnapshotDto Available(IReadOnlyList<FirmPayrollEmployeeCostDto> employees) =>
        new(true, null, employees);
}

/// <summary>
/// Agrège le coût employeur des collaborateurs depuis la paie tenue par le cabinet sur son propre
/// tenant.
/// </summary>
/// <remarks>
/// <para>
/// Fournisseur « au mieux » : la base de paie du cabinet peut être absente, vide ou injoignable.
/// Aucune de ces situations n'est une erreur — l'implémentation retourne
/// <see cref="FirmPayrollCostSnapshotDto.Unavailable"/> et le cabinet retombe sur la saisie
/// manuelle du coût employeur.
/// </para>
/// <para>
/// À la date d'écriture, un tenant de type cabinet dispose bien d'une base tenant mais n'a pas le
/// module Paie parmi ses modules activés : l'import ne produira donc de données qu'à partir du
/// moment où le cabinet tiendra effectivement sa propre paie dans l'application.
/// </para>
/// </remarks>
public interface IFirmPayrollCostProvider
{
    Task<FirmPayrollCostSnapshotDto> GetAnnualEmployerCostsAsync(
        Guid firmTenantId,
        int year,
        CancellationToken cancellationToken = default);
}
