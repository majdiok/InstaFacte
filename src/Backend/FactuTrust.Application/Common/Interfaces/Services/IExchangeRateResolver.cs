using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Taux retenu pour une écriture, après résolution serveur et contrôle d'une éventuelle surcharge.
/// </summary>
/// <param name="CurrencyCode">Code devise normalisé.</param>
/// <param name="Rate">Taux appliqué : unités de devise de tenue pour UNE unité de devise étrangère.</param>
/// <param name="ReferenceRate">Taux de la table, avant surcharge. Égal à <paramref name="Rate"/> si aucune surcharge.</param>
/// <param name="IsOverridden">Vrai si le taux appliqué diffère de celui de la table.</param>
/// <param name="IsFunctional">Vrai si la devise est celle de tenue des comptes (taux 1, aucune conversion).</param>
public sealed record ResolvedExchangeRate(
    string CurrencyCode,
    decimal Rate,
    decimal ReferenceRate,
    bool IsOverridden,
    bool IsFunctional);

/// <summary>
/// Résout le taux de change applicable à une écriture.
///
/// <para>
/// <b>Le client ne fixe jamais le taux.</b> Le serveur part toujours de la table des taux ; un taux
/// transmis n'est accepté que s'il reste dans la tolérance configurée <b>et</b> si l'appelant
/// détient <c>accounting:exchange_rate_override</c>. Sans ce contrôle, n'importe quel saisisseur
/// pourrait piloter le montant en dinar porté en comptabilité.
/// </para>
/// </summary>
public interface IExchangeRateResolver
{
    Task<Result<ResolvedExchangeRate>> ResolveAsync(
        string? currencyCode,
        DateTime entryDate,
        decimal? requestedRate,
        CancellationToken cancellationToken = default);
}
