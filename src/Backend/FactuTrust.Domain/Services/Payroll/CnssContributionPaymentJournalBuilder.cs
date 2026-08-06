using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Construit l'écriture de décaissement CNSS : débit 453 / crédit trésorerie.
/// </summary>
public static class CnssContributionPaymentJournalBuilder
{
    public const string SocialOrgAccount = PayrollJournalEntryBuilder.SocialOrgAccount;

    public static Result<IReadOnlyList<JournalLineInput>> BuildPaymentLines(
        decimal amount,
        string label,
        string creditAccount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(creditAccount);

        var rounded = R(amount);
        if (rounded <= 0)
        {
            return Result.Failure<IReadOnlyList<JournalLineInput>>(Error.Validation(
                "Amount",
                "Le montant du versement CNSS doit être strictement positif."));
        }

        var entryLabel = label.Trim();
        var lines = new List<JournalLineInput>
        {
            new(SocialOrgAccount, entryLabel, rounded, 0, null, ThirdPartyKind.None),
            new(creditAccount, entryLabel, 0, rounded, null, ThirdPartyKind.None)
        };

        return Result.Success<IReadOnlyList<JournalLineInput>>(lines);
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
