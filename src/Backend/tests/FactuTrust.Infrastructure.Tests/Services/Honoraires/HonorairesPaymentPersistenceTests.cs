using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities.Honoraires;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.Honoraires;

/// <summary>
/// Régression : l'encaissement d'un honoraires cabinet remontait un
/// <c>DbUpdateConcurrencyException</c> (traduit en HTTP 409 CONCURRENCY_CONFLICT) alors qu'il
/// n'y avait ni concurrence ni ligne existante. Cause : la PK Guid des <see cref="HonorairesPayment"/>
/// (dérivés d'<see cref="Domain.Common.Entity"/>) était considérée par EF Core comme
/// <see cref="Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd"/>. Le change-tracker,
/// découvrant l'enfant via <c>invoice.RecordPayment</c>, voyait un Id « déjà positionné » et
/// émettait un UPDATE au lieu d'un INSERT, échouant sur 0 ligne modifiée.
///
/// La correction (<c>PersistenceConventions.ApplyClientGeneratedGuidKeys</c>) marque les PK
/// Guid des entités du domaine en <see cref="Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never"/>.
/// Ce test doit donc échouer sans la convention et passer avec.
///
/// SQL Server requis (LocalDB éphémère) : le comportement discuté est spécifique au relational
/// tracker et à la table splitting Money — InMemory ne le reproduit pas.
/// </summary>
public sealed class HonorairesPaymentPersistenceTests : IDisposable
{
    private readonly string? _connectionString;
    private readonly bool _canRun;
    private readonly TenantAmbientTransaction _ambient = new();
    private readonly TenantDbContextFactory? _factory;

    public HonorairesPaymentPersistenceTests()
    {
        _connectionString = Environment.GetEnvironmentVariable("FACTUTRUST_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            var dbName = $"FactuTrust_HonPay_{Guid.NewGuid():N}";
            _connectionString = $"Server=(localdb)\\mssqllocaldb;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
        }

        _canRun = CanConnectAndCreate(_connectionString);
        if (!_canRun)
            return;

        var tenantContext = new Mock<ITenantContext>();
        tenantContext.SetupGet(c => c.ConnectionString).Returns(_connectionString);

        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);

