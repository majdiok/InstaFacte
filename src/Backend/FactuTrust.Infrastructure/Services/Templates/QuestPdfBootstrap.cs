using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services.Templates;

/// <summary>
/// Initialise les réglages globaux QuestPDF (licence Community, fallback de police Unicode) une seule
/// fois, quel que soit le point d'entrée (PdfService historique ou modèles visuels unifiés).
/// </summary>
internal static class QuestPdfBootstrap
{
    static QuestPdfBootstrap()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        QuestPDF.Settings.UseEnvironmentFonts = true;
    }

    /// <summary>No-op qui force l'exécution du constructeur statique.</summary>
    public static void EnsureInitialized()
    {
    }
}
