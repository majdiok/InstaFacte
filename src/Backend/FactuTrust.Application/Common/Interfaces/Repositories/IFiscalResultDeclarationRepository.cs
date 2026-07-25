using FactuTrust.Domain.Entities.Fiscal;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>Paramètres d'upsert d'une feuille de détermination du résultat fiscal.</summary>
public sealed record FiscalDeclarationUpsert(
    int FiscalYear,
    TaxpayerKind TaxpayerKind,
    decimal AccountingResult,
    decimal AppliedIsRate,
    decimal LocalTurnoverTtc,
    decimal AcomptesPaid,
    decimal WithholdingSuffered,
    decimal PriorTaxCredit,
    IReadOnlyList<FiscalAdjustmentLine> Adjustments,
    IReadOnlyList<FiscalCarryForwardItem> CarryForwards,
    /// <summary>Régime de minimum d'impôt (droit commun par défaut).</summary>
    MinimumTaxRegime MinimumTaxRegime = MinimumTaxRegime.Standard);

/// <summary>Persistance de la feuille de détermination du résultat fiscal (une par exercice, tenant).</summary>
public interface IFiscalResultDeclarationRepository
{
    /// <summary>Charge la feuille de l'exercice (avec ses lignes et reports), ou null si absente.</summary>
    Task<FiscalResultDeclaration?> GetByYearAsync(int fiscalYear, CancellationToken ct = default);

    /// <summary>Crée ou met à jour la feuille de l'exercice (brouillon). Échoue si déjà finalisée.</summary>
    Task<FiscalResultDeclaration> UpsertAsync(FiscalDeclarationUpsert upsert, CancellationToken ct = default);

    /// <summary>Finalise la feuille de l'exercice. Retourne false si aucune feuille n'existe.</summary>
    Task<bool> FinalizeAsync(int fiscalYear, string userId, CancellationToken ct = default);
}
