using System.IO.Compression;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Export d'archive de dossier (ZIP, lecture seule). Contrôle du contenu : les entrées attendues
/// sont présentes, et les CSV journal/balance sont OCTET POUR OCTET identiques aux exports unitaires
/// (l'archive ne recalcule rien, elle assemble). Le FEC est best-effort.
/// </summary>
public sealed class DossierArchiveExportTests
{
    private static readonly byte[] JournalCsv = "journal_csv"u8.ToArray();
    private static readonly byte[] BalanceCsv = "balance_csv"u8.ToArray();

    private static ExportDossierArchiveQueryHandler BuildHandler(bool fecSucceeds)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetChartOfAccountsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ChartOfAccountDto>>(new[]
            {
                new ChartOfAccountDto { AccountNumber = "4111", Label = "Clients", AccountClass = 4, NatureType = 0, IsActive = true }
            }));
        mediator.Setup(m => m.Send(It.IsAny<GetJournalEntriesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<JournalEntryDto>>(Array.Empty<JournalEntryDto>()));
        mediator.Setup(m => m.Send(It.IsAny<GetBalanceQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<BalanceRowDto>>(Array.Empty<BalanceRowDto>()));

        var export = new Mock<IAccountingExportService>();
        export.Setup(x => x.ExportJournalToCsv(It.IsAny<IReadOnlyList<JournalEntryDto>>())).Returns(JournalCsv);
        export.Setup(x => x.ExportBalanceToCsv(It.IsAny<IReadOnlyList<BalanceRowDto>>())).Returns(BalanceCsv);

        var fec = new Mock<IFecExportService>();
        fec.Setup(x => x.ExportFecAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fecSucceeds
                ? Result.Success("fec_content"u8.ToArray())
                : Result.Failure<byte[]>(Error.Validation("Fec", "Exercice vide.")));

        var clients = new Mock<IClientRepository>();
        clients.Setup(x => x.GetActiveClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Client>());
        var suppliers = new Mock<ISupplierRepository>();
        suppliers.Setup(x => x.GetActiveSuppliersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Supplier>());

        var companies = new Mock<ICompanyRepository>();
        companies.Setup(x => x.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Company?)null);

        return new ExportDossierArchiveQueryHandler(
            mediator.Object, export.Object, fec.Object, clients.Object, suppliers.Object, companies.Object);
    }

    private static Dictionary<string, byte[]> ReadZip(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var map = new Dictionary<string, byte[]>();
        foreach (var entry in zip.Entries)
        {
            using var s = entry.Open();
            using var buffer = new MemoryStream();
            s.CopyTo(buffer);
            map[entry.FullName] = buffer.ToArray();
        }
        return map;
    }

    [Fact]
    public async Task Archive_ContainsExpectedEntries_AndReusesUnitExports()
    {
        var result = await BuildHandler(fecSucceeds: true).Handle(new ExportDossierArchiveQuery(2026), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var files = ReadZip(result.Value);

        Assert.Contains("plan_comptable.csv", files.Keys);
        Assert.Contains("journal_general.csv", files.Keys);
        Assert.Contains("balance.csv", files.Keys);
        Assert.Contains("tiers.csv", files.Keys);
        Assert.Contains("fec.txt", files.Keys);
        Assert.Contains("manifest.txt", files.Keys);

        // Identité octet pour octet avec les exports unitaires : l'archive assemble, ne recalcule pas.
        Assert.Equal(JournalCsv, files["journal_general.csv"]);
        Assert.Equal(BalanceCsv, files["balance.csv"]);
        Assert.Equal("fec_content"u8.ToArray(), files["fec.txt"]);
    }

    [Fact]
    public async Task Archive_OmitsFec_WhenExportFails()
    {
        var result = await BuildHandler(fecSucceeds: false).Handle(new ExportDossierArchiveQuery(2026), CancellationToken.None);

        Assert.True(result.IsSuccess);   // le ZIP est produit malgré l'échec FEC
        var files = ReadZip(result.Value);
        Assert.DoesNotContain("fec.txt", files.Keys);
        Assert.Contains("balance.csv", files.Keys);
    }

    [Fact]
    public async Task Archive_InvalidYear_IsRejected()
    {
        var result = await BuildHandler(fecSucceeds: true).Handle(new ExportDossierArchiveQuery(1800), CancellationToken.None);
        Assert.True(result.IsFailure);
    }
}
