using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

// ── Export PDF de la déclaration mensuelle ─────────────────────────────────────

public sealed record ExportVatDeclarationPdfQuery(int Year, int Month) : IRequest<Result<byte[]>>;

public sealed class ExportVatDeclarationPdfQueryHandler : IRequestHandler<ExportVatDeclarationPdfQuery, Result<byte[]>>
{
    private readonly IMediator _mediator;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportVatDeclarationPdfQueryHandler(IMediator mediator, IPdfService pdf, ICompanyRepository companies)
    {
        _mediator = mediator;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportVatDeclarationPdfQuery request, CancellationToken cancellationToken)
    {
        var declaration = await _mediator.Send(new GetVatDeclarationQuery(request.Year, request.Month), cancellationToken);
        if (declaration.IsFailure)
            return Result.Failure<byte[]>(declaration.Error);

        var company = await _companies.GetDefaultAsync(cancellationToken);
        var bytes = await _pdf.GenerateVatDeclarationPdfAsync(declaration.Value, company?.Name ?? "Société", cancellationToken);
        return Result.Success(bytes);
    }
}

// ── Export PDF de la liasse NCT ────────────────────────────────────────────────

public sealed record ExportNctStatementsPdfQuery(int FiscalYear) : IRequest<Result<byte[]>>;

public sealed class ExportNctStatementsPdfQueryHandler : IRequestHandler<ExportNctStatementsPdfQuery, Result<byte[]>>
{
    private readonly IMediator _mediator;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportNctStatementsPdfQueryHandler(IMediator mediator, IPdfService pdf, ICompanyRepository companies)
    {
        _mediator = mediator;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportNctStatementsPdfQuery request, CancellationToken cancellationToken)
    {
        var statements = await _mediator.Send(new GetNctStatementsQuery(request.FiscalYear), cancellationToken);
        if (statements.IsFailure)
            return Result.Failure<byte[]>(statements.Error);

        var company = await _companies.GetDefaultAsync(cancellationToken);
        var bytes = await _pdf.GenerateNctLiassePdfAsync(statements.Value, company?.Name ?? "Société", cancellationToken);
        return Result.Success(bytes);
    }
}
