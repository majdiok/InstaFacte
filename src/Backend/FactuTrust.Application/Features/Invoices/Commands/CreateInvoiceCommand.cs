using FactuTrust.Application.Configuration;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Pricing;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Invoices.Commands;

/// <summary>
/// Command to create a new invoice.
/// </summary>
public sealed record CreateInvoiceCommand(CreateInvoiceDto Invoice) : IRequest<Result<Guid>>;

/// <summary>
/// Validator for CreateInvoiceCommand.
/// </summary>
public sealed class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceCommandValidator()
    {
        RuleFor(x => x.Invoice.ClientId)
            .NotEmpty()
            .WithMessage("Le client est obligatoire");

        RuleFor(x => x.Invoice.IssueDate)
            .NotEmpty()
            .WithMessage("La date d'émission est obligatoire");

        RuleFor(x => x.Invoice.DueDate)
            .GreaterThanOrEqualTo(x => x.Invoice.IssueDate)
            .When(x => x.Invoice.DueDate.HasValue)
            .WithMessage("La date d'échéance doit être postérieure à la date d'émission");

        RuleFor(x => x.Invoice.Lines)
            .NotEmpty()
            .WithMessage("La facture doit contenir au moins une ligne");

        RuleForEach(x => x.Invoice.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId)
                .NotEmpty()
                .WithMessage("Le produit est obligatoire");

            line.RuleFor(l => l.Quantity)
                .GreaterThan(0)
                .WithMessage("La quantité doit être supérieure à zéro");

            line.RuleFor(l => l.DiscountPercent)
                .InclusiveBetween(0, 100)
                .When(l => l.DiscountPercent.HasValue)
                .WithMessage("La remise doit être comprise entre 0% et 100%");
        });
    }
}

