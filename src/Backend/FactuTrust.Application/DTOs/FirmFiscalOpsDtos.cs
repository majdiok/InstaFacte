namespace FactuTrust.Application.DTOs;

/// <summary>
/// Aggregated fiscal KPIs across active client dossiers (cabinet fan-out).
/// </summary>
public sealed record FirmFiscalOpsSummaryDto
{
    public int OverdueSchedulesCount { get; init; }
    public int UpcomingWithin7DaysCount { get; init; }
    public int TejPendingCount { get; init; }
    public int LiasseDraftsCount { get; init; }
    public int DtsPendingCount { get; init; }
    public decimal OverdueEstimatedAmount { get; init; }
    public decimal Upcoming7DaysEstimatedAmount { get; init; }
}
