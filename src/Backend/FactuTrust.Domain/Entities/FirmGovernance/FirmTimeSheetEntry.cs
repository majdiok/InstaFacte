using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.FirmGovernance;

public sealed class FirmTimeSheetEntry : Entity
{
    public const int MaxTagsLength = 200;

    public Guid FirmTenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string UserDisplayName { get; private set; } = null!;
    public Guid? FirmClientAssignmentId { get; private set; }
    public string? ClientCompanyName { get; private set; }
    public DateTime WorkDate { get; private set; }
    public decimal Hours { get; private set; }
    public TimeSpan? StartTime { get; private set; }
    public TimeSpan? EndTime { get; private set; }
    public string? ActivityCode { get; private set; }
    public string? Notes { get; private set; }
    public bool IsBillable { get; private set; } = true;
    public string? WorkLocation { get; private set; }
    public string? Tags { get; private set; }
    public FirmTimeSheetStatus Status { get; private set; } = FirmTimeSheetStatus.Draft;

    /// <summary>Dérivé de <see cref="Status"/> — conservé pour les lecteurs legacy.</summary>
    public bool IsValidated { get; private set; }

    public DateTime? ValidatedAt { get; private set; }
    public Guid? ValidatedByUserId { get; private set; }
    public string? ValidatedByDisplayName { get; private set; }

    /// <summary>Horodatage UTC du démarrage timer ; null si pas de timer actif.</summary>
    public DateTime? TimerStartedAtUtc { get; private set; }

    private FirmTimeSheetEntry() { }

    public static Result<FirmTimeSheetEntry> Create(
        Guid firmTenantId,
        Guid userId,
        string userDisplayName,
        DateTime workDate,
        decimal hours,
        Guid? assignmentId = null,
        string? clientCompanyName = null,
        TimeSpan? startTime = null,
        TimeSpan? endTime = null)
    {
        if (firmTenantId == Guid.Empty || userId == Guid.Empty)
            return Result.Failure<FirmTimeSheetEntry>(Error.Validation("Tenant", "Cabinet et utilisateur requis"));

        var resolvedHours = ResolveHours(hours, startTime, endTime);
        if (resolvedHours.IsFailure)
            return Result.Failure<FirmTimeSheetEntry>(resolvedHours.Error);

        return Result.Success(new FirmTimeSheetEntry
        {
            FirmTenantId = firmTenantId,
            UserId = userId,
            UserDisplayName = userDisplayName.Trim(),
            FirmClientAssignmentId = assignmentId,
            ClientCompanyName = clientCompanyName?.Trim(),
            WorkDate = workDate.Date,
            Hours = MillimeRounding.Round(resolvedHours.Value),
            StartTime = startTime,
            EndTime = endTime,
            Status = FirmTimeSheetStatus.Draft,
            IsValidated = false
        });
    }

    /// <summary>Crée une ligne brouillon de timer (heures provisoires 0.25 jusqu'au stop).</summary>
    public static Result<FirmTimeSheetEntry> CreateTimerDraft(
        Guid firmTenantId,
        Guid userId,
        string userDisplayName,
        DateTime workDate,
        DateTime timerStartedAtUtc,
        Guid? assignmentId = null,
        string? clientCompanyName = null,
        string? activityCode = null,
        bool isBillable = true)
    {
        if (firmTenantId == Guid.Empty || userId == Guid.Empty)
            return Result.Failure<FirmTimeSheetEntry>(Error.Validation("Tenant", "Cabinet et utilisateur requis"));
        if (timerStartedAtUtc == default)
            return Result.Failure<FirmTimeSheetEntry>(Error.Validation("Timer", "Horodatage timer requis"));

        return Result.Success(new FirmTimeSheetEntry
        {
            FirmTenantId = firmTenantId,
            UserId = userId,
            UserDisplayName = userDisplayName.Trim(),
            FirmClientAssignmentId = assignmentId,
            ClientCompanyName = clientCompanyName?.Trim(),
            WorkDate = workDate.Date,
            Hours = 0.25m,
            ActivityCode = activityCode?.Trim(),
            IsBillable = isBillable,
            Status = FirmTimeSheetStatus.Draft,
            IsValidated = false,
            TimerStartedAtUtc = timerStartedAtUtc
        });
    }

