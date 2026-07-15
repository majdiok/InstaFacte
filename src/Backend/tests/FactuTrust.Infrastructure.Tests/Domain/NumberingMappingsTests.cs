using FactuTrust.Application.Features.Numbering;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class NumberingMappingsTests
{
    [Fact]
    public void ToDefaultDto_WithLegacySequence_ExposesEffectiveCounters()
    {
        var dto = NumberingMappings.ToDefaultDto(NumberingDocumentType.Invoice, 2026, "FAC-2026-000051", 50);

        Assert.Equal(50, dto.CurrentSequence);
        Assert.Equal(51, dto.StartNumber);
        Assert.Equal(51, dto.NextSequence);
        Assert.Equal(51, dto.MinimumStartNumber);
        Assert.True(dto.HasIssuedDocuments);
        Assert.True(dto.IsFormatLocked);
    }
}
