namespace FactuTrust.Application.Configuration;

public sealed class PayrollOptions
{
    public const string SectionName = "Features:Payroll";

    /// <summary>
    /// When true, firm-exclusive payroll operations are restricted to delegated firm context
    /// for companies with an active cabinet assignment.
    /// </summary>
    public bool FirmExclusiveOperations { get; set; } = true;
}
