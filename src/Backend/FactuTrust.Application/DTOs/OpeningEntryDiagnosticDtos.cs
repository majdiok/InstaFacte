namespace FactuTrust.Application.DTOs;

/// <summary>
/// Écart ligne à ligne entre le solde attendu (recalculé, ancré — T8) et le solde réellement
/// porté par l'écriture d'à-nouveau active pour un compte (et un tiers éventuel). Signé
/// débit − crédit, comme les lignes d'écriture.
/// </summary>
public sealed record OpeningEntryLineDiscrepancyDto(
    string AccountNumber,
    Guid? ThirdPartyId,
    decimal ExpectedAmount,
    decimal ActualAmount,
    decimal Difference);

/// <summary>
/// Diagnostic d'une écriture d'à-nouveau active (<c>SourceOpeningBalance</c>, non extournée) :
/// recalcule les soldes attendus pour l'exercice clôturé et les compare ligne à ligne aux
/// montants réellement portés par l'écriture en base (T8, point 3).
/// </summary>
public sealed record OpeningEntryDiagnosticDto(
    int ClosedFiscalYear,
    Guid OpeningEntryId,
    bool HasDiscrepancy,
    IReadOnlyList<OpeningEntryLineDiscrepancyDto> Discrepancies);
