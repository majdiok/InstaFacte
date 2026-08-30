using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.RecurringContracts;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.RecurringContracts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Tests.Services.RecurringContracts;

/// <summary>
/// Harnais partagé des tests de service du module Contrats récurrents (pattern InMemory,
/// cf. Stock/StockMutationServicePassthroughTests) : base InMemory unique par fixture,
/// utilisateur courant et annuaire de membres stubbés, médiateur non appelable (les chemins
/// testés ne déclenchent jamais le billing).
/// </summary>
internal sealed class RecurringContractTestHarness
{
    public TestTenantDbContextFactory Factory { get; } = new();
    public TestCurrentUser CurrentUser { get; } = new();
    public StubTenantMemberDirectory Members { get; } = new();

    public RecurringContractService CreateService(
        IMediator? mediator = null,
        IPlanQuotaService? planQuota = null)
    {
        var med = mediator ?? new ThrowingMediator();
        return new RecurringContractService(
            Factory,
            CurrentUser,
            med,
            new RecurringContractBillingService(
                med,
                Options.Create(new RecurringContractsOptions { Enabled = true, BillingJobEnabled = true }),
                NullLogger<RecurringContractBillingService>.Instance),
            Options.Create(new RecurringContractsOptions { Enabled = true, BillingJobEnabled = true }),
            Members,
            planQuota ?? new AllowAllPlanQuota());
    }

    public async Task<Client> SeedClientAsync(string name)
    {
        await using var ctx = Factory.CreateContext();
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create($"{Guid.NewGuid():N}@example.com").Value;
        var client = Client.Create(name, ClientType.Individual, address, email).Value;
        ctx.Clients.Add(client);
        await ctx.SaveChangesAsync();
        return client;
    }

    /// <summary>Crée et persiste un contrat brouillon avec lignes, puis applique <paramref name="configure"/> (Activate, etc.).</summary>
    public async Task<RecurringContract> SeedContractAsync(
        Guid clientId,
        Action<RecurringContract>? configure = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        BillingFrequency frequency = BillingFrequency.Monthly,
        int billingDay = 1,
        bool autoRenew = true,
        int noticePeriodDays = 30,
        string? number = null,
        string? reference = null,
        string? notes = null,
        Guid? sourceQuoteId = null,
        Guid? paymentTermTemplateId = null,
        Action<RecurringContract>? lines = null)
    {
        var contract = RecurringContract.CreateDraft(
            clientId, frequency, billingDay, startDate ?? DateTime.UtcNow.Date,
            endDate, autoRenew, noticePeriodDays, "TND",
            paymentTermTemplateId, sourceQuoteId, reference, notes).Value;
        contract.AssignNumber(number ?? $"CTR-T-{Guid.NewGuid():N}"[..12]);

        lines?.Invoke(contract);

        // Configure (Activate/Suspend/Cancel/MarkExpired/AdvanceBillingSchedule…) avant persistance.
        configure?.Invoke(contract);

        await using var ctx = Factory.CreateContext();
        ctx.RecurringContracts.Add(contract);
        await ctx.SaveChangesAsync();
        return contract;
    }

    public async Task<RecurringContractBillingRun> SeedRunAsync(
        Guid contractId,
        DateTime periodFrom,
        DateTime periodTo,
        RecurringContractBillingRunStatus status = RecurringContractBillingRunStatus.Invoiced,
        decimal fixedAmount = 0m,
        decimal usageAmount = 0m,
        decimal prorationAmount = 0m,
        Guid? invoiceDraftId = null,
        Guid? invoiceId = null)
    {
        var run = RecurringContractBillingRun.CreatePending(contractId, periodFrom, periodTo);
        if (status is RecurringContractBillingRunStatus.DraftCreated or RecurringContractBillingRunStatus.Invoiced)
            run.MarkDraftCreated(invoiceDraftId ?? Guid.NewGuid(), fixedAmount, usageAmount, prorationAmount);
        if (status == RecurringContractBillingRunStatus.Invoiced)
            run.MarkInvoiced(invoiceId ?? Guid.NewGuid());
        if (status == RecurringContractBillingRunStatus.Failed)
            run.MarkFailed("Échec de test");
        if (status == RecurringContractBillingRunStatus.Skipped)
            run.MarkSkipped("Ignoré (test)");

        await using var ctx = Factory.CreateContext();
        ctx.RecurringContractBillingRuns.Add(run);
        await ctx.SaveChangesAsync();
        return run;
    }

    /// <summary>Crée et persiste une facture rattachée au client (ligne libre unique).</summary>
    public async Task<Invoice> SeedInvoiceAsync(
        Guid clientId,
        DateTime issueDate,
        decimal amountHt,
        string numberPrefix = "FA",
        bool validate = true,
        bool cancel = false,
        DateTime? dueDate = null)
    {
        await using var ctx = Factory.CreateContext();
        var client = await ctx.Clients.FirstAsync(c => c.Id == clientId);
        var invoice = Invoice.Create(
            InvoiceNumber.Create(numberPrefix, issueDate.Year, Random.Shared.Next(1, 999999)),
            client, issueDate, dueDate ?? issueDate.AddDays(30)).Value;
        var add = invoice.AddCustomLine(
            designation: "Prestation", description: null, quantity: 1m, unit: "Unité",
            unitPrice: Money.Create(amountHt), vatRate: VatRate.Standard);
        if (!add.IsSuccess) throw new InvalidOperationException(add.Error.Description);
        if (validate)
        {
            var v = invoice.Validate();
            if (!v.IsSuccess) throw new InvalidOperationException(v.Error.Description);
        }
        if (cancel)
        {
            var c = invoice.Cancel("Annulation de test");
            if (!c.IsSuccess) throw new InvalidOperationException(c.Error.Description);
        }
        ctx.Invoices.Add(invoice);
        await ctx.SaveChangesAsync();
        return invoice;
    }

