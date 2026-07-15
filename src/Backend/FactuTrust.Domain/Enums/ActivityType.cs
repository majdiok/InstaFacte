namespace FactuTrust.Domain.Enums;

public enum ActivityType
{
    Call = 0,
    Email = 1,
    Meeting = 2,
    Task = 3,
    Note = 4
}

public static class ActivityTypeExtensions
{
    public static string ToDisplayString(this ActivityType type) => type switch
    {
        ActivityType.Call => "Appel",
        ActivityType.Email => "Email",
        ActivityType.Meeting => "Rendez-vous",
        ActivityType.Task => "Tâche",
        ActivityType.Note => "Note",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static string ToIcon(this ActivityType type) => type switch
    {
        ActivityType.Call => "pi-phone",
        ActivityType.Email => "pi-envelope",
        ActivityType.Meeting => "pi-calendar",
        ActivityType.Task => "pi-check-square",
        ActivityType.Note => "pi-file-edit",
        _ => "pi-circle"
    };
}
