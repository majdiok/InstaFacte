using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Lot G : assistant d'écritures d'inventaire — génération de l'écriture + extourne planifiée,
/// permutation des lignes, types permanents sans extourne, comptes inactifs refusés, brouillard.
/// </summary>
public sealed class AssistedInventoryEntryServiceTests
{
    private readonly string _dbName = $"InventoryDb_{Guid.NewGuid()}";
    private readonly TestTenantDbContextFactory _factory;

    public AssistedInventoryEntryServiceTests()
    {
        _factory = new TestTenantDbContextFactory(_dbName);
        SeedAccounts();
    }

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

    private void SeedAccounts()
    {
        using var ctx = _factory.CreateContext();
        ctx.ChartOfAccounts.Add(ChartOfAccount.Create("471", "Compte d'attente", 4, "47", AccountNatureType.Debit).Value);
        ctx.ChartOfAccounts.Add(ChartOfAccount.Create("613", "Loyers", 6, "61", AccountNatureType.Debit).Value);
        ctx.ChartOfAccounts.Add(ChartOfAccount.Create("491", "Provision clients", 4, "49", AccountNatureType.Credit).Value);
        ctx.ChartOfAccounts.Add(ChartOfAccount.Create("681", "Dotations", 6, "68", AccountNatureType.Debit).Value);
        ctx.SaveChanges();
    }

    private void SeedClosedPeriod(int year, int month)
    {
        var period = AccountingPeriod.Create(year, month, new DateTime(year, month, 1), new DateTime(year, month, 28));
        period.Close("test");
        period.SetAuditInfo("test", false);
        using var ctx = _factory.CreateContext();
        ctx.AccountingPeriods.Add(period);
        ctx.SaveChanges();
    }

