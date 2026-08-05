using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Services.Payroll.BankTransfer;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.BankTransfer;

/// <summary>
/// Prévisualisation du fichier de virement bancaire pour un cycle Validé/Clôturé.
/// </summary>
public sealed record GeneratePayrollBankTransferQuery(
    Guid RunId,
    Guid? BankAccountId = null,
    string? TransferLabel = null) : IRequest<Result<PayrollBankTransferPreviewDto>>;

public sealed class GeneratePayrollBankTransferQueryHandler
    : IRequestHandler<GeneratePayrollBankTransferQuery, Result<PayrollBankTransferPreviewDto>>
{
    private readonly IPayrollRunRepository _runs;
    private readonly IEmployeeRepository _employees;
    private readonly ICompanyRepository _companies;
    private readonly IBankAccountRepository _bankAccounts;
    private readonly IEmployeeGarnishmentRepository _garnishments;

    public GeneratePayrollBankTransferQueryHandler(
        IPayrollRunRepository runs,
        IEmployeeRepository employees,
        ICompanyRepository companies,
        IBankAccountRepository bankAccounts,
        IEmployeeGarnishmentRepository garnishments)
    {
        _runs = runs;
        _employees = employees;
        _companies = companies;
        _bankAccounts = bankAccounts;
        _garnishments = garnishments;
    }

    public async Task<Result<PayrollBankTransferPreviewDto>> Handle(
        GeneratePayrollBankTransferQuery request,
        CancellationToken cancellationToken)
    {
        var built = await PayrollBankTransferOrchestrator.BuildBatchAsync(
            request.RunId,
            request.BankAccountId,
            request.TransferLabel,
            _runs,
            _employees,
            _companies,
            _bankAccounts,
            _garnishments,
            cancellationToken);

        if (built.IsFailure)
            return Result.Failure<PayrollBankTransferPreviewDto>(built.Error);

        var (batch, debtorAccountId) = built.Value;
        return Result.Success(MapPreview(batch, debtorAccountId));
    }

    internal static PayrollBankTransferPreviewDto MapPreview(
        PayrollBankTransferBatch batch,
        Guid? debtorAccountId) =>
        new()
        {
            PayrollRunId = batch.PayrollRunId,
            Year = batch.Year,
            Month = batch.Month,
            PeriodLabel = batch.PeriodLabel,
            TransferLabel = batch.TransferLabel,
            CompanyName = batch.Company?.Name,
            DebtorAccount = batch.DebtorAccount is null
                ? null
                : new PayrollBankTransferDebtorDto
                {
                    BankAccountId = debtorAccountId,
                    Rib = batch.DebtorAccount.Rib,
                    Iban = batch.DebtorAccount.Iban,
                    BankName = batch.DebtorAccount.BankName,
                    BankCode = batch.DebtorAccount.BankCode
                },
            EligibleCount = batch.EligibleCount,
            TotalAmount = batch.TotalAmount,
            Lines = batch.Lines.Select(l => new PayrollBankTransferLineDto
            {
                EmployeeId = l.EmployeeId,
                PayslipId = l.PayslipId,
                EmployeeNumber = l.EmployeeNumber,
                LastName = l.LastName,
                FirstName = l.FirstName,
                FullName = $"{l.FirstName} {l.LastName}".Trim(),
                Rib = l.Rib,
                Iban = l.Iban,
                NetSalary = l.NetSalary,
                TransferLabel = l.TransferLabel,
                Cin = l.Cin,
                CnssNumber = l.CnssNumber
            }).ToList(),
            ExcludedLines = batch.ExcludedLines.Select(e => new PayrollBankTransferExcludedLineDto
            {
                EmployeeId = e.EmployeeId,
                PayslipId = e.PayslipId,
                EmployeeNumber = e.EmployeeNumber,
                EmployeeName = e.EmployeeName,
                NetSalary = e.NetSalary,
                Reason = e.Reason.ToString(),
                ReasonDisplay = e.ReasonDisplay
            }).ToList(),
            Warnings = batch.Warnings.Select(w => new PayrollBankTransferWarningDto
            {
                Code = w.Code.ToString(),
                Message = w.Message
            }).ToList()
        };
}
