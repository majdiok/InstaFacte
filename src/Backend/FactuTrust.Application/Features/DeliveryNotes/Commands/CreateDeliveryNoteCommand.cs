using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Pricing;
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

namespace FactuTrust.Application.Features.DeliveryNotes.Commands;

/// <summary>
/// Command to create a new delivery note.
/// </summary>
public sealed record CreateDeliveryNoteCommand(CreateDeliveryNoteDto DeliveryNote) : IRequest<Result<Guid>>;

/// <summary>
/// Validator for CreateDeliveryNoteCommand.
/// Every line must reference a valid product — free-text lines are no longer allowed.
/// </summary>
public sealed class CreateDeliveryNoteCommandValidator : AbstractValidator<CreateDeliveryNoteCommand>
{
    public CreateDeliveryNoteCommandValidator()
    {
        RuleFor(x => x.DeliveryNote.ClientId)
            .NotEmpty()
            .WithMessage("Le client est obligatoire");

        RuleFor(x => x.DeliveryNote.IssueDate)
            .NotEmpty()
            .WithMessage("La date d'émission est obligatoire");

        RuleFor(x => x.DeliveryNote.DeliveryAddress)
            .NotEmpty()
            .WithMessage("L'adresse de livraison est obligatoire")
            .MaximumLength(500)
            .WithMessage("L'adresse de livraison ne peut pas dépasser 500 caractères");

        RuleFor(x => x.DeliveryNote.Lines)
            .NotEmpty()
            .WithMessage("Le bon de livraison doit contenir au moins une ligne");

        RuleForEach(x => x.DeliveryNote.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId)
                .NotEmpty()
                .WithMessage("Le produit est obligatoire pour chaque ligne");

            line.RuleFor(l => l.OrderedQuantity)
                .GreaterThan(0)
                .WithMessage("La quantité doit être supérieure à zéro");
        });
    }
}

/// <summary>
/// Handler for CreateDeliveryNoteCommand.
/// Fetches each product and snapshots its data into the delivery note line.
/// </summary>
public sealed class CreateDeliveryNoteCommandHandler : IRequestHandler<CreateDeliveryNoteCommand, Result<Guid>>
{
    private const string ContextErrorMessage = "Erreur de contexte. Assurez-vous que vous êtes connecté à une entreprise valide.";

    private readonly IDeliveryNoteRepository _deliveryNoteRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly ILogger<CreateDeliveryNoteCommandHandler> _logger;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly ITenantContext _tenantContext;
    private readonly ILinePricingOrchestrator _linePricingOrchestrator;

