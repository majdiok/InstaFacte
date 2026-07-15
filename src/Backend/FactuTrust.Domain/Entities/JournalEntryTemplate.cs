using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Reusable template for a manual journal entry — captures journal code, default label,
/// and a list of lines (account + optional pre-set amounts) that the user can apply to
/// the manual entry screen. Per-company (multi-tenancy via TenantDbContext).
/// </summary>
public sealed class JournalEntryTemplate : AggregateRoot
{
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string JournalCode { get; private set; } = null!;
    public string? LabelTemplate { get; private set; }
    public bool IsActive { get; private set; }
    public int UsageCount { get; private set; }

    // ── Récurrence planifiée (écritures d'abonnement : loyers, assurances…) ──────
    /// <summary>Fréquence de génération automatique (None = modèle purement manuel).</summary>
    public RecurrenceFrequency RecurrenceFrequency { get; private set; } = RecurrenceFrequency.None;
    /// <summary>Jour du mois de l'échéance (1–28 pour exister dans tous les mois).</summary>
    public int? RecurrenceDayOfMonth { get; private set; }
    public DateTime? RecurrenceStartDate { get; private set; }
    public DateTime? RecurrenceEndDate { get; private set; }
    /// <summary>Prochaine échéance à générer (avancée à chaque exécution).</summary>
    public DateTime? NextRunDate { get; private set; }
    public DateTime? LastRunAt { get; private set; }

    /// <summary>Vrai si le modèle est planifié (récurrence configurée et active).</summary>
    public bool IsRecurring => RecurrenceFrequency != RecurrenceFrequency.None;

    private readonly List<JournalEntryTemplateLine> _lines = new();
    public IReadOnlyCollection<JournalEntryTemplateLine> Lines => _lines.AsReadOnly();

    private JournalEntryTemplate() { }

    public static Result<JournalEntryTemplate> Create(
        string name,
        string journalCode,
        string? description = null,
        string? labelTemplate = null)
    {
        name = name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
            return Result.Failure<JournalEntryTemplate>(Error.Validation("Name", "Le nom du modèle est obligatoire"));
        if (name.Length > 200)
            return Result.Failure<JournalEntryTemplate>(Error.Validation("Name", "Le nom du modèle ne doit pas dépasser 200 caractères"));

        journalCode = journalCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(journalCode))
            return Result.Failure<JournalEntryTemplate>(Error.Validation("JournalCode", "Le journal est obligatoire"));