        _factory = new TenantDbContextFactory(
            tenantContext.Object,
            new Mock<IMediator>().Object,
            _ambient,
            NullLogger<TenantDbContext>.Instance,
            NullLoggerFactory.Instance,
            new ConfigurationBuilder().Build(),
            hostEnvironment.Object);
    }

    /// <summary>
    /// Chemin exactement identique au bug reporté : le service charge une facture validée avec
    /// ses paiements, ajoute un nouveau paiement via <c>invoice.RecordPayment</c> — SANS
    /// <c>db.HonorairesPayments.Add</c> — puis sauvegarde. Avec la convention en place l'UPDATE
    /// devient INSERT et la ligne est persistée.
    /// </summary>
    [Fact]
    public async Task RecordPayment_PersistsPaymentRow_AndUpdatesInvoiceState()
    {
        if (!_canRun) return;

        var invoiceId = await SeedValidatedInvoiceAsync(totalHt: 160m);

        int savedVersion;
        await using (var db = _factory!.CreateIsolatedContext())
        {
            // Chemin exact du bug : Include(Payments) => l'agrégat est tracké, le paiement
            // n'est PAS explicitement ajouté au DbSet.
            var invoice = await db.HonorairesInvoices
                .Include(i => i.Payments)
                .FirstAsync(i => i.Id == invoiceId);

            var payment = HonorairesPayment.Create(
                invoice,
                paymentDate: new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc),
                amount: Money.Create(190.400m, invoice.Currency),
                clientWithholdingAmount: Money.Zero(invoice.Currency),
                method: PaymentMethod.BankTransfer,
                reference: "VIR-2026-000001",
                notes: "Encaissement complet",
                bankAccountLabel: "BIAT — 0801").Value;

            var record = invoice.RecordPayment(payment);
            Assert.True(record.IsSuccess);

            // Le vrai test : SaveChangesAsync ne doit PLUS lever DbUpdateConcurrencyException.
            await db.SaveChangesAsync();
            savedVersion = invoice.Version;
        }

        await using var verify = _factory!.CreateIsolatedContext();
        var persistedPayment = await verify.HonorairesPayments.AsNoTracking()
            .SingleOrDefaultAsync(p => p.HonorairesInvoiceId == invoiceId);
        Assert.NotNull(persistedPayment);
        Assert.Equal(190.400m, persistedPayment!.Amount.Amount);
        Assert.Equal(PaymentMethod.BankTransfer, persistedPayment.Method);

        var persistedInvoice = await verify.HonorairesInvoices.AsNoTracking()
            .Include(i => i.Payments)
            .SingleAsync(i => i.Id == invoiceId);
        Assert.Equal(HonorairesInvoiceStatus.Paid, persistedInvoice.Status);
        Assert.Equal(190.400m, persistedInvoice.AmountPaid.Amount);
        Assert.Equal(0m, persistedInvoice.AmountDue);
        Assert.Single(persistedInvoice.Payments);
        // Verrouillage optimiste : deux mutations (Validate + RecordPayment) sur la même facture,
        // Version 1 → 2 → 3. La colonne est un token de concurrence, plus un simple compteur.
        Assert.Equal(3, persistedInvoice.Version);
        Assert.Equal(3, savedVersion);
    }

    /// <summary>
    /// Un encaissement partiel doit laisser la facture en <see cref="HonorairesInvoiceStatus.PartiallyPaid"/>
    /// avec le bon reste dû et Version incrémentée.
    /// </summary>
    [Fact]
    public async Task RecordPartialPayment_MovesInvoiceToPartiallyPaid()
    {
        if (!_canRun) return;

        var invoiceId = await SeedValidatedInvoiceAsync(totalHt: 200m);

        await using (var db = _factory!.CreateIsolatedContext())
        {
            var invoice = await db.HonorairesInvoices
                .Include(i => i.Payments)
                .FirstAsync(i => i.Id == invoiceId);

            var payment = HonorairesPayment.Create(
                invoice,
                paymentDate: new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc),
                amount: Money.Create(80m, invoice.Currency),
                clientWithholdingAmount: Money.Zero(invoice.Currency),
                method: PaymentMethod.Cash).Value;

            var record = invoice.RecordPayment(payment);
            Assert.True(record.IsSuccess);

            await db.SaveChangesAsync();
        }

        await using var verify = _factory!.CreateIsolatedContext();
        var persisted = await verify.HonorairesInvoices.AsNoTracking()
            .Include(i => i.Payments)
            .SingleAsync(i => i.Id == invoiceId);

        Assert.Equal(HonorairesInvoiceStatus.PartiallyPaid, persisted.Status);
        Assert.Equal(80m, persisted.AmountPaid.Amount);
        Assert.Equal(158m, persisted.AmountDue); // 200 * 1.19 - 80 = 158
        Assert.Single(persisted.Payments);
        // Validate + RecordPayment => Version 1 → 2 → 3.
        Assert.Equal(3, persisted.Version);
    }

    /// <summary>
    /// Bug latent identique : <c>UpdateInvoiceDraftAsync</c> appelle <c>ReplaceLines</c> sur un
    /// brouillon tracké, ce qui remplace le contenu sans jamais faire <c>db.HonorairesInvoiceLines.Add</c>.
    /// Sans la convention, l'INSERT devient UPDATE et la sauvegarde échoue.
    /// </summary>
    [Fact]
    public async Task ReplaceLines_OnSavedDraft_PersistsNewLines()
    {
        if (!_canRun) return;

        Guid draftId;
        await using (var db = _factory!.CreateIsolatedContext())
        {
            var draft = HonorairesInvoice.CreateDraft(
                Guid.NewGuid(), "Cabinet Test", new DateTime(2026, 8, 1)).Value;
            draft.AddLine("Tenue comptable", null, 1m, Money.Create(100m), VatRate.Standard);
            db.HonorairesInvoices.Add(draft);
            await db.SaveChangesAsync();
            draftId = draft.Id;
        }

        await using (var db = _factory!.CreateIsolatedContext())
        {
            var draft = await db.HonorairesInvoices.Include(i => i.Lines).FirstAsync(i => i.Id == draftId);
            var replace = draft.ReplaceLines(new[]
            {
                ((string?)"A1", "Déclaration fiscale", (string?)null, 1m, Money.Create(250m), VatRate.Standard, (decimal?)null),
                ((string?)"A2", "Bilan annuel",       (string?)null, 1m, Money.Create(400m), VatRate.Standard, (decimal?)null)
            });
            Assert.True(replace.IsSuccess);
            await db.SaveChangesAsync();
        }

        await using var verify = _factory!.CreateIsolatedContext();
        var reloaded = await verify.HonorairesInvoices.AsNoTracking()
            .Include(i => i.Lines)
            .SingleAsync(i => i.Id == draftId);

        Assert.Equal(2, reloaded.Lines.Count);
        Assert.Equal(650m, reloaded.SubTotal.Amount);
    }

    private async Task<Guid> SeedValidatedInvoiceAsync(decimal totalHt)
    {
        await using var db = _factory!.CreateIsolatedContext();
        var invoice = HonorairesInvoice.CreateDraft(
            firmClientAssignmentId: Guid.NewGuid(),
            clientName: "Client Cabinet Test",
            issueDate: new DateTime(2026, 8, 1),
            dueDate: new DateTime(2026, 8, 31)).Value;

        invoice.AddLine("Prestation d'honoraires", null, 1m, Money.Create(totalHt), VatRate.Standard);
        invoice.AssignNumber("FAC-TEST-000001");

        var validate = invoice.Validate();
        Assert.True(validate.IsSuccess);

        db.HonorairesInvoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    private bool CanConnectAndCreate(string connectionString)
    {
        try
        {
            using var context = NewRawContext(connectionString);
            context.Database.EnsureCreated();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static TenantDbContext NewRawContext(string connectionString)
        => new(new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connectionString)
            .Options);

    public void Dispose()
    {
        if (!_canRun || string.IsNullOrWhiteSpace(_connectionString))
            return;

        try
        {
            using var context = NewRawContext(_connectionString);
            context.Database.EnsureDeleted();
        }
        catch
        {
            // Nettoyage best-effort.
        }
    }
}
