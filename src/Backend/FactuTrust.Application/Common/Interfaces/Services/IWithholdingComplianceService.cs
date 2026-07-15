using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IWithholdingComplianceService
{
    Task<WithholdingComplianceCheckResult> CheckComplianceAsync(int year, int month, CancellationToken ct = default);
    DateTime GetNextTejDeadline(int year, int month);
    bool IsDeadlineApproaching(int year, int month, int warningDays = 5);
    bool IsThresholdApplicable(string operationCode, decimal amountTTC);
}

public record WithholdingComplianceCheckResult(
    bool IsCompliant,
    List<ComplianceAlert> Alerts,
    DateTime NextDeadline,
    int DaysUntilDeadline,
    int PendingCertificates,
    decimal PendingAmount);

public record ComplianceAlert(
    ComplianceAlertSeverity Severity,
    string Code,
    string Message);

public enum ComplianceAlertSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}