    /// <summary>Crée et persiste un avoir rattaché à une facture d'origine.</summary>
    public async Task<Invoice> SeedCreditNoteAsync(
        Guid clientId,
        Guid linkedInvoiceId,
        DateTime issueDate,
        decimal amountHtMagnitude,
        bool validate = true,
        bool cancel = false)
    {
        await using var ctx = Factory.CreateContext();
        var client = await ctx.Clients.FirstAsync(c => c.Id == clientId);
        var note = Invoice.CreateCreditNote(
            InvoiceNumber.Create("AVO", issueDate.Year, Random.Shared.Next(1, 999999)),
            client, issueDate, linkedInvoiceId).Value;
        var add = note.AddCustomLine(
            designation: "Avoir", description: null, quantity: 1m, unit: "Unité",
            unitPrice: Money.Create(amountHtMagnitude), vatRate: VatRate.Standard);
        if (!add.IsSuccess) throw new InvalidOperationException(add.Error.Description);
        if (validate)
        {
            var v = note.Validate();
            if (!v.IsSuccess) throw new InvalidOperationException(v.Error.Description);
        }
        if (cancel)
        {
            var c = note.Cancel("Annulation de test");
            if (!c.IsSuccess) throw new InvalidOperationException(c.Error.Description);
        }
        ctx.Invoices.Add(note);
        await ctx.SaveChangesAsync();
        return note;
    }

    /// <summary>Rattache une facture (ou un avoir) existante à un contrat et à un run de facturation.</summary>
    public async Task LinkInvoiceToContractAsync(Guid invoiceId, Guid contractId, Guid runId)
    {
        await using var ctx = Factory.CreateContext();
        var invoice = await ctx.Invoices.FirstAsync(i => i.Id == invoiceId);
        invoice.SetRecurringContractSource(contractId, runId);
        await ctx.SaveChangesAsync();
    }

    public async Task<Company> SeedCompanyAsync(
        string name = "Société Test",
        string? bankName = "BIAT",
        string? iban = "TN5904018104004942712345",
        string? rib = "04018104004942712345")
    {
        await using var ctx = Factory.CreateContext();
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create($"{Guid.NewGuid():N}@example.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var company = Company.Create(name, address, nif, email).Value;
        company.SetBankInfo(bankName, iban, rib);
        ctx.Companies.Add(company);
        await ctx.SaveChangesAsync();
        return company;
    }
}

internal sealed class AllowAllPlanQuota : IPlanQuotaService
{
    public Task<Result> EnsureCanCreateInvoiceAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success());

    public Task OnInvoiceCreatedAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed class DenyingPlanQuota : IPlanQuotaService
{
    public Task<Result> EnsureCanCreateInvoiceAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Failure(Error.Validation("Quota", "Limite mensuelle de factures atteinte.")));

    public Task OnInvoiceCreatedAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>Enregistre les commandes MediatR et renvoie des succès configurables.</summary>
internal sealed class RecordingMediator : IMediator
{
    public List<object> Sent { get; } = [];
    public Func<object, object?>? OnSend { get; set; }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        Sent.Add(request);
        if (OnSend is not null)
        {
            var result = OnSend(request);
            if (result is TResponse typed)
                return Task.FromResult(typed);
        }

        throw new InvalidOperationException($"Aucune réponse configurée pour {request.GetType().Name}.");
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    public Task Publish(object notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
        => Task.CompletedTask;
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
        => throw new NotSupportedException();
}

internal sealed class TestTenantDbContextFactory : ITenantDbContextFactory
{
    private readonly string _name = $"RecurrentContractsTests_{Guid.NewGuid()}";

    public TenantDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase(_name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TenantDbContext(options);
    }
}

internal sealed class TestCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; } = Guid.NewGuid();
    public string? Email => "recurring-tests@example.com";
    public Guid? TenantId { get; } = Guid.NewGuid();
    public UserRole? Role => UserRole.Administrator;
    public bool IsAuthenticated => true;
    public bool IsAccountingFirmDelegatedContext => false;
    public Guid? PortalClientId => null;
    public bool IsClientPortal => false;
    public string? IpAddress => "127.0.0.1";
    public string? UserAgent => "FactuTrust.Tests";
    public bool HasPermission(string permission) => true;
}

internal sealed class StubTenantMemberDirectory : ITenantMemberDirectory
{
    private readonly Dictionary<Guid, string> _names = new();

    public void Register(Guid userId, string displayName) => _names[userId] = displayName;

    public Task<Result<(Guid Id, string DisplayName)>> GetMemberAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_names.TryGetValue(userId, out var name)
            ? Result.Success((userId, name))
            : Result.Failure<(Guid, string)>(Error.NotFound("User", userId)));
}

/// <summary>Les chemins couverts par ces tests ne passent jamais par le médiateur (billing exclu).</summary>
internal sealed class ThrowingMediator : IMediator
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Mediator must not be called in these tests.");
    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Mediator must not be called in these tests.");
    public Task Publish(object notification, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Mediator must not be called in these tests.");
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
        => throw new NotSupportedException("Mediator must not be called in these tests.");
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Mediator must not be called in these tests.");
    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Mediator must not be called in these tests.");
    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
        => throw new NotSupportedException("Mediator must not be called in these tests.");
}
