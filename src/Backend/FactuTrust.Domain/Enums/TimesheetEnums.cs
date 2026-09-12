namespace FactuTrust.Domain.Enums;

/// <summary>Odoo-equivalent invoicing policy on service products.</summary>
public enum ServiceInvoicingPolicy
{
    PrepaidFixedPrice = 0,
    BasedOnTimesheets = 1,
    BasedOnMilestones = 2,
    BasedOnDeliveredQuantities = 3,
    ProgressSituations = 4
}

/// <summary>What to create when a service product is sold (Odoo Create on Order).</summary>
public enum ServiceCreateOnOrder
{
    Nothing = 0,
    Project = 1,
    ProjectAndTask = 2,
    Task = 3
}

public enum TimesheetEncodingMethod
{
    Hours = 0,
    Days = 1
}

public enum TimesheetAccessLevel
{
    None = 0,
    OwnOnly = 1,
    AllTimesheets = 2,
    Administrator = 3
}

public enum TimesheetEntrySource
{
    Manual = 0,
    Timer = 1,
    Task = 2,
    TimeOff = 3
}

public enum ProjectUpdateStatus
{
    OnTrack = 0,
    AtRisk = 1,
    OffTrack = 2,
    OnHold = 3,
    Done = 4
}

public enum TimeOffRequestStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Refused = 3
}

public static class TimesheetEnumExtensions
{
    public static string ToDisplayString(this ServiceInvoicingPolicy policy) => policy switch
    {
        ServiceInvoicingPolicy.PrepaidFixedPrice => "Forfait / prépayé",
        ServiceInvoicingPolicy.BasedOnTimesheets => "Basé sur les feuilles de temps",
        ServiceInvoicingPolicy.BasedOnMilestones => "Basé sur les jalons",
        ServiceInvoicingPolicy.BasedOnDeliveredQuantities => "Basé sur les quantités livrées",
        ServiceInvoicingPolicy.ProgressSituations => "Situations de travaux (BTP)",
        _ => policy.ToString()
    };

    public static string ToDisplayString(this ProjectUpdateStatus status) => status switch
    {
        ProjectUpdateStatus.OnTrack => "Dans les temps",
        ProjectUpdateStatus.AtRisk => "À risque",
        ProjectUpdateStatus.OffTrack => "En retard",
        ProjectUpdateStatus.OnHold => "En pause",
        ProjectUpdateStatus.Done => "Terminé",
        _ => status.ToString()
    };

    public static ProjectBillingMode ToProjectBillingMode(this ServiceInvoicingPolicy policy) => policy switch
    {
        ServiceInvoicingPolicy.PrepaidFixedPrice => ProjectBillingMode.FixedPrice,
        ServiceInvoicingPolicy.BasedOnTimesheets => ProjectBillingMode.TimeAndMaterials,
        ServiceInvoicingPolicy.BasedOnMilestones => ProjectBillingMode.Milestone,
        ServiceInvoicingPolicy.ProgressSituations => ProjectBillingMode.ProgressSituations,
        _ => ProjectBillingMode.TimeAndMaterials
    };
}
