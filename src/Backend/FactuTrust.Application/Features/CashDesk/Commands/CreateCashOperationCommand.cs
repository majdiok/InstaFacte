using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Notifications;
using FactuTrust.Application.Features.CashDesk.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Options;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;

namespace FactuTrust.Application.Features.CashDesk.Commands;

/// <summary>
/// Command to create a new cash desk operation (debit or credit).
/// </summary>
public sealed record CreateCashOperationCommand(CreateCashOperationRequest Request) : IRequest<Result<CashOperationListItemDto>>;

public sealed class CreateCashOperationCommandHandler
    : IRequestHandler<CreateCashOperationCommand, Result<CashOperationListItemDto>>
{
    private readonly ICashOperationRepository _cashOperationRepository;
    private readonly ICashOperationNumberGenerator _numberGenerator;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly IPublisher _publisher;
    private readonly AccountingSettings _settings;

    public CreateCashOperationCommandHandler(
        ICashOperationRepository cashOperationRepository,
        ICashOperationNumberGenerator numberGenerator,
        ICurrentUser currentUser,
        IAuditService auditService,
        IPublisher publisher,
        IOptions<AccountingSettings> settings)
    {
        _cashOperationRepository = cashOperationRepository;
        _numberGenerator = numberGenerator;
        _currentUser = currentUser;
        _auditService = auditService;
        _publisher = publisher;
        _settings = settings.Value;
    }

    public async Task<Result<CashOperationListItemDto>> Handle(
        CreateCashOperationCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.TenantId is null)
            return Result.Failure<CashOperationListItemDto>(Error.Unauthorized("Tenant manquant"));

        if (request.Request.VatRate is not null && !_settings.CashDeskVatEnabled)
            return Result.Failure<CashOperationListItemDto>(Error.Validation("VatRate", "La TVA caisse n'est pas activée"));

        var fiscalYear = request.Request.OperationDate.Year;
        var operationNumber = await _numberGenerator.ReserveNextNumberAsync(
            _currentUser.TenantId.Value,
            fiscalYear,
            request.Request.OperationType,
            cancellationToken);

        var money = Money.Create(request.Request.Amount, Money.DefaultCurrency);

        var createResult = CashOperation.Create(
            number: operationNumber,
            operationType: request.Request.OperationType,
            operationDate: request.Request.OperationDate,
            method: request.Request.Method,
            amount: money,
            label: request.Request.Label,
            category: request.Request.Category,
            revenueCategory: request.Request.RevenueCategory,
            reference: request.Request.Reference,
            notes: request.Request.Notes,
            vatRate: request.Request.VatRate);

        if (createResult.IsFailure)
            return Result.Failure<CashOperationListItemDto>(createResult.Error);

        var operation = createResult.Value;
        operation.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: false);

        var saved = await _cashOperationRepository.AddAsync(operation, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.CashOperation.Created,
            "CashOperation",
            saved.Id,
            newValues: new
            {
                number = saved.Number.Value,
                operationType = saved.OperationType.ToString(),
                amount = saved.Amount.Amount,
                operationDate = saved.OperationDate,
                method = saved.Method.ToString(),
                category = saved.Category?.ToString(),
                revenueCategory = saved.RevenueCategory?.ToString(),
                label = saved.Label,
                reference = saved.Reference,
                vatRate = saved.VatRate?.ToString()
            },
            cancellationToken: cancellationToken);

        await _publisher.Publish(new CashOperationCreatedForAccountingNotification(saved.Id), cancellationToken);

        var dto = MapToDto(saved);
        return Result.Success(dto);
    }

    private static CashOperationListItemDto MapToDto(CashOperation op)
    {
        var vatSplit = CashOperationVatCalculator.TrySplitTtc(op.Amount.Amount, op.VatRate);

        return new()
        {
            Id = op.Id,
            OperationType = op.OperationType,
            OperationTypeDisplay = op.OperationType == CashOperationType.Debit ? "Débit" : "Crédit",
            OperationDate = op.OperationDate,
            Method = op.Method,
            MethodDisplay = op.Method.ToDisplayString(),
            Label = op.Label,
            Category = op.Category,
            CategoryDisplay = op.Category?.ToDisplayString(),
            RevenueCategory = op.RevenueCategory,
            RevenueCategoryDisplay = op.RevenueCategory?.ToDisplayString(),
            Document = op.Number.Value,
            Amount = op.Amount.Amount,
            Currency = op.Amount.Currency,
            Reference = op.Reference,
            Notes = op.Notes,
            Status = op.Status,
            Origin = op.Origin,
            SourceType = op.SourceType,
            SourceId = op.SourceId,
            SourceInvoiceId = null,
            SourceInvoiceNumber = null,
            SourceSupplierInvoiceId = null,
            SourceSupplierInvoiceNumber = null,
            VatRatePercent = vatSplit?.RatePercent,
            HtAmount = vatSplit?.Ht,
            VatAmount = vatSplit?.Vat
        };
    }
}
