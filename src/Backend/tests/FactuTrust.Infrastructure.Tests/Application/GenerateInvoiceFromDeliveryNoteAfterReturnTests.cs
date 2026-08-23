using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.DeliveryNotes.Commands;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class GenerateInvoiceFromDeliveryNoteAfterReturnTests
{
    [Fact]
    public async Task MonoBl_AfterPartialReturn_InvoicesRemainingQuantityOnly()
    {
        var (bl, line) = DeliveredNote(10m);
        Assert.True(bl.RecordReturn(line.Id, 3m).IsSuccess);

        Invoice? captured = null;
        var handler = CreateMonoHandler(bl, invoice => captured = invoice);

        var result = await handler.Handle(
            new GenerateInvoiceFromDeliveryNoteCommand(bl.Id, new GenerateInvoiceFromDeliveryNoteDto(null, null, null, null)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.NotNull(captured);
        var invoiceLine = Assert.Single(captured!.Lines);
        Assert.Equal(7m, invoiceLine.Quantity);
    }

    [Fact]
    public async Task MonoBl_AfterFullReturn_Fails()
    {
        var (bl, line) = DeliveredNote(6m);
        Assert.True(bl.RecordReturn(line.Id, 6m).IsSuccess);

        var handler = CreateMonoHandler(bl, _ => { });
        var result = await handler.Handle(
            new GenerateInvoiceFromDeliveryNoteCommand(bl.Id, new GenerateInvoiceFromDeliveryNoteDto(null, null, null, null)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("retournées", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Grouped_TwoBls_OnePartiallyReturned_UsesInvoiceableQuantities()
    {
        var client = NewClient();
        var product = NewProduct();
        var (bl1, line1) = DeliveredNote(10m, product, client);
        var (bl2, _) = DeliveredNote(4m, product, client);
        Assert.True(bl1.RecordReturn(line1.Id, 3m).IsSuccess);

        Invoice? captured = null;
        var handler = CreateGroupedHandler(new[] { bl1, bl2 }, invoice => captured = invoice);

        var result = await handler.Handle(
            new GenerateInvoiceFromDeliveryNotesCommand(new GenerateInvoiceFromDeliveryNotesDto(
                new[] { bl1.Id, bl2.Id }, null, null, null, null)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.NotNull(captured);
        var invoiceLine = Assert.Single(captured!.Lines);
        Assert.Equal(11m, invoiceLine.Quantity);
    }

    private static GenerateInvoiceFromDeliveryNoteCommandHandler CreateMonoHandler(
        DeliveryNote bl,
        Action<Invoice> capture)
    {
        var blRepo = new Mock<IDeliveryNoteRepository>();
        blRepo.Setup(r => r.GetByIdWithDetailsAsync(bl.Id, It.IsAny<CancellationToken>())).ReturnsAsync(bl);

        var invoiceRepo = new Mock<IInvoiceRepository>();
        invoiceRepo
            .Setup(r => r.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .Callback<Invoice, CancellationToken>((i, _) => capture(i))
            .ReturnsAsync((Invoice i, CancellationToken _) => i);

        var stamp = new Mock<IFiscalStampResolver>();
        stamp.Setup(s => s.ResolveSignedStampAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Money.Create(1m));

        var numbers = new Mock<IInvoiceNumberGenerator>();
        numbers.Setup(n => n.ReserveNextNumberAsync(It.IsAny<Guid>(), "FAC", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InvoiceNumber.Create("FAC", 2026, 50));

        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());

        var unitOfWork = new Mock<ITenantUnitOfWork>();
        unitOfWork
            .Setup(u => u.ExecuteAsync(It.IsAny<Func<CancellationToken, Task<Result<Guid>>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<Result<Guid>>>, CancellationToken>((action, ct) => action(ct));

        return new GenerateInvoiceFromDeliveryNoteCommandHandler(
            blRepo.Object, invoiceRepo.Object, stamp.Object, numbers.Object, tenant.Object, unitOfWork.Object);
    }

    private static GenerateInvoiceFromDeliveryNotesCommandHandler CreateGroupedHandler(
        IReadOnlyList<DeliveryNote> notes,
        Action<Invoice> capture)
    {
        var blRepo = new Mock<IDeliveryNoteRepository>();
        foreach (var note in notes)
        {
            var captured = note;
            blRepo.Setup(r => r.GetByIdWithDetailsAsync(captured.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(captured);
        }

        var invoiceRepo = new Mock<IInvoiceRepository>();
        invoiceRepo
            .Setup(r => r.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .Callback<Invoice, CancellationToken>((i, _) => capture(i))
            .ReturnsAsync((Invoice i, CancellationToken _) => i);

        var stamp = new Mock<IFiscalStampResolver>();
        stamp.Setup(s => s.ResolveSignedStampAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Money.Create(1m));

        var numbers = new Mock<IInvoiceNumberGenerator>();
        numbers.Setup(n => n.ReserveNextNumberAsync(It.IsAny<Guid>(), "FAC", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InvoiceNumber.Create("FAC", 2026, 51));

        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());

        var unitOfWork = new Mock<ITenantUnitOfWork>();
        unitOfWork
            .Setup(u => u.ExecuteAsync(It.IsAny<Func<CancellationToken, Task<Result<Guid>>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<Result<Guid>>>, CancellationToken>((action, ct) => action(ct));

        return new GenerateInvoiceFromDeliveryNotesCommandHandler(
            blRepo.Object,
            invoiceRepo.Object,
            stamp.Object,
            numbers.Object,
            tenant.Object,
            new Mock<ICurrentUser>().Object,
            new Mock<IAuditService>().Object,
            unitOfWork.Object);
    }

    private static (DeliveryNote Note, DeliveryNoteLine Line) DeliveredNote(
        decimal quantity,
        Product? product = null,
        Client? client = null)
    {
        product ??= NewProduct();
        var note = DeliveryNote.Create(
            DeliveryNoteNumber.Generate(2026, Random.Shared.Next(100, 999)).Value,
            client ?? NewClient(),
            new DateTime(2026, 8, 1),
            "12 avenue Habib Bourguiba").Value;
        Assert.True(note.AddLine(product, quantity).IsSuccess);
        Assert.True(note.Confirm().IsSuccess);
        var line = Assert.Single(note.Lines);
        Assert.True(line.RecordDelivery(quantity).IsSuccess);
        Assert.True(note.RecordDelivery(new DateTime(2026, 8, 2), "Réceptionnaire").IsSuccess);
        return (note, line);
    }

    private static Client NewClient()
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create($"c{Guid.NewGuid():N}@example.com").Value;
        return Client.Create("Client facture", ClientType.Individual, address, email).Value;
    }

    private static Product NewProduct() =>
        Product.Create(
            $"P{Guid.NewGuid():N}"[..8], "Article", ProductType.Product, Money.Create(20m), VatRate.Standard,
            Guid.NewGuid()).Value;
}
