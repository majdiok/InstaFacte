using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.StockTransfers.Commands;

public sealed record CreateStockTransferCommand(CreateStockTransferDto Dto) : IRequest<Result<Guid>>;

public sealed class CreateStockTransferCommandValidator : AbstractValidator<CreateStockTransferCommand>
{
    public CreateStockTransferCommandValidator()
    {
        RuleFor(x => x.Dto.SourceWarehouseId)
            .NotEmpty()
            .WithMessage("L'entrepôt source est obligatoire");

        RuleFor(x => x.Dto.DestinationWarehouseId)
            .NotEmpty()
            .WithMessage("L'entrepôt de destination est obligatoire")
            .NotEqual(x => x.Dto.SourceWarehouseId)
            .WithMessage("L'entrepôt de destination doit être différent de l'entrepôt source");

        RuleFor(x => x.Dto.TransferDate)
            .NotEmpty()
            .WithMessage("La date de transfert est obligatoire");

        RuleFor(x => x.Dto.Lines)
            .NotEmpty()
            .WithMessage("Le transfert doit contenir au moins une ligne");

        RuleForEach(x => x.Dto.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId)
                .NotEmpty()
                .WithMessage("Le produit est obligatoire");

            line.RuleFor(l => l.RequestedQuantity)
                .GreaterThan(0)
                .WithMessage("La quantité demandée doit être supérieure à zéro");
        });
    }
}

public sealed class CreateStockTransferCommandHandler : IRequestHandler<CreateStockTransferCommand, Result<Guid>>
{
    private readonly IStockTransferRepository _stockTransferRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly ILogger<CreateStockTransferCommandHandler> _logger;
    private readonly ITenantContext _tenantContext;
    private readonly IDocumentNumberService _documentNumberService;

    public CreateStockTransferCommandHandler(
        IStockTransferRepository stockTransferRepository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        ICurrentUser currentUser,
        IAuditService auditService,
        ILogger<CreateStockTransferCommandHandler> logger,
        ITenantContext tenantContext,
        IDocumentNumberService documentNumberService)
    {
        _stockTransferRepository = stockTransferRepository;
        _warehouseRepository = warehouseRepository;
        _productRepository = productRepository;
        _currentUser = currentUser;
        _auditService = auditService;
        _logger = logger;
        _tenantContext = tenantContext;
        _documentNumberService = documentNumberService;
    }

    public async Task<Result<Guid>> Handle(CreateStockTransferCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        var sourceWarehouse = await _warehouseRepository.GetByIdAsync(dto.SourceWarehouseId, cancellationToken);
        if (sourceWarehouse is null)
            return Result.Failure<Guid>(Error.NotFound("Warehouse", dto.SourceWarehouseId));
        if (!sourceWarehouse.IsActive)
            return Result.Failure<Guid>(Error.Validation("SourceWarehouse", "L'entrepôt source est désactivé"));

        var destWarehouse = await _warehouseRepository.GetByIdAsync(dto.DestinationWarehouseId, cancellationToken);
        if (destWarehouse is null)
            return Result.Failure<Guid>(Error.NotFound("Warehouse", dto.DestinationWarehouseId));
        if (!destWarehouse.IsActive)
            return Result.Failure<Guid>(Error.Validation("DestinationWarehouse", "L'entrepôt de destination est désactivé"));

        if (_tenantContext.TenantId is null || _tenantContext.TenantId == Guid.Empty)
            return Result.Failure<Guid>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        // Generate transfer number (atomic sequence)
        var year = dto.TransferDate.Year;
        var docResult = await _documentNumberService.ReserveNextAsync(
            _tenantContext.TenantId.Value,
            NumberingDocumentType.StockTransfer,
            year,
            dto.TransferDate,
            cancellationToken);
        var number = StockTransferNumber.Create(docResult.Prefix ?? "TR", docResult.Year, docResult.Sequence);

        var transferResult = StockTransfer.Create(
            number,
            dto.SourceWarehouseId,
            dto.DestinationWarehouseId,
            dto.TransferDate,
            dto.Reference,
            dto.Notes);

        if (transferResult.IsFailure)
            return Result.Failure<Guid>(transferResult.Error);

        var transfer = transferResult.Value;

        foreach (var lineDto in dto.Lines)
        {
            var product = await _productRepository.GetByIdAsync(lineDto.ProductId, cancellationToken);
            if (product is null)
                return Result.Failure<Guid>(Error.NotFound("Product", lineDto.ProductId));

            if (!product.IsActive)
                return Result.Failure<Guid>(Error.Validation("Product", $"Le produit '{product.Name}' est désactivé"));

            var addResult = transfer.AddLine(product, lineDto.RequestedQuantity, lineDto.Notes);
            if (addResult.IsFailure)
                return Result.Failure<Guid>(addResult.Error);
        }

        transfer.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");
        await _stockTransferRepository.AddAsync(transfer, cancellationToken);

        try
        {
            await _auditService.LogAsync(
                "StockTransfer.Created",
                "StockTransfer",
                transfer.Id,
                newValues: new { Number = transfer.Number.Value, LineCount = transfer.Lines.Count },
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit logging failed for StockTransfer.Created ({StockTransferId})", transfer.Id);
        }

        return Result.Success(transfer.Id);
    }
}
