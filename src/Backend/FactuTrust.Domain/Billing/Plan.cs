using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Billing;

/// <summary>
/// Lot C1 — Plan tarifaire configurable côté admin plateforme.
///
/// Coexiste avec l'enum <see cref="FactuTrust.Domain.Enums.SubscriptionPlan"/> pour
/// rétro-compatibilité : tant que <c>Plans</c> contient les 3 lignes seed
/// (Free / Monthly / Annual) avec mêmes <c>Code</c> que l'enum, le comportement
/// fonctionnel est identique.
///
/// Une suppression réelle est interdite quand des tenants utilisent le plan ;
/// utiliser <see cref="Archive"/> à la place (plan masqué dans la sélection
/// publique mais l'historique des subscriptions reste valide).
/// </summary>
public sealed class Plan : Entity
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsPublic { get; private set; }
    public bool IsActive { get; private set; }
    public BillingPeriod BillingPeriod { get; private set; }
    public decimal BasePriceTND { get; private set; }
    public string Currency { get; private set; } = "TND";
    public int TrialDays { get; private set; }
    public int SortOrder { get; private set; }
    public DateTime? ArchivedAt { get; private set; }

    private readonly List<PlanLimit> _limits = new();
    public IReadOnlyCollection<PlanLimit> Limits => _limits.AsReadOnly();

    private readonly List<PlanFeature> _features = new();
    public IReadOnlyCollection<PlanFeature> Features => _features.AsReadOnly();

    private readonly List<PlanModule> _modules = new();
    public IReadOnlyCollection<PlanModule> Modules => _modules.AsReadOnly();

    private Plan() { }

    public static Plan Create(
        string code,
        string name,
        string? description,
        BillingPeriod billingPeriod,
        decimal basePriceTND,
        bool isPublic = true,
        int trialDays = 0,
        int sortOrder = 0,
        string currency = "TND")
    {
        return new Plan
        {
            Code = (code ?? string.Empty).Trim(),
            Name = (name ?? string.Empty).Trim(),
            Description = description?.Trim(),
            IsPublic = isPublic,
            IsActive = true,
            BillingPeriod = billingPeriod,
            BasePriceTND = basePriceTND,
            Currency = string.IsNullOrEmpty(currency) ? "TND" : currency,
            TrialDays = Math.Max(0, trialDays),
            SortOrder = sortOrder,
            ArchivedAt = null
        };
    }

    public void Update(
        string name,
        string? description,
        bool isPublic,
        BillingPeriod billingPeriod,
        decimal basePriceTND,
        int trialDays,
        int sortOrder,
        string currency)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name.Trim();
        Description = description?.Trim();
        IsPublic = isPublic;
        BillingPeriod = billingPeriod;
        BasePriceTND = basePriceTND;
        TrialDays = Math.Max(0, trialDays);
        SortOrder = sortOrder;
        Currency = string.IsNullOrEmpty(currency) ? "TND" : currency;
    }

    /// <summary>Archive le plan : le retire de la sélection publique sans toucher aux subscriptions existantes.</summary>
    public void Archive()
    {
        IsActive = false;
        IsPublic = false;
        ArchivedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        IsActive = true;
        ArchivedAt = null;
    }

    /// <summary>Remplace l'ensemble des limites du plan par la collection fournie.</summary>
    public void ReplaceLimits(IEnumerable<(string Key, string Value)> limits)
    {
        _limits.Clear();
        foreach (var (key, value) in limits)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            _limits.Add(PlanLimit.Create(Id, key.Trim(), (value ?? string.Empty).Trim()));
        }
    }

    /// <summary>Remplace l'ensemble des features du plan par la collection fournie.</summary>
    public void ReplaceFeatures(IEnumerable<(string Key, bool Enabled)> features)
    {
        _features.Clear();
        foreach (var (key, enabled) in features)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            _features.Add(PlanFeature.Create(Id, key.Trim(), enabled));
        }
    }

    /// <summary>Remplace l'ensemble des modules inclus dans le plan.</summary>
    public void ReplaceModules(IEnumerable<(int Module, bool IsIncluded)> modules)
    {
        _modules.Clear();
        foreach (var (module, included) in modules)
        {
            _modules.Add(PlanModule.Create(Id, module, included));
        }
    }
}
