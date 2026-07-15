using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Famille de journaux (Ventes, Achats, Trésorerie, OD, Immobilisations, À-Nouveaux…).
/// </summary>
public sealed class JournalFamily : Entity
{
    public string Code { get; private set; } = null!;
    public string Label { get; private set; } = null!;

    private JournalFamily() { }

    public static Result<JournalFamily> Create(string code, string label)
    {
        code = code?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(code))
            return Result.Failure<JournalFamily>(Error.Validation("Code", "Le code de la famille est obligatoire."));

        label = label?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(label))
            return Result.Failure<JournalFamily>(Error.Validation("Label", "Le libellé est obligatoire."));

        return Result.Success(new JournalFamily { Code = code, Label = label });
    }

    public Result UpdateLabel(string label)
    {
        label = label?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(label))
            return Result.Failure(Error.Validation("Label", "Le libellé est obligatoire."));
        Label = label;
        return Result.Success();
    }
}
