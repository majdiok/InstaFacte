namespace FactuTrust.Domain.Enums;

public enum FiscalObligationType
{
    MonthlyDeclaration = 0,
    ProvisionalCorporateTaxInstallment = 1,
    WithholdingTax = 2,
    Fodec = 3,
    QuarterlyVat = 4,
    FinancialStatements = 5,
    SemiAnnualFinancialStatements = 6,
    PersonalIncomeTaxInstallment = 7,
    /// <summary>Déclaration trimestrielle des salaires CNSS (DTS).</summary>
    CnssDtsQuarterly = 8,
    /// <summary>Versement mensuel des retenues IRPP salariés.</summary>
    PayrollIrppWithholding = 9,
    /// <summary>Versement mensuel des cotisations CNSS.</summary>
    CnssMonthlyRemittance = 10,
    Other = 99
}
