using FactuTrust.Application.Features.Accounting.Audit;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class AuditCorrectionLinkBuilderTests
{
    [Fact]
    public void Drafts_links_to_entry_search_with_draft_status()
    {
        var link = AuditCorrectionLinkBuilder.Build(
            "drafts", "/accounting/journal", null,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            2026, null, Array.Empty<AuditCorrectionLinkBuilder.LineContext>());

        Assert.Equal("/accounting/entry-search", link.Route);
        Assert.Equal("0", link.QueryParams["status"]);
        Assert.Equal("1", link.QueryParams["autoSearch"]);
    }

    [Fact]
    public void Unlettered_prefills_lettering_account_and_auto_load()
    {
        var link = AuditCorrectionLinkBuilder.Build(
            "unlettered", "/accounting/lettering", "4111/4011",
            null, null, 2026, null,
            [new AuditCorrectionLinkBuilder.LineContext(null, null, "4111", "Client", null)]);

        Assert.Equal("/accounting/lettering", link.Route);
        Assert.Equal("4111", link.QueryParams["account"]);
        Assert.Equal("1", link.QueryParams["autoLoad"]);
        Assert.Equal("1", link.QueryParams["unletteredOnly"]);
    }

    [Fact]
    public void Piece_duplicates_parses_journal_and_entry_number()
    {
        var link = AuditCorrectionLinkBuilder.Build(
            "health-piece-duplicates", "/accounting/entry-search", null,
            null, null, 2026, null,
            [new AuditCorrectionLinkBuilder.LineContext(null, null, null, "Doublon", "JC-21")]);

        Assert.Equal("/accounting/entry-search", link.Route);
        Assert.Equal("JC", link.QueryParams["journalCode"]);
        Assert.Equal("21", link.QueryParams["entryNumber"]);
        Assert.Equal("1", link.QueryParams["autoSearch"]);
    }

    [Fact]
    public void ParseJournalPiece_splits_journal_and_number()
    {
        AuditCorrectionLinkBuilder.ParseJournalPiece("JC-21", out var journal, out var number);
        Assert.Equal("JC", journal);
        Assert.Equal("21", number);
    }

    [Fact]
    public void Open_periods_links_to_closing_with_fiscal_year()
    {
        var link = AuditCorrectionLinkBuilder.Build(
            "open-periods", "/accounting/closing", null,
            null, null, 2026, null, Array.Empty<AuditCorrectionLinkBuilder.LineContext>());

        Assert.Equal("/accounting/closing", link.Route);
        Assert.Equal("2026", link.QueryParams["fiscalYear"]);
    }
}
