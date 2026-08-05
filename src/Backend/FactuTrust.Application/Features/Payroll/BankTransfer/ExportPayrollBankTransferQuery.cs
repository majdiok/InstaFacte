using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Services.Payroll.BankTransfer;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.BankTransfer;

/// <summary>
/// Export fichier de virement bancaire (CSV standard) pour un cycle Validé/Clôturé.
/// </summary>
public sealed record ExportPayrollBankTransferQuery(
    Guid RunId,
    Guid? BankAccountId = null,
    string? TransferLabel = null,
    string? Format = "csv") : IRequest<Result<PayrollBankTransferFileDto>>;

public sealed record PayrollBankTransferFileDto(
    byte[] Content,
    string FileName,
    string ContentType);

public sealed class ExportPayrollBankTransferQueryHandler
    : IRequestHandler<ExportPayrollBankTransferQuery, Result<PayrollBankTransferFileDto>>
{
    private readonly IPayrollRunRepository _runs;
    private readonly IEmployeeRepository _employees;
    private readonly ICompanyRepository _companies;
    private readonly IBankAccountRepository _bankAccounts;
    private readonly IEmployeeGarnishmentRepository _garnishments;

    public ExportPayrollBankTransferQueryHandler(
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

    public async Task<Result<PayrollBankTransferFileDto>> Handle(
        ExportPayrollBankTransferQuery request,
        CancellationToken cancellationToken)
    {
        if (!PayrollBankTransferFormatRegistry.TryParseFormat(request.Format, out var format))
        {
            return Result.Failure<PayrollBankTransferFileDto>(Error.Validation(
                "Format",
                "Format d'export non supporté. Utilisez « csv »."));
        }

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
            return Result.Failure<PayrollBankTransferFileDto>(built.Error);

        var (batch, _) = built.Value;
        if (batch.EligibleCount == 0)
        {
            return Result.Failure<PayrollBankTransferFileDto>(Error.Validation(
                "Lines",
                "Aucun salarié éligible (RIB valide et net > 0)."));
        }

        var writer = PayrollBankTransferFormatRegistry.GetWriter(format);
        var bytes = writer.Write(batch);
        var fileName = $"virement_paie_{batch.Year}_{batch.Month:D2}.{writer.FileExtension}";

        return Result.Success(new PayrollBankTransferFileDto(bytes, fileName, writer.ContentType));
    }
}
