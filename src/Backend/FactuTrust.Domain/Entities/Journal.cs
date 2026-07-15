using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Journal comptable (code + libellé + famille). Les 7 journaux standards (JV, JA, JC, JB, JOD, JIM, JAN)
/// sont marqués <see cref="IsSystem"/> et ne peuvent pas être supprimés/désactivés.
/// </summary>
public sealed class Journal : Entity
{
    public string Code { get; private set; } = null!;
    public string Label { get; private set; } = null!;
    public Guid? FamilyId { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }

    private Journal() { }

    public static Result<Journal> Create(string code, string label, Guid? familyId, bool isSystem = false)
    {
        code = code?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(code))
            return Result.Failure<Journal>(Error.Validation("Code", "Le code du journal est obligatoire."));
        if (code.Length > 10)
            return Result.Failure<Journal>(Error.Validation("Code", "Le code du journal ne peut pas dépasser 10 caractères."));

        label = label?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(label))
            return Result.Failure<Journal>(Error.Validation("Label", "Le libellé est obligatoire."));

        return Result.Success(new Journal
        {
            Code = code,
            Label = label,
            FamilyId = familyId,
            IsSystem = isSystem,
            IsActive = true
        });
    }

    public Result Update(string label, Guid? familyId)
    {
        label = label?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(label))
            return Result.Failure(Error.Validation("Label", "Le libellé est obligatoire."));
        Label = label;
        FamilyId = familyId;
        return Result.Success();
    }

    public void Deactivate()
    {
        if (!IsSystem)
            IsActive = false;
    }

    public void Reactivate() => IsActive = true;
}
