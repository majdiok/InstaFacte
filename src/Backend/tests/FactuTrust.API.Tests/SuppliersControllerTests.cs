using FactuTrust.API.Controllers;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Suppliers.Commands;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.API.Tests;

public sealed class SuppliersControllerTests
{
    [Fact]
    public async Task CreateSupplier_WhenConflict_Returns409WithMetadataNotEmptyGuid()
    {
        var existingId = Guid.NewGuid();
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<CreateSupplierCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Guid>(Error.Conflict(
                "Un fournisseur existe déjà avec cet email.",
                new Dictionary<string, object?>
                {
                    ["existingSupplierId"] = existingId.ToString(),
                    ["field"] = "email"
                })));

        var controller = new SuppliersController(mediator.Object, NullLogger<SuppliersController>.Instance);

        var result = await controller.CreateSupplier(
            new CreateSupplierDto
            {
                Name = "Ste test",
                Type = SupplierType.Individual,
                Street = "1 rue",
                City = "Tunis",
                Governorate = "Tunis",
                Email = "dup@test.com"
            },
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var body = Assert.IsType<FactuTrust.Application.DTOs.ApiResponse<object>>(conflict.Value);
        Assert.False(body.Success);
        Assert.Equal("Un fournisseur existe déjà avec cet email.", body.Message);
        Assert.Contains("Un fournisseur existe déjà avec cet email.", body.Errors);
        var data = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(body.Data);
        Assert.Equal(existingId.ToString(), data["existingSupplierId"]);
        Assert.Equal("email", data["field"]);
    }
}
