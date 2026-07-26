using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Accounting;

/// <summary>
/// Filtre post-build de la liasse NCT pour l'export PDF du dialogue (ne mute jamais la source).
/// </summary>
public static class NctLiasseExportFilter
{
    public static Result<NctLiasseExportView> Apply(NctFinancialStatementsDto source, NctLiasseExportOptions options)
    {
        if (options.FiscalYear is < 2000 or > 2100)
            return Result.Failure<NctLiasseExportView>(Error.Validation("FiscalYear", "Exercice invalide."));

        if (!options.HasAnySelection)
            return Result.Failure<NctLiasseExportView>(Error.Validation(
                "Selection",
                "Sélectionnez au moins un état ou une note annexe."));

        var selected = new HashSet<int>(options.SelectedNoteNumbers);
        var notes = source.DetailedNotes
            .Where(n => selected.Contains(n.Number) && IsFamilyIncluded(n.Family, options))
            .ToList();

        // Clone léger : on conserve les sections non demandées vides côté vue via flags,
        // sans altérer le DTO source passé en entrée.
        var view = new NctLiasseExportView
        {
            Statements = source,
            Options = options,
            IncludeAssets = options.IncludeAssets,
            IncludeLiabilities = options.IncludeLiabilities,
            IncludeIncomeStatement = options.IncludeIncomeStatement,
            IncludeCashFlow = options.IncludeCashFlow,
            DetailedNotes = notes
        };

        return Result.Success(view);
    }

    private static bool IsFamilyIncluded(NctAnnexFamily family, NctLiasseExportOptions options) =>
        family switch
        {
            NctAnnexFamily.Actif => options.IncludeAnnexAssets,
            NctAnnexFamily.Passif => options.IncludeAnnexLiabilities,
            NctAnnexFamily.IncomeStatement => options.IncludeAnnexIncomeStatement,
            NctAnnexFamily.CashFlow => options.IncludeAnnexCashFlow,
            _ => false
        };
}
