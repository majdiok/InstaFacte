using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class PurchaseOrderProjectAssignTests
{
    [Fact]
    public void AssignToProject_SetsProjectId()
    {
        var po = CreateDraftOrder();
        var projectId = Guid.NewGuid();

        var result = po.AssignToProject(projectId);

        Assert.True(result.IsSuccess);
        Assert.Equal(projectId, po.ProjectId);
    }

    [Fact]
    public void AssignToProject_EmptyGuid_ClearsProjectId()
    {
        var po = CreateDraftOrder();
        Assert.True(po.AssignToProject(Guid.NewGuid()).IsSuccess);

        var result = po.AssignToProject(Guid.Empty);

        Assert.True(result.IsSuccess);
        Assert.Null(po.ProjectId);
    }

    [Fact]
    public void AssignToProject_ReassignsToAnotherProject()
    {
        var po = CreateDraftOrder();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        Assert.True(po.AssignToProject(first).IsSuccess);

        Assert.True(po.AssignToProject(second).IsSuccess);

        Assert.Equal(second, po.ProjectId);
    }

    private static PurchaseOrder CreateDraftOrder()
    {
        var address = Address.Create("1 rue test", "Tunis", "Tunis").Value;
        var email = Email.Create("supplier.project@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var supplier = Supplier.Create("Fournisseur Test", SupplierType.Business, address, email, nif: nif).Value;
        var number = PurchaseOrderNumber.Create("BC", 2026, 9001);
        return PurchaseOrder.Create(number, supplier, new DateTime(2026, 9, 1)).Value;
    }
}
