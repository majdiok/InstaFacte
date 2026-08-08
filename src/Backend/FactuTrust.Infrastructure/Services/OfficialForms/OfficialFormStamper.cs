using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace FactuTrust.Infrastructure.Services.OfficialForms;

/// <summary>
/// Moteur générique de tamponnage d'un formulaire officiel préimprimé : ouvre un gabarit PDF
/// embarqué et dessine les valeurs fournies aux coordonnées décrites par une
/// <see cref="OfficialFormFieldMap"/>.
///
/// Pourquoi ne pas régénérer le document avec QuestPDF (moteur PDF historique du projet) :
/// QuestPDF <b>ne sait pas importer un PDF existant</b>, et le formulaire officiel de la DGI est
/// un document arabe RTL de 12 pages dont la mise en page doit être reproduite au trait près.
/// On conserve donc le gabarit officiel tel quel et on n'y ajoute que des chiffres — approche
/// qui évite entièrement le façonnage arabe et garantit la conformité visuelle.
///
/// Le gabarit fourni par la DGI ne comporte <b>aucun champ AcroForm</b> (PDF plat issu de Word) :
/// le positionnement absolu est la seule option possible.
/// </summary>
public sealed class OfficialFormStamper
{
    private readonly ILogger<OfficialFormStamper> _logger;

    private static int _initialized;

    public OfficialFormStamper(ILogger<OfficialFormStamper> logger) => _logger = logger;

    /// <summary>
    /// Applique <paramref name="values"/> sur le gabarit décrit par <paramref name="map"/> et
    /// retourne le PDF résultant.
    ///
    /// Les clés absentes de <paramref name="values"/> — ou dont la valeur est vide — laissent la
    /// case <b>vierge</b> : sur une déclaration fiscale, une case vide et un « 0,000 » n'ont pas
    /// le même sens, et le formulaire se remplit traditionnellement en n'inscrivant que les
    /// lignes concernées.
    /// </summary>
    /// <param name="map">Carte des coordonnées du millésime visé.</param>
    /// <param name="values">Valeurs déjà formatées, indexées par clé de case.</param>
    public byte[] Stamp(OfficialFormFieldMap map, IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(values);

        EnsureInitialized();

        using var template = OpenTemplate(map.TemplateFile);

        // NB : PdfDocument.PageCount devient inaccessible après Save() — on le lit maintenant.
        var pageCount = template.PageCount;

        // Un seul XGraphics par page : le créer est coûteux et le recréer sur une page déjà
        // ouverte réinitialiserait l'état graphique.
        var canvases = new Dictionary<int, XGraphics>();
        var fonts = new Dictionary<double, XFont>();
        var unknownKeys = new List<string>();

        try
        {
            foreach (var (key, rawValue) in values)
            {
                if (string.IsNullOrWhiteSpace(rawValue))
                    continue;

                if (!map.ByKey.TryGetValue(key, out var field))
                {
                    unknownKeys.Add(key);
                    continue;
                }

                if (field.Page < 1 || field.Page > pageCount)
                {
                    _logger.LogWarning(
                        "Formulaire officiel {Template} : la case {Key} vise la page {Page}, hors du gabarit ({PageCount} pages). Case ignorée.",
                        map.TemplateVersion, key, field.Page, pageCount);
                    continue;
                }

                if (!canvases.TryGetValue(field.Page, out var gfx))
                {
                    gfx = XGraphics.FromPdfPage(
                        template.Pages[field.Page - 1],
                        XGraphicsPdfPageOptions.Append);
                    canvases[field.Page] = gfx;
                }

                if (!fonts.TryGetValue(field.FontSize, out var font))
                {
                    font = new XFont(OfficialFormFontResolver.FontFamily, field.FontSize, XFontStyleEx.Regular);
                    fonts[field.FontSize] = font;
                }

                Draw(gfx, font, field, rawValue!);
            }

            if (unknownKeys.Count > 0)
            {
                // Symptôme d'une carte désynchronisée du binder : visible en log, jamais bloquant
                // pour l'utilisateur (le reste du formulaire est correctement rempli).
                _logger.LogWarning(
                    "Formulaire officiel {Template} : {Count} clé(s) inconnue(s) dans la carte — {Keys}.",
                    map.TemplateVersion, unknownKeys.Count, string.Join(", ", unknownKeys));
            }

            foreach (var gfx in canvases.Values)
                gfx.Dispose();
            canvases.Clear();

            using var output = new MemoryStream();
            template.Save(output, closeStream: false);
            return output.ToArray();
        }
        finally
        {
            foreach (var gfx in canvases.Values)
                gfx.Dispose();
        }
    }

    private static void Draw(XGraphics gfx, XFont font, OfficialFormField field, string value)
    {
        // XStringAlignment.Far fait coïncider le bord droit du texte avec X (vérifié au Lot 0),
        // ce qui correspond au remplissage des colonnes de montants du formulaire.
        var alignment = field.Align switch
        {
            OfficialFormAlign.Left => XStringAlignment.Near,
            OfficialFormAlign.Center => XStringAlignment.Center,
            _ => XStringAlignment.Far
        };

        // Les emplacements du gabarit sont matérialisés par des suites de points ; sans masque,
        // ceux-ci transparaissent entre les caractères (« Ste.Bouzgarou »). On blanchit donc la
        // stricte emprise du texte avant de l'écrire, comme le ferait un formulaire dactylographié.
        var size = gfx.MeasureString(value, font);
        var left = alignment switch
        {
            XStringAlignment.Near => field.X,
            XStringAlignment.Center => field.X - size.Width / 2,
            _ => field.X - size.Width
        };

        const double padding = 0.6;
        gfx.DrawRectangle(
            XBrushes.White,
            new XRect(
                left - padding,
                field.Y - font.GetHeight() * 0.78,
                size.Width + padding * 2,
                font.GetHeight() * 0.95));

        var format = new XStringFormat
        {
            Alignment = alignment,
            LineAlignment = XLineAlignment.BaseLine
        };

        gfx.DrawString(value, font, XBrushes.Black, new XPoint(field.X, field.Y), format);
    }

    private static PdfDocument OpenTemplate(string templateFile)
    {
        var resourceName = $"FactuTrust.Infrastructure.Resources.OfficialForms.{templateFile}";
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Gabarit de formulaire officiel introuvable : « {resourceName} ».");

        // PdfReader a besoin d'un flux redimensionnable en mode Modify.
        var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;

        return PdfReader.Open(buffer, PdfDocumentOpenMode.Modify);
    }

    /// <summary>
    /// Initialisation globale de PDFsharp, idempotente et thread-safe.
    /// </summary>
    private static void EnsureInitialized()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
            return;

        // Certains PDF produits par Word référencent des encodages hérités absents du profil
        // .NET par défaut ; sans ce fournisseur, PdfReader.Open échoue à la lecture.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Aucune police système n'est installée dans l'image runtime : on impose la nôtre.
        GlobalFontSettings.FontResolver ??= new OfficialFormFontResolver();
    }
}
