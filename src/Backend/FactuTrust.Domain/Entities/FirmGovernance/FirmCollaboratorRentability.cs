using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.FirmGovernance;

/// <summary>
/// Snapshot annuel de rentabilité collaborateur (parité Décisiel CollaboratorRentability).
/// Formule : Rentabilité = CA − MS − MS admin − Charges IT/Mgmt − Charges exploitation.
/// </summary>
public sealed class FirmCollaboratorRentability : Entity
{
    public Guid FirmTenantId { get; private set; }
    public Guid CollaboratorUserId { get; private set; }
    public string CollaboratorDisplayName { get; private set; } = null!;
    public int Year { get; private set; }
    public decimal Rentability { get; private set; }

    /// <summary>
    /// Valeur du solde avant le passage au modèle de marge sur coût direct.
    /// </summary>
    /// <remarks>
    /// Renseignée une seule fois, au premier recalcul global. Des rentabilités déjà communiquées
    /// changent de valeur lors de ce basculement : sans cette conservation, il serait impossible de
    /// justifier l'écart auprès d'un associé qui a vu l'ancien chiffre.
    /// </remarks>
    public decimal? LegacyRentability { get; private set; }

    public DateTime? RecalculatedAt { get; private set; }

    private readonly List<FirmCollaboratorRentabilityLine> _lines = new();
    public IReadOnlyCollection<FirmCollaboratorRentabilityLine> Lines => _lines;

    private FirmCollaboratorRentability() { }

    public static Result<FirmCollaboratorRentability> Create(
        Guid firmTenantId,
        Guid collaboratorUserId,
        string displayName,
        int year)
    {
        if (firmTenantId == Guid.Empty || collaboratorUserId == Guid.Empty)
            return Result.Failure<FirmCollaboratorRentability>(Error.Validation("Tenant", "Cabinet et collaborateur requis"));
        if (year < 2000 || year > 2100)
            return Result.Failure<FirmCollaboratorRentability>(Error.Validation("Year", "Année invalide"));

        return Result.Success(new FirmCollaboratorRentability
        {
            FirmTenantId = firmTenantId,
            CollaboratorUserId = collaboratorUserId,
            CollaboratorDisplayName = displayName.Trim(),
            Year = year
        });
    }

    public void ReplaceLines(IEnumerable<FirmCollaboratorRentabilityLine> lines)
    {
        _lines.Clear();
        _lines.AddRange(lines);
        Recalculate();
    }

    public void SetLine(FirmRentabilityReference reference, decimal value, Guid? lineCollaboratorUserId = null)
    {
        var existing = _lines.FirstOrDefault(l =>
            l.ReferenceCode == reference && l.LineCollaboratorUserId == lineCollaboratorUserId);
        if (existing is not null)
            existing.SetValue(value);
        else
            _lines.Add(FirmCollaboratorRentabilityLine.Create(Id, reference, value, lineCollaboratorUserId));
        Recalculate();
    }

    public decimal GetTotal(FirmRentabilityReference reference) =>
        _lines.Where(l => l.ReferenceCode == reference).Sum(l => l.Value);

    /// <summary>
    /// Masse salariale retenue : le détail par collaborateur prime sur le montant agrégé.
    /// </summary>
    /// <remarks>
    /// Règle unique, appliquée à l'identique par <see cref="Recalculate"/> et par les projections
    /// de lecture. Deux règles différentes faisaient auparavant diverger la colonne « Masse
    /// salariale » de la liste et la rentabilité effectivement stockée.
    /// </remarks>
    public decimal GetPayrollCost()
    {
        var hasDetails = _lines.Any(l => PayrollDetailReferences.Contains(l.ReferenceCode));
        if (hasDetails)
            return _lines.Where(l => PayrollDetailReferences.Contains(l.ReferenceCode)).Sum(l => l.Value);
        return GetTotal(FirmRentabilityReference.PayrollCost);
    }

    private static readonly FirmRentabilityReference[] PayrollDetailReferences =
    {
        FirmRentabilityReference.PayrollGross,
        FirmRentabilityReference.EmployerContributions,
        FirmRentabilityReference.PayrollExtras
    };

    /// <summary>
    /// Formule Décisiel : CA − MS − Admin − IT − Exploitation.
    /// </summary>
    /// <remarks>
    /// Seul point d'écriture de <see cref="Rentability"/> : la valeur est toujours dérivée des
    /// lignes réellement persistées, jamais de montants transmis en parallèle.
    /// </remarks>
    public void Recalculate()
    {
        Rentability = Compute(
            GetTotal(FirmRentabilityReference.TotalRevenue),
            GetPayrollCost(),
            GetTotal(FirmRentabilityReference.AdminPayrollCharge),
            GetTotal(FirmRentabilityReference.ItManagementCharge),
            GetTotal(FirmRentabilityReference.OperatingCharge));
    }

    /// <summary>
    /// Marque le snapshot comme recalculé, en préservant la valeur d'origine au premier passage.
    /// </summary>
    /// <remarks>
    /// Idempotent : rejouer le recalcul met à jour l'horodatage mais n'écrase jamais
    /// <see cref="LegacyRentability"/>, sinon la valeur d'origine serait perdue dès la deuxième
    /// exécution.
    /// </remarks>
    public void MarkRecalculated()
    {
        LegacyRentability ??= Rentability;
        RecalculatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Rentabilité encaissée : la rentabilité comptable diminuée des honoraires non recouvrés.
    /// </summary>
    /// <remarks>
    /// Indicateur dérivé, jamais stocké. La formule Décisiel reste intacte : injecter les soldes
    /// clients dans <see cref="Rentability"/> changerait le sens d'un chiffre déjà utilisé.
    /// </remarks>
    public decimal GetCollectedRentability() =>
        MillimeRounding.Round(Rentability - GetTotal(FirmRentabilityReference.ClientDebitBalance));

    public static decimal Compute(
        decimal totalRevenue,
        decimal payrollCost,
        decimal adminCharge,
        decimal itCharge,
        decimal operatingCharge) =>
        MillimeRounding.Round(totalRevenue - payrollCost - adminCharge - itCharge - operatingCharge);
}

public sealed class FirmCollaboratorRentabilityLine : Entity
{
    public Guid CollaboratorRentabilityId { get; private set; }
    public FirmRentabilityReference ReferenceCode { get; private set; }
    public decimal Value { get; private set; }
    public Guid? LineCollaboratorUserId { get; private set; }

    private FirmCollaboratorRentabilityLine() { }

    public static FirmCollaboratorRentabilityLine Create(
        Guid rentabilityId,
        FirmRentabilityReference reference,
        decimal value,
        Guid? lineCollaboratorUserId = null) =>
        new()
        {
            CollaboratorRentabilityId = rentabilityId,
            ReferenceCode = reference,
            Value = MillimeRounding.Round(value),
            LineCollaboratorUserId = lineCollaboratorUserId
        };

    public void SetValue(decimal value) => Value = MillimeRounding.Round(value);
}