    public CreateDeliveryNoteCommandHandler(
        IDeliveryNoteRepository deliveryNoteRepository,
        IClientRepository clientRepository,
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditService auditService,
        ILogger<CreateDeliveryNoteCommandHandler> logger,
        IDocumentNumberService documentNumberService,
        ITenantContext tenantContext,
        ILinePricingOrchestrator linePricingOrchestrator)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
        _clientRepository = clientRepository;
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditService = auditService;
        _logger = logger;
        _documentNumberService = documentNumberService;
        _tenantContext = tenantContext;
        _linePricingOrchestrator = linePricingOrchestrator;
    }

    public async Task<Result<Guid>> Handle(CreateDeliveryNoteCommand request, CancellationToken cancellationToken)
    {
        var dto = request.DeliveryNote;

        if (dto == null)
            return Result.Failure<Guid>(Error.Validation("Request", "Les données du bon de livraison sont requises"));

        if (dto.ClientId == Guid.Empty)
            return Result.Failure<Guid>(Error.Validation("ClientId", "L'identifiant du client est invalide"));

        // Tenant/context validation is performed upstream by TenantMiddleware (returns 401/403 before
        // reaching this handler). Do NOT re-validate here — the redundant check used to mask real errors.

        // Get client
        var client = await _clientRepository.GetByIdAsync(dto.ClientId, cancellationToken);
        if (client is null)
            return Result.Failure<Guid>(Error.NotFound("Client", dto.ClientId));

        if (!client.IsActive)
            return Result.Failure<Guid>(Error.Validation("Client", "Ce client est désactivé"));

        // Validate lines
        if (dto.Lines == null || dto.Lines.Count == 0)
            return Result.Failure<Guid>(Error.Validation("Lines", "Le bon de livraison doit contenir au moins une ligne"));

        // Validate warehouse if provided
        if (dto.WarehouseId.HasValue)
        {
            var warehouse = await _warehouseRepository.GetByIdAsync(dto.WarehouseId.Value, cancellationToken);
            if (warehouse is null)
                return Result.Failure<Guid>(Error.NotFound("Warehouse", dto.WarehouseId.Value));
            if (!warehouse.IsActive)
                return Result.Failure<Guid>(Error.Validation("Warehouse", "Cet entrepôt est désactivé"));
        }

        // Generate delivery note number (atomic sequence)
        if (_tenantContext.TenantId is null || _tenantContext.TenantId == Guid.Empty)
            return Result.Failure<Guid>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        var year = dto.IssueDate.Year;
        var numberResult = await _documentNumberService.ReserveNextAsync(
            _tenantContext.TenantId.Value,
            NumberingDocumentType.DeliveryNote,
            year,
            dto.IssueDate,
            cancellationToken);

        var deliveryNoteNumber = DeliveryNoteNumber.FromRendered(
            numberResult.Value, numberResult.Year, numberResult.Sequence);

        // Create delivery note
        var deliveryNoteResult = DeliveryNote.Create(
            deliveryNoteNumber,
            client,
            dto.IssueDate,
            dto.DeliveryAddress,
            dto.DeliveryCity,
            dto.DeliveryPostalCode,
            dto.Reference,
            dto.Notes,
            dto.AllowGroupInvoicing,
            dto.WarehouseId);

        if (deliveryNoteResult.IsFailure)
            return Result.Failure<Guid>(deliveryNoteResult.Error);

        var deliveryNote = deliveryNoteResult.Value;

        // Add lines — every line MUST reference a valid, active product
        foreach (var lineDto in dto.Lines)
        {
            if (lineDto.ProductId == Guid.Empty)
                return Result.Failure<Guid>(Error.Validation("ProductId", "Le produit est obligatoire pour chaque ligne"));

            var product = await _productRepository.GetByIdAsync(lineDto.ProductId, cancellationToken);
            if (product is null)
                return Result.Failure<Guid>(Error.NotFound("Produit", lineDto.ProductId));

            if (!product.IsActive)
                return Result.Failure<Guid>(Error.Validation("Produit", $"Le produit '{product.Name}' est désactivé"));

            if (lineDto.DiscountPercent is { } discount
                && product.IsDiscountEnabled
                && product.MaxDiscountPercent.HasValue
                && discount > product.MaxDiscountPercent.Value)
            {
                return Result.Failure<Guid>(Error.Validation(
                    "DiscountPercent",
                    $"La remise ne peut pas dépasser {product.MaxDiscountPercent.Value}% pour le produit '{product.Name}'"));
            }

            var pricing = await _linePricingOrchestrator.ResolveAsync(
                dto.ClientId,
                product,
                lineDto.OrderedQuantity,
                dto.IssueDate,
                lineDto.DiscountPercent,
                priceOverride: null,
                cancellationToken);

            if (pricing.IsFailure)
                return Result.Failure<Guid>(pricing.Error);

            var addResult = deliveryNote.AddLine(
                product,
                lineDto.OrderedQuantity,
                lineDto.Notes,
                pricing.Value.DiscountPercent,
                unitPriceOverride: pricing.Value.UnitPriceHT,
                appliedPromotionId: pricing.Value.AppliedPromotion?.PromotionId,
                appliedPromotionName: pricing.Value.AppliedPromotion?.PromotionName);
            if (addResult.IsFailure)
                return Result.Failure<Guid>(addResult.Error);
        }

        // Set audit info
        deliveryNote.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");

        // Save
        try
        {
            await _deliveryNoteRepository.AddAsync(deliveryNote, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            // Log full details for diagnostics. Do NOT translate every InvalidOperationException
            // into a misleading "Erreur de contexte" — that masked real EF Core tracking bugs
            // (e.g. ProductCategory duplicate key) and triggered a false auto-logout on the frontend.
            _logger.LogError(ex, "Failed to create delivery note: {Message}", ex.Message);

            // Treat as a tenant/context error ONLY when the message explicitly references
            // the multi-tenancy context (raised by TenantDbContextFactory when ConnectionString is null).
            var isTenantContextError =
                ex.Message.Contains("contexte d'entreprise", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("Aucun contexte", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase);

            if (isTenantContextError)
                return Result.Failure<Guid>(Error.Validation("Context", ContextErrorMessage));

            // Generic persistence failure — message intentionally does NOT contain "contexte"
            // so the frontend interceptor doesn't mistake it for a tenant issue.
            return Result.Failure<Guid>(Error.Validation(
                "DeliveryNote",
                "Une erreur technique est survenue lors de l'enregistrement du bon de livraison. Veuillez réessayer."));
        }

        // Audit log
        try
        {
            await _auditService.LogAsync(
                AuditActions.DeliveryNote.Created,
                "DeliveryNote",
                deliveryNote.Id,
                newValues: new { deliveryNote.Number.Value, LineCount = deliveryNote.Lines.Count },
                cancellationToken: cancellationToken);
        }
        catch
        {
            // Non-critical operation
        }

        return Result.Success(deliveryNote.Id);
    }
}
