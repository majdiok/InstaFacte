using System.Reflection;
using PdfSharp.Fonts;

namespace FactuTrust.Infrastructure.Services.OfficialForms;

/// <summary>
/// Résolveur de police pour PDFsharp. Obligatoire : PDFsharp 6 lève une exception sur
/// <c>DrawString</c> si aucun résolveur n'est enregistré, et l'image runtime de l'API
/// (<c>deploy/Dockerfile.api</c>) installe les <i>bibliothèques</i> de rendu (fontconfig,
/// freetype, harfbuzz) mais <b>aucun fichier de police système</b>.
///
/// La police est donc embarquée dans l'assembly (Lato, SIL OFL — la même famille que celle
/// utilisée par tous les autres PDF de l'application via QuestPDF, cf.
/// <c>PdfService.DefaultTextStyle</c>), ce qui garantit un rendu identique sur Windows,
/// Linux et en CI.
///
/// Seuls des chiffres, des séparateurs et de courtes chaînes latines sont tamponnés sur les
/// formulaires officiels : les libellés arabes sont déjà imprimés dans le gabarit. Aucun
/// façonnage (shaping) RTL n'est donc nécessaire.
/// </summary>
internal sealed class OfficialFormFontResolver : IFontResolver
{
    /// <summary>Nom de famille à passer à <see cref="PdfSharp.Drawing.XFont"/>.</summary>
    internal const string FontFamily = "FactuTrustOfficialForm";

    private const string FaceName = "FactuTrustOfficialForm#Regular";
    private const string ResourceName =
        "FactuTrust.Infrastructure.Resources.OfficialForms.Fonts.Lato-Regular.ttf";

    private static readonly Lazy<byte[]> FontData = new(LoadFont, LazyThreadSafetyMode.ExecutionAndPublication);

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        => new FontResolverInfo(FaceName);

    public byte[]? GetFont(string faceName) => FontData.Value;

    private static byte[] LoadFont()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Police embarquée introuvable : « {ResourceName} ». Vérifier l'entrée EmbeddedResource " +
                "du csproj (Resources\\OfficialForms\\Fonts\\Lato-Regular.ttf).");

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
