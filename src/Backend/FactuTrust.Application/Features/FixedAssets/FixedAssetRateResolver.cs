using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Features.FixedAssets;

/// <summary>
/// Résout le taux et la durée d'amortissement d'un actif : la catégorie fournit les valeurs
/// par défaut (taux légal tunisien), l'utilisateur peut les surcharger (taux et durée
/// synchronisés : taux = 100 / durée).
/// </summary>
public static class FixedAssetRateResolver
{
    public static Result<(decimal RatePercent, decimal LifeYears)> Resolve(
        bool isNonDepreciable,
        decimal categoryRatePercent,
        decimal categoryLifeYears,
        decimal? overrideRatePercent,
        decimal? overrideLifeYears)
    {
        if (isNonDepreciable)
            return Result.Success((0m, 0m));

        var rate = categoryRatePercent;
        var life = categoryLifeYears;

        if (overrideRatePercent is { } r)
        {
            if (r <= 0 || r > 100)
                return Result.Failure<(decimal, decimal)>(
                    Error.Validation("DepreciationRatePercent", "Le taux d'amortissement doit être compris entre 0 et 100 %."));
            rate = r;
            life = overrideLifeYears is > 0 ? overrideLifeYears.Value : Math.Round(100m / r, 2);
        }
        else if (overrideLifeYears is { } y)
        {
            if (y <= 0 || y > 100)
                return Result.Failure<(decimal, decimal)>(
                    Error.Validation("UsefulLifeYears", "La durée d'utilisation doit être comprise entre 0 et 100 ans."));
            life = y;
            rate = Math.Round(100m / y, 4);
        }

        return Result.Success((rate, life));
    }
}
