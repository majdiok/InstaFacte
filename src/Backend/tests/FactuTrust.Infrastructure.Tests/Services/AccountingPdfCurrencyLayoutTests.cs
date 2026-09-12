using System.Net.Http;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Multi-devises, lot 4 : colonne Devise dans les PDF du journal et du grand livre.
///
/// <para>
/// <b>Ce que ces tests remplacent.</b> Une colonne ajoutée à un tableau PDF peut faire déborder la
/// page sans que rien ne le signale à la compilation. QuestPDF, lui, <b>lève</b> quand le contenu
/// ne tient pas dans les largeurs déclarées : générer sans exception, avec des libellés
/// délibérément longs, vaut donc contrôle de mise en page.
/// </para>
/// </summary>
public sealed class AccountingPdfCurrencyLayoutTests
{
    /// <summary>Libellé volontairement long : c'est lui qui pousse la colonne relative à ses limites.</summary>
    private const string LongLabel =
        "Achat de prestations de sous-traitance informatique auprès d'un fournisseur établi "
        + "hors de Tunisie, facture FS-2026-000198 du 12 février 2026, échéance à 60 jours";

    private static PdfService BuildPdfService() =>
        new(new Mock<IHttpClientFactory>().Object, new Mock<IDocumentTemplateRegistry>().Object);

    private static AccountingReportHeader Header(string title) =>
        new("Société de Test SARL", "1234567/A/M/000", title, "Février 2026");

