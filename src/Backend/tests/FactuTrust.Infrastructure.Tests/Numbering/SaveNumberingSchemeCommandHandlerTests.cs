using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Numbering.Commands;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Numbering;

public sealed class SaveNumberingSchemeCommandHandlerTests
{
    [Fact]
    public async Task Handle_CreatesSchemeSeededFromLegacySequence()
    {
        var tenantId = Guid.NewGuid();
        var repository = new Mock<INumberingSchemeRepository>();
        repository.Setup(r => r.GetByTypeAsync(tenantId, NumberingDocumentType.Invoice, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentNumberingScheme?)null);

        DocumentNumberingScheme? savedScheme = null;
        repository.Setup(r => r.AddAsync(It.IsAny<DocumentNumberingScheme>(), It.IsAny<CancellationToken>()))
            .Callback<DocumentNumberingScheme, CancellationToken>((scheme, _) => savedScheme = scheme)
            .Returns(Task.CompletedTask);

        var numberService = new Mock<IDocumentNumberService>();
        numberService.Setup(s => s.GetEffectiveCurrentSequenceAsync(tenantId, NumberingDocumentType.Invoice, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);
        numberService.Setup(s => s.PreviewNextAsync(tenantId, NumberingDocumentType.Invoice, 2026, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("FAC-2026-000051");

        var audit = new Mock<IAuditService>();
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.TenantId).Returns(tenantId);

        var handler = new SaveNumberingSchemeCommandHandler(repository.Object, numberService.Object, audit.Object, tenantContext.Object);
        var blocks = NumberingSchemeDefaults.GetDefaultBlocks(NumberingDocumentType.Invoice)
            .Select((b, index) => new NumberingBlockDto { Type = b.Type, Value = b.Value, Order = index, Label = b.Type.ToDisplayString() })
            .ToList();

        var result = await handler.Handle(
            new SaveNumberingSchemeCommand(NumberingDocumentType.Invoice, new SaveNumberingSchemeRequest { StartNumber = 55, Blocks = blocks }, 2026),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(savedScheme);
        Assert.Equal(50, savedScheme!.CurrentSequence);
        Assert.Equal(55, result.Value.NextSequence);
    }

    [Fact]
    public async Task Handle_RejectsFormatWithoutDocumentNumberBlock()
    {
        var tenantId = Guid.NewGuid();
        var repository = new Mock<INumberingSchemeRepository>();
        var numberService = new Mock<IDocumentNumberService>();
        var audit = new Mock<IAuditService>();
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.TenantId).Returns(tenantId);

        var handler = new SaveNumberingSchemeCommandHandler(
            repository.Object, numberService.Object, audit.Object, tenantContext.Object);

        // A format with a prefix and a separator but NO document-number block must be rejected,
        // so a tenant can never persist a scheme that would later break document emission.
        var blocks = new List<NumberingBlockDto>
        {
            new() { Type = NumberingBlockType.FreeText, Value = "FAC", Order = 0, Label = "Texte libre" },
            new() { Type = NumberingBlockType.Separator, Value = "-", Order = 1, Label = "Separateur" }
        };

        var result = await handler.Handle(
            new SaveNumberingSchemeCommand(
                NumberingDocumentType.Invoice,
                new SaveNumberingSchemeRequest { StartNumber = 1, Blocks = blocks },
                2026),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("bloc numero", result.Error.Description);
        repository.Verify(r => r.AddAsync(It.IsAny<DocumentNumberingScheme>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.UpdateAsync(It.IsAny<DocumentNumberingScheme>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
