namespace FactuTrust.Application.DTOs;

/// <summary>
/// Mode de libellé de la colonne N-1 (2A) : les montants restent toujours l'exercice civil N-1 complet ;
/// seul le libellé d'en-tête / colonne change.
/// </summary>
public enum NctPreviousYearLabelMode
{
    /// <summary>Libellé N-1 = 31/12/{fiscalYear - 1}.</summary>
    YearEnd31Dec = 0,
    /// <summary>Libellé N-1 = AsOfDate moins 1 an (affichage uniquement).</summary>
    SameCalendarDate = 1
}

/// <summary>
/// Options d'export PDF filtré pour le dialogue « États financiers ».
/// <see cref="AsOfDate"/> et <see cref="PreviousYearLabelMode"/> n'affectent que les libellés (choix 2A).
/// </summary>
public sealed record NctLiasseExportOptions
{
    public int FiscalYear { get; init; }
    public DateOnly AsOfDate { get; init; }
    public NctPreviousYearLabelMode PreviousYearLabelMode { get; init; } = NctPreviousYearLabelMode.YearEnd31Dec;
    public bool IncludeAssets { get; init; }
    public bool IncludeLiabilities { get; init; }
    public bool IncludeIncomeStatement { get; init; }
    public bool IncludeCashFlow { get; init; }
    public bool IncludeAnnexAssets { get; init; }
    public bool IncludeAnnexLiabilities { get; init; }
    public bool IncludeAnnexIncomeStatement { get; init; }
    public bool IncludeAnnexCashFlow { get; init; }
    public IReadOnlyList<int> SelectedNoteNumbers { get; init; } = Array.Empty<int>();

    /// <summary>Sélection complète (toutes sections + toutes notes du catalogue enrichi).</summary>
    public static NctLiasseExportOptions All(int fiscalYear, IReadOnlyList<int>? noteNumbers = null) => new()
    {
        FiscalYear = fiscalYear,
        AsOfDate = new DateOnly(fiscalYear, 12, 31),
        PreviousYearLabelMode = NctPreviousYearLabelMode.YearEnd31Dec,
        IncludeAssets = true,
        IncludeLiabilities = true,
        IncludeIncomeStatement = true,
        IncludeCashFlow = true,
        IncludeAnnexAssets = true,
        IncludeAnnexLiabilities = true,
        IncludeAnnexIncomeStatement = true,
        IncludeAnnexCashFlow = true,
        SelectedNoteNumbers = noteNumbers ?? Array.Empty<int>()
    };

    public bool HasAnyDocumentSection =>
        IncludeAssets || IncludeLiabilities || IncludeIncomeStatement || IncludeCashFlow;

    public bool HasAnyAnnexFamily =>
        IncludeAnnexAssets || IncludeAnnexLiabilities || IncludeAnnexIncomeStatement || IncludeAnnexCashFlow;

    public bool HasAnySelection =>
        HasAnyDocumentSection || (HasAnyAnnexFamily && SelectedNoteNumbers.Count > 0);

    /// <summary>Libellé colonne exercice N (année ou date d'arrêté).</summary>
    public string CurrentPeriodLabel => AsOfDate.ToString("dd/MM/yyyy");

    /// <summary>Libellé colonne N-1 selon le mode radio (montants inchangés).</summary>
    public string PreviousPeriodLabel => PreviousYearLabelMode switch
    {
        NctPreviousYearLabelMode.SameCalendarDate => AsOfDate.AddYears(-1).ToString("dd/MM/yyyy"),
        _ => new DateOnly(FiscalYear - 1, 12, 31).ToString("dd/MM/yyyy")
    };
}

/// <summary>
/// Vue export filtrée : clone non muté de la liasse + métadonnées d'en-tête pour le PDF dialogue.
/// </summary>
public sealed record NctLiasseExportView
{
    public NctFinancialStatementsDto Statements { get; init; } = new();
    public NctLiasseExportOptions Options { get; init; } = null!;
    public bool IncludeAssets { get; init; }
    public bool IncludeLiabilities { get; init; }
    public bool IncludeIncomeStatement { get; init; }
    public bool IncludeCashFlow { get; init; }
    public IReadOnlyList<NctDetailedNoteDto> DetailedNotes { get; init; } = Array.Empty<NctDetailedNoteDto>();
}
