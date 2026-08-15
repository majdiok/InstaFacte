using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Treasury;

/// <summary>
/// Engagement de trésorerie récurrent saisi par l'utilisateur : loyer, abonnement, traite,
/// prélèvement d'assurance, subvention attendue…
/// </summary>
/// <remarks>
/// Comble le seul angle mort des collecteurs automatiques : ces flux sont parfaitement connus du
/// dirigeant mais n'existent nulle part en base tant qu'aucune facture n'a été émise. Sans eux, la
/// projection surestime systématiquement le solde.
/// </remarks>
public sealed class RecurringCashCommitment : Entity
{
    public string Label { get; private set; } = null!;

    public CashFlowDirection Direction { get; private set; }

    /// <summary>Montant de chaque occurrence, toujours positif.</summary>
    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "TND";

    public CashCommitmentFrequency Frequency { get; private set; }

    /// <summary>
    /// Jour du mois de l'occurrence, dans [1..31]. Un jour dépassant la longueur du mois est
    /// ramené au dernier jour lors de l'expansion (31 en février → 28 ou 29).
    /// </summary>
    public int DayOfMonth { get; private set; }

    public DateTime StartDate { get; private set; }

    /// <summary>Fin de l'engagement. Null quand il est à durée indéterminée.</summary>
    public DateTime? EndDate { get; private set; }

    /// <summary>Catégorie libre, alignée sur celles de la caisse pour rester cohérent.</summary>
    public string? Category { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    private RecurringCashCommitment() { }

    public static RecurringCashCommitment Create(
        string label,
        CashFlowDirection direction,
        decimal amount,
        CashCommitmentFrequency frequency,
        int dayOfMonth,
        DateTime startDate,
        DateTime? endDate = null,
        string? category = null,
        string? notes = null,
        string currency = "TND")
    {
        Validate(label, amount, dayOfMonth, startDate, endDate);

        return new RecurringCashCommitment
        {
            Label = label.Trim(),
            Direction = direction,
            Amount = MillimeRounding.Round(amount),
            Currency = string.IsNullOrWhiteSpace(currency) ? "TND" : currency.Trim().ToUpperInvariant(),
            Frequency = frequency,
            DayOfMonth = dayOfMonth,
            StartDate = startDate.Date,
            EndDate = endDate?.Date,
            Category = Truncate(category, 100),
            Notes = Truncate(notes, 500),
            IsActive = true
        };
    }

    public void Update(
        string label,
        CashFlowDirection direction,
        decimal amount,
        CashCommitmentFrequency frequency,
        int dayOfMonth,
        DateTime startDate,
        DateTime? endDate,
        string? category,
        string? notes)
    {
        Validate(label, amount, dayOfMonth, startDate, endDate);

        Label = label.Trim();
        Direction = direction;
        Amount = MillimeRounding.Round(amount);
        Frequency = frequency;
        DayOfMonth = dayOfMonth;
        StartDate = startDate.Date;
        EndDate = endDate?.Date;
        Category = Truncate(category, 100);
        Notes = Truncate(notes, 500);
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    /// <summary>Nombre de mois séparant deux occurrences.</summary>
    public int MonthStep => Frequency switch
    {
        CashCommitmentFrequency.Monthly => 1,
        CashCommitmentFrequency.Quarterly => 3,
        CashCommitmentFrequency.SemiAnnual => 6,
        CashCommitmentFrequency.Annual => 12,
        _ => 0
    };

    private static void Validate(
        string label,
        decimal amount,
        int dayOfMonth,
        DateTime startDate,
        DateTime? endDate)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Le libellé de l'engagement est obligatoire.", nameof(label));

        if (amount <= 0m)
            throw new ArgumentException("Le montant doit être strictement positif.", nameof(amount));

        if (dayOfMonth is < 1 or > 31)
            throw new ArgumentException("Le jour du mois doit être dans [1..31].", nameof(dayOfMonth));

        if (endDate.HasValue && endDate.Value.Date < startDate.Date)
            throw new ArgumentException("La date de fin doit être ≥ la date de début.", nameof(endDate));
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
