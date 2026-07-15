using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Infrastructure.Services.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Tests d'extraction de pièces jointes pour l'Assistant IA. Cible :
/// - Le pipeline PDF layout-aware (NearestNeighbour + Docstrum) doit produire un
///   texte avec espaces et sauts de ligne corrects (fix du bug visible dans la capture
///   utilisateur où "Date facture : 09/05/2026Échéance : 08/06/2026" sortait collé).
/// - Les formats DOCX, XLSX, TXT, CSV.
/// - Les comportements d'erreur (format inconnu).
/// </summary>
public class AiDocumentTextExtractorTests
{
    private static readonly Mock<IAiPdfRenderer> StubPdfRenderer = BuildPdfRendererStub();
    private static readonly Mock<IAiOcrService> StubOcr = BuildOcrStub();

    static AiDocumentTextExtractorTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static AiDocumentTextExtractor Build()
    {
        return new AiDocumentTextExtractor(
            NullLogger<AiDocumentTextExtractor>.Instance,
            StubPdfRenderer.Object,
            StubOcr.Object,
            BuildImagePreprocessingStub().Object);
    }

    private static Mock<IImagePreprocessingService> BuildImagePreprocessingStub()
    {
        var m = new Mock<IImagePreprocessingService>();
        m.Setup(x => x.PreprocessForOcr(It.IsAny<byte[]>())).Returns<byte[]>(b => b);
        return m;
    }

    private static Mock<IAiPdfRenderer> BuildPdfRendererStub()
    {
        var m = new Mock<IAiPdfRenderer>(MockBehavior.Loose);
        m.SetupGet(x => x.IsAvailable).Returns(false);
        return m;
    }

    private static Mock<IAiOcrService> BuildOcrStub()
    {
        var m = new Mock<IAiOcrService>(MockBehavior.Loose);
        m.SetupGet(x => x.IsAvailable).Returns(false);
        return m;
    }

