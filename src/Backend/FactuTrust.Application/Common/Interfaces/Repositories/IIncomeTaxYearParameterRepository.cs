using FactuTrust.Domain.Entities.Fiscal;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>Valeurs saisies par le comptable pour un exercice (écran de paramétrage fiscal).</summary>
public sealed record IncomeTaxParameterUpsert(
    decimal IsStandardRate,
    decimal IsReducedRate,
    decimal IsSectorRate,
    decimal MinTaxRate,
    decimal MinTaxReducedRate,
    decimal MinTaxFloorTnd,
    decimal MinTaxFloorReducedTnd,
    bool CssApplies,
    decimal CssRate,
    decimal CssFloorTnd,
    decimal AcompteRate,
    int AcompteCount,
    int DeficitCarryForwardYears,
    bool RoundTaxableToDinar,
    string IrppBracketsJson);

/// <summary>Résout les paramètres d'impôt versionnés par exercice (tenant).</summary>
public interface IIncomeTaxYearParameterRepository
{
    /// <summary>Paramètres de l'exercice ; repli sur les défauts légaux si aucune ligne n'existe.</summary>
    Task<IncomeTaxYearParameter> GetOrDefaultAsync(int fiscalYear, CancellationToken ct = default);

    /// <summary>
    /// Enregistre les paramètres saisis par le comptable pour l'exercice (création si absente).
    /// La ligne est alors marquée « modifiée par l'utilisateur » et n'est plus rafraîchie par les défauts.
    /// </summary>
    Task<IncomeTaxYearParameter> UpsertAsync(int fiscalYear, IncomeTaxParameterUpsert upsert, CancellationToken ct = default);
}
