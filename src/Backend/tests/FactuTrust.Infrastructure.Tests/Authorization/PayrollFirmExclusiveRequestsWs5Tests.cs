using FactuTrust.Application.Authorization;
using FactuTrust.Application.Features.Payroll.Commands;
using FactuTrust.Application.Features.Payroll.Reports;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Authorization;

/// <summary>
/// WS-5 : les requêtes firm-only de remédiation historique (diagnostic, reclassement, dé-solde,
/// exposition) doivent être enregistrées dans <see cref="PayrollFirmExclusiveRequests"/> pour que la
/// double barrière (politique <c>PayrollFirmOperation</c> + pipeline MediatR) s'applique.
/// </summary>
public sealed class PayrollFirmExclusiveRequestsWs5Tests
{
    [Fact]
    public void Registers_diagnostic_and_remediation_requests_as_firm_exclusive()
    {
        Assert.True(PayrollFirmExclusiveRequests.IsFirmExclusiveRequest(typeof(PayrollComplianceDiagnosticQuery)));
        Assert.True(PayrollFirmExclusiveRequests.IsFirmExclusiveRequest(typeof(GeneratePayrollReclassificationCommand)));
        Assert.True(PayrollFirmExclusiveRequests.IsFirmExclusiveRequest(typeof(UnsettleAdvanceCommand)));
        Assert.True(PayrollFirmExclusiveRequests.IsFirmExclusiveRequest(typeof(UnsettleLoanInstallmentCommand)));
        Assert.True(PayrollFirmExclusiveRequests.IsFirmExclusiveRequest(typeof(GetPayrollExposureReportQuery)));
        Assert.True(PayrollFirmExclusiveRequests.IsFirmExclusiveRequest(typeof(ExportPayrollExposureReportQuery)));
    }

    [Fact]
    public void Does_not_flag_regular_read_query_as_firm_exclusive()
    {
        // Une query de lecture non-remédiation (livre de paie) ne doit pas être barrée firm.
        Assert.False(PayrollFirmExclusiveRequests.IsFirmExclusiveRequest(typeof(GeneratePayrollBookQuery)));
    }
}
