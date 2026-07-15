using System.Security.Cryptography;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.WithholdingTax.Commands;

public record ExportTejXmlCommand(TejXmlExportRequest Request) : IRequest<Result<TejXmlExportResultDto>>;

public class ExportTejXmlHandler : IRequestHandler<ExportTejXmlCommand, Result<TejXmlExportResultDto>>
{
    private readonly ISupplierInvoiceTejDeclarationBuilder _tejBuilder;
    private readonly ICompanyRepository _companyRepo;
    private readonly ITejXmlGeneratorService _xmlGenerator;
    private readonly IAuditService _audit;
    private readonly ITejXmlExportLogRepository _exportLogs;
    private readonly ICurrentUser _currentUser;

    public ExportTejXmlHandler(
        ISupplierInvoiceTejDeclarationBuilder tejBuilder,
        ICompanyRepository companyRepo,
        ITejXmlGeneratorService xmlGenerator,
        IAuditService audit,
        ITejXmlExportLogRepository exportLogs,
        ICurrentUser currentUser)
    {
        _tejBuilder = tejBuilder;
        _companyRepo = companyRepo;
        _xmlGenerator = xmlGenerator;
        _audit = audit;
        _exportLogs = exportLogs;
        _currentUser = currentUser;
    }

    public async Task<Result<TejXmlExportResultDto>> Handle(ExportTejXmlCommand command, CancellationToken ct)
    {
        var req = command.Request;
        var company = await _companyRepo.GetDefaultAsync(ct);
        if (company is null)
            return Result.Failure<TejXmlExportResultDto>(Error.Validation("Company", "Aucune société par défaut configurée"));

        var buildResult = await _tejBuilder.BuildForPeriodAsync(req.Year, req.Month, ct);
        if (buildResult.IsFailure)
            return Result.Failure<TejXmlExportResultDto>(buildResult.Error);

        var payloads = buildResult.Value;
        if (payloads.Count == 0)
        {
            return Result.Failure<TejXmlExportResultDto>(Error.Validation(
                "Declaration",
                "Aucune facture fournisseur soldée avec retenue à la source pour cette période"));
        }

        var result = await _xmlGenerator.GenerateAsync(company, payloads, req.Year, req.Month, req.SubmissionType, ct);

        var auditAction = result.IsValid
            ? AuditActions.TejExport.XmlGenerated
            : AuditActions.TejExport.ValidationFailed;

        await _audit.LogAsync(
            auditAction,
            "TejExport",
            newValues: new { req.Year, req.Month, req.SubmissionType, result.CertificateCount, result.IsValid, ErrorCount = result.ValidationErrors.Count },
            cancellationToken: ct);

        var hash = SHA256.HashData(result.XmlContent);
        var summary = result.ValidationErrors.Count > 0
            ? string.Join("; ", result.ValidationErrors.Take(5))
            : null;
        var log = TejXmlExportLog.Create(
            req.Year,
            req.Month,
            (int)req.SubmissionType,
            result.FileName,
            hash,
            result.CertificateCount,
            result.IsValid,
            summary,
            _currentUser.Email);
        await _exportLogs.AddAsync(log, ct);

        return Result.Success(result);
    }
}
