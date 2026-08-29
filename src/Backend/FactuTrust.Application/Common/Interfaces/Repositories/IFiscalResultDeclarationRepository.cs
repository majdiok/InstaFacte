using FactuTrust.Domain.Entities.Fiscal;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Paramètres d'upsert d'une feuille de détermination du résultat fiscal.
/// </summary>
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

/// <summary>
/// Issue de la finalisation conditionnelle atomique (T10, porte de concurrence).
/// </summary>
public enum FiscalFinalizeOutcome
{
    /// <summary>Aucune feuille n'existe pour l'exercice.</summary>
    Absent = 0,
    /// <summary>La feuille existait encore au brouillon et a été finalisée.</summary>
    Finalized = 1,
    /// <summary>La feuille était déjà finalisée (aucune ligne affectée).</summary>
    AlreadyFinalized = 2
}

/// <summary>Persistance de la feuille de détermination du résultat fiscal (une par exercice, tenant).</summary>
public interface IFiscalResultDeclarationRepository
{
    /// <summary>Charge la feuille de l'exercice (avec ses lignes et reports), ou null si absente.</summary>
    Task<FiscalResultDeclaration?> GetByYearAsync(int fiscalYear, CancellationToken ct = default);

    /// <summary>Crée ou met à jour la feuille de l'exercice (brouillon). Échoue si déjà finalisée.</summary>
    Task<FiscalResultDeclaration> UpsertAsync(FiscalDeclarationUpsert upsert, CancellationToken ct = default);

    /// <summary>
    /// Finalise la feuille de l'exercice de façon CONDITIONNELLE et ATOMIQUE (T10, porte de concurrence) :
    /// <c>UPDATE … WHERE Status = Draft</c> — seule une feuille encore au brouillon est finalisée.
    /// Distingue <see cref="FiscalFinalizeOutcome.Absent"/> (aucune feuille) de
    /// <see cref="FiscalFinalizeOutcome.AlreadyFinalized"/> (concurrence : un autre l'a déjà finalisée).
    /// L'index existant <c>(SourceEntityType, SourceEntityId)</c> reste non unique (usage légitime
    /// multi-écritures par la paie après extourne) ; l'unicité de l'écriture d'impôt découle de
    /// l'idempotence par <c>GetBySourceAsync(SourceFiscalTax, declarationId)</c> dans la même transaction.
    /// </summary>
    Task<FiscalFinalizeOutcome> FinalizeAsync(int fiscalYear, string userId, CancellationToken ct = default);
}
