using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Loans;

/// <summary>
/// Registre des emprunts et tableau d'amortissement. Portée ÉDITION : la création génère
/// l'échéancier, mais aucune échéance n'est comptabilisée automatiquement.
/// </summary>
internal static class LoanMapper
{
    public static LoanDto ToDto(Loan loan) => new()
    {
        Id = loan.Id,
        LoanNumber = loan.LoanNumber,
        Label = loan.Label,
        LenderName = loan.LenderName,
        Principal = loan.Principal,
        AnnualRatePercent = loan.AnnualRatePercent,
        StartDate = loan.StartDate,
        InstallmentCount = loan.InstallmentCount,
        Periodicity = (int)loan.Periodicity,
        Method = (int)loan.Method,
        LoanAccountNumber = loan.LoanAccountNumber,
        InterestAccountNumber = loan.InterestAccountNumber,
        BankAccountNumber = loan.BankAccountNumber,
        Status = (int)loan.Status,
        Notes = loan.Notes,
        TotalInterest = loan.TotalInterest,
        TotalRepayment = loan.TotalRepayment
    };

    public static LoanScheduleLineDto ToDto(LoanScheduleLine line) => new()
    {
        InstallmentNumber = line.InstallmentNumber,
        DueDate = line.DueDate,
        OpeningBalance = line.OpeningBalance,
        InterestAmount = line.InterestAmount,
        PrincipalAmount = line.PrincipalAmount,
        InstallmentAmount = line.InstallmentAmount,
        ClosingBalance = line.ClosingBalance
    };
}

// ── Création ────────────────────────────────────────────────────────────────────

public sealed record CreateLoanCommand(CreateLoanRequest Request) : IRequest<Result<Guid>>;

public sealed class CreateLoanCommandHandler : IRequestHandler<CreateLoanCommand, Result<Guid>>
{
    private readonly ILoanRepository _loans;
    private readonly IAuditService _auditService;

    public CreateLoanCommandHandler(ILoanRepository loans, IAuditService auditService)
    {
        _loans = loans;
        _auditService = auditService;
    }

    public async Task<Result<Guid>> Handle(CreateLoanCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;

        if (!Enum.IsDefined(typeof(LoanPeriodicity), r.Periodicity))
            return Result.Failure<Guid>(Error.Validation("Periodicity", "Périodicité inconnue."));
        if (!Enum.IsDefined(typeof(LoanAmortizationMethod), r.Method))
            return Result.Failure<Guid>(Error.Validation("Method", "Mode d'amortissement inconnu."));

        // Numéro : fourni par l'utilisateur, sinon séquentiel EMP-{année}-{n} sur l'année de départ.
        var loanNumber = r.LoanNumber?.Trim();
        if (string.IsNullOrEmpty(loanNumber))
        {
            var year = r.StartDate.Year;
            var count = await _loans.CountByYearPrefixAsync(year, cancellationToken);
            loanNumber = $"EMP-{year}-{count + 1:D3}";
        }

        var existing = await _loans.GetByLoanNumberAsync(loanNumber, cancellationToken);
        if (existing is not null)
            return Result.Failure<Guid>(Error.Conflict($"Un emprunt porte déjà le numéro {loanNumber}."));

        var create = Loan.Create(
            loanNumber, r.Label, r.LenderName, r.Principal, r.AnnualRatePercent, r.StartDate,
            r.InstallmentCount, (LoanPeriodicity)r.Periodicity, (LoanAmortizationMethod)r.Method,
            r.LoanAccountNumber, r.InterestAccountNumber, r.BankAccountNumber, r.Notes);
        if (create.IsFailure)
            return Result.Failure<Guid>(create.Error);

        var loan = create.Value;
        loan.SetAuditInfo("system", false);
        await _loans.AddAsync(loan, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.SubAccountCreated,
            "Loan",
            loan.Id,
            newValues: new { loan.LoanNumber, loan.Principal, loan.InstallmentCount, ScheduleLines = loan.ScheduleLines.Count },
            cancellationToken: cancellationToken);

        return Result.Success(loan.Id);
    }
}

