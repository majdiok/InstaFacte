using FactuTrust.Application.Common;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Pricing;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Constants;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.InvoiceWizard.Commands;

/// <summary>
/// Command to submit a draft and create the final invoice.
/// Uses idempotency key to prevent double submissions.
/// </summary>
public sealed record SubmitInvoiceCommand(
    Guid DraftId,
    string IdempotencyKey) : IRequest<Result<InvoiceCreatedResultDto>>;

/// <summary>
/// Handler for SubmitInvoiceCommand.
/// </summary>
public sealed class SubmitInvoiceCommandHandler 
    : IRequestHandler<SubmitInvoiceCommand, Result<InvoiceCreatedResultDto>>
{
    private readonly IInvoiceDraftRepository _draftRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IClientRepository _clientRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IProductRepository _productRepository;
    private readonly IInvoiceNumberGenerator _numberGenerator;
    private readonly IInvoiceComplianceValidator _complianceValidator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IFiscalStampResolver _fiscalStampResolver;
    private readonly ILinePricingOrchestrator _linePricingOrchestrator;
    private readonly AccountingSettings _accountingSettings;
    private readonly ILogger<SubmitInvoiceCommandHandler> _logger;

    public SubmitInvoiceCommandHandler(
        IInvoiceDraftRepository draftRepository,
        IInvoiceRepository invoiceRepository,
        IClientRepository clientRepository,
        ICompanyRepository companyRepository,
        IProductRepository productRepository,
        IInvoiceNumberGenerator numberGenerator,
        IInvoiceComplianceValidator complianceValidator,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditService auditService,
        IWarehouseRepository warehouseRepository,
        IFiscalStampResolver fiscalStampResolver,
        ILinePricingOrchestrator linePricingOrchestrator,
        IOptions<AccountingSettings> accountingSettings,
        ILogger<SubmitInvoiceCommandHandler> logger)
    {
        _draftRepository = draftRepository;
        _invoiceRepository = invoiceRepository;
        _clientRepository = clientRepository;
        _companyRepository = companyRepository;
        _productRepository = productRepository;
        _numberGenerator = numberGenerator;
        _complianceValidator = complianceValidator;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditService = auditService;
        _warehouseRepository = warehouseRepository;
        _fiscalStampResolver = fiscalStampResolver;
        _linePricingOrchestrator = linePricingOrchestrator;
        _accountingSettings = accountingSettings.Value;
        _logger = logger;
    }

    public async Task<Result<InvoiceCreatedResultDto>> Handle(
        SubmitInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load and validate draft
        var draft = await _draftRepository.GetByIdAsync(command.DraftId, cancellationToken);
        
        if (draft is null)
            return Result.Failure<InvoiceCreatedResultDto>(
                Error.NotFound("Draft", command.DraftId));

        if (draft.IsExpired)
            return Result.Failure<InvoiceCreatedResultDto>(
                Error.Validation("Draft", "Ce brouillon a expiré"));

        // 2. Check for double submission
        var startResult = draft.StartSubmission(command.IdempotencyKey);
        if (startResult.IsFailure)
        {
            // If already converted, return the existing invoice
            if (draft.IsConverted && draft.ConvertedInvoiceId.HasValue)
            {
                var existingInvoice = await _invoiceRepository.GetByIdAsync(
                    draft.ConvertedInvoiceId.Value, cancellationToken);
                
                if (existingInvoice != null)
                {
                    return Result.Success(new InvoiceCreatedResultDto
                    {
                        InvoiceId = existingInvoice.Id,
                        InvoiceNumber = existingInvoice.Number.Value,
                        Status = existingInvoice.Status.ToDisplayString(),
                        CreatedAt = existingInvoice.CreatedAt
                    });
                }
            }
            
            return Result.Failure<InvoiceCreatedResultDto>(startResult.Error);
        }

        try
        {
            await _draftRepository.UpdateAsync(draft, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 3. Validate completeness
            if (!draft.IsComplete)
                return FailSubmission(draft, 
                    Error.Validation("Draft", "Le brouillon est incomplet"));

            // 4. Run compliance validation
            var validation = await _complianceValidator.ValidateAsync(draft, cancellationToken);
            if (!validation.CanProceed)
            {
                var errors = string.Join(", ", 
                    validation.Checks
                        .Where(c => c.Status == "ERROR")
                        .Select(c => c.Label));
                
                return FailSubmission(draft, 
                    Error.Validation("Compliance", $"Erreurs de conformité: {errors}"));
            }



            // 6. Get or create client
            var client = await GetOrCreateClientAsync(draft, cancellationToken);
            if (client is null)
                return FailSubmission(draft, 
                    Error.NotFound("Client", draft.ClientId ?? Guid.Empty));


            // 8. Reserve invoice number (atomic operation)
            var tenantId = _currentUser.TenantId ?? Guid.Empty;
            var metadata = draft.GetMetadata()!;
            var prefix = draft.Type == InvoiceType.CreditNote ? "AVO" : "FAC";

            if (metadata.WarehouseId is { } warehouseId)
            {
                var warehouse = await _warehouseRepository.GetByIdAsync(warehouseId, cancellationToken);
                if (warehouse is null)
                    return FailSubmission(draft, Error.NotFound("Warehouse", warehouseId));
                if (!warehouse.IsActive)
                    return FailSubmission(draft,
                        Error.Validation("Warehouse", "Cet entrepôt est désactivé"));
            }

            var invoiceNumber = await _numberGenerator.ReserveNextNumberAsync(
                tenantId, prefix, metadata.IssueDate.Year, cancellationToken);

            _logger.LogInformation(
                "Reserved invoice number {Number} for draft {DraftId}",
                invoiceNumber.Value, draft.Id);

            // 9. Create invoice — un avoir porte le lien vers la facture rectifiée, jusqu'ici
            //    présent dans les métadonnées du brouillon mais jamais reporté sur l'agrégat.
            Result<Invoice> invoiceResult;
            if (draft.Type == InvoiceType.CreditNote)
            {
                if (metadata.LinkedInvoiceId is not { } linkedInvoiceId || linkedInvoiceId == Guid.Empty)
                    return FailSubmission(draft,
                        Error.Validation("LinkedInvoiceId", "La facture d'origine est obligatoire pour un avoir"));

                invoiceResult = Invoice.CreateCreditNote(
                    invoiceNumber,
                    client,
                    metadata.IssueDate,
                    linkedInvoiceId,
                    metadata.DueDate,
                    metadata.InternalReference,
                    notes: null,
                    paymentTerms: draft.GetPaymentLegal()?.PaymentTerms,
                    warehouseId: metadata.WarehouseId);
            }
            else
            {
                invoiceResult = Invoice.Create(
                    invoiceNumber,
                    client,
                    metadata.IssueDate,
                    metadata.DueDate,
                    metadata.InternalReference,
                    notes: null,
                    paymentTerms: draft.GetPaymentLegal()?.PaymentTerms,
                    warehouseId: metadata.WarehouseId,
                    type: draft.Type);
            }

            if (invoiceResult.IsFailure)
                return FailSubmission(draft, invoiceResult.Error);

            var invoice = invoiceResult.Value;

            invoice.SetIssuerCompanyId(draft.SellerId);

            // 10. Add invoice lines
            foreach (var line in draft.GetLines())
            {
                Product? product = null;
                if (!string.IsNullOrEmpty(line.ProductId) && Guid.TryParse(line.ProductId, out var productId))
                {
                    product = await _productRepository.GetByIdAsync(productId, cancellationToken);

                    // The line references a real catalog product but it could not be resolved
                    // (deleted, or not visible in this tenant context). Falling through to a custom
                    // line would set InvoiceLine.ProductId = Guid.Empty and violate the required
                    // Products foreign key with an opaque DB error. Fail fast with an actionable
                    // message instead (mirrors CreateInvoiceCommand's product validation).
                    if (product is null)
                        return FailSubmission(draft, Error.NotFound("Produit", productId));
                }

                // Calculate discount percent from value if type is AMOUNT
                decimal? manualDiscountPercent = null;
                if (line.DiscountValue.HasValue && line.DiscountValue.Value > 0)
                {
                    if (line.DiscountType == "PERCENT")
                    {
                        manualDiscountPercent = line.DiscountValue;
                    }
                    else
                    {
                        var gross = line.Quantity * line.UnitPriceHT;
                        manualDiscountPercent = gross > 0 ? (line.DiscountValue / gross) * 100 : null;
                    }
                }

                Result addResult;
                var fodecRate = _accountingSettings.FodecRatePercent;
                if (product != null)
                {
                    Money? priceOverride = line.PriceOverridden
                        ? Money.Create(line.UnitPriceHT, metadata.Currency)
                        : null;

                    var pricing = await _linePricingOrchestrator.ResolveAsync(
                        client.Id,
                        product,
                        line.Quantity,
                        metadata.IssueDate,
                        manualDiscountPercent,
                        priceOverride,
                        cancellationToken);

                    if (pricing.IsFailure)
                        return FailSubmission(draft, pricing.Error);

                    addResult = invoice.AddLine(
                        product,
                        line.Quantity,
                        pricing.Value.UnitPriceHT,
                        pricing.Value.DiscountPercent,
                        fodecRate,
                        pricing.Value.AppliedPromotion?.PromotionId,
                        pricing.Value.AppliedPromotion?.PromotionName);
                }
                else
                {
                    var unitPrice = Money.Create(line.UnitPriceHT, metadata.Currency);

                    // Create custom line without product reference
                    // Convert numeric VAT rate to enum
                    var vatRate = line.VatRate switch
                    {
                        0 => Domain.Enums.VatRate.Exempt,
                        7 => Domain.Enums.VatRate.Reduced,
                        13 => Domain.Enums.VatRate.Intermediate,
                        19 => Domain.Enums.VatRate.Standard,
                        _ => Domain.Enums.VatRate.Standard
                    };
                    
                    addResult = invoice.AddCustomLine(
                        line.Designation,
                        line.Description,
                        line.Quantity,
                        line.Unit ?? "Unité",
                        unitPrice,
                        vatRate,
                        manualDiscountPercent,
                        line.FodecApplicable,
                        fodecRate);
                }

                if (addResult.IsFailure)
                    return FailSubmission(draft, addResult.Error);
            }

            var stampMoney = await _fiscalStampResolver.ResolveSignedStampAsync(
                draft.Type == InvoiceType.CreditNote,
                cancellationToken);
            var stampResult = invoice.SetFiscalStampAmount(stampMoney);
            if (stampResult.IsFailure)
                return FailSubmission(draft, stampResult.Error);

            // 11. Set payment info
            var paymentLegal = draft.GetPaymentLegal();
            if (paymentLegal != null)
            {
                invoice.SetPaymentInfo(
                    method: paymentLegal.PaymentMethod,
                    bankName: paymentLegal.BankName,
                    iban: paymentLegal.Iban,
                    rib: paymentLegal.Rib);

                if (!string.IsNullOrEmpty(paymentLegal.CustomMention))
                {
                    invoice.AddLegalMention(paymentLegal.CustomMention);
                }
            }

            // 10. Validate the invoice (changes status from Draft to Validated)
            var validateResult = invoice.Validate();
            if (validateResult.IsFailure)
                return FailSubmission(draft, validateResult.Error);

            invoice.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");

            // 11. Save invoice
            await _invoiceRepository.AddAsync(invoice, cancellationToken);
            
            // 12. Mark draft as converted
            draft.MarkAsConverted(invoice.Id);
            await _draftRepository.UpdateAsync(draft, cancellationToken);

            // 13. Commit transaction
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 14. Audit log
            await _auditService.LogAsync(
                AuditActions.Invoice.Created,
                "Invoice",
                invoice.Id,
                newValues: new 
                { 
                    invoice.Number.Value, 
                    invoice.TotalAmount.Amount,
                    FromDraft = draft.Id
                },
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Successfully created invoice {InvoiceNumber} (ID: {InvoiceId}) from draft {DraftId}",
                invoice.Number.Value, invoice.Id, draft.Id);

            return Result.Success(new InvoiceCreatedResultDto
            {
                InvoiceId = invoice.Id,
                InvoiceNumber = invoice.Number.Value,
                Status = invoice.Status.ToDisplayString(),
                CreatedAt = invoice.CreatedAt
            });
        }
        catch (Exception ex)
        {
            if (SqlExceptionHelper.IsSchemaDrift(ex))
            {
                var sqlEx = SqlExceptionHelper.FindSqlException(ex)!;
                _logger.LogError(ex,
                    "Schema drift on invoice submit for draft {DraftId} (SqlError {Number}): {Detail}",
                    command.DraftId, sqlEx.Number, sqlEx.Message);

                return FailSubmission(draft, Error.Validation(
                    "Submission",
                    "Le schéma de base de données n'est pas à jour pour cette entreprise. Lancez la migration."));
            }

            var detail = ex.InnerException?.Message ?? ex.Message;
            if (ex is DbUpdateException dbEx)
            {
                _logger.LogError(dbEx,
                    "Database error submitting invoice from draft {DraftId}: {Detail}. See inner exception for SQL details.",
                    command.DraftId, detail);

                // Keep the raw SQL detail in the logs only; surface a stable, non-leaky message.
                return FailSubmission(draft,
                    Error.Validation("Submission", "Erreur lors de l'enregistrement de la facture en base de données."));
            }

            _logger.LogError(ex,
                "Failed to submit invoice from draft {DraftId}: {Detail}",
                command.DraftId, detail);

            // Surface the real (already French) domain/infrastructure message instead of an opaque
            // generic string, so the wizard and the POS can show an actionable error to the user
            // (e.g. an invalid numbering format). Aligns with the standard CreateInvoiceCommand path.
            return FailSubmission(draft,
                Error.Validation("Submission", detail));
        }
    }

    private async Task<Client?> GetOrCreateClientAsync(
        InvoiceDraft draft, 
        CancellationToken cancellationToken)
    {
        if (draft.ClientId.HasValue)
        {
            return await _clientRepository.GetByIdAsync(
                draft.ClientId.Value, cancellationToken);
        }

        var newClientData = draft.GetNewClient();
        if (newClientData is null)
            return null;

        // Réutiliser l'unique "Client passager" seedé par tenant au lieu d'en créer un
        // nouveau à chaque vente POS sans client. Ciblé sur l'email passager pour ne PAS
        // modifier le comportement des autres "nouveaux clients" du wizard. Cohérent avec
        // l'invariant d'unicité d'email déjà appliqué par CreateClientCommand.
        if (!string.IsNullOrWhiteSpace(newClientData.Email) &&
            string.Equals(newClientData.Email.Trim(), DefaultPassengerClient.Email,
                StringComparison.OrdinalIgnoreCase))
        {
            var existingPassenger = await _clientRepository.GetByEmailAsync(
                newClientData.Email, cancellationToken);
            if (existingPassenger is not null)
                return existingPassenger;
            // Si introuvable (tenant ancien jamais seedé) : on laisse le code ci-dessous
            // créer UNE fois le passager ; les ventes suivantes le réutiliseront.
        }

        // Create new client
        var clientType = newClientData.TaxType switch
        {
            "TAX_SUBJECT" => ClientType.Business,
            "TAX_EXEMPT" => ClientType.Individual,
            _ => ClientType.Individual
        };

        NIF? nif = null;
        if (!string.IsNullOrEmpty(newClientData.Nif))
        {
            var nifResult = NIF.Create(newClientData.Nif);
            if (nifResult.IsSuccess)
                nif = nifResult.Value;
        }

        var addressResult = Address.Create(
            newClientData.Street ?? string.Empty,
            newClientData.City ?? string.Empty,
            newClientData.Governorate ?? string.Empty,
            newClientData.StreetLine2,
            newClientData.PostalCode,
            "Tunisie");

        if (addressResult.IsFailure)
            return null;

        var emailResult = Email.Create(newClientData.Email);
        if (emailResult.IsFailure)
            return null;

        PhoneNumber? phone = null;
        if (!string.IsNullOrEmpty(newClientData.Phone))
        {
            var phoneResult = PhoneNumber.Create(newClientData.Phone);
            if (phoneResult.IsSuccess)
                phone = phoneResult.Value;
        }

        var clientResult = Client.Create(
            newClientData.Name,
            clientType,
            addressResult.Value,
            emailResult.Value,
            nif,
            phone,
            newClientData.ContactPerson);

        if (clientResult.IsFailure)
            return null;

        var client = clientResult.Value;
        client.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");

        await _clientRepository.AddAsync(client, cancellationToken);

        _logger.LogInformation(
            "Created new client {ClientName} (ID: {ClientId}) during invoice submission",
            client.Name, client.Id);

        return client;
    }

    private Result<InvoiceCreatedResultDto> FailSubmission(InvoiceDraft draft, Error error)
    {
        draft.CancelSubmission();
        // Note: We don't save here to avoid masking the original error
        // The submission will be retryable after timeout
        
        _logger.LogWarning(
            "Invoice submission failed for draft {DraftId}: {ErrorCode} - {ErrorMessage}",
            draft.Id, error.Code, error.Description);
        
        return Result.Failure<InvoiceCreatedResultDto>(error);
    }
}

