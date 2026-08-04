using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Pricing;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using MediatR;

namespace FactuTrust.Application.Features.Pricing.Commands;

// ───────────────────────────── Promotions ─────────────────────────────

public sealed record CreatePromotionCommand(
    string Name,
    DateTime StartsOn,
    DateTime EndsOn,
    PromotionDiscountType DiscountType,
    decimal? DiscountPercent,
    decimal? DiscountAmount,
    Guid? ProductId,
    Guid? ProductCategoryId,
    Guid? ClientId,
    decimal MinQuantity,
    int Priority) : IRequest<Result<Guid>>;

public sealed class CreatePromotionCommandHandler : IRequestHandler<CreatePromotionCommand, Result<Guid>>
{
    private readonly IPromotionRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public CreatePromotionCommandHandler(
        IPromotionRepository repository, ICurrentUser currentUser, IAuditService auditService)
    {
        _repository = repository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result<Guid>> Handle(CreatePromotionCommand request, CancellationToken cancellationToken)
    {
        var result = Promotion.Create(
            request.Name, request.StartsOn, request.EndsOn,
            request.DiscountType, request.DiscountPercent,
            request.DiscountAmount is > 0 ? Money.Create(request.DiscountAmount.Value) : null,
            request.ProductId, request.ProductCategoryId, request.ClientId,
            request.MinQuantity <= 0 ? 1m : request.MinQuantity,
            request.Priority);

        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        var promotion = result.Value;
        promotion.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");
        await _repository.AddAsync(promotion, cancellationToken);

        await _auditService.LogAsync(
            "Promotion.Created", "Promotion", promotion.Id,
            newValues: new { promotion.Name, promotion.StartsOn, promotion.EndsOn },
            cancellationToken: cancellationToken);

        return Result.Success(promotion.Id);
    }
}

public sealed record UpdatePromotionCommand(
    Guid Id,
    string Name,
    DateTime StartsOn,
    DateTime EndsOn,
    PromotionDiscountType DiscountType,
    decimal? DiscountPercent,
    decimal? DiscountAmount,
    decimal MinQuantity,
    int Priority,
    bool IsActive,
    Guid? ProductId,
    Guid? ProductCategoryId,
    Guid? ClientId) : IRequest<Result>;

public sealed class UpdatePromotionCommandHandler : IRequestHandler<UpdatePromotionCommand, Result>
{
    private readonly IPromotionRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public UpdatePromotionCommandHandler(
        IPromotionRepository repository, ICurrentUser currentUser, IAuditService auditService)
    {
        _repository = repository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(UpdatePromotionCommand request, CancellationToken cancellationToken)
    {
        var promotion = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (promotion is null)
            return Result.Failure(Error.NotFound("Promotion", request.Id));

        var result = promotion.Update(
            request.Name, request.StartsOn, request.EndsOn,
            request.DiscountType, request.DiscountPercent,
            request.DiscountAmount is > 0 ? Money.Create(request.DiscountAmount.Value) : null,
            request.MinQuantity, request.Priority);

        if (result.IsFailure)
            return result;

        var scopeResult = promotion.UpdateScope(request.ProductId, request.ProductCategoryId, request.ClientId);
        if (scopeResult.IsFailure)
            return scopeResult;

        // Désactiver n'affecte aucun document émis : la remise y est figée.
        if (request.IsActive) promotion.Activate(); else promotion.Deactivate();

        promotion.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);
        await _repository.UpdateAsync(promotion, cancellationToken);

        await _auditService.LogAsync(
            "Promotion.Updated", "Promotion", promotion.Id,
            newValues: new { promotion.Name, promotion.IsActive },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record DeletePromotionCommand(Guid Id) : IRequest<Result>;

public sealed class DeletePromotionCommandHandler : IRequestHandler<DeletePromotionCommand, Result>
{
    private readonly IPromotionRepository _repository;
    private readonly IAuditService _auditService;

    public DeletePromotionCommandHandler(IPromotionRepository repository, IAuditService auditService)
    {
        _repository = repository;
        _auditService = auditService;
    }

    public async Task<Result> Handle(DeletePromotionCommand request, CancellationToken cancellationToken)
    {
        var promotion = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (promotion is null)
            return Result.Failure(Error.NotFound("Promotion", request.Id));

        await _repository.DeleteAsync(promotion, cancellationToken);

        await _auditService.LogAsync(
            "Promotion.Deleted", "Promotion", request.Id,
            oldValues: new { promotion.Name },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

// ─────────────────────── Conditions de règlement ───────────────────────

public sealed record UpsertPaymentTermTemplateCommand(
    Guid? Id,
    string Name,
    int DelayDays,
    PaymentDueMode DueMode,
    int? DueDayOfMonth,
    decimal? EarlyPaymentDiscountPercent,
    int? EarlyPaymentDays,
    bool IsActive,
    bool IsDefault) : IRequest<Result<Guid>>;

public sealed class UpsertPaymentTermTemplateCommandHandler
    : IRequestHandler<UpsertPaymentTermTemplateCommand, Result<Guid>>
{
    private readonly IPaymentTermTemplateRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public UpsertPaymentTermTemplateCommandHandler(
        IPaymentTermTemplateRepository repository, ICurrentUser currentUser, IAuditService auditService)
    {
        _repository = repository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result<Guid>> Handle(
        UpsertPaymentTermTemplateCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId?.ToString() ?? "system";
        PaymentTermTemplate template;

        if (request.Id is { } id)
        {
            var existing = await _repository.GetByIdAsync(id, cancellationToken);
            if (existing is null)
                return Result.Failure<Guid>(Error.NotFound("Condition de règlement", id));

            var update = existing.Update(
                request.Name, request.DelayDays, request.DueMode, request.DueDayOfMonth,
                request.EarlyPaymentDiscountPercent, request.EarlyPaymentDays);

            if (update.IsFailure)
                return Result.Failure<Guid>(update.Error);

            template = existing;
        }
        else
        {
            var created = PaymentTermTemplate.Create(
                request.Name, request.DelayDays, request.DueMode, request.DueDayOfMonth,
                request.EarlyPaymentDiscountPercent, request.EarlyPaymentDays);

            if (created.IsFailure)
                return Result.Failure<Guid>(created.Error);

            template = created.Value;
        }

        if (request.IsActive) template.Activate(); else template.Deactivate();

        if (request.IsDefault)
        {
            // L'index unique filtré refuserait un second défaut : on libère la place AVANT.
            await _repository.ClearDefaultAsync(template.Id, cancellationToken);
            template.MarkAsDefault();
        }
        else
        {
            template.ClearDefault();
        }

        template.SetAuditInfo(userId, isUpdate: request.Id.HasValue);

        if (request.Id.HasValue)
            await _repository.UpdateAsync(template, cancellationToken);
        else
            await _repository.AddAsync(template, cancellationToken);

        await _auditService.LogAsync(
            request.Id.HasValue ? "PaymentTermTemplate.Updated" : "PaymentTermTemplate.Created",
            "PaymentTermTemplate", template.Id,
            newValues: new { template.Name, template.DelayDays, template.IsDefault },
            cancellationToken: cancellationToken);

        return Result.Success(template.Id);
    }
}

public sealed record DeletePaymentTermTemplateCommand(Guid Id) : IRequest<Result>;

public sealed class DeletePaymentTermTemplateCommandHandler
    : IRequestHandler<DeletePaymentTermTemplateCommand, Result>
{
    private readonly IPaymentTermTemplateRepository _repository;
    private readonly IAuditService _auditService;

    public DeletePaymentTermTemplateCommandHandler(
        IPaymentTermTemplateRepository repository, IAuditService auditService)
    {
        _repository = repository;
        _auditService = auditService;
    }

    public async Task<Result> Handle(DeletePaymentTermTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (template is null)
            return Result.Failure(Error.NotFound("Condition de règlement", request.Id));

        // Aucun document ne référence le modèle : il ne sert qu'à produire le texte et
        // l'échéance à la création. Supprimer n'altère donc aucun document émis.
        await _repository.DeleteAsync(template, cancellationToken);

        await _auditService.LogAsync(
            "PaymentTermTemplate.Deleted", "PaymentTermTemplate", request.Id,
            oldValues: new { template.Name },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
