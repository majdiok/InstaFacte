using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Numbering;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Numbering.Commands;

public sealed record SaveNumberingSchemeCommand(NumberingDocumentType DocumentType, SaveNumberingSchemeRequest Request, int? FiscalYear = null) : IRequest<Result<NumberingSchemeDto>>;

public sealed class SaveNumberingSchemeCommandValidator : AbstractValidator<SaveNumberingSchemeCommand>
{
    public SaveNumberingSchemeCommandValidator()
    {
        RuleFor(x => x.Request.StartNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Request.Blocks).NotEmpty();
    }
}

public sealed class SaveNumberingSchemeCommandHandler : IRequestHandler<SaveNumberingSchemeCommand, Result<NumberingSchemeDto>>
{
    private readonly INumberingSchemeRepository _repository;
    private readonly IDocumentNumberService _numberService;
    private readonly IAuditService _auditService;
    private readonly ITenantContext _tenantContext;

    public SaveNumberingSchemeCommandHandler(INumberingSchemeRepository repository, IDocumentNumberService numberService, IAuditService auditService, ITenantContext tenantContext)
    {
        _repository = repository;
        _numberService = numberService;
        _auditService = auditService;
        _tenantContext = tenantContext;
    }

    public async Task<Result<NumberingSchemeDto>> Handle(SaveNumberingSchemeCommand command, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<NumberingSchemeDto>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        var fiscalYear = command.FiscalYear ?? DateTime.UtcNow.Year;
        var effectiveSequence = await _numberService.GetEffectiveCurrentSequenceAsync(
            tenantId.Value, command.DocumentType, fiscalYear, cancellationToken);

        var blocks = NumberingMappings.ToDomainBlocks(command.Request.Blocks);
        var blockValidation = NumberingFormatRenderer.ValidateBlocks(blocks);
        if (blockValidation.IsFailure) return Result.Failure<NumberingSchemeDto>(blockValidation.Error);

        var scheme = await _repository.GetByTypeAsync(tenantId.Value, command.DocumentType, fiscalYear, cancellationToken);
        var isNew = scheme is null;
        scheme ??= DocumentNumberingScheme.CreateDefault(tenantId.Value, command.DocumentType, fiscalYear, effectiveSequence);

        scheme.ReconcileCurrentSequence(effectiveSequence);

        if (!scheme.IsFormatLocked)
        {
            var formatResult = scheme.UpdateFormat(blocks);
            if (formatResult.IsFailure) return Result.Failure<NumberingSchemeDto>(formatResult.Error);
        }

        var startResult = scheme.UpdateStartNumber(command.Request.StartNumber, allowBelowCurrent: !scheme.IsFormatLocked);
        if (startResult.IsFailure) return Result.Failure<NumberingSchemeDto>(startResult.Error);

        if (isNew) await _repository.AddAsync(scheme, cancellationToken);
        else await _repository.UpdateAsync(scheme, cancellationToken);

        var preview = await _numberService.PreviewNextAsync(tenantId.Value, command.DocumentType, fiscalYear, DateTime.UtcNow, cancellationToken);
        try { await _auditService.LogAsync("NumberingScheme.Updated", "DocumentNumberingScheme", scheme.Id, newValues: new { command.DocumentType, scheme.StartNumber }, cancellationToken: cancellationToken); } catch { }
        return Result.Success(NumberingMappings.ToDto(scheme, preview, scheme.CurrentSequence));
    }
}

public sealed record ResetNumberingSchemeCommand(NumberingDocumentType DocumentType, int? FiscalYear = null) : IRequest<Result<NumberingSchemeDto>>;

public sealed class ResetNumberingSchemeCommandHandler : IRequestHandler<ResetNumberingSchemeCommand, Result<NumberingSchemeDto>>
{
    private readonly INumberingSchemeRepository _repository;
    private readonly IDocumentNumberService _numberService;
    private readonly ITenantContext _tenantContext;

    public ResetNumberingSchemeCommandHandler(INumberingSchemeRepository repository, IDocumentNumberService numberService, ITenantContext tenantContext)
    {
        _repository = repository;
        _numberService = numberService;
        _tenantContext = tenantContext;
    }

    public async Task<Result<NumberingSchemeDto>> Handle(ResetNumberingSchemeCommand command, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<NumberingSchemeDto>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        var fiscalYear = command.FiscalYear ?? DateTime.UtcNow.Year;
        var effectiveSequence = await _numberService.GetEffectiveCurrentSequenceAsync(
            tenantId.Value, command.DocumentType, fiscalYear, cancellationToken);

        var scheme = await _repository.GetByTypeAsync(tenantId.Value, command.DocumentType, fiscalYear, cancellationToken);
        if (scheme is null)
        {
            var preview = await _numberService.PreviewNextAsync(tenantId.Value, command.DocumentType, fiscalYear, DateTime.UtcNow, cancellationToken);
            return Result.Success(NumberingMappings.ToDefaultDto(command.DocumentType, fiscalYear, preview, effectiveSequence));
        }

        if (scheme.IsFormatLocked)
            return Result.Failure<NumberingSchemeDto>(Error.Validation("Format", "Le format ne peut pas etre reinitialise car des documents ont deja ete emis."));

        scheme.ResetToDefault();
        await _repository.UpdateAsync(scheme, cancellationToken);
        var example = await _numberService.PreviewNextAsync(tenantId.Value, command.DocumentType, fiscalYear, DateTime.UtcNow, cancellationToken);
        return Result.Success(NumberingMappings.ToDto(scheme, example, effectiveSequence));
    }
}
