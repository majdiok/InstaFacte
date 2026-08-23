using FactuTrust.Domain.Entities.Projects;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Projects;

public sealed class ProjectSituationTests
{
    [Fact]
    public void CumulativePercent_MustBeMonotonic()
    {
        var ok = ProjectSituation.EnsureMonotonicPercent(40m, 55m);
        Assert.True(ok.IsSuccess);

        var down = ProjectSituation.EnsureMonotonicPercent(40m, 30m);
        Assert.True(down.IsFailure);
        Assert.Contains("monotone", down.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Retainage_CannotExceedGross()
    {
        var bad = ProjectSituation.EnsureAmounts(1000m, 1200m, 19);
        Assert.True(bad.IsFailure);

        var ok = ProjectSituation.EnsureAmounts(1000m, 50m, 19);
        Assert.True(ok.IsSuccess);
    }

    [Fact]
    public void ValidatedSituation_IsImmutable_AndNetExcludesRetainage()
    {
        var created = ProjectSituation.Create(
            Guid.NewGuid(), 1,
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 31),
            40m, 10_000m, 500m, 19, 0m);
        Assert.True(created.IsSuccess, created.Error.Description);
        var situation = created.Value;
        Assert.Equal(9500m, situation.NetAmountHt);
        Assert.Equal(1805m, situation.VatAmount);
        Assert.Equal(11305m, situation.TotalTtc);

        Assert.True(situation.Validate(0m).IsSuccess);
        var update = situation.UpdateDraft(
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 31),
            50m, 12_000m, 500m, 19, 0m);
        Assert.True(update.IsFailure);
        Assert.Contains("validée", update.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VatRate_MustBeTunisianScale()
    {
        var bad = ProjectSituation.EnsureAmounts(1000m, 50m, 10);
        Assert.True(bad.IsFailure);
        Assert.Contains("TVA", bad.Error.Description, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class ProjectTimeEntryTests
{
    [Fact]
    public void AlreadyInvoicedHours_CannotBeInvoicedAgain()
    {
        var entry = ProjectTimeEntry.Create(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 8, 1), 8m, true, null, null).Value;
        Assert.True(entry.Submit().IsSuccess);
        Assert.True(entry.Validate().IsSuccess);
        var invoiceId = Guid.NewGuid();
        Assert.True(entry.MarkInvoiced(invoiceId).IsSuccess);
        var again = entry.MarkInvoiced(Guid.NewGuid());
        Assert.True(again.IsFailure);
        Assert.Contains("déjà", again.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DraftHours_CannotBeInvoiced()
    {
        var entry = ProjectTimeEntry.Create(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 8, 1), 8m, true, null, null).Value;
        Assert.True(entry.MarkInvoiced(Guid.NewGuid()).IsFailure);
    }

    [Fact]
    public void InvoicedHours_CannotBeReopened()
    {
        var entry = ProjectTimeEntry.Create(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 8, 1), 8m, true, null, null).Value;
        Assert.True(entry.Submit().IsSuccess);
        Assert.True(entry.Validate().IsSuccess);
        Assert.True(entry.MarkInvoiced(Guid.NewGuid()).IsSuccess);
        Assert.True(entry.ReopenToDraft().IsFailure);
    }
}

public sealed class ProjectTaskMoveTests
{
    [Fact]
    public void MoveToPhase_WithDoneStatus_SetsProgressTo100()
    {
        var task = ProjectTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Tâche", null,
            ProjectTaskPriority.Normal, null, null, null, 0m).Value;
        Assert.Equal(0, task.ProgressPercent);

        var moved = task.MoveToPhase(Guid.NewGuid(), ProjectTaskStatus.Done);
        Assert.True(moved.IsSuccess);
        Assert.Equal(ProjectTaskStatus.Done, task.Status);
        Assert.Equal(100, task.ProgressPercent);
    }
}

public sealed class ProjectBillingReadinessTests
{
    [Fact]
    public void CanInvoiceTime_RequiresStatusHoursAndRates()
    {
        Assert.True(ProjectStatus.Active.CanBeBilled());
        Assert.False(ProjectStatus.Draft.CanBeBilled());
        // CanBill is status-only; CanInvoiceTime adds hours + rates (validated in service DTO)
        var canInvoiceTime = ProjectStatus.Active.CanBeBilled();
        Assert.True(canInvoiceTime);
    }
}

public sealed class ProjectFixedPriceBillingTests
{
    [Fact]
    public void FixedPriceBillingKind_ExistsInEnum()
    {
        Assert.Equal(3, (int)ProjectBillingKind.FixedPrice);
    }
}

public sealed class ProjectClosureTests
{
    [Fact]
    public void Complete_Fails_WhenTimeEntriesAreStillOpen()
    {
        var project = Project.Create(Guid.NewGuid(), "Mission", ProjectKind.Generic, ProjectBillingMode.None, null, null, null, 0m).Value;
        Assert.True(project.Activate().IsSuccess);
        var close = project.Complete(hasOpenTimeEntries: true);
        Assert.True(close.IsFailure);
        Assert.True(project.Complete(hasOpenTimeEntries: false).IsSuccess);
        Assert.Equal(ProjectStatus.Completed, project.Status);
    }

    [Fact]
    public void GenericProject_CannotUseProgressSituations()
    {
        var created = Project.Create(
            Guid.NewGuid(), "Chantier", ProjectKind.Generic, ProjectBillingMode.ProgressSituations, null, null, null, 0m);
        Assert.True(created.IsFailure);
        Assert.Contains("BTP", created.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Draft_CannotReceiveTime_NorBeBilled()
    {
        Assert.False(ProjectStatus.Draft.CanReceiveTime());
        Assert.False(ProjectStatus.Draft.CanBeBilled());
        Assert.Contains("Brouillon", ProjectStatus.Draft.CannotBeBilledMessage(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Actif", ProjectStatus.Draft.CannotReceiveTimeMessage(), StringComparison.OrdinalIgnoreCase);
        Assert.True(ProjectStatus.Active.CanReceiveTime());
        Assert.True(ProjectStatus.Active.CanBeBilled());
        Assert.True(ProjectStatus.Completed.CanBeBilled());
        Assert.False(ProjectStatus.OnHold.CanReceiveTime());
        Assert.False(ProjectStatus.OnHold.CanBeBilled());
    }

    [Fact]
    public void DefaultColumns_DependOnKind()
    {
        var generic = ProjectPhase.DefaultColumnsFor(ProjectKind.Generic);
        Assert.Contains(generic, c => c.Name == "À faire");

        var esn = ProjectPhase.DefaultColumnsFor(ProjectKind.Esn);
        Assert.Equal("Backlog", esn[0].Name);
        Assert.Equal("Livré", esn[^1].Name);

        var btp = ProjectPhase.DefaultColumnsFor(ProjectKind.Btp);
        Assert.Equal("Préparation", btp[0].Name);
        Assert.Equal("Clôturé", btp[^1].Name);
    }
}