    [Fact]
    public async Task ExtractAsync_RejectsUnknownFormat()
    {
        var bytes = Encoding.UTF8.GetBytes("ignored");
        using var stream = new MemoryStream(bytes);

        var result = await Build().ExtractAsync(stream, "data.bin", "application/octet-stream");

        Assert.False(result.Success);
        Assert.Contains("Format", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractAsync_ReadsPlainText()
    {
        var content = "Bonjour le monde\nSeconde ligne";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = await Build().ExtractAsync(stream, "note.txt", "text/plain");

        Assert.True(result.Success);
        Assert.Equal("txt", result.Format);
        Assert.Contains("Bonjour le monde", result.Text);
        Assert.Contains("Seconde ligne", result.Text);
        Assert.False(result.Truncated);
    }

    [Fact]
    public async Task ExtractAsync_ReadsCsv()
    {
        var content = "col1,col2\nA,B\nC,D";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = await Build().ExtractAsync(stream, "data.csv", "text/csv");

        Assert.True(result.Success);
        Assert.Equal("csv", result.Format);
        Assert.Contains("A,B", result.Text);
    }

    [Fact]
    public async Task ExtractAsync_PdfWithNativeText_ReinsertsSpaces()
    {
        // Génère un PDF avec des couples "label : valeur" — chaque morceau est posé
        // en colonnes différentes, ce qui reproduit exactement le bug du screenshot
        // (sortie collée "Date facture : 09/05/2026Échéance : 08/06/2026" sans la
        // refonte layout-aware).
        var pdfBytes = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4);
                page.Content().Column(col =>
                {
                    col.Item().Text("FACTURE N° AVO-2026-000002");
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Date facture : 09/05/2026");
                        row.RelativeItem().Text("Échéance : 08/06/2026");
                    });
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Total HT : 2,010.500 TND");
                        row.RelativeItem().Text("TVA 19% : 381.995 TND");
                    });
                    col.Item().Text("TOTAL TTC : 2,391.495 TND");
                });
            });
        }).GeneratePdf();

        using var stream = new MemoryStream(pdfBytes);
        var result = await Build().ExtractAsync(stream, "facture.pdf", "application/pdf");

        Assert.True(result.Success);
        Assert.Equal("pdf", result.Format);
        Assert.Equal(1, result.PageCount);

        // Critique : les labels et valeurs doivent rester séparés par un espace.
        Assert.Contains("Date facture", result.Text);
        Assert.Contains("09/05/2026", result.Text);
        Assert.Contains("Échéance", result.Text);
        Assert.Contains("08/06/2026", result.Text);
        Assert.Contains("TOTAL TTC", result.Text);
        Assert.Contains("2,391.495", result.Text);

        // Régression du bug : ne doit PAS produire de concaténation directe
        // "09/05/2026Échéance" ni "381.995Total".
        Assert.DoesNotContain("2026Échéance", result.Text);
        Assert.DoesNotContain("381.995Total", result.Text);
    }

    [Fact]
    public async Task ExtractAsync_PdfMultiPage_InsertsPageHeaders()
    {
        var pdfBytes = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4);
                page.Content().Text("Contenu page 1");
            });
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4);
                page.Content().Text("Contenu page 2");
            });
        }).GeneratePdf();

        using var stream = new MemoryStream(pdfBytes);
        var result = await Build().ExtractAsync(stream, "doc.pdf", "application/pdf");

        Assert.True(result.Success);
        Assert.Equal(2, result.PageCount);
        Assert.Contains("--- Page 1 ---", result.Text);
        Assert.Contains("--- Page 2 ---", result.Text);
        Assert.Contains("Contenu page 1", result.Text);
        Assert.Contains("Contenu page 2", result.Text);
    }

    [Fact]
    public async Task ExtractAsync_CorruptPdf_FailsGracefully()
    {
        var notAPdf = Encoding.UTF8.GetBytes("Hello, world (definitely not a PDF)");
        using var stream = new MemoryStream(notAPdf);

        var result = await Build().ExtractAsync(stream, "fake.pdf", "application/pdf");

        Assert.False(result.Success);
        Assert.Contains("PDF", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractAsync_Docx_ReadsParagraphsAndTables()
    {
        var docxBytes = BuildDocxInMemory("Premier paragraphe.", "Deuxième paragraphe.");
        using var stream = new MemoryStream(docxBytes);

        var result = await Build().ExtractAsync(stream, "test.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        Assert.True(result.Success);
        Assert.Equal("docx", result.Format);
        Assert.Contains("Premier paragraphe.", result.Text);
        Assert.Contains("Deuxième paragraphe.", result.Text);
    }

    [Fact]
    public async Task ExtractAsync_Xlsx_ReadsCellsTabSeparated()
    {
        var xlsxBytes = BuildXlsxInMemory("Feuille1", new[]
        {
            new[] { "Produit", "Quantité", "PU" },
            new[] { "Stylo", "7", "1.5" },
            new[] { "Bureau", "5", "400" }
        });
        using var stream = new MemoryStream(xlsxBytes);

        var result = await Build().ExtractAsync(stream, "stock.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        Assert.True(result.Success);
        Assert.Equal("xlsx", result.Format);
        Assert.Contains("=== Feuille : Feuille1 ===", result.Text);
        Assert.Contains("Produit\tQuantité\tPU", result.Text);
        Assert.Contains("Stylo\t7\t1.5", result.Text);
        Assert.Contains("Bureau\t5\t400", result.Text);
    }

    [Fact]
    public async Task ExtractAsync_Image_WithoutOcrService_ReturnsImagePayloadForVisionFallback()
    {
        // OCR indisponible : succès avec texte vide + image pour fallback vision côté handler.
        var fakeImageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        using var stream = new MemoryStream(fakeImageBytes);

        var result = await Build().ExtractAsync(stream, "scan.png", "image/png");

        Assert.True(result.Success);
        Assert.Equal(string.Empty, result.Text);
        Assert.NotNull(result.Pages[0].ImageBase64);
        Assert.Contains(result.Warnings, w => w.Contains("optique", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExtractAsync_TextTruncatesAt200kCharacters()
    {
        var huge = new string('A', 250_000);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(huge));

        var result = await Build().ExtractAsync(stream, "big.txt", "text/plain");

        Assert.True(result.Success);
        Assert.True(result.Truncated);
        Assert.True(result.Text.Length <= 200_000 + 5);  // +marqueur "\n…"
        Assert.EndsWith("…", result.Text);
    }

    private static byte[] BuildDocxInMemory(params string[] paragraphs)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(new Body(
                paragraphs.Select(p =>
                    new Paragraph(new Run(new Text(p))) as OpenXmlElement
                ).ToArray()
            ));
            main.Document.Save();
        }
        return ms.ToArray();
    }

    private static byte[] BuildXlsxInMemory(string sheetName, string[][] rows)
    {
        using var ms = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add(sheetName);
            for (var r = 0; r < rows.Length; r++)
            {
                for (var c = 0; c < rows[r].Length; c++)
                {
                    ws.Cell(r + 1, c + 1).Value = rows[r][c];
                }
            }
            wb.SaveAs(ms);
        }
        return ms.ToArray();
    }
}
