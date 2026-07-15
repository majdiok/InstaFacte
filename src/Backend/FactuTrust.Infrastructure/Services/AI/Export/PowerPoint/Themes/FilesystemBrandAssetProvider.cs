using Microsoft.Extensions.Logging;

using FactuTrust.Domain.Constants;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;

/// <summary>
/// Loads branding assets from the filesystem (wwwroot folder of the API project). The result is
/// cached in memory for the lifetime of the application — assets are static at runtime.
/// </summary>
public sealed class FilesystemBrandAssetProvider : IBrandAssetProvider
{
    private static readonly string[] CandidateRelativePaths = new[]
    {
        Path.Combine("wwwroot", "assets", "theme", "pluto", "images", "logo", "logo.png"),
        Path.Combine("wwwroot", "logo.png"),
        Path.Combine("assets", "logo.png")
    };

    private readonly Lazy<byte[]?> _logoBytes;
    private readonly ILogger<FilesystemBrandAssetProvider> _logger;

    public FilesystemBrandAssetProvider(ILogger<FilesystemBrandAssetProvider> logger)
    {
        _logger = logger;
        _logoBytes = new Lazy<byte[]?>(LoadLogo, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public byte[]? GetLogoPng() => _logoBytes.Value;

    private byte[]? LoadLogo()
    {
        var roots = new[]
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        };

        foreach (var root in roots.Distinct())
        {
            foreach (var rel in CandidateRelativePaths)
            {
                var fullPath = Path.Combine(root, rel);
                if (File.Exists(fullPath))
                {
                    try
                    {
                        return File.ReadAllBytes(fullPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Logo trouvé mais illisible: {Path}. L'export PowerPoint continuera sans logo.",
                            fullPath);
                    }
                }
            }
        }

        _logger.LogInformation(
            "Aucun logo {Brand} trouvé sur disque (chemins testés: {Paths}). " +
            "Les exports PowerPoint utiliseront un en-tête textuel.",
            BrandConstants.Name,
            string.Join(", ", CandidateRelativePaths));
        return null;
    }
}
