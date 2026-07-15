namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>Résout les paramètres fiscaux RS versionnés par année (tenant).</summary>
public interface IWithholdingFiscalYearParameterRepository
{
    /// <summary>Seuil TTC RS7 en TND pour l'année ; défaut 1000 si aucune ligne en base.</summary>
    Task<decimal> GetRs7TtcThresholdAsync(int fiscalYear, CancellationToken ct = default);
}
