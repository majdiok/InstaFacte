using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.HrDocuments;

public sealed record GenerateEmploymentCertificateQuery(Guid EmployeeId) : IRequest<Result<byte[]>>;

public sealed class GenerateEmploymentCertificateQueryHandler
    : IRequestHandler<GenerateEmploymentCertificateQuery, Result<byte[]>>
{
    private readonly PayrollHrDocumentDataLoader _loader;
    private readonly IPdfService _pdf;

    public GenerateEmploymentCertificateQueryHandler(PayrollHrDocumentDataLoader loader, IPdfService pdf)
    {
        _loader = loader;
        _pdf = pdf;
    }

    public async Task<Result<byte[]>> Handle(GenerateEmploymentCertificateQuery request, CancellationToken cancellationToken)
    {
        var dtoResult = await _loader.BuildEmploymentCertificateAsync(request.EmployeeId, cancellationToken);
        if (dtoResult.IsFailure)
            return Result.Failure<byte[]>(dtoResult.Error);

        var bytes = await _pdf.GenerateEmploymentCertificatePdfAsync(dtoResult.Value, cancellationToken);
        if (bytes.Length == 0)
            return Result.Failure<byte[]>(Error.Validation("Pdf", "Le PDF généré est vide."));

        return Result.Success(bytes);
    }
}
