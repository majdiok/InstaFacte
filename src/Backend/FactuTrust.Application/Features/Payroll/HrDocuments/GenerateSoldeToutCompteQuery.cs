using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.HrDocuments;

public sealed record GenerateSoldeToutCompteQuery(Guid EmployeeId) : IRequest<Result<byte[]>>;

public sealed class GenerateSoldeToutCompteQueryHandler
    : IRequestHandler<GenerateSoldeToutCompteQuery, Result<byte[]>>
{
    private readonly PayrollHrDocumentDataLoader _loader;
    private readonly IPdfService _pdf;

    public GenerateSoldeToutCompteQueryHandler(PayrollHrDocumentDataLoader loader, IPdfService pdf)
    {
        _loader = loader;
        _pdf = pdf;
    }

    public async Task<Result<byte[]>> Handle(GenerateSoldeToutCompteQuery request, CancellationToken cancellationToken)
    {
        var dtoResult = await _loader.BuildSoldeToutCompteAsync(request.EmployeeId, cancellationToken);
        if (dtoResult.IsFailure)
            return Result.Failure<byte[]>(dtoResult.Error);

        var bytes = await _pdf.GenerateSoldeToutComptePdfAsync(dtoResult.Value, cancellationToken);
        if (bytes.Length == 0)
            return Result.Failure<byte[]>(Error.Validation("Pdf", "Le PDF généré est vide."));

        return Result.Success(bytes);
    }
}
