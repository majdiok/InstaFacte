using FactuTrust.Application.Features.Accounting;
using FactuTrust.Application.Features.Accounting.DocumentImport;
using FactuTrust.Domain.Common;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.DocumentImport;

/// <summary>
/// Le mapper est une fonction pure : ces tests couvrent le cœur comptable sans base ni IA.
/// </summary>
public sealed class AccountingEntryLineMapperTests
{
    private static ProposalAccountPlan SaleAccounts(string thirdPartyAccount = TunisianPostingAccounts.Client) =>
        new()
        {
            ThirdPartyAccount = thirdPartyAccount,
            DefaultResultAccount = TunisianPostingAccounts.SalesOfGoods,
            VatAccount = TunisianPostingAccounts.VatCollected,
            FodecAccount = TunisianPostingAccounts.Fodec,
            StampAccount = TunisianPostingAccounts.FiscalStampOnSale
        };

    private static ProposalAccountPlan PurchaseAccounts() =>
        new()
        {
            ThirdPartyAccount = TunisianPostingAccounts.Supplier,
            DefaultResultAccount = TunisianPostingAccounts.PurchasesOfGoods,
            VatAccount = TunisianPostingAccounts.VatDeductibleGoods,
            FodecAccount = TunisianPostingAccounts.Fodec,
            StampAccount = TunisianPostingAccounts.FiscalStampOnPurchase
        };

    /// <summary>La facture FAC-2026-000012 du dossier : 2 taux de TVA + timbre.</summary>
    private static AccountingDocumentExtractionDto TwoRateInvoice() => new()
    {
        DocumentType = DocumentTypes.Invoice,
        DocumentNumber = "FAC-2026-000012",
        IssueDate = new DateOnly(2026, 7, 12),
        VatBreakdown =
        [
            new ExtractedVatBucketDto(13, 2500.000m, 325.000m),
            new ExtractedVatBucketDto(19, 1200.000m, 228.000m)
        ],
        TotalHt = 3700.000m,
        TotalVat = 553.000m,
        FiscalStampAmount = 1.000m,
        TotalTtc = 4254.000m
    };

    [Fact]
    public void Build_SaleInvoice_ProducesTheTunisianChartOfAccountsEntry()
    {
        var lines = AccountingEntryLineMapper.Build(
            TwoRateInvoice(), DocumentDirections.Sale, SaleAccounts(), thirdParty: null);

        Assert.Collection(
            lines,
            l =>
            {
                Assert.Equal(TunisianPostingAccounts.Client, l.AccountNumber);
                Assert.Equal(4254.000m, l.Debit);
                Assert.Equal(0m, l.Credit);
                Assert.Equal(ProposedLineRoles.ThirdParty, l.Role);
            },
            l =>
            {
                Assert.Equal(TunisianPostingAccounts.SalesOfGoods, l.AccountNumber);
                Assert.Equal(3700.000m, l.Credit);
            },
            l =>
            {
                Assert.Equal(TunisianPostingAccounts.VatCollected, l.AccountNumber);
                Assert.Equal(325.000m, l.Credit);
                Assert.Equal(13, l.VatRatePercent);
            },
            l =>
            {
                Assert.Equal(TunisianPostingAccounts.VatCollected, l.AccountNumber);
                Assert.Equal(228.000m, l.Credit);
                Assert.Equal(19, l.VatRatePercent);
            },
            l =>
            {
                Assert.Equal(TunisianPostingAccounts.FiscalStampOnSale, l.AccountNumber);
                Assert.Equal(1.000m, l.Credit);
            });
    }

    [Fact]
    public void Build_PurchaseInvoice_DebitsChargesAndDeductibleVatAndCreditsTheSupplier()
    {
        var lines = AccountingEntryLineMapper.Build(
            TwoRateInvoice(), DocumentDirections.Purchase, PurchaseAccounts(), thirdParty: null);

        Assert.Equal(TunisianPostingAccounts.PurchasesOfGoods, lines[0].AccountNumber);
        Assert.Equal(3700.000m, lines[0].Debit);

        Assert.Equal(TunisianPostingAccounts.VatDeductibleGoods, lines[1].AccountNumber);
        Assert.Equal(325.000m, lines[1].Debit);
        Assert.Equal(TunisianPostingAccounts.VatDeductibleGoods, lines[2].AccountNumber);
        Assert.Equal(228.000m, lines[2].Debit);

        // Timbre à l'achat : charge au DÉBIT (6654), et non 4371 comme à la vente.
        Assert.Equal(TunisianPostingAccounts.FiscalStampOnPurchase, lines[3].AccountNumber);
        Assert.Equal(1.000m, lines[3].Debit);

        var counterpart = lines[^1];
        Assert.Equal(TunisianPostingAccounts.Supplier, counterpart.AccountNumber);
        Assert.Equal(4254.000m, counterpart.Credit);
        Assert.Equal(0m, counterpart.Debit);
    }

