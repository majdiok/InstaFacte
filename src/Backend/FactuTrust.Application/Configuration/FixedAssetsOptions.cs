namespace FactuTrust.Application.Configuration;

public sealed class FixedAssetsOptions
{
    public const string SectionName = "Features:FixedAssets";

    public bool Enabled { get; set; } = true;
}