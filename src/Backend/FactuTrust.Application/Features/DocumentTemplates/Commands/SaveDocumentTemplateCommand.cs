using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.DocumentTemplates.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.DocumentTemplates.Commands;

public sealed record SaveDocumentTemplateCommand(PrintableDocumentType DocumentType, SaveDocumentTemplateRequest Request)
    : IRequest<Result<DocumentTemplatePreferenceDto>>;

public sealed class SaveDocumentTemplateCommandHandler
    : IRequestHandler<SaveDocumentTemplateCommand, Result<DocumentTemplatePreferenceDto>>
{
    private readonly IDocumentTemplatePreferenceRepository _repository;
    private readonly IDocumentTemplateRegistry _registry;
    private readonly IAuditService _auditService;
    private readonly ITenantContext _tenantContext;

    public SaveDocumentTemplateCommandHandler(
        IDocumentTemplatePreferenceRepository repository,
        IDocumentTemplateRegistry registry,
        IAuditService auditService,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _registry = registry;
        _auditService = auditService;
        _tenantContext = tenantContext;
    }

    public async Task<Result<DocumentTemplatePreferenceDto>> Handle(SaveDocumentTemplateCommand command, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<DocumentTemplatePreferenceDto>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        var templateKey = command.Request.TemplateKey?.Trim() ?? string.Empty;
        if (!_registry.Exists(templateKey))
            return Result.Failure<DocumentTemplatePreferenceDto>(Error.Validation("TemplateKey", $"Modèle inconnu : '{templateKey}'."));

        var existing = await _repository.GetByTypeAsync(tenantId.Value, command.DocumentType, cancellationToken);
        if (existing is null)
        {
            existing = DocumentTemplatePreference.Create(tenantId.Value, command.DocumentType, templateKey);
            await _repository.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.SetTemplate(templateKey);
            await _repository.UpdateAsync(existing, cancellationToken);
        }

        try
        {
            await _auditService.LogAsync("DocumentTemplate.Updated", "DocumentTemplatePreference", existing.Id,
                newValues: new { command.DocumentType, templateKey }, cancellationToken: cancellationToken);
        }
        catch { /* audit best-effort */ }

        return Result.Success(DocumentTemplateDtoFactory.Build(command.DocumentType, templateKey, _registry));
    }
}
