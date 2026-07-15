using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Infrastructure.Services.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Baseline diagnostic (plan import image BL) :
/// R1 — sans fichiers *.traineddata dans Resources/Tessdata, IsAvailable=false
///      → API 400 « Reconnaissance optique indisponible… »
/// R2 — qwen2.5:7b-instruct n'est pas vision → images sans fallback vision échouent si OCR vide
/// R5 — UI affichait Http failure 400 au lieu du message ApiResponse (corrigé phase 1)
/// </summary>
public sealed class InvoiceImportBaselineTests
{
    [Fact]
    public void TesseractOcrService_ReportsAvailability_FromTessdataFolder()
    {
        var ocr = new TesseractOcrService(NullLogger<TesseractOcrService>.Instance);
        var tessPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Tessdata");
        var hasFiles = Directory.Exists(tessPath)
            && Directory.EnumerateFiles(tessPath, "*.traineddata").Any();

        Assert.Equal(hasFiles, ocr.IsAvailable);
    }
}
