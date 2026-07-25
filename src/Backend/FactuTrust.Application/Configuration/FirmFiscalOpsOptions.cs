namespace FactuTrust.Application.Configuration;

public sealed class FirmFiscalOpsOptions
{
    public const string SectionName = "Features:FirmFiscalOps";

    public bool Enabled { get; set; } = true;
}
