using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Suppliers.Commands;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.ValueObjects;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class CreateSupplierCommandHandlerTests
{
    [Fact]
    public async Task Handle_DuplicateEmail_ReturnsConflictWithExistingIdAndField()
    {
        var existing = BuildSupplier("dup@test.com", nif: null);
        var (handler, repo) = Build(existingByEmail: existing);

        var result = await handler.Handle(Command(email: "dup@test.com"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        Assert.Equal("Un fournisseur existe déjà avec cet email.", result.Error.Description);
        Assert.NotNull(result.Error.Metadata);
        Assert.Equal(existing.Id.ToString(), result.Error.Metadata!["existingSupplierId"]);
        Assert.Equal("email", result.Error.Metadata["field"]);
        repo.Verify(r => r.AddAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicateNif_ReturnsConflictWithExistingIdAndField()
    {
        var existing = BuildSupplier("other@test.com", nif: "1234567/A/B/C/000");
        var (handler, repo) = Build(existingByNif: existing);

        var result = await handler.Handle(
            Command(email: "new@test.com", nif: "1234567/A/B/C/000", type: SupplierType.Business),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
        Assert.Equal("Un fournisseur existe déjà avec ce matricule fiscal.", result.Error.Description);
        Assert.NotNull(result.Error.Metadata);
        Assert.Equal(existing.Id.ToString(), result.Error.Metadata!["existingSupplierId"]);
        Assert.Equal("nif", result.Error.Metadata["field"]);
        repo.Verify(r => r.AddAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (
        CreateSupplierCommandHandler Handler,
        Mock<ISupplierRepository> Repo) Build(
        Supplier? existingByEmail = null,
        Supplier? existingByNif = null)
    {
        var repo = new Mock<ISupplierRepository>();
        repo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingByEmail);
        repo.Setup(r => r.GetByNifAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingByNif);
        repo.Setup(r => r.AddAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Supplier s, CancellationToken _) => s);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.LogAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.TenantId).Returns(Guid.NewGuid());

        var withholding = new Mock<IWithholdingTaxRepository>();

        return (new CreateSupplierCommandHandler(
            repo.Object,
            currentUser.Object,
            audit.Object,
            tenant.Object,
            withholding.Object), repo);
    }

    private static CreateSupplierCommand Command(
        string email,
        string? nif = null,
        SupplierType type = SupplierType.Individual) =>
        new(new CreateSupplierDto
        {
            Name = "Ste test",
            Type = type,
            Nif = nif,
            Street = "1 rue",
            City = "Tunis",
            Governorate = "Tunis",
            Email = email,
            PaymentTermDays = 30
        });

    private static Supplier BuildSupplier(string email, string? nif)
    {
        var address = Address.Create("1 rue", "Tunis", "Tunis").Value;
        var emailVo = Email.Create(email).Value;
        NIF? nifVo = nif is null ? null : NIF.Create(nif).Value;
        return Supplier.Create(
            "Existant",
            nifVo is null ? SupplierType.Individual : SupplierType.Business,
            address,
            emailVo,
            nif: nifVo).Value;
    }
}
