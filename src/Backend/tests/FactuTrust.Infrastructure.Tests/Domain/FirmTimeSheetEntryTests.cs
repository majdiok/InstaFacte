using FactuTrust.Domain.Entities.FirmGovernance;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class FirmTimeSheetEntryTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ManagerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Create_valid_entry_succeeds()
    {
        var result = FirmTimeSheetEntry.Create(FirmId, UserId, "Jean Test", new DateTime(2026, 7, 20), 4.5m);
        Assert.True(result.IsSuccess);
        Assert.Equal(4.5m, result.Value.Hours);
        Assert.False(result.Value.IsValidated);
    }

    [Fact]
    public void Create_rejects_invalid_hours()
    {
        var result = FirmTimeSheetEntry.Create(FirmId, UserId, "Jean Test", DateTime.UtcNow, 0);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Update_blocked_after_validate()
    {
        var entry = FirmTimeSheetEntry.Create(FirmId, UserId, "Jean Test", new DateTime(2026, 7, 20), 2m).Value;
        entry.Validate(ManagerId, "Chef Test");
        var update = entry.Update(new DateTime(2026, 7, 21), 3m, null, null, "COMPTA", null, true);
        Assert.True(update.IsFailure);
    }

    [Fact]
    public void Validate_records_author_and_timestamp()
    {
        var entry = FirmTimeSheetEntry.Create(FirmId, UserId, "Jean Test", new DateTime(2026, 7, 20), 2m).Value;

        var result = entry.Validate(ManagerId, "Chef Test");

        Assert.True(result.IsSuccess);
        Assert.True(entry.IsValidated);
        Assert.Equal(ManagerId, entry.ValidatedByUserId);
        Assert.Equal("Chef Test", entry.ValidatedByDisplayName);
        Assert.NotNull(entry.ValidatedAt);
    }

    [Fact]
    public void Validate_twice_is_refused()
    {
        // Un succès silencieux masquerait une double validation et écraserait la trace du premier valideur.
        var entry = FirmTimeSheetEntry.Create(FirmId, UserId, "Jean Test", new DateTime(2026, 7, 20), 2m).Value;
        entry.Validate(ManagerId, "Chef Test");

        var second = entry.Validate(Guid.NewGuid(), "Autre Chef");

        Assert.True(second.IsFailure);
        Assert.Equal(ManagerId, entry.ValidatedByUserId);
    }

    [Fact]
    public void Unvalidate_clears_the_validation_trace_and_reopens_edition()
    {
        var entry = FirmTimeSheetEntry.Create(FirmId, UserId, "Jean Test", new DateTime(2026, 7, 20), 2m).Value;
        entry.Validate(ManagerId, "Chef Test");

        var unvalidated = entry.Unvalidate();

        Assert.True(unvalidated.IsSuccess);
        Assert.False(entry.IsValidated);
        Assert.Null(entry.ValidatedAt);
        Assert.Null(entry.ValidatedByUserId);
        Assert.Null(entry.ValidatedByDisplayName);
        Assert.True(entry.Update(new DateTime(2026, 7, 21), 3m, null, null, "COMPTA", null, true).IsSuccess);
    }

    [Fact]
    public void Unvalidate_on_a_draft_is_refused()
    {
        var entry = FirmTimeSheetEntry.Create(FirmId, UserId, "Jean Test", new DateTime(2026, 7, 20), 2m).Value;
        Assert.True(entry.Unvalidate().IsFailure);
    }

    [Fact]
    public void Update_succeeds_when_not_validated()
    {
        var entry = FirmTimeSheetEntry.Create(FirmId, UserId, "Jean Test", new DateTime(2026, 7, 20), 2m).Value;
        var update = entry.Update(new DateTime(2026, 7, 21), 3.25m, null, null, "REVUE", "note", false);
        Assert.True(update.IsSuccess);
        Assert.Equal(3.25m, entry.Hours);
        Assert.Equal("REVUE", entry.ActivityCode);
        Assert.False(entry.IsBillable);
    }
}
