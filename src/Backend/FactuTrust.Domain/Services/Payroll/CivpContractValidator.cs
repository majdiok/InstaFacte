using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Validation des contrats SIVP/CIVP (durée maximale 12 mois).
/// </summary>
public static class CivpContractValidator
{
    public const int MaxSivpDurationMonths = 12;

    public static Result Validate(EmploymentContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        if (contract.Type != ContractType.Sivp)
            return Result.Success();

        var start = contract.CivpStartDate ?? contract.StartDate;
        var end = contract.CivpEndDate ?? contract.EndDate;
        if (!end.HasValue)
            return Result.Failure(Error.Validation("CivpEndDate", "La date de fin CIVP/SIVP est obligatoire."));

        if (end.Value.Date < start.Date)
            return Result.Failure(Error.Validation("CivpEndDate", "La date de fin CIVP ne peut pas être antérieure à la date de début."));

        var months = TerminationIndemnityCalculator.CountFullMonths(start.Date, end.Value.Date);
        if (months > MaxSivpDurationMonths)
        {
            return Result.Failure(Error.Validation(
                "CivpDuration",
                $"La durée maximale d'un contrat SIVP/CIVP est de {MaxSivpDurationMonths} mois."));
        }

        if (contract.CivpStateGrant < 0 || contract.CivpEmployerAllowance < 0)
            return Result.Failure(Error.Validation("CivpGrant", "Les montants CIVP ne peuvent pas être négatifs."));

        return Result.Success();
    }
}
