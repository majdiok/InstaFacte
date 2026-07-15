using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IBankStatementPdfImportService
{
    Task<Result<BankStatementFilePreviewDto>> PreviewAsync(
        byte[] content,
        string fileName,
        string contentType,
        BankStatementFileFormat format,
        CancellationToken cancellationToken = default);
}
