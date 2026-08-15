using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Treasury;

/// <summary>
/// Un flux de trésorerie attendu, daté et rattaché à sa source métier.
/// </summary>
/// <remarks>
/// <see cref="ContractualDate"/> est la date due au contrat (échéance de facture, date d'échéance
/// fiscale). <see cref="ExpectedDate"/> est la date à laquelle le flux est réellement attendu,
/// après application du retard de paiement observé pour ce tiers. Conserver les deux permet
/// d'expliquer un décalage à l'utilisateur au lieu de lui présenter une date sortie de nulle part.
/// </remarks>
public sealed class CashFlowForecastLine : Entity
{
    public Guid ForecastRunId { get; private set; }

    public CashFlowDirection Direction { get; private set; }

    public CashFlowSourceType SourceType { get; private set; }

    /// <summary>Identifiant de l'entité source (facture, échéance fiscale, cycle de paie…).</summary>
    public Guid? SourceId { get; private set; }

    /// <summary>Référence lisible de la source (numéro de facture, période de paie…).</summary>
    public string? SourceReference { get; private set; }

    /// <summary>Libellé affiché dans les listes de flux.</summary>
    public string Label { get; private set; } = null!;

    /// <summary>Tiers concerné (client, fournisseur, organisme), quand il y en a un.</summary>
    public string? ThirdPartyName { get; private set; }

    /// <summary>Date due au contrat.</summary>
    public DateTime ContractualDate { get; private set; }

    /// <summary>Date réellement attendue, retard historique inclus.</summary>
    public DateTime ExpectedDate { get; private set; }

    /// <summary>Montant brut du flux, toujours positif — le sens est porté par <see cref="Direction"/>.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Probabilité de réalisation dans [0..100].</summary>
    public decimal ProbabilityPercent { get; private set; }

    /// <summary>Montant retenu dans l'agrégat réaliste : <see cref="Amount"/> × probabilité.</summary>
    public decimal WeightedAmount { get; private set; }

    /// <summary>Vrai quand le flux est contractuellement certain (effet accepté, dette constatée).</summary>
    public bool IsConfirmed { get; private set; }

    private CashFlowForecastLine() { }

    public static CashFlowForecastLine Create(
        CashFlowDirection direction,
        CashFlowSourceType sourceType,
        string label,
        DateTime contractualDate,
        DateTime expectedDate,
        decimal amount,
        decimal probabilityPercent,
        bool isConfirmed,
        Guid? sourceId = null,
        string? sourceReference = null,
        string? thirdPartyName = null)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Le libellé du flux est obligatoire.", nameof(label));

        if (amount < 0m)
            throw new ArgumentException(
                "Le montant d'un flux est toujours positif ; le sens est porté par Direction.",
                nameof(amount));

        if (probabilityPercent < 0m || probabilityPercent > 100m)
            throw new ArgumentException("ProbabilityPercent doit être dans [0..100].", nameof(probabilityPercent));

        var rounded = MillimeRounding.Round(amount);

        return new CashFlowForecastLine
        {
            Direction = direction,
            SourceType = sourceType,
            SourceId = sourceId,
            SourceReference = Truncate(sourceReference, 100),
            Label = label.Trim(),
            ThirdPartyName = Truncate(thirdPartyName, 200),
            ContractualDate = contractualDate.Date,
            ExpectedDate = expectedDate.Date,
            Amount = rounded,
            ProbabilityPercent = probabilityPercent,
            WeightedAmount = MillimeRounding.Round(rounded * probabilityPercent / 100m),
            IsConfirmed = isConfirmed
        };
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
