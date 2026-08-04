using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.FirmGovernance;
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
        Assert.Equal(FirmTimeSheetStatus.Draft, result.Value.Status);
        Assert.Null(result.Value.StartTime);
    }

    [Fact]
    public void Create_with_start_end_derives_hours()
    {
        var result = FirmTimeSheetEntry.Create(
            FirmId, UserId, "Jean Test", new DateTime(2026, 7, 20), hours: 0,
            startTime: TimeSpan.FromHours(9), endTime: TimeSpan.FromHours(12.5));
        Assert.True(result.IsSuccess);
        Assert.Equal(3.5m, result.Value.Hours);
        Assert.Equal(TimeSpan.FromHours(9), result.Value.StartTime);
        Assert.Equal(TimeSpan.FromHours(12.5), result.Value.EndTime);
    }

    [Fact]
    public void Create_rejects_end_before_start()
    {
        var result = FirmTimeSheetEntry.Create(
            FirmId, UserId, "Jean Test", new DateTime(2026, 7, 20), 1m,
            startTime: TimeSpan.FromHours(14), endTime: TimeSpan.FromHours(10));
        Assert.True(result.IsFailure);
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
        entry.Submit();
        entry.Validate(ManagerId, "Chef Test");
        var update = entry.Update(new DateTime(2026, 7, 21), 3m, null, null, "COMPTA", null, true);
        Assert.True(update.IsFailure);
    }

    [Fact]
    public void Update_blocked_after_submit()
    {
        var entry = FirmTimeSheetEntry.Create(FirmId, UserId, "Jean Test", new DateTime(2026, 7, 20), 2m).Value;
        Assert.True(entry.Submit().IsSuccess);
        Assert.True(entry.Update(new DateTime(2026, 7, 21), 3m, null, null, "COMPTA", null, true).IsFailure);
    }

    [Fact]
    public void Submit_then_validate_sets_status_and_is_validated()
    {
        var entry = FirmTimeSheetEntry.Create(FirmId, UserId, "Jean Test", new DateTime(2026, 7, 20), 2m).Value;
        Assert.True(entry.Submit().IsSuccess);
        Assert.Equal(FirmTimeSheetStatus.Submitted, entry.Status);
        Assert.False(entry.IsValidated);

        Assert.True(entry.Validate(ManagerId, "Chef Test").IsSuccess);
        Assert.Equal(FirmTimeSheetStatus.Validated, entry.Status);
        Assert.True(entry.IsValidated);
    }

    [Fact]
    public void Validate_from_draft_is_refused()
    {
        var entry = FirmTimeSheetEntry.Create(FirmId, UserId, "Jean Test", new DateTime(2026, 7, 20), 2m).Value;
        var result = entry.Validate(ManagerId, "Chef Test");
        Assert.True(result.IsFailure);
        Assert.Equal(FirmTimeSheetStatus.Draft, entry.Status);
        Assert.False(entry.IsValidated);
    }

    [Fact]
    public void Validate_records_author_and_timestamp()
    {
        var entry = FirmTimeSheetEntry.Create(FirmId, UserId, "Jean Test", new DateTime(2026, 7, 20), 2m).Value;
        entry.Submit();

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
        var entry = FirmTimeSheetEntry.Create(FirmId, UserId, "Jean Test", new DateTime(2026, 7, 20), 2m).Value;
        entry.Submit();
        entry.Validate(ManagerId, "Chef Test");

        var second = entry.Validate(Guid.NewGuid(), "Autre Chef");

        Assert.True(second.IsFailure);
        Assert.Equal(ManagerId, entry.ValidatedByUserId);
    }

    [Fact]
    public void Unvalidate_clears_the_validation_trace_and_reopens_edition()
    {
        var entry = FirmTimeSheetEntry.Create(FirmId, UserId, "Jean Test", new DateTime(2026, 7, 20), 2m).Value;
        entry.Submit();
        entry.Validate(ManagerId, "Chef Test");

        var unvalidated = entry.Unvalidate();

        Assert.True(unvalidated.IsSuccess);
        Assert.False(entry.IsValidated);
        Assert.Equal(FirmTimeSheetStatus.Draft, entry.Status);
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

    [Fact]
    public void Update_with_slot_and_tags_location()
    {
        var entry = FirmTimeSheetEntry.Create(FirmId, UserId, "Jean Test", new DateTime(2026, 7, 20), 2m).Value;
        var update = entry.Update(
            new DateTime(2026, 7, 20), 0, null, null, "REVUE", "note", true,
            startTime: TimeSpan.FromHours(8),
            endTime: TimeSpan.FromHours(10),
            workLocation: "Remote",
            tags: "Urgent, Client");
        Assert.True(update.IsSuccess);
        Assert.Equal(2m, entry.Hours);
        Assert.Equal("Remote", entry.WorkLocation);
        Assert.Equal("urgent,client", entry.Tags);
    }

    [Fact]
    public void Slots_overlap_detection()
    {
        Assert.True(FirmTimeSheetEntry.SlotsOverlap(
            TimeSpan.FromHours(9), TimeSpan.FromHours(12),
            TimeSpan.FromHours(11), TimeSpan.FromHours(13)));
        Assert.False(FirmTimeSheetEntry.SlotsOverlap(
            TimeSpan.FromHours(9), TimeSpan.FromHours(12),
            TimeSpan.FromHours(12), TimeSpan.FromHours(14)));
    }

    [Fact]
    public void Overlap_validator_reports_anomaly()
    {
        var anomalies = TimeSheetLegalValidator.ValidateSlotOverlap(
            TimeSpan.FromHours(9), TimeSpan.FromHours(11),
            [(TimeSpan.FromHours(10), TimeSpan.FromHours(12))]);
        Assert.Single(anomalies);
        Assert.Equal(TimeSheetAnomalyKind.SlotOverlap, anomalies[0].Kind);
    }

    [Fact]
    public void Stop_timer_materializes_slot()
    {
        var started = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);
        var entry = FirmTimeSheetEntry.CreateTimerDraft(
            FirmId, UserId, "Jean Test", new DateTime(2026, 7, 20), started).Value;
        Assert.NotNull(entry.TimerStartedAtUtc);

        var stop = entry.StopTimer(started.AddHours(2));
        Assert.True(stop.IsSuccess);
        Assert.Null(entry.TimerStartedAtUtc);
        Assert.Equal(2m, entry.Hours);
        Assert.NotNull(entry.StartTime);
        Assert.NotNull(entry.EndTime);
    }
}
