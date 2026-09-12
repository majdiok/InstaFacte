namespace FactuTrust.Domain.Enums;

public enum ProjectKind
{
    Generic = 0,
    Esn = 1,
    Btp = 2
}

public enum ProjectBillingMode
{
    None = 0,
    TimeAndMaterials = 1,
    FixedPrice = 2,
    Milestone = 3,
    ProgressSituations = 4
}

public enum ProjectStatus
{
    Draft = 0,
    Active = 1,
    OnHold = 2,
    Completed = 3,
    Cancelled = 4
}

public enum ProjectMemberRole
{
    Viewer = 0,
    Member = 1,
    Manager = 2
}

public enum ProjectTaskPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

public enum ProjectTaskStatus
{
    Todo = 0,
    InProgress = 1,
    Waiting = 2,
    Done = 3,
    Cancelled = 4
}

public enum ProjectTimeEntryStatus
{
    Draft = 0,
    Submitted = 1,
    Validated = 2
}

public enum ProjectCostSource
{
    Manual = 0,
    Time = 1,
    StockExit = 2,
    Purchase = 3,
    Expense = 4
}

public enum ProjectSituationStatus
{
    Draft = 0,
    Validated = 1
}

public enum ProjectBillingKind
{
    TimeAndMaterials = 0,
    Milestone = 1,
    Situation = 2,
    FixedPrice = 3,
    TaskFixed = 4,
    TaskHourly = 5
}

public enum ProjectTaskBillingMethod
{
    Fixed = 1,
    Hourly = 2
}

public static class ProjectEnumExtensions
{
    public static string ToDisplayString(this ProjectKind kind) => kind switch
    {
        ProjectKind.Generic => "Général",
        ProjectKind.Esn => "ESN / Services",
        ProjectKind.Btp => "BTP / Chantier",
        _ => kind.ToString()
    };

    public static string ToDisplayString(this ProjectBillingMode mode) => mode switch
    {
        ProjectBillingMode.None => "Aucune",
        ProjectBillingMode.TimeAndMaterials => "Régie (temps)",
        ProjectBillingMode.FixedPrice => "Forfait",
        ProjectBillingMode.Milestone => "Jalons",
        ProjectBillingMode.ProgressSituations => "Situations de travaux",
        _ => mode.ToString()
    };

    public static string ToDisplayString(this ProjectStatus status) => status switch
    {
        ProjectStatus.Draft => "Brouillon",
        ProjectStatus.Active => "Actif",
        ProjectStatus.OnHold => "En pause",
        ProjectStatus.Completed => "Terminé",
        ProjectStatus.Cancelled => "Annulé",
        _ => status.ToString()
    };

    public static string ToDisplayString(this ProjectMemberRole role) => role switch
    {
        ProjectMemberRole.Viewer => "Lecteur",
        ProjectMemberRole.Member => "Membre",
        ProjectMemberRole.Manager => "Responsable",
        _ => role.ToString()
    };

    public static string ToDisplayString(this ProjectTaskPriority priority) => priority switch
    {
        ProjectTaskPriority.Low => "Basse",
        ProjectTaskPriority.Normal => "Normale",
        ProjectTaskPriority.High => "Haute",
        ProjectTaskPriority.Urgent => "Urgente",
        _ => priority.ToString()
    };

    public static string ToDisplayString(this ProjectTaskStatus status) => status switch
    {
        ProjectTaskStatus.Todo => "À faire",
        ProjectTaskStatus.InProgress => "En cours",
        ProjectTaskStatus.Waiting => "En attente",
        ProjectTaskStatus.Done => "Terminé",
        ProjectTaskStatus.Cancelled => "Annulé",
        _ => status.ToString()
    };

    public static string ToDisplayString(this ProjectTimeEntryStatus status) => status switch
    {
        ProjectTimeEntryStatus.Draft => "Brouillon",
        ProjectTimeEntryStatus.Submitted => "Soumis",
        ProjectTimeEntryStatus.Validated => "Validé",
        _ => status.ToString()
    };

    public static string ToDisplayString(this ProjectTimeEntryStatus status, bool isInvoiced) =>
        isInvoiced ? "Facturé" : status.ToDisplayString();

    public static string ToDisplayString(this ProjectCostSource source) => source switch
    {
        ProjectCostSource.Manual => "Manuel",
        ProjectCostSource.Time => "Temps",
        ProjectCostSource.StockExit => "Sortie stock",
        ProjectCostSource.Purchase => "Achat",
        ProjectCostSource.Expense => "Dépense",
        _ => source.ToString()
    };

    public static bool CanBeEdited(this ProjectStatus status) =>
        status is ProjectStatus.Draft or ProjectStatus.Active or ProjectStatus.OnHold;

    public static bool CanReceiveTime(this ProjectStatus status) =>
        status is ProjectStatus.Active;

    public static bool CanProcessExistingTime(this ProjectStatus status) =>
        status is ProjectStatus.Active or ProjectStatus.OnHold or ProjectStatus.Completed;

    public static bool CanBeBilled(this ProjectStatus status) =>
        status is ProjectStatus.Active or ProjectStatus.Completed;

    public static string CannotReceiveTimeMessage(this ProjectStatus status) => status switch
    {
        ProjectStatus.Draft => "Activez le projet pour saisir du temps. La saisie est interdite en statut Brouillon.",
        ProjectStatus.OnHold => "La saisie de temps est interdite pour un projet en pause.",
        ProjectStatus.Completed => "Aucune nouvelle saisie de temps n'est autorisée sur un projet terminé.",
        ProjectStatus.Cancelled => "Aucune nouvelle saisie de temps n'est autorisée sur un projet annulé.",
        _ => "Activez le projet pour saisir du temps. La saisie est réservée aux projets Actif."
    };

    public const string TimesheetsDisabledMessage =
        "La saisie des temps est désactivée pour ce projet.";

    public static string CannotProcessExistingTimeMessage(this ProjectStatus status) => status switch
    {
        ProjectStatus.Draft => "Activez le projet pour traiter les temps. Le traitement est interdit en statut Brouillon.",
        ProjectStatus.Cancelled => "Le traitement des temps est interdit pour un projet annulé.",
        _ => "Ce projet ne permet pas le traitement des temps dans son statut actuel."
    };

    public static string CannotBeBilledMessage(this ProjectStatus status) =>
        status is ProjectStatus.Draft
            ? "Activez le projet (statut Brouillon) avant de facturer. La facturation est réservée aux projets Actif ou Terminé."
            : "Ce projet ne peut pas être facturé dans son statut actuel.";
}