    [Fact]
    public void Build_CreditNote_MirrorsTheDirectionsWithPositiveAmounts()
    {
        var creditNote = TwoRateInvoice() with
        {
            DocumentType = DocumentTypes.CreditNote,
            DocumentNumber = "AVO-2026-000003"
        };

        var lines = AccountingEntryLineMapper.Build(
            creditNote, DocumentDirections.Sale, SaleAccounts(), thirdParty: null);

        var client = Assert.Single(lines, l => l.AccountNumber == TunisianPostingAccounts.Client);
        Assert.Equal(4254.000m, client.Credit);
        Assert.Equal(0m, client.Debit);

        var sales = Assert.Single(lines, l => l.AccountNumber == TunisianPostingAccounts.SalesOfGoods);
        Assert.Equal(3700.000m, sales.Debit);

        Assert.All(lines, l => Assert.True(l.Debit >= 0m && l.Credit >= 0m));
    }

    [Fact]
    public void Build_UsesTheThirdPartyProfileCollectiveAccountWhenProvided()
    {
        var thirdParty = new ProposedThirdPartyDto
        {
            Kind = 1,
            MatchedId = Guid.NewGuid(),
            CollectiveAccountNumber = "41110001"
        };

        var lines = AccountingEntryLineMapper.Build(
            TwoRateInvoice(), DocumentDirections.Sale, SaleAccounts("41110001"), thirdParty);

        var counterpart = lines[0];
        Assert.Equal("41110001", counterpart.AccountNumber);
        Assert.Equal(thirdParty.MatchedId, counterpart.ThirdPartyId);
        Assert.Equal(1, counterpart.ThirdPartyKind);
    }

    [Fact]
    public void Build_WithoutAMatchedThirdParty_LeavesTheAuxiliaryEmpty()
    {
        // Un thirdPartyId sans thirdPartyKind valide est rejeté par ManualJournalLineMapper :
        // les deux doivent rester nuls ensemble.
        var unmatched = new ProposedThirdPartyDto
        {
            Kind = 2,
            MatchedId = null,
            CollectiveAccountNumber = TunisianPostingAccounts.Supplier
        };

        var lines = AccountingEntryLineMapper.Build(
            TwoRateInvoice(), DocumentDirections.Purchase, PurchaseAccounts(), unmatched);

        var counterpart = lines[^1];
        Assert.Null(counterpart.ThirdPartyId);
        Assert.Null(counterpart.ThirdPartyKind);
    }

    [Fact]
    public void Build_SplitsHtAcrossSeveralResultAccounts()
    {
        var accounts = SaleAccounts() with
        {
            HtByAccount = new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                [TunisianPostingAccounts.SalesOfGoods] = 2500.000m,
                [TunisianPostingAccounts.SalesOfServices] = 1200.000m
            }
        };

        var lines = AccountingEntryLineMapper.Build(
            TwoRateInvoice(), DocumentDirections.Sale, accounts, thirdParty: null);