        return Result.Success(new JournalEntryTemplate
        {
            Name = name,
            Description = description?.Trim(),
            JournalCode = journalCode,
            LabelTemplate = labelTemplate?.Trim(),
            IsActive = true,
            UsageCount = 0
        });
    }

    public Result Update(string name, string journalCode, string? description, string? labelTemplate)
    {
        name = name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
            return Result.Failure(Error.Validation("Name", "Le nom du modèle est obligatoire"));
        if (name.Length > 200)
            return Result.Failure(Error.Validation("Name", "Le nom du modèle ne doit pas dépasser 200 caractères"));

        journalCode = journalCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(journalCode))
            return Result.Failure(Error.Validation("JournalCode", "Le journal est obligatoire"));

        Name = name;
        JournalCode = journalCode;
        Description = description?.Trim();
        LabelTemplate = labelTemplate?.Trim();
        return Result.Success();
    }

    public void AddLine(JournalEntryTemplateLine line) => _lines.Add(line);
    public void ClearLines() => _lines.Clear();
    public void IncrementUsage() => UsageCount++;
    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;

    /// <summary>
    /// Configure la récurrence planifiée. Exige des lignes TOUTES à montants fixes et équilibrées
    /// (la génération automatique n'a personne pour saisir les montants manquants).
    /// </summary>
    public Result ConfigureRecurrence(
        RecurrenceFrequency frequency,
        int dayOfMonth,
        DateTime startDate,
        DateTime? endDate)
    {
        if (frequency == RecurrenceFrequency.None)
            return Result.Failure(Error.Validation("Recurrence", "Choisissez une fréquence (mensuelle, trimestrielle ou annuelle)."));

        if (dayOfMonth is < 1 or > 28)
            return Result.Failure(Error.Validation("Recurrence", "Le jour d'échéance doit être entre 1 et 28 (présent dans tous les mois)."));

        if (endDate.HasValue && endDate.Value.Date < startDate.Date)
            return Result.Failure(Error.Validation("Recurrence", "La date de fin est antérieure à la date de début."));

        var fixedCheck = ValidateFixedBalancedLines();
        if (fixedCheck.IsFailure)
            return fixedCheck;

        RecurrenceFrequency = frequency;
        RecurrenceDayOfMonth = dayOfMonth;
        RecurrenceStartDate = startDate.Date;
        RecurrenceEndDate = endDate?.Date;
        NextRunDate = ComputeFirstRunDate(startDate.Date, dayOfMonth);
        return Result.Success();
    }

    public void DisableRecurrence()
    {
        RecurrenceFrequency = RecurrenceFrequency.None;
        RecurrenceDayOfMonth = null;
        RecurrenceStartDate = null;
        RecurrenceEndDate = null;
        NextRunDate = null;
    }

    /// <summary>
    /// Enregistre une exécution : avance l'échéance (null si la prochaine dépasse la date de fin —
    /// la récurrence est alors épuisée).
    /// </summary>
    public void MarkRun(DateTime runDate)
    {
        LastRunAt = DateTime.UtcNow;
        UsageCount++;
        if (RecurrenceFrequency == RecurrenceFrequency.None || RecurrenceDayOfMonth is null)
        {
            NextRunDate = null;
            return;
        }

        var next = ComputeNextRunDate(runDate.Date, RecurrenceFrequency, RecurrenceDayOfMonth.Value);
        NextRunDate = RecurrenceEndDate.HasValue && next > RecurrenceEndDate.Value ? null : next;
    }

    /// <summary>Première échéance ≥ date de début, au jour configuré.</summary>
    public static DateTime ComputeFirstRunDate(DateTime startDate, int dayOfMonth)
    {
        var candidate = new DateTime(startDate.Year, startDate.Month, dayOfMonth);
        return candidate >= startDate ? candidate : candidate.AddMonths(1);
    }

    /// <summary>Échéance suivante après <paramref name="fromDate"/> selon la fréquence.</summary>
    public static DateTime ComputeNextRunDate(DateTime fromDate, RecurrenceFrequency frequency, int dayOfMonth)
    {
        var months = frequency switch
        {
            RecurrenceFrequency.Monthly => 1,
            RecurrenceFrequency.Quarterly => 3,
            RecurrenceFrequency.Yearly => 12,
            _ => throw new ArgumentOutOfRangeException(nameof(frequency))
        };
        var next = fromDate.AddMonths(months);
        return new DateTime(next.Year, next.Month, Math.Min(dayOfMonth, 28));
    }

    /// <summary>Les lignes doivent toutes porter un montant fixe, et l'ensemble être équilibré.</summary>
    private Result ValidateFixedBalancedLines()
    {
        if (_lines.Count < 2)
            return Result.Failure(Error.Validation("Recurrence", "Le modèle doit avoir au moins deux lignes pour être planifié."));

        decimal debit = 0, credit = 0;
        foreach (var line in _lines)
        {
            var d = line.FixedDebit ?? 0m;
            var c = line.FixedCredit ?? 0m;
            if (d == 0m && c == 0m)
                return Result.Failure(Error.Validation("Recurrence",
                    $"La ligne {line.LineNumber} ({line.AccountNumber}) n'a pas de montant fixe : un modèle planifié doit être entièrement chiffré."));
            debit += d;
            credit += c;
        }

        if (Math.Round(debit, 3) != Math.Round(credit, 3))
            return Result.Failure(Error.Validation("Recurrence",
                "Les montants fixes du modèle ne sont pas équilibrés (débit ≠ crédit)."));

        return Result.Success();
    }
}