/// <summary>
/// Handler for CreateInvoiceCommand.
/// </summary>
public sealed class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, Result<Guid>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly IFiscalStampResolver _fiscalStampResolver;
    private readonly ITenantContext _tenantContext;
    private readonly IPlanQuotaService _planQuota;
    private readonly IInvoiceNumberGenerator _numberGenerator;
    private readonly AccountingSettings _accountingSettings;
    private readonly IPriceResolver _priceResolver;

    public CreateInvoiceCommandHandler(
        IInvoiceRepository invoiceRepository,
        IClientRepository clientRepository,
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditService auditService,
        IFiscalStampResolver fiscalStampResolver,
        ITenantContext tenantContext,
        IPlanQuotaService planQuota,
        IInvoiceNumberGenerator numberGenerator,
        IOptions<AccountingSettings> accountingSettings,
        IPriceResolver priceResolver)
    {
        _priceResolver = priceResolver;
        _invoiceRepository = invoiceRepository;
        _clientRepository = clientRepository;
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditService = auditService;
        _fiscalStampResolver = fiscalStampResolver;
        _tenantContext = tenantContext;
        _planQuota = planQuota;
        _numberGenerator = numberGenerator;
        _accountingSettings = accountingSettings.Value;
    }

    public async Task<Result<Guid>> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Invoice;

        // Validate DTO
        if (dto == null)
            return Result.Failure<Guid>(Error.Validation("Request", "Les données de la facture sont requises"));

        // Lot C1 — Enforcement quota plan (MaxInvoicesPerMonth) AVANT toute écriture.
        var tenantIdForQuota = _tenantContext.TenantId ?? Guid.Empty;
        if (tenantIdForQuota != Guid.Empty)
        {
            var quotaCheck = await _planQuota.EnsureCanCreateInvoiceAsync(tenantIdForQuota, cancellationToken);
            if (quotaCheck.IsFailure)
                return Result.Failure<Guid>(quotaCheck.Error);
        }

        // Validate client ID
        if (dto.ClientId == Guid.Empty)
            return Result.Failure<Guid>(Error.Validation("ClientId", "L'identifiant du client est invalide"));

        // Get client
        var client = await _clientRepository.GetByIdAsync(dto.ClientId, cancellationToken);
        if (client is null)
            return Result.Failure<Guid>(Error.NotFound("Client", dto.ClientId));

        if (!client.IsActive)
            return Result.Failure<Guid>(Error.Validation("Client", "Ce client est désactivé"));

        // Validate issue date
        if (dto.IssueDate == default)
            return Result.Failure<Guid>(Error.Validation("IssueDate", "La date d'émission est requise"));

        // Validate due date if provided
        if (dto.DueDate.HasValue && dto.DueDate.Value < dto.IssueDate)
            return Result.Failure<Guid>(Error.Validation("DueDate", "La date d'échéance doit être postérieure à la date d'émission"));

        // Validate lines
        if (dto.Lines == null || dto.Lines.Count == 0)
            return Result.Failure<Guid>(Error.Validation("Lines", "La facture doit contenir au moins une ligne"));

        // Validate warehouse if provided
        if (dto.WarehouseId.HasValue)
        {
            var warehouse = await _warehouseRepository.GetByIdAsync(dto.WarehouseId.Value, cancellationToken);
            if (warehouse is null)
                return Result.Failure<Guid>(Error.NotFound("Warehouse", dto.WarehouseId.Value));
            if (!warehouse.IsActive)
                return Result.Failure<Guid>(Error.Validation("Warehouse", "Cet entrepôt est désactivé"));
        }

        // Generate invoice number (atomic sequence)
        var tenantId = _tenantContext.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<Guid>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        var year = dto.IssueDate.Year;
        var invoiceNumber = await _numberGenerator.ReserveNextNumberAsync(
            tenantId.Value, "FAC", year, cancellationToken);

        // Create invoice
        var invoiceResult = Invoice.Create(
            invoiceNumber,
            client,
            dto.IssueDate,
            dto.DueDate,
            dto.Reference,
            dto.Notes,
            dto.PaymentTerms,
            dto.WarehouseId);

        if (invoiceResult.IsFailure)
            return Result.Failure<Guid>(invoiceResult.Error);

        var invoice = invoiceResult.Value;

        // Add lines with validation
        foreach (var lineDto in dto.Lines)
        {
            // Validate product ID
            if (lineDto.ProductId == Guid.Empty)
                return Result.Failure<Guid>(Error.Validation("ProductId", "L'identifiant du produit est invalide"));

            // Validate quantity
            if (lineDto.Quantity <= 0)
                return Result.Failure<Guid>(Error.Validation("Quantity", "La quantité doit être supérieure à zéro"));

            // Get product
            var product = await _productRepository.GetByIdAsync(lineDto.ProductId, cancellationToken);
            if (product is null)
                return Result.Failure<Guid>(Error.NotFound("Produit", lineDto.ProductId));

            if (!product.IsActive)
                return Result.Failure<Guid>(Error.Validation("Produit", $"Le produit '{product.Name}' est désactivé"));

            // Create custom price if provided
            Money? customPrice = null;
            if (lineDto.CustomUnitPrice.HasValue)
            {
                if (lineDto.CustomUnitPrice.Value < 0)
                    return Result.Failure<Guid>(Error.Validation("CustomUnitPrice", "Le prix unitaire personnalisé ne peut pas être négatif"));
                
                try
                {
                    customPrice = Money.Create(lineDto.CustomUnitPrice.Value);
                }
                catch (ArgumentException ex)
                {
                    return Result.Failure<Guid>(Error.Validation("CustomUnitPrice", ex.Message));
                }
            }

            // Validate discount percent if provided
            if (lineDto.DiscountPercent.HasValue)
            {
                if (lineDto.DiscountPercent.Value < 0 || lineDto.DiscountPercent.Value > 100)
                    return Result.Failure<Guid>(Error.Validation("DiscountPercent", "La remise doit être comprise entre 0% et 100%"));

                if (product.IsDiscountEnabled && product.MaxDiscountPercent.HasValue &&
                    lineDto.DiscountPercent.Value > product.MaxDiscountPercent.Value)
                {
                    return Result.Failure<Guid>(Error.Validation(
                        "DiscountPercent",
                        $"La remise ne peut pas dépasser {product.MaxDiscountPercent.Value}% pour le produit '{product.Name}'"));
                }
            }

            // Prix forcé par l'utilisateur, sinon résolu par le point unique (prix négocié →
            // grille → catalogue). Le prix obtenu est gravé sur la ligne : la facture ne
            // bougera plus si une grille change ensuite.
            if (customPrice is null)
            {
                var priceResult = await _priceResolver.ResolveUnitPriceAsync(
                    dto.ClientId, product.Id, lineDto.Quantity, dto.IssueDate, cancellationToken);
                if (priceResult.IsFailure)
                    return Result.Failure<Guid>(priceResult.Error);

                customPrice = priceResult.Value.UnitPriceHT;
            }

            // Add line to invoice
            var addResult = invoice.AddLine(
                product,
                lineDto.Quantity,
                customPrice,
                lineDto.DiscountPercent,
                _accountingSettings.FodecRatePercent);
            if (addResult.IsFailure)
                return Result.Failure<Guid>(addResult.Error);
        }

        var stampMoney = await _fiscalStampResolver.ResolveSignedStampAsync(isCreditNote: false, cancellationToken);
        var stampResult = invoice.SetFiscalStampAmount(stampMoney);
        if (stampResult.IsFailure)
            return Result.Failure<Guid>(stampResult.Error);

        // Set audit info
        invoice.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");

        // Save (repository handles SaveChanges internally)
        // Note: The repository now properly tracks Client and Product entities to prevent PRIMARY KEY violations.
        // If any database exceptions occur, they will be handled by the ExceptionHandlingMiddleware.
        try
        {
            await _invoiceRepository.AddAsync(invoice, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Handle multi-tenancy context issues
            return Result.Failure<Guid>(Error.Validation("Context", 
                "Erreur de contexte. Assurez-vous que vous êtes connecté à une entreprise valide."));
        }

        // Audit log
        try
        {
            await _auditService.LogAsync(
                AuditActions.Invoice.Created,
                "Invoice",
                invoice.Id,
                newValues: new { invoice.Number.Value, invoice.TotalAmount.Amount },
                cancellationToken: cancellationToken);
        }
        catch
        {
            // Log audit failure but don't fail the invoice creation
            // This is a non-critical operation
        }

        // Lot C1 — Incrémente le compteur mensuel après création réussie (best-effort,
        // n'échoue jamais la facture si la Subscription est introuvable).
        try
        {
            if (tenantIdForQuota != Guid.Empty)
                await _planQuota.OnInvoiceCreatedAsync(tenantIdForQuota, cancellationToken);
        }
        catch
        {
            // Non-critique : la facture est déjà persistée. Le prochain GET réinitialisera
            // le compteur via ResetMonthlyCounter si nécessaire.
        }

        return Result.Success(invoice.Id);
    }
}