        Assert.Equal(2500.000m, Assert.Single(lines, l => l.AccountNumber == TunisianPostingAccounts.SalesOfGoods).Credit);
        Assert.Equal(1200.000m, Assert.Single(lines, l => l.AccountNumber == TunisianPostingAccounts.SalesOfServices).Credit);
        AssertBalanced(lines);
    }

    [Fact]
    public void Build_SaleWithFodec_CreditsTheDedicatedAccount()
    {
        var doc = TwoRateInvoice() with { FodecAmount = 37.000m, TotalTtc = 4291.000m };

        var lines = AccountingEntryLineMapper.Build(
            doc, DocumentDirections.Sale, SaleAccounts(), thirdParty: null);

        Assert.Equal(37.000m, Assert.Single(lines, l => l.AccountNumber == TunisianPostingAccounts.Fodec).Credit);
        AssertBalanced(lines);
    }

    [Fact]
    public void Build_PurchaseWithFodec_KeepsItOnTheExpenseAccount()
    {
        // Le FODEC payé à l'achat n'est pas récupérable : il fait partie du coût.
        var doc = TwoRateInvoice() with { FodecAmount = 37.000m };

        var lines = AccountingEntryLineMapper.Build(
            doc, DocumentDirections.Purchase, PurchaseAccounts(), thirdParty: null);

        Assert.DoesNotContain(lines, l => l.AccountNumber == TunisianPostingAccounts.Fodec);
        var fodecLine = Assert.Single(lines, l => l.Role == ProposedLineRoles.Fodec);
        Assert.Equal(TunisianPostingAccounts.PurchasesOfGoods, fodecLine.AccountNumber);
        Assert.Equal(37.000m, fodecLine.Debit);
        AssertBalanced(lines);
    }

    [Theory]
    // Les trois factures d'exemple.
    [InlineData(3700.000, 325.000, 228.000, 1.000, 4254.000)]
    [InlineData(5550.000, 487.500, 342.000, 1.000, 6380.500)]
    [InlineData(1250.000, 162.500, 0, 1.000, 1413.500)]
    // Cas piégeux : montants au millime, sommes non triviales.
    [InlineData(0.001, 0.000, 0.000, 0.001, 0.002)]
    [InlineData(1234.567, 160.494, 0, 1.000, 1396.061)]
    public void Build_AlwaysBalancesAtTheMillime(
        double ht, double vat13, double vat19, double stamp, double _)
    {
        var buckets = new List<ExtractedVatBucketDto>();
        if ((decimal)vat13 != 0m)
            buckets.Add(new ExtractedVatBucketDto(13, (decimal)ht, (decimal)vat13));
        if ((decimal)vat19 != 0m)
            buckets.Add(new ExtractedVatBucketDto(19, 0m, (decimal)vat19));

        var doc = new AccountingDocumentExtractionDto
        {
            DocumentNumber = "TEST",
            TotalHt = (decimal)ht,
            TotalVat = (decimal)vat13 + (decimal)vat19,
            FiscalStampAmount = (decimal)stamp,
            VatBreakdown = buckets
        };

        AssertBalanced(AccountingEntryLineMapper.Build(
            doc, DocumentDirections.Sale, SaleAccounts(), thirdParty: null));
        AssertBalanced(AccountingEntryLineMapper.Build(
            doc, DocumentDirections.Purchase, PurchaseAccounts(), thirdParty: null));
    }

    [Fact]
    public void Build_WhenTheDocumentTtcDisagrees_TheEntryStillBalances()
    {
        // Le TTC imprimé est faux (OCR douteux) : la contrepartie reste la somme des autres lignes.
        var doc = TwoRateInvoice() with { TotalTtc = 9999.999m };

        var lines = AccountingEntryLineMapper.Build(
            doc, DocumentDirections.Sale, SaleAccounts(), thirdParty: null);

        AssertBalanced(lines);
        Assert.Equal(4254.000m, lines[0].Debit);
    }

    /// <summary>
    /// Reproduit la règle d'équilibre du domaine : <c>JournalEntry.BuildLines</c> compare
    /// <c>Math.Round(somme, 3)</c> à l'identique.
    /// </summary>
    private static void AssertBalanced(IReadOnlyList<ProposedLineDto> lines)
    {
        Assert.NotEmpty(lines);
        Assert.True(lines.Count >= 2, "Le domaine exige au moins deux lignes.");
        Assert.All(lines, l => Assert.False(l.Debit > 0m && l.Credit > 0m,
            "Une ligne ne peut pas porter un débit et un crédit."));
        Assert.All(lines, l => Assert.True(l.Debit > 0m || l.Credit > 0m,
            "Chaque ligne doit porter un montant."));

        Assert.Equal(
            MillimeRounding.Round(lines.Sum(l => l.Debit)),
            MillimeRounding.Round(lines.Sum(l => l.Credit)));
    }
}
