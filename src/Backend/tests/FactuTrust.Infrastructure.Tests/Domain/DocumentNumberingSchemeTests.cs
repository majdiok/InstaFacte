using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class DocumentNumberingSchemeTests
{
    private static DocumentNumberingScheme CreateUnlockedScheme(int currentSequence = 0) =>
        DocumentNumberingScheme.CreateDefault(Guid.NewGuid(), NumberingDocumentType.Invoice, 2026, currentSequence);

    [Fact]
    public void UpdateStartNumber_WhenUnlocked_AllowsLoweringStartNumberWithoutBumpingSequence()
    {
        var scheme = CreateUnlockedScheme();

        Assert.True(scheme.UpdateStartNumber(200, allowBelowCurrent: true).IsSuccess);
        Assert.Equal(200, scheme.StartNumber);
        Assert.Equal(0, scheme.CurrentSequence);

        Assert.True(scheme.UpdateStartNumber(150, allowBelowCurrent: true).IsSuccess);
        Assert.Equal(150, scheme.StartNumber);
        Assert.Equal(0, scheme.CurrentSequence);
    }

    [Fact]
    public void UpdateStartNumber_WhenLocked_RejectsStartNumberBelowOrEqualCurrentSequence()
    {
        var scheme = CreateUnlockedScheme(currentSequence: 50);
        scheme.ReconcileCurrentSequence(50);

        var result = scheme.UpdateStartNumber(50, allowBelowCurrent: false);

        Assert.True(result.IsFailure);
        Assert.Contains("50", result.Error.Description);
        Assert.Contains("51", result.Error.Description);
    }

    [Fact]
    public void ReconcileCurrentSequence_UpdatesSequenceAndLocksWhenDocumentsExist()
    {
        var scheme = CreateUnlockedScheme();
        scheme.ReconcileCurrentSequence(25);

        Assert.Equal(25, scheme.CurrentSequence);
        Assert.True(scheme.IsFormatLocked);
    }

    [Fact]
    public void PreviewNextSequence_UsesMaxOfNextCounterAndStartNumber()
    {
        var scheme = CreateUnlockedScheme(currentSequence: 10);
        scheme.UpdateStartNumber(100, allowBelowCurrent: true);

        Assert.Equal(100, scheme.PreviewNextSequence());
    }

    [Fact]
    public void RepairFormatToDefault_RestoresValidFormat_EvenWhenLocked_PreservingSequence()
    {
        var scheme = CreateUnlockedScheme(currentSequence: 50);
        scheme.ReconcileCurrentSequence(50);
        Assert.True(scheme.IsFormatLocked);

        scheme.RepairFormatToDefault();

        var blocks = scheme.GetBlocks();
        Assert.True(NumberingFormatRenderer.ValidateBlocks(blocks).IsSuccess);
        Assert.Contains(blocks, b => b.Type.IsDocumentNumberBlock());
        // Sequence counters untouched → numbering continuity preserved.
        Assert.Equal(50, scheme.CurrentSequence);
    }
}
