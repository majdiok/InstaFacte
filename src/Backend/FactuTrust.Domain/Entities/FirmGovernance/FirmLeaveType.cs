using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

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

    /// <summary>
    /// Type de congé de paie produit à l'approbation. Nul = aucun effet sur le bulletin.
    /// </summary>
    /// <remarks>
    /// C'est ce qui transforme un congé cabinet en effet paie réel : une retenue pour
    /// <see cref="LeaveType.Unpaid"/>, une indemnité journalière pour <see cref="LeaveType.Sick"/>.
    /// Un type créé par un cabinet arrive non mappé : il reste sans effet tant que le cabinet ne
    /// l'a pas explicitement rattaché, plutôt que de deviner un impact sur la paie.
    /// </remarks>
    public LeaveType? PayrollLeaveType { get; private set; }

    /// <summary>
    /// Le temps posé sur ce type est-il décompté du temps de présence productif ?
    /// </summary>
    /// <remarks>
    /// Distinct de <see cref="DeductsBalance"/> (droit à congés) et de
    /// <see cref="PayrollLeaveType"/> (effet sur le bulletin) : le télétravail est du temps
    /// travaillé, et la formation est déjà absorbée par le taux de productivité de l'exercice —
    /// la recompter ici la déduirait deux fois.
    /// </remarks>
    public bool CountsAsAbsence { get; private set; }

    private FirmLeaveType() { }

    public static Result<FirmLeaveType> Create(
        Guid firmTenantId,
        string code,
        string label,
        string colorHex,
        bool deductsBalance,
        bool requiresApproval = true,
        bool isSystem = false,
        int sortOrder = 0,
        LeaveType? payrollLeaveType = null,
        bool countsAsAbsence = false)
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
            SortOrder = sortOrder,
            PayrollLeaveType = payrollLeaveType,
            CountsAsAbsence = countsAsAbsence
        });
    }

    public Result Update(
        string label,
        string colorHex,
        bool deductsBalance,
        bool requiresApproval,
        int sortOrder,
        LeaveType? payrollLeaveType = null,
        bool countsAsAbsence = false)
    {
        if (string.IsNullOrWhiteSpace(label))
            return Result.Failure(Error.Validation("Label", "Le libellé est obligatoire."));

        Label = label.Trim();
        ColorHex = NormalizeColor(colorHex);
        DeductsBalance = deductsBalance;
        RequiresApproval = requiresApproval;
        SortOrder = sortOrder;
        PayrollLeaveType = payrollLeaveType;
        CountsAsAbsence = countsAsAbsence;
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

    /// <summary>
    /// Catalogue système seedé à la première ouverture (idempotent par code).
    /// </summary>
    /// <remarks>
    /// Les colonnes <c>PayrollLeaveType</c> et <c>CountsAsAbsence</c> traduisent le type cabinet
    /// vers ses deux effets : le bulletin d'une part, le temps de présence de l'autre. Formation et
    /// télétravail n'ont ni l'un ni l'autre — la formation est déjà couverte par le taux de
    /// productivité, et le télétravail est du temps travaillé.
    /// </remarks>
    public static IReadOnlyList<FirmLeaveTypeSeed> DefaultCatalog =>
        new FirmLeaveTypeSeed[]
        {
            new("PAID",     "Congé payé",       "#22c55e", true,  true, LeaveType.Paid,     true),
            new("RTT",      "RTT",              "#f97316", true,  true, LeaveType.Recovery, true),
            new("TRAINING", "Formation",        "#3b82f6", false, true, null,               false),
            new("SICK",     "Arrêt maladie",    "#ef4444", false, true, LeaveType.Sick,     true),
            new("REMOTE",   "Télétravail",      "#a78bfa", false, true, null,               false),
            new("UNPAID",   "Congé sans solde", "#64748b", false, true, LeaveType.Unpaid,   true),
            new("OTHER",    "Autre",            "#94a3b8", false, true, LeaveType.Other,    false)
        };
}

/// <summary>Définition d'un type d'absence du catalogue système.</summary>
public sealed record FirmLeaveTypeSeed(
    string Code,
    string Label,
    string ColorHex,
    bool DeductsBalance,
    bool RequiresApproval,
    LeaveType? PayrollLeaveType,
    bool CountsAsAbsence);