    private static void AssertIsPdf(byte[] bytes)
    {
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 1000, "Le PDF généré est anormalement court.");
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }

    private static JournalEntryDto Entry(string currencyCode, decimal rate) => new()
    {
        EntryNumber = 57,
        JournalCode = "JA",
        EntryDate = new DateTime(2026, 2, 12),
        Label = LongLabel,
        CurrencyCode = currencyCode,
        ExchangeRate = rate,
        Lines = new List<JournalEntryLineDto>
        {
            new()
            {
                AccountNumber = "60400000", Label = LongLabel,
                Debit = 3302.000m, Credit = 0m,
                DebitInCurrency = currencyCode == "TND" ? 0m : 1000m
            },
            new()
            {
                AccountNumber = "40110000", Label = LongLabel,
                Debit = 0m, Credit = 3302.000m,
                CreditInCurrency = currencyCode == "TND" ? 0m : 1000m
            }
        }
    };

    private static LedgerRowDto LedgerRow(string currencyCode, decimal amountInCurrency) => new()
    {
        EntryDate = new DateTime(2026, 2, 12),
        JournalCode = "JA",
        PieceNumber = 57,
        Label = LongLabel,
        Debit = 3302.000m,
        Credit = 0m,
        RunningBalance = 3302.000m,
        CurrencyCode = currencyCode,
        AmountInCurrency = amountInCurrency
    };

    // ── Journal ────────────────────────────────────────────────────────────

    [Fact]
    public async Task JournalPdf_InFunctionalCurrency_StillRenders()
    {
        var bytes = await BuildPdfService().GenerateJournalPdfAsync(
            new[] { Entry("TND", 1m) }, Header("Journal"), CancellationToken.None);

        AssertIsPdf(bytes);
    }

    [Fact]
    public async Task JournalPdf_WithForeignCurrency_RendersWithoutOverflow()
    {
        var bytes = await BuildPdfService().GenerateJournalPdfAsync(
            new[] { Entry("EUR", 3.30200m) }, Header("Journal"), CancellationToken.None);

        AssertIsPdf(bytes);
    }

    [Fact]
    public async Task JournalPdf_MixingCurrencies_RendersWithoutOverflow()
    {
        // Cas réel d'un dossier qui bascule : des écritures en dinar et en devise sur la même période.
        var bytes = await BuildPdfService().GenerateJournalPdfAsync(
            new[] { Entry("TND", 1m), Entry("EUR", 3.30200m), Entry("USD", 3.10000m) },
            Header("Journal"), CancellationToken.None);

        AssertIsPdf(bytes);
    }

    /// <summary>
    /// La colonne n'apparaît que si la période contient une opération en devise : un dossier
    /// mono-devise obtient donc un document strictement identique à celui d'avant le chantier.
    /// </summary>
    [Fact]
    public async Task JournalPdf_InFunctionalCurrency_IsShorterThanTheForeignOne()
    {
        var svc = BuildPdfService();
        var functional = await svc.GenerateJournalPdfAsync(new[] { Entry("TND", 1m) }, Header("Journal"), CancellationToken.None);
        var foreign = await svc.GenerateJournalPdfAsync(new[] { Entry("EUR", 3.30200m) }, Header("Journal"), CancellationToken.None);

        Assert.True(foreign.Length > functional.Length,
            "Le PDF en devise doit porter une colonne de plus que celui en devise de tenue.");
    }

    // ── Grand livre ────────────────────────────────────────────────────────

    [Fact]
    public async Task LedgerPdf_InFunctionalCurrency_StillRenders()
    {
        var bytes = await BuildPdfService().GenerateLedgerPdfAsync(
            "40110000", new[] { LedgerRow("TND", 0m) }, Header("Grand livre"), CancellationToken.None);

        AssertIsPdf(bytes);
    }

    [Fact]
    public async Task LedgerPdf_WithForeignCurrency_RendersWithoutOverflow()
    {
        var bytes = await BuildPdfService().GenerateLedgerPdfAsync(
            "40110000", new[] { LedgerRow("EUR", 1000m) }, Header("Grand livre"), CancellationToken.None);

        AssertIsPdf(bytes);
    }

    // -- Grand livre tiers ---------------------------------------------------
    //
    // C'est la mise en page la plus contrainte : huit colonnes constantes avant la colonne Devise.
    // Si une largeur devait deborder quelque part, c'est ici.

    private static ThirdPartyLedgerDto ThirdPartyLedger(string currencyCode, int rowCount = 1) => new()
    {
        ThirdPartyId = Guid.NewGuid(),
        ThirdPartyName = "Fournisseur International SARL",
        OpeningBalance = 1500.000m,
        Rows = Enumerable.Range(0, rowCount).Select(_ => new ThirdPartyLedgerRowDto
        {
            EntryDate = new DateTime(2026, 2, 12),
            JournalCode = "JA",
            PieceNumber = 57,
            PieceRef = "FS-2026-000198",
            AccountNumber = "40110000",
            Label = LongLabel,
            Debit = 3302.000m,
            Credit = 0m,
            RunningBalance = 4802.000m,
            LetteringCode = "L00042",
            CurrencyCode = currencyCode,
            AmountInCurrency = currencyCode == "TND" ? 0m : 1000m
        }).ToList()
    };

    [Fact]
    public async Task ThirdPartyLedgerPdf_InFunctionalCurrency_StillRenders()
    {
        var bytes = await BuildPdfService().GenerateThirdPartyLedgerPdfAsync(
            ThirdPartyLedger("TND"), Header("Grand livre tiers"), CancellationToken.None);

        AssertIsPdf(bytes);
    }

    [Fact]
    public async Task ThirdPartyLedgerPdf_WithForeignCurrency_RendersWithoutOverflow()
    {
        var bytes = await BuildPdfService().GenerateThirdPartyLedgerPdfAsync(
            ThirdPartyLedger("EUR"), Header("Grand livre tiers"), CancellationToken.None);

        AssertIsPdf(bytes);
    }

    [Fact]
    public async Task ThirdPartyLedgerPdf_WithManyForeignRows_RendersWithoutOverflow()
    {
        var bytes = await BuildPdfService().GenerateThirdPartyLedgerPdfAsync(
            ThirdPartyLedger("EUR", rowCount: 120), Header("Grand livre tiers"), CancellationToken.None);

        AssertIsPdf(bytes);
    }

    [Fact]
    public async Task ThirdPartyLedgerPdf_InFunctionalCurrency_IsShorterThanTheForeignOne()
    {
        var svc = BuildPdfService();
        var functional = await svc.GenerateThirdPartyLedgerPdfAsync(ThirdPartyLedger("TND"), Header("Grand livre tiers"), CancellationToken.None);
        var foreign = await svc.GenerateThirdPartyLedgerPdfAsync(ThirdPartyLedger("EUR"), Header("Grand livre tiers"), CancellationToken.None);

        Assert.True(foreign.Length > functional.Length,
            "Le PDF en devise doit porter une colonne de plus que celui en devise de tenue.");
    }

    [Fact]
    public async Task LedgerPdf_WithManyForeignRows_RendersWithoutOverflow()
    {
        // Volume suffisant pour forcer la pagination : c'est là qu'un en-tête trop large se voit.
        var rows = Enumerable.Range(0, 120).Select(_ => LedgerRow("EUR", 1000m)).ToList();

        var bytes = await BuildPdfService().GenerateLedgerPdfAsync(
            "40110000", rows, Header("Grand livre"), CancellationToken.None);

        AssertIsPdf(bytes);
    }
}