    public Result Update(
        DateTime workDate,
        decimal hours,
        Guid? assignmentId,
        string? clientCompanyName,
        string? activityCode,
        string? notes,
        bool isBillable,
        TimeSpan? startTime = null,
        TimeSpan? endTime = null,
        string? workLocation = null,
        string? tags = null)
    {
        if (!CanEdit())
            return Result.Failure(Error.Validation("TimeSheet", "Feuille de temps non modifiable (soumise ou validée)."));

        var resolvedHours = ResolveHours(hours, startTime, endTime);
        if (resolvedHours.IsFailure)
            return Result.Failure(resolvedHours.Error);

        var locationResult = NormalizeWorkLocation(workLocation);
        if (locationResult.IsFailure)
            return Result.Failure(locationResult.Error);

        var tagsResult = NormalizeTags(tags);
        if (tagsResult.IsFailure)
            return Result.Failure(tagsResult.Error);

        WorkDate = workDate.Date;
        Hours = MillimeRounding.Round(resolvedHours.Value);
        StartTime = startTime;
        EndTime = endTime;
        FirmClientAssignmentId = assignmentId;
        ClientCompanyName = clientCompanyName?.Trim();
        ActivityCode = activityCode?.Trim();
        Notes = notes?.Trim();
        IsBillable = isBillable;
        WorkLocation = locationResult.Value;
        Tags = tagsResult.Value;
        return Result.Success();
    }

    public Result Submit()
    {
        if (Status == FirmTimeSheetStatus.Validated)
            return Result.Failure(Error.Validation("TimeSheet", "Feuille de temps déjà validée."));
        if (Status == FirmTimeSheetStatus.Submitted)
            return Result.Failure(Error.Validation("TimeSheet", "Feuille de temps déjà soumise."));
        if (TimerStartedAtUtc.HasValue)
            return Result.Failure(Error.Validation("TimeSheet", "Arrêtez le timer avant de soumettre."));

        Status = FirmTimeSheetStatus.Submitted;
        SyncValidatedFlag();
        return Result.Success();
    }

    public Result Validate(Guid validatedByUserId, string validatedByDisplayName)
    {
        if (Status == FirmTimeSheetStatus.Validated)
            return Result.Failure(Error.Validation("TimeSheet", "Feuille de temps déjà validée."));
        if (validatedByUserId == Guid.Empty)
            return Result.Failure(Error.Validation("User", "Auteur de la validation requis"));
        if (TimerStartedAtUtc.HasValue)
            return Result.Failure(Error.Validation("TimeSheet", "Arrêtez le timer avant de valider."));

        Status = FirmTimeSheetStatus.Validated;
        SyncValidatedFlag();
        ValidatedAt = DateTime.UtcNow;
        ValidatedByUserId = validatedByUserId;
        ValidatedByDisplayName = string.IsNullOrWhiteSpace(validatedByDisplayName)
            ? "Manager"
            : validatedByDisplayName.Trim();
        return Result.Success();
    }

    public Result Unvalidate()
    {
        if (Status != FirmTimeSheetStatus.Validated)
            return Result.Failure(Error.Validation("TimeSheet", "Feuille de temps déjà en brouillon."));

        Status = FirmTimeSheetStatus.Draft;
        SyncValidatedFlag();
        ValidatedAt = null;
        ValidatedByUserId = null;
        ValidatedByDisplayName = null;
        return Result.Success();
    }

