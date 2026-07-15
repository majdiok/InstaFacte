namespace FactuTrust.Domain.Enums;

/// <summary>When a Studio automation (ERP bridge) fires.</summary>
public enum StudioAutomationTrigger
{
    /// <summary>Runs (best-effort) right after a record is created.</summary>
    OnCreate = 0,

    /// <summary>Runs (best-effort) right after a record is updated.</summary>
    OnUpdate = 1,

    /// <summary>Runs only when explicitly executed by a user on a record.</summary>
    Manual = 2,

    /// <summary>Runs on a schedule (recurring) — reserved for the scheduled phase.</summary>
    Scheduled = 3
}

/// <summary>Outcome of one automation execution against one record.</summary>
public enum StudioAutomationRunStatus
{
    Pending = 0,
    Success = 1,
    Failed = 2,
    Skipped = 3
}
