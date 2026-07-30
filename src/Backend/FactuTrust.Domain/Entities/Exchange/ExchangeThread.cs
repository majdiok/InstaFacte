using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Exchange;

/// <summary>
/// Bidirectional exchange workspace between a company and its accounting firm (1:1 with active assignment).
/// </summary>
public sealed class ExchangeThread : AggregateRoot
{
    public const int SubjectMaxLength = 200;

    public Guid FirmClientAssignmentId { get; private set; }
    public Guid FirmTenantId { get; private set; }
    public Guid CompanyTenantId { get; private set; }
    public ExchangeThreadStatus Status { get; private set; }
    public string? Subject { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public Guid? ClosedByUserId { get; private set; }
    public DateTime? LastActivityAt { get; private set; }

    private ExchangeThread() { }

    public static Result<ExchangeThread> Create(
        Guid firmClientAssignmentId,
        Guid firmTenantId,
        Guid companyTenantId,
        string? subject = null)
    {
        if (firmClientAssignmentId == Guid.Empty)
            return Result.Failure<ExchangeThread>(Error.Validation("Assignment", "Affectation invalide"));
        if (firmTenantId == Guid.Empty || companyTenantId == Guid.Empty)
            return Result.Failure<ExchangeThread>(Error.Validation("Tenant", "Identifiants tenant invalides"));

        var now = DateTime.UtcNow;
        return Result.Success(new ExchangeThread
        {
            FirmClientAssignmentId = firmClientAssignmentId,
            FirmTenantId = firmTenantId,
            CompanyTenantId = companyTenantId,
            Status = ExchangeThreadStatus.Open,
            Subject = Truncate(subject?.Trim(), SubjectMaxLength),
            LastActivityAt = now
        });
    }

    public Result Close(Guid closedByUserId)
    {
        if (Status == ExchangeThreadStatus.Closed)
            return Result.Failure(Error.Validation("Status", "L'échange est déjà clos"));
        if (closedByUserId == Guid.Empty)
            return Result.Failure(Error.Validation("User", "Utilisateur invalide"));

        Status = ExchangeThreadStatus.Closed;
        ClosedAt = DateTime.UtcNow;
        ClosedByUserId = closedByUserId;
        TouchActivity();
        IncrementVersion();
        return Result.Success();
    }

    public Result Reopen()
    {
        if (Status != ExchangeThreadStatus.Closed)
            return Result.Failure(Error.Validation("Status", "Seul un échange clos peut être rouvert"));

        Status = ExchangeThreadStatus.Open;
        ClosedAt = null;
        ClosedByUserId = null;
        TouchActivity();
        IncrementVersion();
        return Result.Success();
    }

    public void TouchActivity()
    {
        LastActivityAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
