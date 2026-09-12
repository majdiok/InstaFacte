using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Services.Accounting;

namespace FactuTrust.Application.Features.Accounting.Commands;

/// <summary>Lignes prêtes à être comptabilisées, et taux effectivement retenu.</summary>
public sealed record PreparedEntryCurrency(IReadOnlyList<JournalLineInput> Lines, ResolvedExchangeRate Rate);

/// <summary>
/// Étape commune à la création et à la modification d'une écriture manuelle : résolution serveur du
/// taux, puis conversion des montants en devise de tenue.
///
/// <para>
/// <b>Les montants en devise de tenue transmis par le client sont ignorés</b> dès lors que
/// l'écriture est en devise : le serveur les recalcule intégralement depuis les montants en devise
/// et le taux qu'il a lui-même résolu. C'est ce qui empêche un client de fixer la valeur comptable
/// d'une écriture.
/// </para>
/// </summary>
public static class ManualEntryCurrencyPreparation
{
    public static async Task<Result<PreparedEntryCurrency>> PrepareAsync(
        IExchangeRateResolver resolver,
        string? currencyCode,
        decimal? requestedRate,
        DateTime entryDate,
        IReadOnlyList<JournalLineInput> lines,
        CancellationToken cancellationToken)
    {
        var resolved = await resolver.ResolveAsync(currencyCode, entryDate, requestedRate, cancellationToken);
        if (resolved.IsFailure)
            return Result.Failure<PreparedEntryCurrency>(resolved.Error);

        var rate = resolved.Value;

        // Devise de tenue : rien à convertir, les montants transmis font foi comme aujourd'hui.
        if (rate.IsFunctional)
            return Result.Success(new PreparedEntryCurrency(lines, rate));

        var converted = ForeignCurrencyConversion.Convert(lines, rate.Rate);
        if (converted.IsFailure)
            return Result.Failure<PreparedEntryCurrency>(converted.Error);

        return Result.Success(new PreparedEntryCurrency(converted.Value, rate));
    }
}