    private AssistedInventoryEntryService BuildService(bool enabled = true, bool brouillard = false)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.Email).Returns("compta@test.tn");
        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var settings = Options.Create(new AccountingSettings { InventoryAssistantEnabled = enabled, BrouillardEnabled = brouillard });
        return new AssistedInventoryEntryService(_factory, currentUser.Object, audit.Object, settings);
    }

    private List<JournalEntry> AllEntries()
    {
        using var ctx = _factory.CreateContext();
        return ctx.JournalEntries.AsNoTracking().Include(e => e.Lines).ToList();
    }

    [Fact]
    public async Task Create_Cca_GeneratesEntryPlusReversal()
    {
        var request = new CreateInventoryEntryRequest
        {
            Kind = (int)InventoryEntryKind.ChargeConstateeDavance,
            EntryDate = new DateTime(2026, 12, 31),
            DebitAccount = "471",
            CreditAccount = "613",
            Amount = 1200m,
            Label = "Assurance payée d'avance",
            AutoReverse = true
        };

        var result = await BuildService().CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.ReversalEntryId);

        var entries = AllEntries();
        Assert.Equal(2, entries.Count);

        var inventory = entries.Single(e => e.Id == result.Value.EntryId);
        Assert.Equal(new DateTime(2026, 12, 31), inventory.EntryDate);
        Assert.Equal(1200m, inventory.Lines.Single(l => l.AccountNumber == "471").DebitAmount.Amount);
        Assert.Equal(1200m, inventory.Lines.Single(l => l.AccountNumber == "613").CreditAmount.Amount);

        var reversal = entries.Single(e => e.Id == result.Value.ReversalEntryId);
        Assert.Equal(new DateTime(2027, 1, 1), reversal.EntryDate); // 1er de l'exercice suivant
        Assert.Equal(inventory.Id, reversal.SourceEntityId);
        Assert.False(reversal.IsReversed); // contre-passation planifiée, PAS une correction
        // Lignes permutées : 471 au crédit, 613 au débit.
        Assert.Equal(1200m, reversal.Lines.Single(l => l.AccountNumber == "471").CreditAmount.Amount);
        Assert.Equal(1200m, reversal.Lines.Single(l => l.AccountNumber == "613").DebitAmount.Amount);
    }

    [Fact]
    public async Task Create_Provision_GeneratesSingleEntryNoReversal()
    {
        var request = new CreateInventoryEntryRequest
        {
            Kind = (int)InventoryEntryKind.ProvisionDepreciation,
            EntryDate = new DateTime(2026, 12, 31),
            DebitAccount = "681",
            CreditAccount = "491",
            Amount = 500m,
            Label = "Provision créance douteuse",
            AutoReverse = false
        };

        var result = await BuildService().CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ReversalEntryId);
        Assert.Single(AllEntries());
    }

    [Fact]
    public async Task Create_MidYear_ReversalDatedFirstOfNextMonth()
    {
        var request = new CreateInventoryEntryRequest
        {
            Kind = (int)InventoryEntryKind.ChargeAPayer,
            EntryDate = new DateTime(2026, 6, 30),
            DebitAccount = "613",
            CreditAccount = "471",
            Amount = 300m,
            Label = "Charge à payer",
            AutoReverse = true
        };

        var result = await BuildService().CreateAsync(request);

        var reversal = AllEntries().Single(e => e.Id == result.Value.ReversalEntryId);
        Assert.Equal(new DateTime(2026, 7, 1), reversal.EntryDate);
    }

    [Fact]
    public async Task Create_InactiveAccount_Fails()
    {
        using (var ctx = _factory.CreateContext())
        {
            var acc = ctx.ChartOfAccounts.Single(c => c.AccountNumber == "613");
            acc.ToggleActive();
            ctx.SaveChanges();
        }
        var request = new CreateInventoryEntryRequest
        {
            Kind = (int)InventoryEntryKind.ChargeConstateeDavance,
            EntryDate = new DateTime(2026, 12, 31),
            DebitAccount = "471", CreditAccount = "613", Amount = 100m, Label = "x", AutoReverse = true
        };

        var result = await BuildService().CreateAsync(request);

        Assert.True(result.IsFailure);
        Assert.Contains("désactivé", result.Error.Description);
        Assert.Empty(AllEntries());
    }

    [Fact]
    public async Task Create_ClosedPeriod_Fails()
    {
        SeedClosedPeriod(2026, 12);
        var request = new CreateInventoryEntryRequest
        {
            Kind = (int)InventoryEntryKind.ChargeConstateeDavance,
            EntryDate = new DateTime(2026, 12, 31),
            DebitAccount = "471", CreditAccount = "613", Amount = 100m, Label = "x", AutoReverse = false
        };

        var result = await BuildService().CreateAsync(request);

        Assert.True(result.IsFailure);
        Assert.Contains("clôturée", result.Error.Description);
    }

    [Fact]
    public async Task Create_BrouillardOn_EntriesAreDraft()
    {
        var request = new CreateInventoryEntryRequest
        {
            Kind = (int)InventoryEntryKind.ChargeConstateeDavance,
            EntryDate = new DateTime(2026, 12, 31),
            DebitAccount = "471", CreditAccount = "613", Amount = 100m, Label = "x", AutoReverse = true
        };

        await BuildService(brouillard: true).CreateAsync(request);

        Assert.All(AllEntries(), e => Assert.Equal(JournalEntryStatus.Brouillon, e.Status));
    }

    [Fact]
    public async Task Create_WhenDisabled_Fails()
    {
        var request = new CreateInventoryEntryRequest
        {
            Kind = (int)InventoryEntryKind.ChargeConstateeDavance,
            EntryDate = new DateTime(2026, 12, 31),
            DebitAccount = "471", CreditAccount = "613", Amount = 100m, Label = "x"
        };

        var result = await BuildService(enabled: false).CreateAsync(request);

        Assert.True(result.IsFailure);
        Assert.Contains("activé", result.Error.Description);
    }
}
