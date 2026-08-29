using FactuTrust.Application.Common.Fiscal;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Implémentation de <see cref="IFiscalYearResolver"/> (plan « Exercices décalés ») déléguant à
/// <see cref="FiscalYearMath"/> (source de vérité canonique, partagée avec le moteur et les
/// handlers). Aucun état : substituable en test et utilisable comme singleton.
/// </summary>
public sealed class FiscalYearResolver : IFiscalYearResolver
{
    public int FiscalYearKey(DateTime date, int fiscalYearStartMonth)
        => FiscalYearMath.Key(date, fiscalYearStartMonth);

    public DateTime FiscalYearStartDateTime(int fiscalYearKey, int fiscalYearStartMonth)
        => FiscalYearMath.StartDateTime(fiscalYearKey, fiscalYearStartMonth);

    public DateTime FiscalYearEndDateTime(int fiscalYearKey, int fiscalYearStartMonth)
        => FiscalYearMath.EndDateTime(fiscalYearKey, fiscalYearStartMonth);

    public string FiscalYearLabel(int fiscalYearKey, int fiscalYearStartMonth, string labelFormat)
        => FiscalYearMath.Label(fiscalYearKey, fiscalYearStartMonth, labelFormat);
}
