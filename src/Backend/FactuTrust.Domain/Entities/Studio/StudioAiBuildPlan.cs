using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Studio;

/// <summary>
/// Un plan de construction proposé par l'assistant Studio IA et soumis à validation utilisateur
/// (flux plan → aperçu → confirmation). <see cref="SpecJson"/> conserve la spécification brute émise
/// par le modèle ; <see cref="SummaryJson"/> l'aperçu structuré affiché au client. L'exécution est
/// déclenchée par un endpoint REST déterministe (jamais par le LLM) et la transition
/// Pending → Executing est protégée par <see cref="RowVersion"/> contre la double confirmation.
/// </summary>
public sealed class StudioAiBuildPlan
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    public StudioAiPlanKind Kind { get; private set; }

    /// <summary>Spécification JSON d'origine (spec_json de l'outil), rejouée à l'exécution.</summary>
    public string SpecJson { get; private set; } = null!;

    /// <summary>Aperçu structuré pré-calculé (checklist) rendu par le frontend avant confirmation.</summary>
    public string SummaryJson { get; private set; } = null!;

    public StudioAiPlanStatus Status { get; private set; }

    public string? ErrorMessage { get; private set; }

    /// <summary>Charge utile du résultat d'exécution (tables créées, liens d'ouverture…).</summary>
    public string? ResultJson { get; private set; }

    public Guid? CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ExecutedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    private StudioAiBuildPlan() { }

    public static StudioAiBuildPlan Create(
        Guid tenantId,
        StudioAiPlanKind kind,
        string specJson,
        string summaryJson,
        Guid? createdBy,
        TimeSpan lifetime)
    {
        var now = DateTime.UtcNow;
        return new StudioAiBuildPlan
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Kind = kind,
            SpecJson = specJson,
            SummaryJson = summaryJson,
            Status = StudioAiPlanStatus.Pending,
            CreatedBy = createdBy,
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime)
        };
    }

    public bool IsExpired(DateTime utcNow) => Status == StudioAiPlanStatus.Pending && utcNow >= ExpiresAt;

    public void MarkExecuting() => Status = StudioAiPlanStatus.Executing;

    public void MarkCompleted(string? resultJson)
    {
        Status = StudioAiPlanStatus.Completed;
        ResultJson = resultJson;
        ErrorMessage = null;
        ExecutedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string error)
    {
        Status = StudioAiPlanStatus.Failed;
        ErrorMessage = error;
        ExecutedAt = DateTime.UtcNow;
    }

    public void MarkCancelled() => Status = StudioAiPlanStatus.Cancelled;

    public void MarkExpired() => Status = StudioAiPlanStatus.Expired;
}
