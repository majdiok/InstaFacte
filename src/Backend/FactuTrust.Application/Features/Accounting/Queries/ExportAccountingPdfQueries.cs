using FactuTrust.Application.Accounting;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

// ── Export PDF de la déclaration mensuelle ─────────────────────────────────────

public sealed record ExportVatDeclarationPdfQuery(
    int Year,
    int Month,
    bool EnforceCompanySubmittedOnly = false) : IRequest<Result<byte[]>>;

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
        var declaration = await _mediator.Send(
            new GetVatDeclarationQuery(request.Year, request.Month, request.EnforceCompanySubmittedOnly),
            cancellationToken);
        if (declaration.IsFailure)
            return Result.Failure<byte[]>(declaration.Error);

        var company = await _companies.GetDefaultAsync(cancellationToken);
        var bytes = await _pdf.GenerateVatDeclarationPdfAsync(declaration.Value, company?.Name ?? "Société", cancellationToken);
        return Result.Success(bytes);
    }
}

// ── Export PDF de la liasse NCT ────────────────────────────────────────────────

/// <param name="Options">Null = chemin legacy (PDF intégral, notes agrégées). Non-null = dialogue filtré.</param>
public sealed record ExportNctStatementsPdfQuery(int FiscalYear, NctLiasseExportOptions? Options = null) : IRequest<Result<byte[]>>;

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
        var companyName = company?.Name ?? "Société";

        if (request.Options is null)
        {
            var legacyBytes = await _pdf.GenerateNctLiassePdfAsync(statements.Value, companyName, cancellationToken);
            return Result.Success(legacyBytes);
        }

        var options = request.Options;
        // Si aucune note explicite mais familles annexes cochées → toutes les notes non vides de ces familles.
        if (options.SelectedNoteNumbers.Count == 0 && options.HasAnyAnnexFamily)
        {
            var autoNotes = statements.Value.DetailedNotes
                .Where(n =>
                    (n.Family == NctAnnexFamily.Actif && options.IncludeAnnexAssets) ||
                    (n.Family == NctAnnexFamily.Passif && options.IncludeAnnexLiabilities) ||
                    (n.Family == NctAnnexFamily.IncomeStatement && options.IncludeAnnexIncomeStatement) ||
                    (n.Family == NctAnnexFamily.CashFlow && options.IncludeAnnexCashFlow))
                .Select(n => n.Number)
                .ToList();
            options = options with { SelectedNoteNumbers = autoNotes };
        }

        var filtered = NctLiasseExportFilter.Apply(statements.Value, options);
        if (filtered.IsFailure)
            return Result.Failure<byte[]>(filtered.Error);

        var bytes = await _pdf.GenerateNctLiassePdfAsync(filtered.Value, companyName, cancellationToken);
        return Result.Success(bytes);
    }
}
