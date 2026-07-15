using System.Text;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Commands;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Plan tiers — Lot Y : mapping des lignes de saisie manuelle avec tiers optionnel
/// (id ⇒ kind obligatoire ; sans tiers ⇒ comportement historique None) et FEC dont
/// CompAuxNum devient le code auxiliaire lisible (fallback GUID sans fiche).
/// </summary>
public sealed class ThirdPartyManualEntryAndFecTests
{
    private static ManualJournalLineRequest Line(Guid? thirdPartyId = null, int? kind = null) => new()
    {
        AccountNumber = "4111",
        LineLabel = "OD client",
        Debit = 100m,
        Credit = 0m,
        ThirdPartyId = thirdPartyId,
        ThirdPartyKind = kind
    };

    [Fact]
    public void Map_WithoutThirdParty_KeepsHistoricalBehavior()
    {
        var result = ManualJournalLineMapper.Map([Line()]);

        Assert.True(result.IsSuccess);
        var input = Assert.Single(result.Value);
        Assert.Null(input.ThirdPartyId);
        Assert.Equal(ThirdPartyKind.None, input.ThirdPartyKind);
    }

    [Fact]
    public void Map_WithClientThirdParty_SetsIdAndKind()
    {
        var id = Guid.NewGuid();

        var result = ManualJournalLineMapper.Map([Line(id, (int)ThirdPartyKind.Client)]);

        Assert.True(result.IsSuccess);
        var input = Assert.Single(result.Value);
        Assert.Equal(id, input.ThirdPartyId);
        Assert.Equal(ThirdPartyKind.Client, input.ThirdPartyKind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]  // None
    [InlineData(3)]  // hors périmètre (Other n'existe pas)
    public void Map_ThirdPartyIdWithoutValidKind_Fails(int? kind)
    {
        var result = ManualJournalLineMapper.Map([Line(Guid.NewGuid(), kind)]);

        Assert.True(result.IsFailure);
        Assert.Contains("Type de tiers obligatoire", result.Error.Description);
    }

    [Fact]
    public void Map_KindWithoutId_IsIgnored()
    {
        var result = ManualJournalLineMapper.Map([Line(thirdPartyId: null, kind: (int)ThirdPartyKind.Client)]);

        Assert.True(result.IsSuccess);
        Assert.Equal(ThirdPartyKind.None, Assert.Single(result.Value).ThirdPartyKind);
    }

    [Fact]
    public void MappedLines_ProduceAuxiliarizedJournalEntry()
    {
        var clientId = Guid.NewGuid();
        var lines = ManualJournalLineMapper.Map(
        [
            Line(clientId, (int)ThirdPartyKind.Client),
            new ManualJournalLineRequest { AccountNumber = "7071", LineLabel = "Vente", Debit = 0m, Credit = 100m }
        ]).Value;

        var entry = JournalEntry.Create(1, "JOD", new DateTime(2026, 7, 1), "OD tiers", Guid.NewGuid(),
            false, "Manual", null, lines, initialStatus: JournalEntryStatus.Validee).Value;

        var clientLine = entry.Lines.Single(l => l.AccountNumber == "4111");
        Assert.Equal(clientId, clientLine.ThirdPartyId);
        Assert.Equal(ThirdPartyKind.Client, clientLine.ThirdPartyKind);
    }

    // ── FEC : CompAuxNum lisible avec fiche, GUID sans fiche ────────────────

    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;
        public TestTenantDbContextFactory(string databaseName) => _databaseName = databaseName;
        public TenantDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
                .Options;
            return new TenantDbContext(options);
        }
    }

    [Fact]
    public async Task Fec_UsesAuxiliaryCode_WhenProfileExists_GuidOtherwise()
    {
        var factory = new TestTenantDbContextFactory($"FecAuxDb_{Guid.NewGuid()}");
        var profiledClient = Guid.NewGuid();
        var unprofiledClient = Guid.NewGuid();

        using (var ctx = factory.CreateContext())
        {
            var profile = ThirdPartyAccountingProfile.Create(
                ThirdPartyKind.Client, profiledClient, "C0042", "4111").Value;
            profile.SetAuditInfo("test", false);
            ctx.ThirdPartyAccountingProfiles.Add(profile);

            foreach (var (id, number) in new[] { (profiledClient, 1), (unprofiledClient, 2) })
            {
                var entry = JournalEntry.Create(number, "JV", new DateTime(2026, 3, 10), "Vente",
                    Guid.NewGuid(), false, "Manual", null, new[]
                    {
                        new JournalLineInput("4111", "Client", 100m, 0m, id, ThirdPartyKind.Client),
                        new JournalLineInput("707", "Vente", 0m, 100m, null, ThirdPartyKind.None)
                    }, initialStatus: JournalEntryStatus.Validee).Value;
                entry.SetAuditInfo("test", false);
                ctx.JournalEntries.Add(entry);
            }
            ctx.SaveChanges();
        }

        var fec = await new FecExportService(factory).ExportFecAsync(2026);

        Assert.True(fec.IsSuccess);
        var text = Encoding.UTF8.GetString(fec.Value);
        Assert.Contains("C0042", text);                                   // code lisible pour le tiers profilé
        Assert.Contains(unprofiledClient.ToString("N"), text);            // fallback GUID pour l'autre
        Assert.DoesNotContain(profiledClient.ToString("N"), text);        // le GUID profilé est remplacé
    }
}
