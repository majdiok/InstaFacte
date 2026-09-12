namespace FactuTrust.Application.DTOs;

/// <summary>Devise du catalogue, avec l'état de configuration de ses taux pour un exercice donné.</summary>
public sealed record CurrencyDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Label { get; init; } = null!;
    public int DecimalPlaces { get; init; }

    /// <summary>0 = Fixe (un taux par exercice), 1 = Mensuelle (un taux par mois).</summary>
    public int RatePeriodicity { get; init; }

    public bool IsActive { get; init; }

    /// <summary>Devise de tenue des comptes : sans taux de change, non supprimable.</summary>
    public bool IsFunctional { get; init; }

    /// <summary>Nombre de taux réellement saisis pour l'exercice interrogé.</summary>
    public int ConfiguredRateCount { get; init; }

    /// <summary>Nombre de taux attendus : 1 si fixe, 12 si mensuelle, 0 pour la devise de tenue.</summary>
    public int ExpectedRateCount { get; init; }
}

public sealed record CurrencyExchangeRateDto
{
    public Guid Id { get; init; }
    public int FiscalYear { get; init; }

    /// <summary>1 à 12, ou <c>null</c> pour un taux fixe couvrant tout l'exercice.</summary>
    public int? Month { get; init; }

    /// <summary>Unités de devise de tenue pour UNE unité de cette devise (1 EUR = 3,31420 TND).</summary>
    public decimal Rate { get; init; }
}

/// <summary>Devise et sa table de taux pour un exercice, tel que le consomme l'écran d'édition.</summary>
public sealed record CurrencyDetailDto
{
    public CurrencyDto Currency { get; init; } = null!;
    public int FiscalYear { get; init; }
    public IReadOnlyList<CurrencyExchangeRateDto> Rates { get; init; } = Array.Empty<CurrencyExchangeRateDto>();
}

/// <summary>
/// Taux que le serveur appliquerait à une écriture, tel que le résout <c>IExchangeRateResolver</c>.
/// Permet à l'écran de saisie d'afficher exactement ce qui sera comptabilisé, sans le recalculer.
/// </summary>
public sealed record ResolvedExchangeRateDto
{
    public string CurrencyCode { get; init; } = null!;
    public decimal Rate { get; init; }
    public decimal ReferenceRate { get; init; }
    public bool IsOverridden { get; init; }
    public bool IsFunctional { get; init; }
}

/// <summary>
/// Réévaluation de clôture. Les comptes sont saisis à chaque exécution, sans défaut persisté :
/// NCT 01 fournit 185 (écarts passif), 275 (écarts actif) et 1515 (provisions), mais un dossier non
/// migré peut ne pas les avoir.
/// </summary>
public sealed record RunClosingRevaluationRequest
{
    public int FiscalYear { get; init; }
    public int Month { get; init; }

    /// <summary>Écarts de conversion passif — gains latents (185 en NCT 01).</summary>
    public string GainAccount { get; init; } = null!;

    /// <summary>Écarts de conversion actif — pertes latentes (275 en NCT 01).</summary>
    public string LossAccount { get; init; } = null!;

    /// <summary>Dotation aux provisions. Facultatif : laissé vide, aucune provision n'est constatée.</summary>
    public string? ProvisionExpenseAccount { get; init; }

    /// <summary>Provisions pour pertes de change (1515 en NCT 01). Facultatif, va de pair avec la dotation.</summary>
    public string? ProvisionAccount { get; init; }
}

public sealed record ClosingRevaluationResultDto
{
    public Guid RevaluationEntryId { get; init; }

    /// <summary>Écriture de contre-passation, datée du premier jour de la période suivante.</summary>
    public Guid ReversalEntryId { get; init; }

    /// <summary>Écriture de provision, si une dotation a été demandée et qu'il existe des pertes latentes.</summary>
    public Guid? ProvisionEntryId { get; init; }

    public int PositionCount { get; init; }
    public decimal TotalLatentGain { get; init; }
    public decimal TotalLatentLoss { get; init; }
}

public sealed record CreateCurrencyRequest
{
    public string Code { get; init; } = null!;
    public string Label { get; init; } = null!;
    public int DecimalPlaces { get; init; } = 2;
    public int RatePeriodicity { get; init; } = 1;
}

public sealed record UpdateCurrencyRequest
{
    public string Label { get; init; } = null!;
    public int DecimalPlaces { get; init; }
    public int RatePeriodicity { get; init; }
}

/// <summary>
/// Remplace la table de taux d'une devise pour un exercice. Une entrée dont le taux est
/// <c>null</c> efface le taux du mois correspondant : l'écran envoie toujours la grille complète.
/// </summary>
public sealed record SaveCurrencyRatesRequest
{
    public int FiscalYear { get; init; }
    public IReadOnlyList<CurrencyRateEntryRequest> Rates { get; init; } = Array.Empty<CurrencyRateEntryRequest>();
}

public sealed record CurrencyRateEntryRequest
{
    /// <summary>1 à 12, ou <c>null</c> pour le taux fixe de l'exercice.</summary>
    public int? Month { get; init; }

    /// <summary><c>null</c> ou 0 efface le taux.</summary>
    public decimal? Rate { get; init; }
}
