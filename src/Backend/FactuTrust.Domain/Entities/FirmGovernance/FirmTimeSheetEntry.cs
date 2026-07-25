using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.FirmGovernance;

public sealed class FirmTimeSheetEntry : Entity
{
    public Guid FirmTenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string UserDisplayName { get; private set; } = null!;
    public Guid? FirmClientAssignmentId { get; private set; }
    public string? ClientCompanyName { get; private set; }
    public DateTime WorkDate { get; private set; }
    public decimal Hours { get; private set; }
    public string? ActivityCode { get; private set; }
    public string? Notes { get; private set; }
    public bool IsBillable { get; private set; } = true;
    public bool IsValidated { get; private set; }

    // ---- Traçabilité de la validation (exigence de revue : qui a validé, et quand) ----

    public DateTime? ValidatedAt { get; private set; }
    public Guid? ValidatedByUserId { get; private set; }
    public string? ValidatedByDisplayName { get; private set; }

    private FirmTimeSheetEntry() { }

    public static Result<FirmTimeSheetEntry> Create(
        Guid firmTenantId,
        Guid userId,
        string userDisplayName,
        DateTime workDate,
        decimal hours,
        Guid? assignmentId = null,
        string? clientCompanyName = null)
    {
        if (firmTenantId == Guid.Empty || userId == Guid.Empty)
            return Result.Failure<FirmTimeSheetEntry>(Error.Validation("Tenant", "Cabinet et utilisateur requis"));
        if (hours <= 0 || hours > 24)
            return Result.Failure<FirmTimeSheetEntry>(Error.Validation("Hours", "Durée invalide"));

        return Result.Success(new FirmTimeSheetEntry
        {
            FirmTenantId = firmTenantId,
            UserId = userId,
            UserDisplayName = userDisplayName.Trim(),
            FirmClientAssignmentId = assignmentId,
            ClientCompanyName = clientCompanyName?.Trim(),
            WorkDate = workDate.Date,
            Hours = MillimeRounding.Round(hours)
        });
    }

    public Result Update(
        DateTime workDate,
        decimal hours,
        Guid? assignmentId,
        string? clientCompanyName,
        string? activityCode,
        string? notes,
        bool isBillable)
    {
        if (IsValidated)
            return Result.Failure(Error.Validation("TimeSheet", "Feuille de temps validée — non modifiable."));
        if (hours <= 0 || hours > 24)
            return Result.Failure(Error.Validation("Hours", "Durée invalide"));

        WorkDate = workDate.Date;
        Hours = MillimeRounding.Round(hours);
        FirmClientAssignmentId = assignmentId;
        ClientCompanyName = clientCompanyName?.Trim();
        ActivityCode = activityCode?.Trim();
        Notes = notes?.Trim();
        IsBillable = isBillable;
        return Result.Success();
    }

    /// <summary>
    /// Valide la saisie et enregistre l'auteur de la validation.
    /// </summary>
    /// <remarks>
    /// Revalider une saisie déjà validée est refusé explicitement : renvoyer un succès silencieux
    /// masquerait une double validation et écraserait la trace du premier valideur.
    /// </remarks>
    public Result Validate(Guid validatedByUserId, string validatedByDisplayName)
    {
        if (IsValidated)
            return Result.Failure(Error.Validation("TimeSheet", "Feuille de temps déjà validée."));
        if (validatedByUserId == Guid.Empty)
            return Result.Failure(Error.Validation("User", "Auteur de la validation requis"));

        IsValidated = true;
        ValidatedAt = DateTime.UtcNow;
        ValidatedByUserId = validatedByUserId;
        ValidatedByDisplayName = string.IsNullOrWhiteSpace(validatedByDisplayName)
            ? "Manager"
            : validatedByDisplayName.Trim();
        return Result.Success();
    }

    /// <summary>
    /// Repasse la saisie en brouillon. Réservé au manager par la couche service.
    /// </summary>
    /// <remarks>
    /// C'est le seul chemin pour corriger ou supprimer une ligne validée : il laisse une trace,
    /// là où une suppression directe effacerait la pièce sans rien conserver.
    /// </remarks>
    public Result Unvalidate()
    {
        if (!IsValidated)
            return Result.Failure(Error.Validation("TimeSheet", "Feuille de temps déjà en brouillon."));

        IsValidated = false;
        ValidatedAt = null;
        ValidatedByUserId = null;
        ValidatedByDisplayName = null;
        return Result.Success();
    }
}
