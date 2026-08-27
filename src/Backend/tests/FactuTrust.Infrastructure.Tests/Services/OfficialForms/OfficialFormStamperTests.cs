using FactuTrust.Infrastructure.Services.OfficialForms;
using Microsoft.Extensions.Logging.Abstractions;
using UglyToad.PdfPig;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.OfficialForms;

/// <summary>
/// Vérifie le moteur de tamponnage des formulaires officiels sur le vrai gabarit DGI embarqué.
///
/// Note de méthode : la vérification se fait au niveau <b>caractère</b> (position exacte), et non
/// par recherche de sous-chaîne dans le texte extrait. Le gabarit contient des suites de points et
/// du texte arabe qui se mélangent aux valeurs tamponnées lors de la reconstruction des lignes :
/// une assertion « le texte contient 1234,500 » échouerait alors même que le PDF est correct.
/// </summary>
public sealed class OfficialFormStamperTests
{
    private const string MapName = "mensuelle-2026";

    private static OfficialFormStamper BuildStamper()
        => new(NullLogger<OfficialFormStamper>.Instance);

    [Fact]
    public void Map_Loads_AndIsInternallyConsistent()
    {
        var map = OfficialFormFieldMap.Load(MapName);

        Assert.Equal("2026", map.TemplateVersion);
        Assert.Equal("mensuelle-2026.pdf", map.TemplateFile);
        Assert.NotEmpty(map.Fields);

        // Toutes les cases doivent viser une page réelle du gabarit.
        Assert.All(map.Fields, f => Assert.InRange(f.Page, 1, map.PageCount));

        // Et rester dans les limites d'une A4 (595,3 x 841,9 pt).
        Assert.All(map.Fields, f =>
        {
            Assert.InRange(f.X, 0d, 595.3d);
            Assert.InRange(f.Y, 0d, 841.9d);
        });

        // L'index par clé impose déjà l'unicité ; on vérifie qu'il couvre bien tout.
        Assert.Equal(map.Fields.Count, map.ByKey.Count);

        Assert.True(map.ByKey.ContainsKey("Withholding.Line3.Base"));
        Assert.True(map.ByKey.ContainsKey("Withholding.Line3.Amount"));
        var line3 = map.ByKey["Withholding.Line3.Amount"];
        Assert.Equal(1, line3.Page);
        Assert.Equal(91.0, line3.X);
        Assert.InRange(line3.Y, 459.1, 459.3);
        Assert.Equal(map.ByKey["Withholding.Line1.Base"].X, map.ByKey["Withholding.Line3.Base"].X);
        Assert.NotEqual(map.ByKey["Withholding.Line4Individuals.Amount"].Y, line3.Y);
    }

    [Fact]
    public void Stamp_PreservesTemplateStructure()
    {
        var map = OfficialFormFieldMap.Load(MapName);

        var bytes = BuildStamper().Stamp(map, new Dictionary<string, string?>
        {
            ["Recap.Total.Total"] = "2 312,380"
        });

        Assert.NotEmpty(bytes);

        using var document = PdfDocument.Open(bytes);

        // Le gabarit officiel ne doit jamais perdre de page.
        Assert.Equal(map.PageCount, document.NumberOfPages);

        // Les libellés arabes préimprimés doivent survivre à l'aller-retour : sans eux, le
        // document ne serait plus le formulaire officiel.
        var page5 = document.GetPage(5);
        var arabicCount = page5.Text.Count(c => c >= '؀' && c <= 'ۿ');
        Assert.True(arabicCount > 500, $"Libellés arabes perdus sur la page 5 (seulement {arabicCount} caractères).");
    }

