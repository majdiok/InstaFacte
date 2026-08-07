using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.HrDocuments;

public sealed record GenerateSalaryCertificateQuery(Guid EmployeeId, int Months) : IRequest<Result<byte[]>>;

public sealed class GenerateSalaryCertificateQueryHandler
    : IRequestHandler<GenerateSalaryCertificateQuery, Result<byte[]>>
{
    private readonly PayrollHrDocumentDataLoader _loader;
    private readonly IPdfService _pdf;

    public GenerateSalaryCertificateQueryHandler(PayrollHrDocumentDataLoader loader, IPdfService pdf)
    {
        _loader = loader;
        _pdf = pdf;
    }

    public async Task<Result<byte[]>> Handle(GenerateSalaryCertificateQuery request, CancellationToken cancellationToken)
    {
        var dtoResult = await _loader.BuildSalaryCertificateAsync(request.EmployeeId, request.Months, cancellationToken);
        if (dtoResult.IsFailure)
            return Result.Failure<byte[]>(dtoResult.Error);

        var bytes = await _pdf.GenerateSalaryCertificatePdfAsync(dtoResult.Value, cancellationToken);
        if (bytes.Length == 0)
            return Result.Failure<byte[]>(Error.Validation("Pdf", "Le PDF généré est vide."));

        return Result.Success(bytes);
    }
}
