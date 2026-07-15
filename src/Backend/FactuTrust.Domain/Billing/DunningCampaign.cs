using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Billing;

/// <summary>Action exécutée à une étape de campagne dunning.</summary>
public enum DunningStepAction
{
    /// <summary>Envoyer un e-mail (template à préciser dans <see cref="DunningStep.EmailTemplateCode"/>).</summary>
    SendEmail = 0,
    /// <summary>Suspendre l'abonnement (subscription Status → Suspended).</summary>
    SuspendSubscription = 1,
    /// <summary>Marquer en PastDue (statut intermédiaire avant suspension).</summary>
    MarkPastDue = 2
}

/// <summary>
/// Lot C6 — Étape d'une campagne dunning. Sérialisée en JSON dans
/// <see cref="DunningCampaign.StepsJson"/>.
/// </summary>
public sealed class DunningStep
{
    /// <summary>Numéro de jour relatif à <c>DueDate</c> (J+1, J+3, J+7, J+14…).</summary>
    public int DaysAfterDueDate { get; set; }
    /// <summary>Action à effectuer.</summary>
    public DunningStepAction Action { get; set; }
    /// <summary>Code de template e-mail (si <c>Action = SendEmail</c>).</summary>
    public string? EmailTemplateCode { get; set; }
    /// <summary>Description courte affichée dans l'admin.</summary>
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// Lot C6 — Campagne dunning : suite ordonnée d'étapes (rappels e-mail puis suspension).
///
/// Une seule campagne <c>IsActive=true</c> peut exister à un instant T (campagne par défaut
/// utilisée pour toutes les souscriptions). L'admin peut créer plusieurs campagnes archivées
/// pour A/B testing futur — la campagne active s'applique à tous les nouveaux états.
/// </summary>
public sealed class DunningCampaign : Entity
{
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    /// <summary>JSON sérialisé d'une <see cref="List{DunningStep}"/>.</summary>
    public string StepsJson { get; private set; } = "[]";
    public bool IsActive { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    private DunningCampaign() { }

    public static DunningCampaign Create(string name, string? description, string stepsJson, Guid createdByUserId, bool isActive = false)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nom requis", nameof(name));
        if (string.IsNullOrWhiteSpace(stepsJson)) throw new ArgumentException("StepsJson requis", nameof(stepsJson));
        return new DunningCampaign
        {
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            StepsJson = stepsJson,
            IsActive = isActive,
            CreatedByUserId = createdByUserId
        };
    }

    public void Update(string name, string? description, string stepsJson)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nom requis", nameof(name));
        if (string.IsNullOrWhiteSpace(stepsJson)) throw new ArgumentException("StepsJson requis", nameof(stepsJson));
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        StepsJson = stepsJson;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    /// <summary>Construit le JSON d'une campagne par défaut 4 étapes (J+1 / J+3 / J+7 / J+14).</summary>
    public static string DefaultStepsJson() =>
        // System.Text.Json compatible — compact mais lisible.
        """
        [
          {"daysAfterDueDate":1,"action":0,"emailTemplateCode":"dunning-step-1-soft","label":"Rappel doux (J+1)"},
          {"daysAfterDueDate":3,"action":2,"emailTemplateCode":"dunning-step-2-second","label":"Deuxième rappel — passage en PastDue (J+3)"},
          {"daysAfterDueDate":7,"action":0,"emailTemplateCode":"dunning-step-3-warning","label":"Avertissement de suspension (J+7)"},
          {"daysAfterDueDate":14,"action":1,"emailTemplateCode":"dunning-step-4-suspended","label":"Suspension automatique (J+14)"}
        ]
        """;
}
