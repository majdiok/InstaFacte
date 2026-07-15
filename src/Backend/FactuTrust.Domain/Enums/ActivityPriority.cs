namespace FactuTrust.Domain.Enums;

public enum ActivityPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Urgent = 3
}

public static class ActivityPriorityExtensions
{
    public static string ToDisplayString(this ActivityPriority priority) => priority switch
    {
        ActivityPriority.Low => "Basse",
        ActivityPriority.Medium => "Moyenne",
        ActivityPriority.High => "Haute",
        ActivityPriority.Urgent => "Urgente",
        _ => throw new ArgumentOutOfRangeException(nameof(priority))
    };

    public static string ToCssClass(this ActivityPriority priority) => priority switch
    {
        ActivityPriority.Low => "secondary",
        ActivityPriority.Medium => "info",
        ActivityPriority.High => "warning",
        ActivityPriority.Urgent => "danger",
        _ => "secondary"
    };
}
