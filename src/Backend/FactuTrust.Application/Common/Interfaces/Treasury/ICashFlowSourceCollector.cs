using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Treasury;

/// <summary>
/// Contexte partagé par tous les collecteurs d'un même calcul.
/// </summary>
public sealed record CashFlowCollectionContext
{
    /// <summary>Premier jour projeté (inclus).</summary>
    public required DateTime From { get; init; }

    /// <summary>Dernier jour projeté (inclus).</summary>
    public required DateTime To { get; init; }

    /// <summary>
    /// Date de référence du calcul. Distincte de <see cref="From"/> : l'ancienneté d'une créance
    /// se mesure par rapport à aujourd'hui, pas au début de la fenêtre projetée.
    /// </summary>
    public required DateTime Today { get; init; }

    public required TreasuryForecastOptions Options { get; init; }

    /// <summary>
    /// Jour de paiement des salaires effectif (réglage du tenant, sinon valeur de configuration).
    /// </summary>
    public required int PayrollPaymentDayOfMonth { get; init; }
}

/// <summary>
/// Flux candidat produit par un collecteur, avant agrégation et persistance.
/// </summary>
/// <remarks>
/// Structure volontairement plate et sans dépendance au domaine persistant : un collecteur peut
/// être testé sur un jeu d'entités en mémoire sans jamais construire de run.
/// </remarks>
public sealed record CashFlowLineDraft
{
    public required CashFlowDirection Direction { get; init; }
    public required CashFlowSourceType SourceType { get; init; }
    public required string Label { get; init; }

    /// <summary>Date due au contrat.</summary>
    public required DateTime ContractualDate { get; init; }

    /// <summary>Date réellement attendue, retard observé inclus.</summary>
    public required DateTime ExpectedDate { get; init; }

    /// <summary>Montant brut, toujours positif.</summary>
    public required decimal Amount { get; init; }

    /// <summary>Probabilité de réalisation dans [0..100].</summary>
    public required decimal ProbabilityPercent { get; init; }

    public bool IsConfirmed { get; init; }
    public Guid? SourceId { get; init; }
    public string? SourceReference { get; init; }
    public string? ThirdPartyName { get; init; }
}

/// <summary>
/// Alimente la projection depuis une source métier. Une implémentation par source, pour qu'une
/// source puisse être désactivée, testée ou corrigée sans toucher aux autres.
/// </summary>
public interface ICashFlowSourceCollector
{
    CashFlowSourceType SourceType { get; }

    /// <summary>
    /// Les sources à risque de double comptage (carnet de commandes, engagements sur bons de
    /// commande) sont désactivées par défaut et ne s'activent que par configuration explicite.
    /// </summary>
    bool IsEnabled(TreasuryForecastOptions options);

    Task<IReadOnlyList<CashFlowLineDraft>> CollectAsync(
        CashFlowCollectionContext context,
        CancellationToken cancellationToken = default);
}