    public Result StopTimer(DateTime stoppedAtUtc)
    {
        if (!TimerStartedAtUtc.HasValue)
            return Result.Failure(Error.Validation("Timer", "Aucun timer actif sur cette saisie."));

        var startLocal = TimerStartedAtUtc.Value;
        var elapsed = stoppedAtUtc - startLocal;
        if (elapsed <= TimeSpan.Zero)
            return Result.Failure(Error.Validation("Timer", "Durée timer invalide."));

        // Cap à 24h ; créneau du jour sans nuitée.
        var startOfDay = TimeSpan.FromHours(Math.Clamp(startLocal.Hour + startLocal.Minute / 60.0 + startLocal.Second / 3600.0, 0, 23.999));
        var hours = Math.Min((decimal)elapsed.TotalHours, 24m);
        if (hours < 0.25m)
            hours = 0.25m;

        var end = startOfDay + TimeSpan.FromHours((double)hours);
        if (end > TimeSpan.FromHours(24))
        {
            end = TimeSpan.FromHours(24) - TimeSpan.FromMinutes(1);
            hours = (decimal)(end - startOfDay).TotalHours;
            if (hours <= 0)
                hours = 0.25m;
        }

        StartTime = RoundToMinute(startOfDay);
        EndTime = RoundToMinute(end);
        Hours = MillimeRounding.Round(hours);
        TimerStartedAtUtc = null;
        return Result.Success();
    }

    public bool CanEdit() => Status == FirmTimeSheetStatus.Draft && !IsValidated;

    public bool HasTimeSlot => StartTime.HasValue && EndTime.HasValue;

    public static Result<decimal> ResolveHours(decimal hours, TimeSpan? startTime, TimeSpan? endTime)
    {
        if (startTime.HasValue || endTime.HasValue)
        {
            if (!startTime.HasValue || !endTime.HasValue)
                return Result.Failure<decimal>(Error.Validation("TimeSlot", "Début et fin de créneau requis ensemble."));
            if (endTime.Value <= startTime.Value)
                return Result.Failure<decimal>(Error.Validation("TimeSlot", "L'heure de fin doit être postérieure au début (pas de nuitée)."));
            var derived = (decimal)(endTime.Value - startTime.Value).TotalHours;
            if (derived <= 0 || derived > 24)
                return Result.Failure<decimal>(Error.Validation("Hours", "Durée invalide"));
            return Result.Success(derived);
        }

        if (hours <= 0 || hours > 24)
            return Result.Failure<decimal>(Error.Validation("Hours", "Durée invalide"));
        return Result.Success(hours);
    }

    public static Result<string?> NormalizeWorkLocation(string? workLocation)
    {
        if (string.IsNullOrWhiteSpace(workLocation))
            return Result.Success<string?>(null);

        var raw = workLocation.Trim();
        if (Enum.TryParse<FirmWorkLocation>(raw, ignoreCase: true, out var parsed))
            return Result.Success<string?>(parsed.ToString());

        return Result.Failure<string?>(Error.Validation("WorkLocation", "Lieu de travail invalide."));
    }

    public static Result<string?> NormalizeTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
            return Result.Success<string?>(null);

        var parts = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.ToLowerInvariant())
            .Where(p => p.Length > 0)
            .Distinct()
            .ToList();
        var normalized = string.Join(',', parts);
        if (normalized.Length > MaxTagsLength)
            return Result.Failure<string?>(Error.Validation("Tags", $"Tags trop longs (max {MaxTagsLength})."));
        return Result.Success<string?>(normalized);
    }

    /// <summary>True si deux créneaux se chevauchent (bornes ouvertes à droite).</summary>
    public static bool SlotsOverlap(TimeSpan aStart, TimeSpan aEnd, TimeSpan bStart, TimeSpan bEnd)
        => aStart < bEnd && bStart < aEnd;

    private void SyncValidatedFlag() => IsValidated = Status == FirmTimeSheetStatus.Validated;

    private static TimeSpan RoundToMinute(TimeSpan value)
        => TimeSpan.FromMinutes(Math.Round(value.TotalMinutes, MidpointRounding.AwayFromZero));
}
