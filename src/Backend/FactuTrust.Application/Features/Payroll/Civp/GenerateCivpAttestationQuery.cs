using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Payroll.Civp;

public sealed record GenerateCivpAttestationQuery(Guid ContractId) : IRequest<Result<byte[]>>;

public sealed class GenerateCivpAttestationQueryHandler : IRequestHandler<GenerateCivpAttestationQuery, Result<byte[]>>
{
    private readonly IEmployeeRepository _employees;
    private readonly ICompanyRepository _companies;
    private readonly IPdfService _pdf;
    private readonly AccountingSettings _settings;

    public GenerateCivpAttestationQueryHandler(
        IEmployeeRepository employees,
        ICompanyRepository companies,
        IPdfService pdf,
        IOptions<AccountingSettings> settings)
    {
        _employees = employees;
        _companies = companies;
        _pdf = pdf;
        _settings = settings.Value;
    }

    public async Task<Result<byte[]>> Handle(GenerateCivpAttestationQuery request, CancellationToken cancellationToken)
    {
        if (!_settings.PayrollCivpEnhancementsEnabled)
            return Result.Failure<byte[]>(Error.Validation("Feature", "Les fonctionnalités CIVP ne sont pas activées."));

        var contract = await _employees.GetContractAsync(request.ContractId, cancellationToken);
        if (contract is null)
            return Result.Failure<byte[]>(Error.NotFound("EmploymentContract", request.ContractId));

        if (contract.Type != ContractType.Sivp)
            return Result.Failure<byte[]>(Error.Validation("Type", "L'attestation CIVP concerne uniquement les contrats SIVP."));

        var employee = await _employees.GetByIdWithContractsAsync(contract.EmployeeId, cancellationToken);
        if (employee is null)
            return Result.Failure<byte[]>(Error.NotFound("Employee", contract.EmployeeId));

        var civpStart = contract.CivpStartDate ?? contract.StartDate;
        var civpEnd = contract.CivpEndDate ?? contract.EndDate;
        if (!civpEnd.HasValue)
            return Result.Failure<byte[]>(Error.Validation("CivpEndDate", "La date de fin CIVP est obligatoire."));

        var company = await _companies.GetDefaultAsync(cancellationToken);
        var dto = new CivpAttestationDto
        {
            EmployerCompanyName = company?.Name ?? "Entreprise",
            EmployerNif = company?.Nif.Value,
            EmployerAddressLine = company?.Address?.ToSingleLine(),
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            EmployeeName = employee.FullName,
            Cin = employee.Cin,
            JobTitle = contract.JobTitle,
            CivpStartDate = civpStart,
            CivpEndDate = civpEnd.Value,
            CivpStateGrant = contract.CivpStateGrant,
            CivpEmployerAllowance = contract.CivpEmployerAllowance,
            AnetiReference = contract.AnetiReference,
            DocumentReference = $"CIVP-{employee.EmployeeNumber}-{DateTime.UtcNow:yyyyMMdd}",
            GeneratedAt = DateTime.UtcNow
        };

        var bytes = await _pdf.GenerateCivpAttestationPdfAsync(dto, cancellationToken);
        return bytes.Length == 0
            ? Result.Failure<byte[]>(Error.Validation("Pdf", "Le PDF généré est vide."))
            : Result.Success(bytes);
    }
}