    [Fact]
    public void Stamp_PlacesValueAtMappedCoordinates()
    {
        var map = OfficialFormFieldMap.Load(MapName);
        var field = map.ByKey["Recap.Total.Total"];

        var bytes = BuildStamper().Stamp(map, new Dictionary<string, string?>
        {
            [field.Key] = "2312,380"
        });

        using var document = PdfDocument.Open(bytes);
        var page = document.GetPage(field.Page);

        // PdfPig travaille en repère PDF natif (origine en bas à gauche) alors que la carte est
        // exprimée en repère haut-gauche : on convertit pour comparer.
        var expectedBaseline = page.Height - field.Y;

        var stamped = page.Letters
            .Where(l => Math.Abs(l.StartBaseLine.Y - expectedBaseline) < 2d
                        && l.Value.Length == 1
                        && (char.IsDigit(l.Value[0]) || l.Value[0] == ','))
            .OrderBy(l => l.StartBaseLine.X)
            .ToList();

        Assert.NotEmpty(stamped);

        var text = string.Concat(stamped.Select(l => l.Value));
        Assert.Contains("2312,380", text);

        // Alignement à droite : la valeur se termine sur le X de la case.
        var right = stamped.Max(l => l.EndBaseLine.X);
        Assert.InRange(right, field.X - 2d, field.X + 2d);
    }

    [Fact]
    public void Stamp_PlacesCssOnArticleThreeNotOnCommissionsLine()
    {
        var map = OfficialFormFieldMap.Load(MapName);
        var line3 = map.ByKey["Withholding.Line3.Amount"];
        var line4 = map.ByKey["Withholding.Line4Individuals.Amount"];

        var bytes = BuildStamper().Stamp(map, new Dictionary<string, string?>
        {
            [line3.Key] = "5,926"
        });

        using var document = PdfDocument.Open(bytes);
        var page = document.GetPage(line3.Page);
        var expectedBaseline = page.Height - line3.Y;

        var stamped = page.Letters
            .Where(l => Math.Abs(l.StartBaseLine.Y - expectedBaseline) < 2d
                        && l.Value.Length == 1
                        && (char.IsDigit(l.Value[0]) || l.Value[0] == ','))
            .OrderBy(l => l.StartBaseLine.X)
            .ToList();

        Assert.NotEmpty(stamped);
        Assert.Contains("5,926", string.Concat(stamped.Select(l => l.Value)));

        var line4Baseline = page.Height - line4.Y;
        var leakedOnLine4 = page.Letters
            .Where(l => Math.Abs(l.StartBaseLine.Y - line4Baseline) < 2d)
            .OrderBy(l => l.StartBaseLine.X)
            .Select(l => l.Value);
        Assert.DoesNotContain("5,926", string.Concat(leakedOnLine4));
    }

    [Fact]
    public void Stamp_LeavesUnsuppliedAndEmptyCellsBlank()
    {
        var map = OfficialFormFieldMap.Load(MapName);

        var blank = BuildStamper().Stamp(map, new Dictionary<string, string?>());
        var withEmpty = BuildStamper().Stamp(map, new Dictionary<string, string?>
        {
            ["Recap.Vat.Principal"] = "",
            ["Recap.Vat.Due"] = null
        });

        using var blankDoc = PdfDocument.Open(blank);
        using var emptyDoc = PdfDocument.Open(withEmpty);

        // Une case vide ne doit rien ajouter : sur une déclaration fiscale, « rien » et « 0,000 »
        // n'ont pas le même sens.
        Assert.Equal(blankDoc.GetPage(9).Letters.Count, emptyDoc.GetPage(9).Letters.Count);
    }

    [Fact]
    public void Stamp_IgnoresUnknownKeysWithoutThrowing()
    {
        var map = OfficialFormFieldMap.Load(MapName);

        var exception = Record.Exception(() => BuildStamper().Stamp(map, new Dictionary<string, string?>
        {
            ["Cle.Qui.NExiste.Pas"] = "123",
            ["Recap.Total.Total"] = "999,000"
        }));

        // Une carte désynchronisée doit se voir dans les logs, jamais casser l'export.
        Assert.Null(exception);
    }
}