// ── Consultation ────────────────────────────────────────────────────────────────

public sealed record GetLoansQuery(int Page, int PageSize, string? Search, int? Status)
    : IRequest<Result<LoanListDto>>;

public sealed class GetLoansQueryHandler : IRequestHandler<GetLoansQuery, Result<LoanListDto>>
{
    private readonly ILoanRepository _loans;

    public GetLoansQueryHandler(ILoanRepository loans)
    {
        _loans = loans;
    }

    public async Task<Result<LoanListDto>> Handle(GetLoansQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var (items, total) = await _loans.SearchAsync(page, pageSize, request.Search, request.Status, cancellationToken);

        return Result.Success(new LoanListDto
        {
            Items = items.Select(LoanMapper.ToDto).ToList(),
            TotalCount = total
        });
    }
}

public sealed record GetLoanScheduleQuery(Guid LoanId) : IRequest<Result<LoanScheduleDto>>;

public sealed class GetLoanScheduleQueryHandler : IRequestHandler<GetLoanScheduleQuery, Result<LoanScheduleDto>>
{
    private readonly ILoanRepository _loans;

    public GetLoanScheduleQueryHandler(ILoanRepository loans)
    {
        _loans = loans;
    }

    public async Task<Result<LoanScheduleDto>> Handle(GetLoanScheduleQuery request, CancellationToken cancellationToken)
    {
        var loan = await _loans.GetByIdAsync(request.LoanId, includeSchedule: true, cancellationToken);
        if (loan is null)
            return Result.Failure<LoanScheduleDto>(Error.NotFound("Loan", request.LoanId));

        var lines = loan.ScheduleLines.OrderBy(l => l.InstallmentNumber).Select(LoanMapper.ToDto).ToList();
        var totalPrincipal = lines.Sum(l => l.PrincipalAmount);

        return Result.Success(new LoanScheduleDto
        {
            Loan = LoanMapper.ToDto(loan),
            Lines = lines,
            TotalPrincipal = totalPrincipal,
            TotalInterest = lines.Sum(l => l.InterestAmount),
            TotalInstallments = lines.Sum(l => l.InstallmentAmount),
            // Contrôle : le capital remboursé couvre exactement l'emprunt et le solde final est nul.
            IsSettled = totalPrincipal == loan.Principal
                        && (lines.Count == 0 || lines[^1].ClosingBalance == 0m)
        });
    }
}

// ── Export du tableau d'amortissement ───────────────────────────────────────────

public sealed record ExportLoanScheduleQuery(Guid LoanId, Common.Enums.AccountingExportFormat Format)
    : IRequest<Result<byte[]>>;

public sealed class ExportLoanScheduleQueryHandler : IRequestHandler<ExportLoanScheduleQuery, Result<byte[]>>
{
    private readonly IMediator _mediator;
    private readonly IAccountingExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportLoanScheduleQueryHandler(
        IMediator mediator, IAccountingExportService export, IPdfService pdf, ICompanyRepository companies)
    {
        _mediator = mediator;
        _export = export;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportLoanScheduleQuery request, CancellationToken cancellationToken)
    {
        var schedule = await _mediator.Send(new GetLoanScheduleQuery(request.LoanId), cancellationToken);
        if (schedule.IsFailure)
            return Result.Failure<byte[]>(schedule.Error);

        var company = await _companies.GetDefaultAsync(cancellationToken);
        var header = new AccountingReportHeader(
            company?.Name ?? "Société", company?.VatCode,
            "Tableau d'amortissement d'emprunt",
            $"{schedule.Value.Loan.LoanNumber} — {schedule.Value.Loan.Label}");

        return request.Format switch
        {
            Common.Enums.AccountingExportFormat.Excel => _export.ExportLoanScheduleToExcel(schedule.Value),
            Common.Enums.AccountingExportFormat.Pdf => await _pdf.GenerateLoanSchedulePdfAsync(schedule.Value, header, cancellationToken),
            _ => _export.ExportLoanScheduleToCsv(schedule.Value)
        };
    }
}
