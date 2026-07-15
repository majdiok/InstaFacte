using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common;

/// <summary>
/// Résout le type RS par défaut du fournisseur : identifiant explicite prioritaire, sinon tranche IS RS7.
/// </summary>
public static class SupplierWithholdingDefaultsResolver
{
    public static async Task<Guid?> ResolveDefaultWithholdingTaxTypeIdAsync(
        Guid? explicitWithholdingTaxTypeId,
        SupplierRs7IsBracket? rs7IsBracket,
        IWithholdingTaxRepository withholdingTaxRepository,
        CancellationToken ct = default)
    {
        var result = await ResolveWithResultAsync(
            explicitWithholdingTaxTypeId,
            rs7IsBracket,
            withholdingTaxRepository,
            ct);

        return result.IsSuccess ? result.Value : null;
    }

    public static async Task<Result<Guid?>> ResolveWithResultAsync(
        Guid? explicitWithholdingTaxTypeId,
        SupplierRs7IsBracket? rs7IsBracket,
        IWithholdingTaxRepository withholdingTaxRepository,
        CancellationToken ct = default)
    {
        var explicitId = NormalizeExplicitTypeId(explicitWithholdingTaxTypeId);
        var hasBracket = rs7IsBracket is not null && rs7IsBracket != SupplierRs7IsBracket.Unspecified;

        if (explicitId.HasValue && hasBracket)
        {
            var explicitType = await withholdingTaxRepository.GetTypeByIdAsync(explicitId.Value, ct);
            if (explicitType is null)
            {
                return Result.Failure<Guid?>(Error.Validation(
                    "DefaultWithholdingTaxTypeId",
                    "Le type de retenue à la source sélectionné est introuvable ou inactif."));
            }

            var inferredBracket = InferRs7BracketFromTypeCode(explicitType.Code);
            if (inferredBracket is null)
            {
                return Result.Failure<Guid?>(Error.Validation(
                    "Rs7IsBracket",
                    "La Tranche IS (RS7) et le Type RS explicite ne peuvent pas être renseignés simultanément. Choisissez l'un ou l'autre."));
            }

            if (inferredBracket != rs7IsBracket)
            {
                return Result.Failure<Guid?>(Error.Validation(
                    "Rs7IsBracket",
                    "Le type RS7 explicite ne correspond pas à la tranche IS sélectionnée."));
            }

            return Result.Success<Guid?>(explicitId);
        }

        if (explicitId.HasValue)
        {
            var explicitType = await withholdingTaxRepository.GetTypeByIdAsync(explicitId.Value, ct);
            if (explicitType is null)
            {
                return Result.Failure<Guid?>(Error.Validation(
                    "DefaultWithholdingTaxTypeId",
                    "Le type de retenue à la source sélectionné est introuvable ou inactif."));
            }

            return Result.Success<Guid?>(explicitId);
        }

        if (hasBracket)
        {
            var code = WithholdingTaxLegislativeReference.TejCodeForRs7Bracket(rs7IsBracket!.Value);
            var type = await withholdingTaxRepository.GetTypeByCodeAsync(code, ct);
            if (type is null)
            {
                return Result.Failure<Guid?>(Error.Validation(
                    "Rs7IsBracket",
                    $"Aucun type RS7 actif trouvé pour le code {code}. Complétez le catalogue des retenues à la source."));
            }

            return Result.Success<Guid?>(type.Id);
        }

        return Result.Success<Guid?>(null);
    }

    public static Guid? NormalizeExplicitTypeId(Guid? explicitWithholdingTaxTypeId) =>
        explicitWithholdingTaxTypeId is { } id && id != Guid.Empty ? id : null;

    public static SupplierRs7IsBracket? InferRs7BracketFromTypeCode(string? operationCode)
    {
        if (string.IsNullOrWhiteSpace(operationCode))
            return null;

        return operationCode.Trim().ToUpperInvariant() switch
        {
            WithholdingTaxLegislativeReference.Rs7CodeNormal25 => SupplierRs7IsBracket.Normal25,
            WithholdingTaxLegislativeReference.Rs7CodeReduced15 => SupplierRs7IsBracket.Reduced15,
            WithholdingTaxLegislativeReference.Rs7CodeReduced10 => SupplierRs7IsBracket.Reduced10,
            _ => null
        };
    }
}
