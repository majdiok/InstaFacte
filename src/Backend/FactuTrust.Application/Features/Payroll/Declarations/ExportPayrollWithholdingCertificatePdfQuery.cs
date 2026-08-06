using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Declarations;

/// <summary>Export PDF d'un certificat de retenue à la source pour un salarié.</summary>
public sealed record ExportPayrollWithholdingCertificatePdfQuery(int Year, Guid EmployeeId) : IRequest<Result<byte[]>>;

public sealed class ExportPayrollWithholdingCertificatePdfQueryHandler
    : IRequestHandler<ExportPayrollWithholdingCertificatePdfQuery, Result<byte[]>>
{
    private readonly IMediator _mediator;
    private readonly IPdfService _pdf;

    public ExportPayrollWithholdingCertificatePdfQueryHandler(IMediator mediator, IPdfService pdf)
    {
        _mediator = mediator;
        _pdf = pdf;
    }

    public async Task<Result<byte[]>> Handle(
        ExportPayrollWithholdingCertificatePdfQuery request,
        CancellationToken cancellationToken)
    {
        var batchResult = await _mediator.Send(
            new GeneratePayrollWithholdingCertificatesQuery(request.Year),
            cancellationToken);
        if (batchResult.IsFailure)
            return Result.Failure<byte[]>(batchResult.Error);

        var line = batchResult.Value.Lines.FirstOrDefault(l => l.EmployeeId == request.EmployeeId);
        if (line is null)
        {
            return Result.Failure<byte[]>(Error.Validation(
                "Employee",
                $"Aucun certificat pour le salarié sur l'exercice {request.Year}."));
        }

        var bytes = await _pdf.GeneratePayrollWithholdingCertificatePdfAsync(
            line, batchResult.Value, cancellationToken);

        if (bytes.Length == 0)
        {
            return Result.Failure<byte[]>(Error.Validation("Pdf", "Le PDF généré est vide."));
        }

        return Result.Success(bytes);
    }
}
