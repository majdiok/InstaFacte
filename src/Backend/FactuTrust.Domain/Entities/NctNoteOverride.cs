using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Personnalisation d'une note annexe NCT par le comptable, pour un exercice donné : titre de
/// remplacement, texte narratif, ou masquage complet de la note.
/// <para>
/// Superposition PURE : sans ligne pour un exercice, la liasse conserve exactement les libellés
/// auto-générés par <c>NctDetailedNoteCatalog</c>. La portée est l'exercice (le libellé d'une note
/// peut légitimement changer d'une année à l'autre), la clé métier étant (FiscalYear, NoteNumber).
/// </para>
/// </summary>
public sealed class NctNoteOverride : Entity
{
    public int FiscalYear { get; private set; }

    /// <summary>Numéro de la note au catalogue (clé stable, contrairement au libellé).</summary>
    public int NoteNumber { get; private set; }

    /// <summary>Titre de remplacement ; null = titre du catalogue conservé.</summary>
    public string? CustomTitle { get; private set; }

    /// <summary>Texte narratif affiché sous le titre ; null = aucun.</summary>
    public string? CustomDescription { get; private set; }

    /// <summary>Vrai = la note est retirée de la liasse et de l'export PDF.</summary>
    public bool IsHidden { get; private set; }

    private NctNoteOverride() { }

    public static Result<NctNoteOverride> Create(
        int fiscalYear, int noteNumber, string? customTitle, string? customDescription, bool isHidden)
    {
        if (fiscalYear is < 2000 or > 2100)
            return Result.Failure<NctNoteOverride>(Error.Validation("FiscalYear", "Exercice invalide."));
        if (noteNumber <= 0)
            return Result.Failure<NctNoteOverride>(Error.Validation("NoteNumber", "Numéro de note invalide."));

        var normalized = Normalize(customTitle, customDescription);
        if (normalized.IsFailure)
            return Result.Failure<NctNoteOverride>(normalized.Error);

        return Result.Success(new NctNoteOverride
        {
            Id = Guid.NewGuid(),
            FiscalYear = fiscalYear,
            NoteNumber = noteNumber,
            CustomTitle = normalized.Value.Title,
            CustomDescription = normalized.Value.Description,
            IsHidden = isHidden
        });
    }

    public Result Update(string? customTitle, string? customDescription, bool isHidden)
    {
        var normalized = Normalize(customTitle, customDescription);
        if (normalized.IsFailure)
            return Result.Failure(normalized.Error);

        CustomTitle = normalized.Value.Title;
        CustomDescription = normalized.Value.Description;
        IsHidden = isHidden;
        return Result.Success();
    }

    /// <summary>Trim + vide ⇒ null (un titre blanc doit rendre la main au catalogue, pas l'effacer).</summary>
    private static Result<(string? Title, string? Description)> Normalize(string? title, string? description)
    {
        var t = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        var d = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (t is { Length: > 300 })
            return Result.Failure<(string?, string?)>(Error.Validation("CustomTitle", "Le titre ne peut pas dépasser 300 caractères."));
        if (d is { Length: > 2000 })
            return Result.Failure<(string?, string?)>(Error.Validation("CustomDescription", "Le texte ne peut pas dépasser 2000 caractères."));

        return Result.Success((t, d));
    }
}
