namespace FactuTrust.Domain.Enums;

public enum RecurringContractStatus
{
    Draft = 0,
    Active = 1,
    Suspended = 2,
    Cancelled = 3,
    Expired = 4
}

public enum RecurringContractLineType
{
    FixedRecurring = 0,
    UsageMetered = 1,
    OneTimeSetup = 2
}

public enum RecurringContractBillingRunStatus
{
    Pending = 0,
    DraftCreated = 1,
    Invoiced = 2,
    Failed = 3,
    Skipped = 4
}

public enum UsageAggregationMode
{
    Sum = 0,
    Max = 1,
    Last = 2
}

public enum UsageRecordSource
{
    Manual = 0,
    Import = 1,
    Api = 2
}

public enum RecurringContractAmendmentType
{
    Upgrade = 0,
    Downgrade = 1,
    AddLine = 2,
    RemoveLine = 3,
    PriceChange = 4,
    Suspend = 5,
    Resume = 6,
    Renewal = 7
}

public enum ProrationPolicy
{
    None = 0,
    DailyProration = 1
}

/// <summary>
/// Statut d'une occurrence d'échéancier prévisionnel (DTO uniquement — jamais persisté).
/// </summary>
public enum RecurringContractScheduleOccurrenceStatus
{
    Upcoming = 0,
    Invoiced = 1,
    DraftGenerated = 2,
    Overdue = 3,
    Failed = 4,
    Skipped = 5
}

public static class RecurringContractStatusExtensions
{
    public static string ToDisplayString(this RecurringContractStatus status) => status switch
    {
        RecurringContractStatus.Draft => "Brouillon",
        RecurringContractStatus.Active => "Actif",
        RecurringContractStatus.Suspended => "Suspendu",
        RecurringContractStatus.Cancelled => "Résilié",
        RecurringContractStatus.Expired => "Expiré",
        _ => status.ToString()
    };

    public static bool CanBeEdited(this RecurringContractStatus status) =>
        status is RecurringContractStatus.Draft;

    public static bool CanBill(this RecurringContractStatus status) =>
        status is RecurringContractStatus.Active;
}

public static class RecurringContractBillingRunStatusExtensions
{
    public static string ToDisplayString(this RecurringContractBillingRunStatus status) => status switch
    {
        RecurringContractBillingRunStatus.Pending => "En attente",
        RecurringContractBillingRunStatus.DraftCreated => "Brouillon créé",
        RecurringContractBillingRunStatus.Invoiced => "Facturé",
        RecurringContractBillingRunStatus.Failed => "Échec",
        RecurringContractBillingRunStatus.Skipped => "Ignoré",
        _ => status.ToString()
    };
}

public static class RecurringContractAmendmentTypeExtensions
{
    public static string ToDisplayString(this RecurringContractAmendmentType type) => type switch
    {
        RecurringContractAmendmentType.Upgrade => "Surclassement",
        RecurringContractAmendmentType.Downgrade => "Réduction",
        RecurringContractAmendmentType.AddLine => "Ajout de ligne",
        RecurringContractAmendmentType.RemoveLine => "Suppression de ligne",
        RecurringContractAmendmentType.PriceChange => "Changement de prix",
        RecurringContractAmendmentType.Suspend => "Suspension",
        RecurringContractAmendmentType.Resume => "Reprise",
        RecurringContractAmendmentType.Renewal => "Renouvellement",
        _ => type.ToString()
    };
}

public static class ProrationPolicyExtensions
{
    public static string ToDisplayString(this ProrationPolicy policy) => policy switch
    {
        ProrationPolicy.None => "Aucun prorata",
        ProrationPolicy.DailyProration => "Prorata journalier",
        _ => policy.ToString()
    };
}

public static class RecurringContractScheduleOccurrenceStatusExtensions
{
    public static string ToDisplayString(this RecurringContractScheduleOccurrenceStatus status) => status switch
    {
        RecurringContractScheduleOccurrenceStatus.Upcoming => "À venir",
        RecurringContractScheduleOccurrenceStatus.Invoiced => "Facturée",
        RecurringContractScheduleOccurrenceStatus.DraftGenerated => "Brouillon",
        RecurringContractScheduleOccurrenceStatus.Overdue => "En retard",
        RecurringContractScheduleOccurrenceStatus.Failed => "Échouée",
        RecurringContractScheduleOccurrenceStatus.Skipped => "Ignorée",
        _ => status.ToString()
    };
}
