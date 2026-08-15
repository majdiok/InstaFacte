using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Features.Treasury;

/// <summary>
/// Erreurs du prévisionnel de trésorerie, avec des codes stables.
/// </summary>
/// <remarks>
/// Les codes sont contractuels vis-à-vis du front : c'est sur eux que le contrôleur choisit son
/// statut HTTP et l'écran son rendu. Les tester par sous-chaîne du message serait fragile dès la
/// première reformulation.
/// </remarks>
public static class CashFlowForecastErrors
{
    public const string RateLimitedCode = "TreasuryForecast.RateLimited";

    public static Error RateLimited(int maxRunsPerDay) => new(
        RateLimitedCode,
        $"Limite de {maxRunsPerDay} recalculs par 24 h atteinte. " +
        "La projection reste consultable ; réessayez plus tard.");

    public static Error RunNotFound { get; } = new(
        "TreasuryForecast.RunNotFound",
        "Projection introuvable.");

    public static Error CommitmentNotFound { get; } = new(
        "TreasuryForecast.CommitmentNotFound",
        "Engagement récurrent introuvable.");
}
