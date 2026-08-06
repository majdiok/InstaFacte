using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Payroll.Declarations.CnssRemittance;

internal static class CnssRemittanceFeatureGuard
{
    public static Result EnsureEnabled(AccountingSettings settings)
    {
        if (!settings.PayrollCnssRemittanceEnabled)
        {
            return Result.Failure(Error.Validation(
                "Feature",
                "Le bordereau CNSS n'est pas activé pour ce dossier."));
        }

        return Result.Success();
    }
}

/// <summary>
/// Charge le cycle de paie et construit le bordereau CNSS mensuel.
/// </summary>
public sealed class CnssRemittanceDataLoader
{
    private readonly IPayrollRunRepository _runs;
    private readonly ICnssContributionPaymentRepository _payments;
    private readonly ITenantCompanySummaryProvider _companySummary;

    public CnssRemittanceDataLoader(
        IPayrollRunRepository runs,
        ICnssContributionPaymentRepository payments,
        ITenantCompanySummaryProvider companySummary)
    {
        _runs = runs;
        _payments = payments;
        _companySummary = companySummary;
    }

    public async Task<Result<CnssContributionRemittanceBatch>> LoadBatchAsync(
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        if (month is < 1 or > 12)
        {
            return Result.Failure<CnssContributionRemittanceBatch>(
                Error.Validation("Month", "Le mois doit être compris entre 1 et 12."));
        }

        if (year is < 2000 or > 2100)
        {
            return Result.Failure<CnssContributionRemittanceBatch>(
                Error.Validation("Year", "L'exercice doit être compris entre 2000 et 2100."));
        }

        var company = await _companySummary.GetCurrentTenantSummaryAsync(cancellationToken);
        if (company is null)
        {
            return Result.Failure<CnssContributionRemittanceBatch>(
                Error.Validation("Company", "Impossible de charger les informations de la société."));
        }

        if (string.IsNullOrWhiteSpace(company.Nif))
        {
            return Result.Failure<CnssContributionRemittanceBatch>(
                Error.Validation("Nif", "Le matricule fiscal (NIF) de l'employeur est obligatoire."));
        }

        var run = await _runs.GetByPeriodWithPayslipsAsync(year, month, cancellationToken);
        if (run is not null
            && run.Status is not PayrollRunStatus.Validated
            and not PayrollRunStatus.Closed)
        {
            run = null;
        }

        var payment = await _payments.GetActiveByPeriodAsync(year, month, cancellationToken);
        var employer = new EmployerSnapshot(
            company.CompanyName,
            company.Nif,
            company.AddressLine,
            company.CnssEmployerNumber);

        var batch = CnssContributionRemittanceBuilder.Build(
            year,
            month,
            run,
            employer,
            payment is not null,
            payment is null ? CnssRemittancePaymentStatus.Pending : payment.Status);

        return Result.Success(batch);
    }

    public async Task<Result<CnssContributionRemittanceDto>> LoadDtoAsync(
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var batchResult = await LoadBatchAsync(year, month, cancellationToken);
        if (batchResult.IsFailure)
            return Result.Failure<CnssContributionRemittanceDto>(batchResult.Error);

        var payment = await _payments.GetActiveByPeriodAsync(year, month, cancellationToken);
        return Result.Success(ToDto(batchResult.Value, payment));
    }

    internal static CnssContributionRemittanceDto ToDto(
        CnssContributionRemittanceBatch batch,
        CnssContributionPayment? payment)
    {
        return new CnssContributionRemittanceDto
        {
            Year = batch.Year,
            Month = batch.Month,
            EmployerCompanyName = batch.EmployerCompanyName,
            EmployerNif = batch.EmployerNif,
            EmployerCnssNumber = batch.EmployerCnssNumber,
            EmployerAddressLine = batch.EmployerAddressLine,
            PayrollRunId = batch.PayrollRunId,
            SourceRunStatus = batch.SourceRunStatus.HasValue ? (int)batch.SourceRunStatus.Value : null,
            IsEligible = batch.IsEligible,
            HasExistingPayment = batch.HasExistingPayment,
            PaymentStatus = batch.PaymentStatus.HasValue ? (int)batch.PaymentStatus.Value : null,
            PaymentStatusDisplay = batch.PaymentStatus?.ToDisplayString(),
            TotalCnssEmployee = batch.TotalCnssEmployee,
            TotalCnssEmployer = batch.TotalCnssEmployer,
            TotalWorkAccident = batch.TotalWorkAccident,
            TotalDue = batch.TotalDue,
            EmployeeCount = batch.EmployeeCount,
            DocumentReference = batch.DocumentReference,
            Warnings = batch.Warnings,
            Lines = batch.Lines.Select(ToLineDto).ToList(),
            Payment = payment is null ? null : ToPaymentDto(payment)
        };
    }

    internal static CnssContributionRemittanceLineDto ToLineDto(CnssContributionRemittanceLine line) =>
        new()
        {
            EmployeeId = line.EmployeeId,
            EmployeeName = line.EmployeeName,
            CnssNumber = line.CnssNumber,
            CnssableGross = line.CnssableGross,
            CnssEmployee = line.CnssEmployee,
            CnssEmployer = line.CnssEmployer,
            WorkAccident = line.WorkAccident,
            LineTotal = line.LineTotal,
            Warnings = line.Warnings
        };

    internal static CnssContributionPaymentDto ToPaymentDto(CnssContributionPayment payment) =>
        new()
        {
            Id = payment.Id,
            Year = payment.Year,
            Month = payment.Month,
            PayrollRunId = payment.PayrollRunId,
            TotalCnssEmployee = payment.TotalCnssEmployee,
            TotalCnssEmployer = payment.TotalCnssEmployer,
            TotalWorkAccident = payment.TotalWorkAccident,
            TotalDue = payment.TotalDue,
            Amount = payment.Amount.Amount,
            PaymentDate = payment.PaymentDate,
            Method = payment.Method.ToString(),
            MethodDisplay = payment.Method.ToDisplayString(),
            BankAccountId = payment.BankAccountId,
            Reference = payment.Reference,
            Notes = payment.Notes,
            IsCancelled = payment.IsCancelled,
            CancelledAt = payment.CancelledAt,
            CancelledBy = payment.CancelledBy,
            CancellationReason = payment.CancellationReason,
            CreatedAt = payment.CreatedAt,
            CreatedBy = payment.CreatedBy
        };
}
