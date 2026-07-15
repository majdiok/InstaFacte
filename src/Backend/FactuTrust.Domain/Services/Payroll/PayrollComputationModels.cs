using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Variables d'entrée d'un calcul de bulletin pour un salarié sur un mois donné.
/// </summary>
public sealed class PayrollComputationInput
{
    /// <summary>Salaire de base mensuel brut.</summary>
    public decimal BaseSalary { get; init; }
    /// <summary>Primes/indemnités imposables et soumises à CNSS.</summary>
    public decimal TaxableCnssableAllowances { get; init; }
    /// <summary>Primes imposables mais non soumises à CNSS (matrice 4 quadrants).</summary>
    public decimal TaxableOnlyAllowances { get; init; }
    /// <summary>Indemnités soumises à CNSS mais non imposables (matrice 4 quadrants).</summary>
    public decimal CnssOnlyAllowances { get; init; }
    /// <summary>Indemnités non imposables et non soumises à CNSS (ajoutées au net).</summary>
    public decimal NonTaxableAllowances { get; init; }
    /// <summary>Montant des heures supplémentaires (imposable et soumis à CNSS).</summary>
    public decimal OvertimeAmount { get; init; }
    /// <summary>Retenue pour absences non rémunérées (réduit le brut).</summary>
    public decimal UnpaidAbsenceAmount { get; init; }
    /// <summary>Autres retenues (avances, oppositions) déduites du net.</summary>
    public decimal OtherDeductions { get; init; }

    public SocialRegime Regime { get; init; } = SocialRegime.Rsna;
    /// <summary>Taux d'accident de travail (charge patronale), en %.</summary>
    public decimal WorkAccidentRate { get; init; }
    /// <summary>Vrai si l'entreprise relève du secteur industriel (taux TFP réduit).</summary>
    public bool IsIndustrialSector { get; init; }

    public bool IsHeadOfFamily { get; init; }
    public int DependentChildren { get; init; }
}

/// <summary>
/// Résultat détaillé d'un calcul de bulletin.
/// </summary>
public sealed class PayrollComputation
{
    /// <summary>Brut total (inclut les indemnités non imposables).</summary>
    public decimal GrossSalary { get; init; }
    /// <summary>Brut soumis à cotisation CNSS et à l'impôt.</summary>
    public decimal CnssableGross { get; init; }
    public decimal CnssEmployee { get; init; }
    public decimal TaxableBaseAfterCnss { get; init; }
    public decimal ProfessionalExpenses { get; init; }
    public decimal FamilyDeductions { get; init; }
    public decimal MonthlyNetTaxable { get; init; }
    public decimal AnnualNetTaxable { get; init; }
    public decimal Irpp { get; init; }
    public decimal Css { get; init; }
    public decimal OtherDeductions { get; init; }
    public decimal NonTaxableAllowances { get; init; }
    public decimal NetSalary { get; init; }

    public decimal CnssEmployer { get; init; }
    public decimal WorkAccidentContribution { get; init; }
    public decimal Tfp { get; init; }
    public decimal Foprolos { get; init; }

    /// <summary>Total des charges patronales.</summary>
    public decimal TotalEmployerCharges => CnssEmployer + WorkAccidentContribution + Tfp + Foprolos;

    public IReadOnlyList<PayrollComputationLine> Lines { get; init; } = Array.Empty<PayrollComputationLine>();
}

/// <summary>
/// Ligne détaillée produite par le calcul (reprise telle quelle dans le bulletin).
/// </summary>
public sealed class PayrollComputationLine
{
    public int Order { get; init; }
    public string Label { get; init; } = string.Empty;
    public PayslipLineKind Kind { get; init; }
    public decimal? Base { get; init; }
    public decimal? Rate { get; init; }
    public decimal Amount { get; init; }
}
