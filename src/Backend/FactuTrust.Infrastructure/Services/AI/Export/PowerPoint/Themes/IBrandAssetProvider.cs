namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;

/// <summary>
/// Supplies branding assets (logo bytes) embedded in PowerPoint exports. Reading from the
/// filesystem (the existing <c>wwwroot</c> logo) is wrapped in an interface so the renderer
/// remains testable.
/// </summary>
public interface IBrandAssetProvider
{
    /// <summary>Returns the PNG bytes of the FactuTrust logo, or <c>null</c> when unavailable.</summary>
    byte[]? GetLogoPng();
}
