namespace FactuTrust.Application.Configuration;

/// <summary>
/// Kill-switch for the native Projects / PSA module.
/// Bound from <c>Features:Projects</c>. Licence <see cref="FactuTrust.Domain.Enums.AppModule.Projects"/>
/// remains required even when this flag is on.
/// </summary>
public sealed class ProjectsOptions
{
    public const string SectionName = "Features:Projects";

    /// <summary>
    /// When false, Projects API returns HTTP 503. Default false so unlicensed tenants stay untouched
    /// until the flag is enabled in configuration.
    /// </summary>
    public bool Enabled { get; set; }
}
