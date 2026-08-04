using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.FirmGovernance;

/// <summary>Type d'absence configurable par cabinet (congés payés, RTT, télétravail…).</summary>
public sealed class FirmLeaveType : Entity
{
    public Guid FirmTenantId { get; private set; }
    public string Code { get; private set; } = null!;
    public string Label { get; private set; } = null!;
    public string ColorHex { get; private set; } = "#64748b";
    public bool DeductsBalance { get; private set; }
    public bool RequiresApproval { get; private set; } = true;
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int SortOrder { get; private set; }

    private FirmLeaveType() { }

    public static Result<FirmLeaveType> Create(
        Guid firmTenantId,
        string code,
        string label,
        string colorHex,
        bool deductsBalance,
        bool requiresApproval = true,
        bool isSystem = false,
        int sortOrder = 0)
    {
        if (firmTenantId == Guid.Empty)
            return Result.Failure<FirmLeaveType>(Error.Validation("Tenant", "Cabinet requis."));

        var normalized = NormalizeCode(code);
        if (string.IsNullOrEmpty(normalized))
            return Result.Failure<FirmLeaveType>(Error.Validation("Code", "Le code est obligatoire."));
        if (normalized.Length > 30)
            return Result.Failure<FirmLeaveType>(Error.Validation("Code", "Le code ne peut pas dépasser 30 caractères."));
        if (string.IsNullOrWhiteSpace(label))
            return Result.Failure<FirmLeaveType>(Error.Validation("Label", "Le libellé est obligatoire."));

        return Result.Success(new FirmLeaveType
        {
            FirmTenantId = firmTenantId,
            Code = normalized,
            Label = label.Trim(),
            ColorHex = NormalizeColor(colorHex),
            DeductsBalance = deductsBalance,
            RequiresApproval = requiresApproval,
            IsSystem = isSystem,
            IsActive = true,
            SortOrder = sortOrder
        });
    }

    public Result Update(string label, string colorHex, bool deductsBalance, bool requiresApproval, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(label))
            return Result.Failure(Error.Validation("Label", "Le libellé est obligatoire."));

        Label = label.Trim();
        ColorHex = NormalizeColor(colorHex);
        DeductsBalance = deductsBalance;
        RequiresApproval = requiresApproval;
        SortOrder = sortOrder;
        return Result.Success();
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public static string NormalizeCode(string? code) =>
        (code ?? string.Empty).Trim().ToUpperInvariant();

    private static string NormalizeColor(string? color)
    {
        var c = (color ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(c)) return "#64748b";
        if (!c.StartsWith('#')) c = "#" + c;
        return c.Length <= 9 ? c : "#64748b";
    }

    /// <summary>Catalogue système seedé à la première ouverture (idempotent par code).</summary>
    public static IReadOnlyList<(string Code, string Label, string ColorHex, bool DeductsBalance, bool RequiresApproval)> DefaultCatalog =>
        new[]
        {
            ("PAID",     "Congé payé",   "#22c55e", true,  true),
            ("RTT",      "RTT",          "#f97316", true,  true),
            ("TRAINING", "Formation",    "#3b82f6", false, true),
            ("SICK",     "Arrêt maladie","#ef4444", false, true),
            ("REMOTE",   "Télétravail",  "#a78bfa", false, true),
            ("UNPAID",   "Congé sans solde", "#64748b", false, true),
            ("OTHER",    "Autre",        "#94a3b8", false, true)
        };
}
