using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.FirmGovernance;

/// <summary>Regroupement d'un code activité, pour les analyses par nature de diligence.</summary>
public enum FirmActivityCategory
{
    /// <summary>Tenue, révision, états financiers.</summary>
    Accounting = 1,
    /// <summary>Déclarations et obligations fiscales.</summary>
    Tax = 2,
    /// <summary>Paie et déclarations sociales.</summary>
    Social = 3,
    /// <summary>Audit légal et commissariat aux comptes.</summary>
    Audit = 4,
    /// <summary>Conseil, assistance, contentieux.</summary>
    Advisory = 5,
    /// <summary>Temps interne au cabinet, non imputable à un client.</summary>
    Internal = 6
}

/// <summary>
/// Nomenclature des diligences du cabinet, utilisée comme référentiel de saisie des temps.
/// </summary>
/// <remarks>
/// Le code activité reste stocké en chaîne libre sur <see cref="FirmTimeSheetEntry"/> : aucune clé
/// étrangère, ce qui laisse vivre les saisies antérieures à la mise en place du référentiel. La
/// validation d'appartenance n'est appliquée que lorsque le cabinet a au moins un code actif.
/// </remarks>
public sealed class FirmActivityCode : Entity
{
    private const decimal MaxDefaultUnitPrice = 999_999_999.999m;

    public Guid FirmTenantId { get; private set; }

    /// <summary>Code court, normalisé en majuscules (ex. <c>TENUE</c>, <c>FISC-M</c>).</summary>
    public string Code { get; private set; } = null!;

    public string Label { get; private set; } = null!;
    public FirmActivityCategory Category { get; private set; }

    /// <summary>Valeur proposée à la saisie ; l'utilisateur reste libre de la modifier ligne à ligne.</summary>
    public bool IsBillableByDefault { get; private set; }

    /// <summary>Tarif unitaire HT suggéré à la facturation (TND, 3 décimales). Null = non configuré.</summary>
    public decimal? DefaultUnitPrice { get; private set; }

    public bool IsActive { get; private set; } = true;
    public int SortOrder { get; private set; }

    private FirmActivityCode() { }

    public static Result<FirmActivityCode> Create(
        Guid firmTenantId,
        string code,
        string label,
        FirmActivityCategory category,
        bool isBillableByDefault = true,
        int sortOrder = 0,
        decimal? defaultUnitPrice = null)
    {
        if (firmTenantId == Guid.Empty)
            return Result.Failure<FirmActivityCode>(Error.Validation("Tenant", "Cabinet requis"));

        var normalized = NormalizeCode(code);
        if (string.IsNullOrEmpty(normalized))
            return Result.Failure<FirmActivityCode>(Error.Validation("Code", "Le code activité est obligatoire."));
        if (normalized.Length > 50)
            return Result.Failure<FirmActivityCode>(Error.Validation("Code", "Le code activité ne peut pas dépasser 50 caractères."));
        if (string.IsNullOrWhiteSpace(label))
            return Result.Failure<FirmActivityCode>(Error.Validation("Label", "Le libellé est obligatoire."));

        var price = NormalizeDefaultUnitPrice(defaultUnitPrice);
        if (price.IsFailure)
            return Result.Failure<FirmActivityCode>(price.Error);

        return Result.Success(new FirmActivityCode
        {
            FirmTenantId = firmTenantId,
            Code = normalized,
            Label = label.Trim(),
            Category = category,
            IsBillableByDefault = isBillableByDefault,
            DefaultUnitPrice = price.Value,
            IsActive = true,
            SortOrder = sortOrder
        });
    }

    /// <summary>Met à jour le libellé et le paramétrage. Le code lui-même reste immuable.</summary>
    /// <remarks>
    /// Renommer un code casserait le lien avec les saisies déjà enregistrées, qui le référencent
    /// par sa valeur : pour changer de code, il faut en désactiver un et en créer un autre.
    /// </remarks>
    public Result Update(
        string label,
        FirmActivityCategory category,
        bool isBillableByDefault,
        int sortOrder,
        decimal? defaultUnitPrice = null)
    {
        if (string.IsNullOrWhiteSpace(label))
            return Result.Failure(Error.Validation("Label", "Le libellé est obligatoire."));

        var price = NormalizeDefaultUnitPrice(defaultUnitPrice);
        if (price.IsFailure)
            return price;

        Label = label.Trim();
        Category = category;
        IsBillableByDefault = isBillableByDefault;
        SortOrder = sortOrder;
        DefaultUnitPrice = price.Value;
        return Result.Success();
    }

    public void Activate() => IsActive = true;

    /// <summary>Retire le code des choix proposés sans toucher aux saisies qui l'utilisent déjà.</summary>
    public void Deactivate() => IsActive = false;

    public static string NormalizeCode(string? code) =>
        (code ?? string.Empty).Trim().ToUpperInvariant();

    private static Result<decimal?> NormalizeDefaultUnitPrice(decimal? defaultUnitPrice)
    {
        if (defaultUnitPrice is null)
            return Result.Success<decimal?>(null);

        if (defaultUnitPrice < 0)
            return Result.Failure<decimal?>(
                Error.Validation("DefaultUnitPrice", "L'honoraire unitaire ne peut pas être négatif."));

        if (defaultUnitPrice > MaxDefaultUnitPrice)
            return Result.Failure<decimal?>(
                Error.Validation("DefaultUnitPrice", "L'honoraire unitaire dépasse la limite autorisée."));

        return Result.Success<decimal?>(Math.Round(defaultUnitPrice.Value, 3, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Nomenclature de départ d'un cabinet d'expertise comptable tunisien.
    /// </summary>
    /// <remarks>
    /// Proposée à la première ouverture de l'écran et jamais réappliquée ensuite : un cabinet qui
    /// adapte, renomme ou désactive des codes ne doit pas les voir réapparaître.
    /// Les tarifs restent à null — le cabinet les renseigne après installation.
    /// </remarks>
    public static IReadOnlyList<(string Code, string Label, FirmActivityCategory Category, bool Billable)> DefaultCatalog =>
    new[]
    {
        ("TENUE",     "Tenue comptable / saisie",                              FirmActivityCategory.Accounting, true),
        ("REVIS",     "Révision des comptes",                                  FirmActivityCategory.Accounting, true),
        ("ETATS",     "États financiers NCT & liasse",                         FirmActivityCategory.Accounting, true),
        ("FISC-M",    "Déclarations mensuelles (TVA, RS, TCL, TFP/FOPROLOS)",  FirmActivityCategory.Tax,        true),
        ("FISC-A",    "Déclaration annuelle IS / IRPP",                        FirmActivityCategory.Tax,        true),
        ("CNSS",      "Déclarations sociales CNSS / DTS trimestrielle",        FirmActivityCategory.Social,     true),
        ("PAIE",      "Paie et bulletins",                                     FirmActivityCategory.Social,     true),
        ("CAC",       "Commissariat aux comptes / audit légal",                FirmActivityCategory.Audit,      true),
        ("CONSEIL",   "Conseil, assistance juridique",                         FirmActivityCategory.Advisory,   true),
        ("CTRL-FISC", "Assistance contrôle fiscal / contentieux",              FirmActivityCategory.Advisory,   true),
        ("DEPL",      "Déplacement client",                                    FirmActivityCategory.Advisory,   true),
        ("ADMIN",     "Administratif interne",                                 FirmActivityCategory.Internal,   false),
        ("FORM",      "Formation",                                             FirmActivityCategory.Internal,   false)
    };
}
