using FactuTrust.Domain.Entities.RecurringContracts;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Tests.Services.RecurringContracts;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Tests de <see cref="RecurringContract.AmendLines"/> (T12/D14) : avenants de lignes sur
/// contrat actif par fenêtres d'effet, à effet immédiat (effectiveDate == date du jour).
/// </summary>
public sealed class RecurringContractAmendLinesTests
{
    private static readonly DateTime Start = new(2026, 1, 1);
    private static readonly DateTime Today = new(2026, 8, 26);

    private static RecurringContract NewActiveContract(Action<RecurringContract>? extraLines = null)
    {
        var contract = RecurringContract.CreateDraft(
            Guid.NewGuid(), BillingFrequency.Monthly, 1, Start).Value;
        contract.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m,
            RecurringContractTestHarness.DefaultProductId);
        // Lignes supplémentaires ajoutées AVANT activation (AddLine exige le statut Draft).
        extraLines?.Invoke(contract);
        Assert.True(contract.Activate().IsSuccess);
        return contract;
    }

    private static RecurringContractAmendLineTarget Target(
        RecurringContract contract, RecurringContractLine? source,
        decimal quantity, decimal unitPrice, string description = "Abonnement",
        RecurringContractLineType type = RecurringContractLineType.FixedRecurring,
        DateTime? effectiveFrom = null)
    {
        var line = RecurringContractLine.Create(
            contract.Id, type, description, quantity, unitPrice, 19m,
            effectiveFrom ?? Today, RecurringContractTestHarness.DefaultProductId).Value;
        return new RecurringContractAmendLineTarget(source?.Id, line);
    }

    [Fact]
    public void AmendLines_PriceChange_ClosesOldLineAtEffectiveDateMinus1_AndOpensNew()
    {
        var contract = NewActiveContract();
        var existing = contract.Lines.Single();

        var result = contract.AmendLines(Today, new[] { Target(contract, existing, 1, 120m) }, Today);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.False(existing.IsActive);
        Assert.Equal(Today.AddDays(-1), existing.EffectiveTo);

        var newLine = contract.Lines.Single(l => l.IsActive);
        Assert.NotEqual(existing.Id, newLine.Id);
        Assert.Equal(120m, newLine.UnitPriceHT);
        Assert.Equal(Today, newLine.EffectiveFrom);
        Assert.Null(newLine.EffectiveTo);
    }

    [Fact]
    public void AmendLines_RemoveLine_DeactivatesOnly()
    {
        var contract = NewActiveContract(
            c => c.AddLine(RecurringContractLineType.FixedRecurring, "Option", 2, 50m, 19m,
                RecurringContractTestHarness.DefaultProductId));
        var keep = contract.Lines.First(l => l.Description == "Abonnement");
        var removed = contract.Lines.First(l => l.Description == "Option");

        // Cible = ligne conservée, inchangée → l'autre est clôturée, aucune recréée.
        var result = contract.AmendLines(Today, new[] { Target(contract, keep, 1, 100m) }, Today);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(2, contract.Lines.Count); // aucune nouvelle ligne
        Assert.True(keep.IsActive);
        Assert.Null(keep.EffectiveTo);
        Assert.False(removed.IsActive);
        Assert.Equal(Today.AddDays(-1), removed.EffectiveTo);
    }

    [Fact]
    public void AmendLines_AddLine_CreatesFromEffectiveDate()
    {
        var contract = NewActiveContract();
        var existing = contract.Lines.Single();

        var result = contract.AmendLines(Today, new[]
        {
            Target(contract, existing, 1, 100m),
            Target(contract, null, 5, 30m, description: "Support premium")
        }, Today);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(2, contract.Lines.Count);
        var added = contract.Lines.Single(l => l.Description == "Support premium");
        Assert.True(added.IsActive);
        Assert.Equal(Today, added.EffectiveFrom);
        Assert.True(existing.IsActive);
    }

    [Fact]
    public void AmendLines_UnchangedLines_Untouched()
    {
        var contract = NewActiveContract();
        var existing = contract.Lines.Single();

        // Cible SANS SourceLineId : correspondance de repli par (LineType, Description).
        var result = contract.AmendLines(Today, new[] { Target(contract, null, 1, 100m) }, Today);

        Assert.True(result.IsSuccess, result.Error?.Description);
        var line = contract.Lines.Single();
        Assert.Equal(existing.Id, line.Id);
        Assert.True(line.IsActive);
        Assert.Null(line.EffectiveTo);
    }

    [Fact]
    public void AmendLines_PastEffectiveDate_Fails()
    {
        var contract = NewActiveContract();
        var existing = contract.Lines.Single();

        var result = contract.AmendLines(
            Today.AddDays(-1), new[] { Target(contract, existing, 1, 120m) }, Today);

        Assert.True(result.IsFailure);
        Assert.True(existing.IsActive); // aucune mutation
    }

    [Fact]
    public void AmendLines_FutureEffectiveDate_Fails()
    {
        // Verrouille la restriction phase 1 « effet immédiat » (D14) : une date d'effet future
        // ouvrirait un trou de facturation (Deactivate bascule IsActive immédiatement).
        var contract = NewActiveContract();
        var existing = contract.Lines.Single();

        var result = contract.AmendLines(
            Today.AddDays(1), new[] { Target(contract, existing, 1, 120m) }, Today);

        Assert.True(result.IsFailure);
        Assert.Contains("date du jour", result.Error.Description);
        Assert.True(existing.IsActive);
    }

    [Theory]
    [InlineData(RecurringContractStatus.Draft)]
    [InlineData(RecurringContractStatus.Suspended)]
    [InlineData(RecurringContractStatus.Cancelled)]
    [InlineData(RecurringContractStatus.Expired)]
    public void AmendLines_NotActive_Fails(RecurringContractStatus status)
    {
        var contract = RecurringContract.CreateDraft(
            Guid.NewGuid(), BillingFrequency.Monthly, 1, Start).Value;
        contract.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m,
            RecurringContractTestHarness.DefaultProductId);
        switch (status)
        {
            case RecurringContractStatus.Draft: break;
            case RecurringContractStatus.Suspended:
                contract.Activate(); contract.Suspend(); break;
            case RecurringContractStatus.Cancelled:
                contract.Activate(); contract.Cancel(); break;
            case RecurringContractStatus.Expired:
                contract.Activate(); contract.MarkExpired(); break;
        }

        var result = contract.AmendLines(Today, new[]
        {
            Target(contract, null, 1, 100m)
        }, Today);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void AmendLines_ResultingInNoActiveLine_Fails()
    {
        var contract = NewActiveContract();
        var existing = contract.Lines.Single();

        // Cible vide → toutes les lignes actives seraient clôturées → refus avant mutation.
        var result = contract.AmendLines(
            Today, Array.Empty<RecurringContractAmendLineTarget>(), Today);

        Assert.True(result.IsFailure);
        Assert.Contains("sans ligne active", result.Error.Description);
        Assert.True(existing.IsActive);
        Assert.Null(existing.EffectiveTo);
    }
}
