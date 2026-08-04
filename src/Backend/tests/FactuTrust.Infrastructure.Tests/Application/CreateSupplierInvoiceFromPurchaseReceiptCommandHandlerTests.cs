using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.PurchaseReceipts.Commands;
using FactuTrust.Application.Features.SupplierInvoices.Services;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class CreateSupplierInvoiceFromPurchaseReceiptCommandHandlerTests
{
    [Fact]
    public async Task Handle_UseSuggestedNumber_CreatesInvoiceWithReservedNumber()
    {
        var (receipt, po, receiptRepo, poRepo) = BuildValidatedReceiptScenario();

        var numberService = new Mock<ISupplierInvoiceNumberService>();
        numberService
            .Setup(s => s.ReserveNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("FS-2026-000045");

        var supplierInvoiceRepo = new Mock<ISupplierInvoiceRepository>();
        supplierInvoiceRepo
            .Setup(r => r.AddAsync(It.IsAny<SupplierInvoice>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SupplierInvoice invoice, CancellationToken _) => invoice);

        var handler = BuildHandler(receiptRepo, poRepo, supplierInvoiceRepo, numberService);

        var result = await handler.Handle(
            new CreateSupplierInvoiceFromPurchaseReceiptCommand(
                receipt.Id,
                "",
                new DateTime(2026, 4, 5),
                UseSuggestedNumber: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        supplierInvoiceRepo.Verify(
            r => r.AddAsync(
                It.Is<SupplierInvoice>(i => i.InvoiceNumber == "FS-2026-000045"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ManualDuplicateNumber_ReturnsConflict()
    {
        var (receipt, po, receiptRepo, poRepo) = BuildValidatedReceiptScenario();

        var numberService = new Mock<ISupplierInvoiceNumberService>();
        numberService
            .Setup(s => s.IsAvailableAsync("FS-2026-W8F6P", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var supplierInvoiceRepo = new Mock<ISupplierInvoiceRepository>();
        var handler = BuildHandler(receiptRepo, poRepo, supplierInvoiceRepo, numberService);

        var result = await handler.Handle(
            new CreateSupplierInvoiceFromPurchaseReceiptCommand(
                receipt.Id,
                "FS-2026-W8F6P",
                new DateTime(2026, 4, 5)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        supplierInvoiceRepo.Verify(r => r.AddAsync(It.IsAny<SupplierInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

  [Fact]
  public async Task Handle_ReturnsCreationResultWithPersistedInvoiceNumber()
  {
      var (receipt, _, receiptRepo, poRepo) = BuildValidatedReceiptScenario();

      var numberService = new Mock<ISupplierInvoiceNumberService>();
      numberService
          .Setup(s => s.ReserveNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync("FS-2026-000777");

      var supplierInvoiceRepo = new Mock<ISupplierInvoiceRepository>();
      supplierInvoiceRepo
          .Setup(r => r.AddAsync(It.IsAny<SupplierInvoice>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync((SupplierInvoice invoice, CancellationToken _) => invoice);

      var handler = BuildHandler(receiptRepo, poRepo, supplierInvoiceRepo, numberService);

      var result = await handler.Handle(
          new CreateSupplierInvoiceFromPurchaseReceiptCommand(
              receipt.Id,
              "",
              new DateTime(2026, 4, 5),
              UseSuggestedNumber: true),
          CancellationToken.None);

      Assert.True(result.IsSuccess);
      Assert.Equal("FS-2026-000777", result.Value.InvoiceNumber);
      Assert.NotEqual(Guid.Empty, result.Value.Id);
  }

  [Fact]
  public async Task Handle_UseSuggestedNumber_RetriesAtomicallyOnDuplicateDbError()
  {
      var (receipt, _, receiptRepo, poRepo) = BuildValidatedReceiptScenario();

      var numberService = new Mock<ISupplierInvoiceNumberService>();
      numberService
          .SetupSequence(s => s.ReserveNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync("FS-2026-000010")
          .ReturnsAsync("FS-2026-000011");

      var addCall = 0;
      var supplierInvoiceRepo = new Mock<ISupplierInvoiceRepository>();
      supplierInvoiceRepo
          .Setup(r => r.AddAsync(It.IsAny<SupplierInvoice>(), It.IsAny<CancellationToken>()))
          .Returns((SupplierInvoice invoice, CancellationToken _) =>
          {
              addCall++;
              if (addCall == 1)
                  throw new InvalidOperationException(
                      "duplicate key value violates unique constraint \"IX_SupplierInvoices_InvoiceNumber\"");
              return Task.FromResult(invoice);
          });

      var handler = BuildHandler(receiptRepo, poRepo, supplierInvoiceRepo, numberService);

      var result = await handler.Handle(
          new CreateSupplierInvoiceFromPurchaseReceiptCommand(
              receipt.Id,
              "",
              new DateTime(2026, 4, 5),
              UseSuggestedNumber: true),
          CancellationToken.None);

      Assert.True(result.IsSuccess);
      Assert.Equal("FS-2026-000011", result.Value.InvoiceNumber);
      Assert.Equal(2, addCall);
      numberService.Verify(s => s.ReserveNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
  }

  [Fact]
  public async Task Handle_ManualNumber_DoesNotRetryOnDuplicateDbError_ReturnsConflictWithMetadata()
  {
      var (receipt, _, receiptRepo, poRepo) = BuildValidatedReceiptScenario();

      var numberService = new Mock<ISupplierInvoiceNumberService>();
      numberService
          .Setup(s => s.IsAvailableAsync("FS-CHOSEN", It.IsAny<CancellationToken>()))
          .ReturnsAsync(true);
      numberService
          .Setup(s => s.PreviewNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync("FS-2026-000050");

      var supplierInvoiceRepo = new Mock<ISupplierInvoiceRepository>();
      supplierInvoiceRepo
          .Setup(r => r.AddAsync(It.IsAny<SupplierInvoice>(), It.IsAny<CancellationToken>()))
          .ThrowsAsync(new InvalidOperationException(
              "duplicate key value violates unique constraint \"IX_SupplierInvoices_InvoiceNumber\""));

      var handler = BuildHandler(receiptRepo, poRepo, supplierInvoiceRepo, numberService);

      var result = await handler.Handle(
          new CreateSupplierInvoiceFromPurchaseReceiptCommand(
              receipt.Id,
              "FS-CHOSEN",
              new DateTime(2026, 4, 5)),
          CancellationToken.None);

      Assert.True(result.IsFailure);
      Assert.Equal("Conflict", result.Error.Code);
      Assert.NotNull(result.Error.Metadata);
      Assert.Equal("FS-2026-000050", result.Error.Metadata!["suggestedInvoiceNumber"]);
      Assert.Equal("FS-CHOSEN", result.Error.Metadata!["conflictingInvoiceNumber"]);
      supplierInvoiceRepo.Verify(r => r.AddAsync(It.IsAny<SupplierInvoice>(), It.IsAny<CancellationToken>()), Times.Once);
      numberService.Verify(s => s.ReserveNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task Handle_ManualUniqueNumber_UsesProvidedNumber()
  {
    var (receipt, po, receiptRepo, poRepo) = BuildValidatedReceiptScenario();

    var numberService = new Mock<ISupplierInvoiceNumberService>();
    numberService
      .Setup(s => s.IsAvailableAsync("FS-MANUAL-99", It.IsAny<CancellationToken>()))
      .ReturnsAsync(true);

    var supplierInvoiceRepo = new Mock<ISupplierInvoiceRepository>();
    supplierInvoiceRepo
      .Setup(r => r.AddAsync(It.IsAny<SupplierInvoice>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((SupplierInvoice invoice, CancellationToken _) => invoice);

    var handler = BuildHandler(receiptRepo, poRepo, supplierInvoiceRepo, numberService);

    var result = await handler.Handle(
      new CreateSupplierInvoiceFromPurchaseReceiptCommand(
        receipt.Id,
        "FS-MANUAL-99",
        new DateTime(2026, 4, 5)),
      CancellationToken.None);

    Assert.True(result.IsSuccess);
    supplierInvoiceRepo.Verify(
      r => r.AddAsync(
        It.Is<SupplierInvoice>(i => i.InvoiceNumber == "FS-MANUAL-99"),
        It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task Handle_UnrelatedDuplicateKeyError_PropagatesInsteadOfFakeConflict()
  {
      // Régression : une violation de clé sur une AUTRE table (PK_Products…) ne doit plus
      // être déguisée en « numéro de facture déjà existant » — le vrai défaut doit remonter.
      var (receipt, _, receiptRepo, poRepo) = BuildValidatedReceiptScenario();

      var numberService = new Mock<ISupplierInvoiceNumberService>();
      numberService
          .Setup(s => s.ReserveNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync("FS-2026-000050");

      var supplierInvoiceRepo = new Mock<ISupplierInvoiceRepository>();
      supplierInvoiceRepo
          .Setup(r => r.AddAsync(It.IsAny<SupplierInvoice>(), It.IsAny<CancellationToken>()))
          .ThrowsAsync(new InvalidOperationException(
              "Violation of PRIMARY KEY constraint 'PK_Products'. Cannot insert duplicate key in object 'dbo.Products'."));

      var handler = BuildHandler(receiptRepo, poRepo, supplierInvoiceRepo, numberService);

      await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
          new CreateSupplierInvoiceFromPurchaseReceiptCommand(
              receipt.Id,
              "",
              new DateTime(2026, 4, 5),
              UseSuggestedNumber: true),
          CancellationToken.None));

      // Aucun numéro supplémentaire n'a été brûlé par une boucle de retry inutile.
      numberService.Verify(
          s => s.ReserveNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
          Times.Once);
  }

  [Fact]
  public async Task Handle_NoInvoiceableLine_DoesNotReserveNumber()
  {
      // Le numéro est une ressource consommée définitivement : aucune réservation ne doit
      // avoir lieu tant que les validations préalables ne sont pas passées.
      var (receipt, _, receiptRepo, poRepo) = BuildValidatedReceiptScenario();

      var numberService = new Mock<ISupplierInvoiceNumberService>();
      var supplierInvoiceRepo = new Mock<ISupplierInvoiceRepository>();
      var handler = BuildHandler(receiptRepo, poRepo, supplierInvoiceRepo, numberService);

      var result = await handler.Handle(
          new CreateSupplierInvoiceFromPurchaseReceiptCommand(
              receipt.Id,
              "",
              new DateTime(2026, 4, 5),
              UseSuggestedNumber: true,
              Lines: [new CreateSupplierInvoiceLineSelection(receipt.Lines.First().Id, 0m)]),
          CancellationToken.None);

      Assert.True(result.IsFailure);
      numberService.Verify(
          s => s.ReserveNextAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
          Times.Never);
      supplierInvoiceRepo.Verify(
          r => r.AddAsync(It.IsAny<SupplierInvoice>(), It.IsAny<CancellationToken>()),
          Times.Never);
  }

    private static CreateSupplierInvoiceFromPurchaseReceiptCommandHandler BuildHandler(
        Mock<IPurchaseReceiptRepository> receiptRepo,
        Mock<IPurchaseOrderRepository> poRepo,
        Mock<ISupplierInvoiceRepository> supplierInvoiceRepo,
        Mock<ISupplierInvoiceNumberService> numberService)
    {
        var withholdingTaxRepo = new Mock<IWithholdingTaxRepository>();
        var withholdingTaxService = new Mock<IWithholdingTaxService>();
        var fiscalYearParams = new Mock<IWithholdingFiscalYearParameterRepository>();
        var publisher = new Mock<IPublisher>();

        return new CreateSupplierInvoiceFromPurchaseReceiptCommandHandler(
            receiptRepo.Object,
            poRepo.Object,
            supplierInvoiceRepo.Object,
            new Mock<IAuditService>().Object,
            NullLogger<CreateSupplierInvoiceFromPurchaseReceiptCommandHandler>.Instance,
            publisher.Object,
            withholdingTaxRepo.Object,
            withholdingTaxService.Object,
            fiscalYearParams.Object,
            numberService.Object);
    }

    private static (PurchaseReceipt receipt, PurchaseOrder po, Mock<IPurchaseReceiptRepository> receiptRepo, Mock<IPurchaseOrderRepository> poRepo)
        BuildValidatedReceiptScenario()
    {
        var supplier = BuildSupplier();
        var warehouse = Warehouse.Create("WH1", "Principal").Value;
        var category = ProductCategory.Create("C", "Cat").Value;
        var price = Money.Create(100m);
        var product = Product.Create(
            "ART-BR", "Papier", ProductType.Product, price, VatRate.Standard,
            category.Id, purchasePrice: price).Value;

        var po = PurchaseOrder.Create(PurchaseOrderNumber.Create("BC", 2026, 42), supplier, new DateTime(2026, 4, 1)).Value;
        Assert.True(po.AddLine(product, 10m).IsSuccess);
        Assert.True(po.Confirm().IsSuccess);
        var poLineId = po.Lines.First().Id;
        Assert.True(po.ReceiveGoods([(poLineId, 4m)]).IsSuccess);

        var receipt = PurchaseReceipt.Create(
            PurchaseReceiptNumber.Create("BR", 2026, 3),
            supplier,
            warehouse,
            new DateTime(2026, 4, 2),
            purchaseOrderId: po.Id).Value;
        Assert.True(receipt.AddLine(product, 4m, price, orderedQuantity: 10m, purchaseOrderLineId: poLineId).IsSuccess);
        Assert.True(receipt.MarkValidated().IsSuccess);

        var receiptRepo = new Mock<IPurchaseReceiptRepository>();
        receiptRepo
            .Setup(r => r.GetByIdWithLinesAsync(receipt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);
        receiptRepo
            .Setup(r => r.UpdateAsync(receipt, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var poRepo = new Mock<IPurchaseOrderRepository>();
        poRepo
            .Setup(r => r.GetByIdWithLinesAsync(po.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(po);
        poRepo
            .Setup(r => r.UpdateAsync(po, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return (receipt, po, receiptRepo, poRepo);
    }

    private static Supplier BuildSupplier()
    {
        var address = Address.Create("1 rue", "Tunis", "Tunis").Value;
        var email = Email.Create("s@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        return Supplier.Create("Fournisseur", SupplierType.Business, address, email, nif: nif).Value;
    }
}
