namespace FactuTrust.Application.Features.Accounting.Audit;

/// <summary>Candidat anomalie produit par une règle avant persistance.</summary>
public sealed record AnomalyLineCandidate(
    Guid? JournalEntryId,
    Guid? JournalEntryLineId,
    DateTime? EntryDate,
    string? AccountNumber,
    string? Label,
    decimal Debit,
    decimal Credit,
    string? PieceRef,
    string? JustificationStatus);

public sealed record AnomalyCandidate(
    string Fingerprint,
    string RuleCode,
    string ModuleCode,
    int Category,
    int Severity,
    string Title,
    string Description,
    string Impact,
    string? AccountRef,
    decimal Amount,
    DateOnly? PeriodFrom,
    DateOnly? PeriodTo,
    IReadOnlyList<AnomalyLineCandidate> Lines,
    IReadOnlyList<string> Recommendations,
    string? DeepLinkRoute);
