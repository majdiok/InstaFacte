namespace FactuTrust.Domain.Enums;

/// <summary>Périodicité des échéances d'un emprunt.</summary>
public enum LoanPeriodicity
{
    Monthly = 0,
    Quarterly = 1,
    SemiAnnual = 2,
    Annual = 3
}

/// <summary>Mode d'amortissement d'un emprunt.</summary>
public enum LoanAmortizationMethod
{
    /// <summary>Annuité constante : chaque échéance a le même montant total (capital + intérêt).</summary>
    ConstantAnnuity = 0,

    /// <summary>Amortissement constant : la part de capital est identique à chaque échéance.</summary>
    ConstantPrincipal = 1
}

/// <summary>Cycle de vie d'un emprunt au registre.</summary>
public enum LoanStatus
{
    Active = 0,
    Repaid = 1,
    Cancelled = 2
}

/// <summary>Nombre d'échéances par an et pas en mois, dérivés de la périodicité.</summary>
public static class LoanPeriodicityExtensions
{
    public static int PeriodsPerYear(this LoanPeriodicity periodicity) => periodicity switch
    {
        LoanPeriodicity.Monthly => 12,
        LoanPeriodicity.Quarterly => 4,
        LoanPeriodicity.SemiAnnual => 2,
        _ => 1
    };

    public static int MonthsPerPeriod(this LoanPeriodicity periodicity) => periodicity switch
    {
        LoanPeriodicity.Monthly => 1,
        LoanPeriodicity.Quarterly => 3,
        LoanPeriodicity.SemiAnnual => 6,
        _ => 12
    };
}
