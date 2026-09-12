using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Devise utilisable en comptabilité, et granularité de sa table de taux.
///
/// <para>
/// Exactement une devise porte <see cref="IsFunctional"/> : la devise de tenue des comptes
/// (<see cref="Money.DefaultCurrency"/>). Tous les montants comptables persistés sont exprimés dans
/// cette devise — voir le garde-fou de <c>JournalEntry.BuildLines</c>. Les autres devises ne servent
/// qu'à qualifier la transaction et à porter sa contre-valeur.
/// </para>
/// </summary>
public sealed class Currency : Entity
{
    /// <summary>Nombre maximal de décimales exploitable : <see cref="Money"/> stocke 3 décimales.</summary>
    public const int MaxDecimalPlaces = Money.DecimalPlaces;

    public const int CodeLength = 3;
    public const int MaxLabelLength = 60;

    /// <summary>Code ISO 4217 sur 3 lettres majuscules (TND, EUR, USD…).</summary>
    public string Code { get; private set; } = null!;

    /// <summary>Libellé affiché (« Euro », « Dinar Tunisien »…).</summary>
    public string Label { get; private set; } = null!;

    /// <summary>Décimales d'affichage et de saisie des montants dans cette devise (2 pour l'euro, 3 pour le dinar).</summary>
    public int DecimalPlaces { get; private set; }

    /// <summary>Granularité de la table des taux : un taux par exercice, ou un taux par mois.</summary>
    public ExchangeRatePeriodicity RatePeriodicity { get; private set; }

    /// <summary>Une devise inactive reste lisible sur les écritures passées mais n'est plus proposée à la saisie.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Devise de tenue des comptes. Une seule ligne, non supprimable, sans taux de change.</summary>
    public bool IsFunctional { get; private set; }

    private Currency() { }

    /// <summary>Crée la devise fonctionnelle du dossier. Réservé à l'amorçage.</summary>
    public static Result<Currency> CreateFunctional(string code, string label, int decimalPlaces)
    {
        var result = Create(code, label, decimalPlaces, ExchangeRatePeriodicity.Fixe);
        if (result.IsFailure)
            return result;

        result.Value.IsFunctional = true;
        return result;
    }

    public static Result<Currency> Create(
        string code,
        string label,
        int decimalPlaces,
        ExchangeRatePeriodicity ratePeriodicity)
    {
        var codeResult = NormalizeCode(code);
        if (codeResult.IsFailure)
            return Result.Failure<Currency>(codeResult.Error);

        var labelResult = NormalizeLabel(label);
        if (labelResult.IsFailure)
            return Result.Failure<Currency>(labelResult.Error);

        var decimalsResult = ValidateDecimalPlaces(decimalPlaces);
        if (decimalsResult.IsFailure)
            return Result.Failure<Currency>(decimalsResult.Error);

        return Result.Success(new Currency
        {
            Code = codeResult.Value,
            Label = labelResult.Value,
            DecimalPlaces = decimalPlaces,
            RatePeriodicity = ratePeriodicity,
            IsActive = true,
            IsFunctional = false
        });
    }

    /// <summary>
    /// Met à jour le libellé, les décimales et la périodicité. Le code n'est jamais modifiable :
    /// il est référencé par les écritures déjà comptabilisées.
    /// </summary>
    public Result Update(string label, int decimalPlaces, ExchangeRatePeriodicity ratePeriodicity)
    {
        var labelResult = NormalizeLabel(label);
        if (labelResult.IsFailure)
            return Result.Failure(labelResult.Error);

        var decimalsResult = ValidateDecimalPlaces(decimalPlaces);
        if (decimalsResult.IsFailure)
            return decimalsResult;

        if (IsFunctional && ratePeriodicity != RatePeriodicity)
            return Result.Failure(Error.Validation("RatePeriodicity",
                "La devise de tenue des comptes n'a pas de taux de change : sa périodicité n'est pas modifiable."));

        Label = labelResult.Value;
        DecimalPlaces = decimalPlaces;
        RatePeriodicity = ratePeriodicity;
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (IsFunctional)
            return Result.Failure(Error.Validation("IsFunctional",
                "La devise de tenue des comptes ne peut pas être désactivée."));

        IsActive = false;
        return Result.Success();
    }

    public void Activate() => IsActive = true;

    private static Result<string> NormalizeCode(string code)
    {
        code = code?.Trim().ToUpperInvariant() ?? string.Empty;

        if (code.Length != CodeLength || !code.All(c => c is >= 'A' and <= 'Z'))
            return Result.Failure<string>(Error.Validation("Code",
                "Le code devise doit être un code ISO 4217 de 3 lettres (TND, EUR, USD…)."));

        return Result.Success(code);
    }

    private static Result<string> NormalizeLabel(string label)
    {
        label = label?.Trim() ?? string.Empty;

        if (label.Length == 0)
            return Result.Failure<string>(Error.Validation("Label", "Le libellé est obligatoire"));

        if (label.Length > MaxLabelLength)
            return Result.Failure<string>(Error.Validation("Label",
                $"Le libellé ne peut pas dépasser {MaxLabelLength} caractères."));

        return Result.Success(label);
    }

    private static Result ValidateDecimalPlaces(int decimalPlaces)
    {
        if (decimalPlaces is < 0 or > MaxDecimalPlaces)
            return Result.Failure(Error.Validation("DecimalPlaces",
                $"Le nombre de décimales doit être compris entre 0 et {MaxDecimalPlaces}."));

        return Result.Success();
    }
}
