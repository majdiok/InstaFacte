using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Common.Validation;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Validates invoices against Tunisian fiscal compliance requirements.
/// Based on:
/// - Code de la TVA tunisien (Loi n° 88-61)
/// - Code de commerce (obligations de facturation)
/// - Décret n° 2001-2727 sur les signatures électroniques
/// </summary>
public sealed class InvoiceComplianceValidator : IInvoiceComplianceValidator
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ILogger<InvoiceComplianceValidator> _logger;

    public InvoiceComplianceValidator(
        ICompanyRepository companyRepository,
        IClientRepository clientRepository,
        IInvoiceRepository invoiceRepository,
        ILogger<InvoiceComplianceValidator> logger)
    {
        _companyRepository = companyRepository;
        _clientRepository = clientRepository;
        _invoiceRepository = invoiceRepository;
        _logger = logger;
    }

    public async Task<WizardValidationResultDto> ValidateAsync(
        InvoiceDraft draft,
        CancellationToken cancellationToken = default)
    {
        var checks = new List<WizardValidationCheckDto>();

        // LEGAL CHECKS
        checks.Add(await CheckSellerInfoAsync(draft, cancellationToken));
        checks.Add(await CheckSellerNifAsync(draft, cancellationToken));
        checks.Add(CheckIssueDate(draft));
        checks.Add(await CheckClientInfoAsync(draft, cancellationToken));
        checks.Add(await CheckClientNifAsync(draft, cancellationToken));

        // CREDIT NOTE CHECKS
        if (draft.GetMetadata()?.Type == InvoiceType.CreditNote)
        {
            checks.Add(await CheckLinkedInvoiceAsync(draft, cancellationToken));
            checks.Add(await CheckCreditNoteAmountAsync(draft, cancellationToken));
        }

        // FISCAL CHECKS
        checks.Add(CheckVatRates(draft));
        checks.Add(CheckVatMention(draft));
        checks.Add(CheckExemptionMention(draft));
        checks.Add(CheckCurrency(draft));

        // CALCULATION CHECKS
        checks.Add(CheckHasLines(draft));
        checks.Add(CheckLineCalculations(draft));
        checks.Add(CheckTotalCalculations(draft));
        checks.Add(CheckAmountsPositive(draft));
        checks.Add(CheckDiscountLimits(draft));

        // FORMAT CHECKS
        checks.Add(CheckDateConsistency(draft));

        var errorCount = checks.Count(c => c.Status == "ERROR");
        var warningCount = checks.Count(c => c.Status == "WARNING");
        var blockingErrors = checks.Where(c => c.Status == "ERROR" && c.IsBlocking).ToList();

        var result = new WizardValidationResultDto
        {
            IsValid = errorCount == 0,
            CanProceed = blockingErrors.Count == 0,
            ErrorCount = errorCount,
            WarningCount = warningCount,
            Checks = checks
        };

        _logger.LogInformation(
            "Compliance validation for draft {DraftId}: Valid={IsValid}, CanProceed={CanProceed}, Errors={Errors}, Warnings={Warnings}",
            draft.Id, result.IsValid, result.CanProceed, errorCount, warningCount);

        return result;
    }

    public Task<WizardValidationResultDto> ValidateInvoiceAsync(
        Invoice invoice,
        CancellationToken cancellationToken = default)
    {
        var checks = new List<WizardValidationCheckDto>();

        // Invoice-specific validations
        checks.Add(CheckInvoiceNumber(invoice));
        checks.Add(CheckInvoiceClient(invoice));
        checks.Add(CheckInvoiceLines(invoice));
        checks.Add(CheckInvoiceVatRates(invoice));
        checks.Add(CheckInvoiceCalculations(invoice));

        var errorCount = checks.Count(c => c.Status == "ERROR");
        var warningCount = checks.Count(c => c.Status == "WARNING");

        return Task.FromResult(new WizardValidationResultDto
        {
            IsValid = errorCount == 0,
            CanProceed = errorCount == 0,
            ErrorCount = errorCount,
            WarningCount = warningCount,
            Checks = checks
        });
    }

    #region Draft Validation Methods

    private async Task<WizardValidationCheckDto> CheckSellerInfoAsync(
        InvoiceDraft draft, CancellationToken cancellationToken)
    {
        var isValid = draft.SellerId.HasValue;
        Company? company = null;

        if (isValid)
        {
            company = await _companyRepository.GetByIdAsync(
                draft.SellerId!.Value, cancellationToken);
            isValid = company != null && !string.IsNullOrEmpty(company.Name);
        }

        return new WizardValidationCheckDto
        {
            Id = "seller-info",
            Category = "LEGAL",
            Label = "Informations émetteur",
            Description = isValid
                ? $"Émetteur: {company?.Name}"
                : "Les informations de l'émetteur sont obligatoires",
            Status = isValid ? "VALID" : "ERROR",
            IsBlocking = true,
            Field = "seller"
        };
    }

    private async Task<WizardValidationCheckDto> CheckSellerNifAsync(
        InvoiceDraft draft, CancellationToken cancellationToken)
    {
        if (!draft.SellerId.HasValue)
        {
            return new WizardValidationCheckDto
            {
                Id = "seller-nif",
                Category = "FISCAL",
                Label = "Matricule fiscal émetteur",
                Description = "L'émetteur n'est pas sélectionné",
                Status = "ERROR",
                IsBlocking = true,
                Field = "seller.nif"
            };
        }

        var company = await _companyRepository.GetByIdAsync(
            draft.SellerId.Value, cancellationToken);

        var nif = company?.Nif?.Value;
        var isValid = TunisianValidationRules.IsValidNif(nif);

        return new WizardValidationCheckDto
        {
            Id = "seller-nif",
            Category = "FISCAL",
            Label = "Matricule fiscal émetteur",
            Description = isValid
                ? $"NIF: {nif}"
                : "Le matricule fiscal de l'émetteur est invalide ou manquant",
            Status = isValid ? "VALID" : "ERROR",
            IsBlocking = true,
            Field = "seller.nif"
        };
    }

    private WizardValidationCheckDto CheckIssueDate(InvoiceDraft draft)
    {
        var metadata = draft.GetMetadata();
        var isValid = metadata != null && metadata.IssueDate != default;

        return new WizardValidationCheckDto
        {
            Id = "issue-date",
            Category = "LEGAL",
            Label = "Date d'émission",
            Description = isValid
                ? $"Date: {metadata!.IssueDate:dd/MM/yyyy}"
                : "La date d'émission est obligatoire",
            Status = isValid ? "VALID" : "ERROR",
            IsBlocking = true,
            Field = "metadata.issueDate"
        };
    }

    private async Task<WizardValidationCheckDto> CheckClientInfoAsync(
        InvoiceDraft draft, CancellationToken cancellationToken)
    {
        string? clientName = null;
        var isValid = false;

        if (draft.ClientId.HasValue)
        {
            var client = await _clientRepository.GetByIdAsync(
                draft.ClientId.Value, cancellationToken);
            clientName = client?.Name;
            isValid = client != null && !string.IsNullOrEmpty(client.Name);
        }
        else
        {
            var newClient = draft.GetNewClient();
            clientName = newClient?.Name;
            isValid = newClient != null && !string.IsNullOrEmpty(newClient.Name);
        }

        return new WizardValidationCheckDto
        {
            Id = "client-info",
            Category = "LEGAL",
            Label = "Informations client",
            Description = isValid
                ? $"Client: {clientName}"
                : "Les informations du client sont obligatoires",
            Status = isValid ? "VALID" : "ERROR",
            IsBlocking = true,
            Field = "client"
        };
    }

    private async Task<WizardValidationCheckDto> CheckClientNifAsync(
        InvoiceDraft draft, CancellationToken cancellationToken)
    {
        string? taxType = null;
        string? nif = null;

        if (draft.ClientId.HasValue)
        {
            var client = await _clientRepository.GetByIdAsync(
                draft.ClientId.Value, cancellationToken);
            taxType = client?.Type.ToString().ToUpperInvariant();
            nif = client?.NIF?.Value;
        }
        else
        {
            var newClient = draft.GetNewClient();
            taxType = newClient?.TaxType;
            nif = newClient?.Nif;
        }

        // NIF only required for tax-subject clients
        var isTaxSubject = TunisianValidationRules.RequiresNif(taxType);

        if (!isTaxSubject)
        {
            return new WizardValidationCheckDto
            {
                Id = "client-nif",
                Category = "FISCAL",
                Label = "Matricule fiscal client",
                Description = "Non requis pour ce type de client",
                Status = "VALID",
                IsBlocking = false,
                Field = "client.nif"
            };
        }

        var hasValidNif = TunisianValidationRules.IsValidNif(nif);

        return new WizardValidationCheckDto
        {
            Id = "client-nif",
            Category = "FISCAL",
            Label = "Matricule fiscal client",
            Description = hasValidNif
                ? $"NIF: {nif}"
                : "Le matricule fiscal est obligatoire pour un client assujetti",
            Status = hasValidNif ? "VALID" : "ERROR",
            IsBlocking = true,
            Field = "client.nif"
        };
    }

    /// <summary>
    /// Validates that the linked invoice exists for credit notes.
    /// </summary>
    private async Task<WizardValidationCheckDto> CheckLinkedInvoiceAsync(
        InvoiceDraft draft, CancellationToken cancellationToken)
    {
        var metadata = draft.GetMetadata();
        var linkedInvoiceId = metadata?.LinkedInvoiceId;

        if (!linkedInvoiceId.HasValue)
        {
            return new WizardValidationCheckDto
            {
                Id = "linked-invoice",
                Category = "LEGAL",
                Label = "Facture liée",
                Description = "Une facture d'avoir doit être liée à une facture existante",
                Status = "ERROR",
                IsBlocking = true,
                Field = "metadata.linkedInvoiceId"
            };
        }

        var linkedInvoice = await _invoiceRepository.GetByIdAsync(linkedInvoiceId.Value, cancellationToken);

        if (linkedInvoice == null)
        {
            return new WizardValidationCheckDto
            {
                Id = "linked-invoice",
                Category = "LEGAL",
                Label = "Facture liée",
                Description = "La facture liée n'existe pas",
                Status = "ERROR",
                IsBlocking = true,
                Field = "metadata.linkedInvoiceId"
            };
        }

        return new WizardValidationCheckDto
        {
            Id = "linked-invoice",
            Category = "LEGAL",
            Label = "Facture liée",
            Description = $"Liée à: {linkedInvoice.Number?.Value}",
            Status = "VALID",
            IsBlocking = false,
            Field = "metadata.linkedInvoiceId"
        };
    }

    /// <summary>
    /// Validates that credit note amount doesn't exceed original invoice.
    /// </summary>
    private async Task<WizardValidationCheckDto> CheckCreditNoteAmountAsync(
        InvoiceDraft draft, CancellationToken cancellationToken)
    {
        var metadata = draft.GetMetadata();
        var linkedInvoiceId = metadata?.LinkedInvoiceId;

        if (!linkedInvoiceId.HasValue)
        {
            return new WizardValidationCheckDto
            {
                Id = "credit-note-amount",
                Category = "CALCULATION",
                Label = "Montant avoir",
                Description = "Facture liée non spécifiée",
                Status = "WARNING",
                IsBlocking = false,
                Field = "totals"
            };
        }

        var linkedInvoice = await _invoiceRepository.GetByIdAsync(linkedInvoiceId.Value, cancellationToken);
        if (linkedInvoice == null)
        {
            return new WizardValidationCheckDto
            {
                Id = "credit-note-amount",
                Category = "CALCULATION",
                Label = "Montant avoir",
                Description = "Impossible de vérifier le montant (facture liée introuvable)",
                Status = "WARNING",
                IsBlocking = false,
                Field = "totals"
            };
        }

        var lines = draft.GetLines();
        var draftTotal = lines.Sum(l =>
        {
            var subtotal = l.Quantity * l.UnitPriceHT;
            var discount = l.DiscountType == "PERCENT"
                ? subtotal * (l.DiscountValue ?? 0) / 100
                : (l.DiscountValue ?? 0);
            var lineHT = subtotal - discount;
            return lineHT + (lineHT * l.VatRate / 100);
        });

        var originalTotal = linkedInvoice.TotalAmount.Amount;
        var isValid = draftTotal <= originalTotal;

        return new WizardValidationCheckDto
        {
            Id = "credit-note-amount",
            Category = "CALCULATION",
            Label = "Montant avoir",
            Description = isValid
                ? $"Montant valide: {TunisianValidationRules.RoundToMillimes(draftTotal):N3} TND ≤ {originalTotal:N3} TND"
                : $"Le montant de l'avoir ({TunisianValidationRules.RoundToMillimes(draftTotal):N3} TND) dépasse la facture originale ({originalTotal:N3} TND)",
            Status = isValid ? "VALID" : "ERROR",
            IsBlocking = true,
            Field = "totals"
        };
    }

    private WizardValidationCheckDto CheckVatRates(InvoiceDraft draft)
    {
        var lines = draft.GetLines();
        var invalidRates = lines
            .Where(l => !TunisianValidationRules.IsValidVatRate(l.VatRate))
            .Select(l => l.VatRate)
            .Distinct()
            .ToList();

        var isValid = invalidRates.Count == 0;

        return new WizardValidationCheckDto
        {
            Id = "vat-rates",
            Category = "FISCAL",
            Label = "Taux de TVA",
            Description = isValid
                ? "Tous les taux TVA sont conformes"
                : $"Taux TVA invalides: {string.Join(", ", invalidRates)}% (autorisés: 0%, 7%, 13%, 19%)",
            Status = isValid ? "VALID" : "ERROR",
            IsBlocking = true,
            Field = "lines.vatRate"
        };
    }

    private WizardValidationCheckDto CheckVatMention(InvoiceDraft draft)
    {
        var paymentLegal = draft.GetPaymentLegal();
        var vatMention = paymentLegal?.VatMention;
        var isValid = !string.IsNullOrWhiteSpace(vatMention);

        return new WizardValidationCheckDto
        {
            Id = "vat-mention",
            Category = "LEGAL",
            Label = "Mention TVA",
            Description = isValid
                ? "Mention TVA présente"
                : "La mention \"TVA due par le vendeur\" est obligatoire",
            Status = isValid ? "VALID" : "WARNING",
            IsBlocking = false,
            Field = "paymentLegal.legalMentions.vatMention"
        };
    }

    private WizardValidationCheckDto CheckExemptionMention(InvoiceDraft draft)
    {
        string? taxType = null;

        if (draft.ClientId.HasValue)
        {
            // Would need async check - simplified here
            return new WizardValidationCheckDto
            {
                Id = "exemption-mention",
                Category = "FISCAL",
                Label = "Mention d'exonération",
                Description = "Vérification différée pour client existant",
                Status = "VALID",
                IsBlocking = false,
                Field = null
            };
        }

        var newClient = draft.GetNewClient();
        taxType = newClient?.TaxType;
        var needsExemption = TunisianValidationRules.RequiresExemptionMention(taxType);

        if (!needsExemption)
        {
            return new WizardValidationCheckDto
            {
                Id = "exemption-mention",
                Category = "FISCAL",
                Label = "Mention d'exonération",
                Description = "Non applicable",
                Status = "VALID",
                IsBlocking = false,
                Field = null
            };
        }

        var paymentLegal = draft.GetPaymentLegal();
        var hasExemption = !string.IsNullOrWhiteSpace(paymentLegal?.ExemptionMention);

        return new WizardValidationCheckDto
        {
            Id = "exemption-mention",
            Category = "FISCAL",
            Label = "Mention d'exonération",
            Description = hasExemption
                ? "Mention d'exonération présente"
                : "Une mention d'exonération est requise pour ce client",
            Status = hasExemption ? "VALID" : "ERROR",
            IsBlocking = true,
            Field = "paymentLegal.legalMentions.exemptionMention"
        };
    }

    private WizardValidationCheckDto CheckCurrency(InvoiceDraft draft)
    {
        var metadata = draft.GetMetadata();
        var currency = metadata?.Currency ?? "TND";
        var isLocal = currency == "TND";

        if (!isLocal)
        {
            return new WizardValidationCheckDto
            {
                Id = "currency",
                Category = "FISCAL",
                Label = "Devise",
                Description = $"Devise étrangère ({currency}) - L'équivalent en TND doit être mentionné sur la facture",
                Status = "WARNING",
                IsBlocking = false,
                Field = "metadata.currency"
            };
        }

        return new WizardValidationCheckDto
        {
            Id = "currency",
            Category = "FISCAL",
            Label = "Devise",
            Description = "Dinar Tunisien (TND)",
            Status = "VALID",
            IsBlocking = false,
            Field = "metadata.currency"
        };
    }

    private WizardValidationCheckDto CheckHasLines(InvoiceDraft draft)
    {
        var lines = draft.GetLines();
        var isValid = lines.Count > 0;

        return new WizardValidationCheckDto
        {
            Id = "has-lines",
            Category = "CALCULATION",
            Label = "Lignes de facturation",
            Description = isValid
                ? $"{lines.Count} ligne(s) de facturation"
                : "La facture doit contenir au moins une ligne",
            Status = isValid ? "VALID" : "ERROR",
            IsBlocking = true,
            Field = "lines"
        };
    }

    /// <summary>
    /// Validates individual line calculations with millime precision.
    /// </summary>
    private WizardValidationCheckDto CheckLineCalculations(InvoiceDraft draft)
    {
        var lines = draft.GetLines();
        if (lines.Count == 0)
        {
            return new WizardValidationCheckDto
            {
                Id = "line-calculations",
                Category = "CALCULATION",
                Label = "Calculs par ligne",
                Description = "Aucune ligne à vérifier",
                Status = "VALID",
                IsBlocking = false,
                Field = "lines"
            };
        }

        var errors = new List<string>();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var subtotal = TunisianValidationRules.RoundToMillimes(line.Quantity * line.UnitPriceHT);

            // Calculate discount
            decimal discount = 0;
            if (line.DiscountType == "PERCENT" && line.DiscountValue.HasValue)
            {
                discount = TunisianValidationRules.RoundToMillimes(subtotal * line.DiscountValue.Value / 100);
            }
            else if (line.DiscountType == "AMOUNT" && line.DiscountValue.HasValue)
            {
                discount = TunisianValidationRules.RoundToMillimes(line.DiscountValue.Value);
            }

            var lineHT = TunisianValidationRules.RoundToMillimes(subtotal - discount);
            var lineVat = TunisianValidationRules.RoundToMillimes(lineHT * line.VatRate / 100);

            // Check for negative amounts (except credit notes)
            if (lineHT < 0)
            {
                errors.Add($"Ligne {i + 1}: Montant HT négatif ({lineHT:N3} TND)");
            }

            // Check discount doesn't exceed subtotal
            if (discount > subtotal)
            {
                errors.Add($"Ligne {i + 1}: Remise ({discount:N3}) dépasse le sous-total ({subtotal:N3})");
            }
        }

        var isValid = errors.Count == 0;

        return new WizardValidationCheckDto
        {
            Id = "line-calculations",
            Category = "CALCULATION",
            Label = "Calculs par ligne",
            Description = isValid
                ? "Tous les calculs de ligne sont valides"
                : string.Join("; ", errors),
            Status = isValid ? "VALID" : "ERROR",
            IsBlocking = true,
            Field = "lines"
        };
    }

    /// <summary>
    /// Validates total calculations with server-side recalculation.
    /// </summary>
    private WizardValidationCheckDto CheckTotalCalculations(InvoiceDraft draft)
    {
        var lines = draft.GetLines();
        if (lines.Count == 0)
        {
            return new WizardValidationCheckDto
            {
                Id = "total-calculations",
                Category = "CALCULATION",
                Label = "Vérification des totaux",
                Description = "Aucune ligne à totaliser",
                Status = "VALID",
                IsBlocking = false,
                Field = "totals"
            };
        }

        // Server-side recalculation (authoritative)
        decimal serverTotalHT = 0;
        decimal serverTotalVat = 0;

        foreach (var line in lines)
        {
            var subtotal = line.Quantity * line.UnitPriceHT;
            var discount = line.DiscountType == "PERCENT"
                ? subtotal * (line.DiscountValue ?? 0) / 100
                : (line.DiscountValue ?? 0);
            var lineHT = subtotal - discount;
            var lineVat = lineHT * line.VatRate / 100;

            serverTotalHT += lineHT;
            serverTotalVat += lineVat;
        }

        serverTotalHT = TunisianValidationRules.RoundToMillimes(serverTotalHT);
        serverTotalVat = TunisianValidationRules.RoundToMillimes(serverTotalVat);
        var serverTotalTTC = TunisianValidationRules.RoundToMillimes(serverTotalHT + serverTotalVat);

        return new WizardValidationCheckDto
        {
            Id = "total-calculations",
            Category = "CALCULATION",
            Label = "Vérification des totaux",
            Description = $"HT: {serverTotalHT:N3} TND | TVA: {serverTotalVat:N3} TND | TTC: {serverTotalTTC:N3} TND",
            Status = "VALID",
            IsBlocking = false,
            Field = "totals"
        };
    }

    private WizardValidationCheckDto CheckAmountsPositive(InvoiceDraft draft)
    {
        var lines = draft.GetLines();
        var metadata = draft.GetMetadata();
        var isCreditNote = metadata?.Type == InvoiceType.CreditNote;

        // Credit notes can have "negative" representation
        if (isCreditNote)
        {
            return new WizardValidationCheckDto
            {
                Id = "amounts-positive",
                Category = "CALCULATION",
                Label = "Montants",
                Description = "Facture d'avoir - montants vérifiés",
                Status = "VALID",
                IsBlocking = false,
                Field = null
            };
        }

        var hasNegativeAmounts = lines.Any(l => l.UnitPriceHT < 0);

        return new WizardValidationCheckDto
        {
            Id = "amounts-positive",
            Category = "CALCULATION",
            Label = "Montants positifs",
            Description = hasNegativeAmounts
                ? "Les montants doivent être positifs pour une facture standard"
                : "Tous les montants sont positifs",
            Status = hasNegativeAmounts ? "ERROR" : "VALID",
            IsBlocking = true,
            Field = "lines"
        };
    }

    /// <summary>
    /// Validates that discounts don't exceed line totals.
    /// </summary>
    private WizardValidationCheckDto CheckDiscountLimits(InvoiceDraft draft)
    {
        var lines = draft.GetLines();
        var invalidDiscounts = new List<string>();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.DiscountValue.HasValue && line.DiscountValue.Value > 0)
            {
                var subtotal = line.Quantity * line.UnitPriceHT;

                if (line.DiscountType == "PERCENT" && line.DiscountValue.Value > 100)
                {
                    invalidDiscounts.Add($"Ligne {i + 1}: Remise {line.DiscountValue.Value}% > 100%");
                }
                else if (line.DiscountType == "AMOUNT" && line.DiscountValue.Value > subtotal)
                {
                    invalidDiscounts.Add($"Ligne {i + 1}: Remise {line.DiscountValue.Value:N3} > sous-total {subtotal:N3}");
                }
            }
        }

        var isValid = invalidDiscounts.Count == 0;

        return new WizardValidationCheckDto
        {
            Id = "discount-limits",
            Category = "CALCULATION",
            Label = "Limites des remises",
            Description = isValid
                ? "Toutes les remises sont dans les limites"
                : string.Join("; ", invalidDiscounts),
            Status = isValid ? "VALID" : "ERROR",
            IsBlocking = true,
            Field = "lines.discountValue"
        };
    }

    private WizardValidationCheckDto CheckDateConsistency(InvoiceDraft draft)
    {
        var metadata = draft.GetMetadata();
        if (metadata?.DueDate == null)
        {
            return new WizardValidationCheckDto
            {
                Id = "date-consistency",
                Category = "FORMAT",
                Label = "Cohérence des dates",
                Description = "Date d'échéance non définie",
                Status = "VALID",
                IsBlocking = false,
                Field = null
            };
        }

        var isValid = metadata.DueDate >= metadata.IssueDate;

        return new WizardValidationCheckDto
        {
            Id = "date-consistency",
            Category = "FORMAT",
            Label = "Cohérence des dates",
            Description = isValid
                ? "Les dates sont cohérentes"
                : "La date d'échéance doit être postérieure à la date d'émission",
            Status = isValid ? "VALID" : "ERROR",
            IsBlocking = true,
            Field = "metadata.dueDate"
        };
    }

    #endregion

    #region Invoice Validation Methods

    private WizardValidationCheckDto CheckInvoiceNumber(Invoice invoice)
    {
        var number = invoice.Number?.Value;
        var isValid = !string.IsNullOrEmpty(number);

        return new WizardValidationCheckDto
        {
            Id = "invoice-number",
            Category = "LEGAL",
            Label = "Numéro de facture",
            Description = isValid
                ? $"Numéro: {number}"
                : "Le numéro de facture est manquant",
            Status = isValid ? "VALID" : "ERROR",
            IsBlocking = true,
            Field = "number"
        };
    }

    private WizardValidationCheckDto CheckInvoiceClient(Invoice invoice)
    {
        var hasClient = invoice.Client != null;

        return new WizardValidationCheckDto
        {
            Id = "invoice-client",
            Category = "LEGAL",
            Label = "Client",
            Description = hasClient
                ? $"Client: {invoice.Client?.Name}"
                : "Le client est manquant",
            Status = hasClient ? "VALID" : "ERROR",
            IsBlocking = true,
            Field = "client"
        };
    }

    private WizardValidationCheckDto CheckInvoiceLines(Invoice invoice)
    {
        var hasLines = invoice.Lines?.Count > 0;

        return new WizardValidationCheckDto
        {
            Id = "invoice-lines",
            Category = "CALCULATION",
            Label = "Lignes de facturation",
            Description = hasLines
                ? $"{invoice.Lines?.Count} ligne(s)"
                : "La facture n'a pas de lignes",
            Status = hasLines ? "VALID" : "ERROR",
            IsBlocking = true,
            Field = "lines"
        };
    }

    private WizardValidationCheckDto CheckInvoiceVatRates(Invoice invoice)
    {
        var lines = invoice.Lines ?? new List<InvoiceLine>();
        var allValid = lines.All(l => TunisianValidationRules.IsValidVatRate((int)l.VatRate));

        return new WizardValidationCheckDto
        {
            Id = "invoice-vat-rates",
            Category = "FISCAL",
            Label = "Taux de TVA",
            Description = allValid
                ? "Taux conformes"
                : "Certains taux TVA sont non conformes",
            Status = allValid ? "VALID" : "ERROR",
            IsBlocking = true,
            Field = "lines.vatRate"
        };
    }

    private WizardValidationCheckDto CheckInvoiceCalculations(Invoice invoice)
    {
        // Verify totals match with millime precision
        var calculatedHT = invoice.Lines?.Sum(l => l.SubTotal.Amount) ?? 0;
        var calculatedVat = invoice.Lines?.Sum(l => l.VatAmount.Amount) ?? 0;
        var calculatedTTC = calculatedHT + calculatedVat;

        var htMatch = TunisianValidationRules.AmountsEqual(calculatedHT, invoice.SubTotal.Amount);
        var vatMatch = TunisianValidationRules.AmountsEqual(calculatedVat, invoice.TotalVat.Amount);
        var ttcMatch = TunisianValidationRules.AmountsEqual(calculatedTTC, invoice.TotalAmount.Amount);

        var isValid = htMatch && vatMatch && ttcMatch;

        return new WizardValidationCheckDto
        {
            Id = "invoice-calculations",
            Category = "CALCULATION",
            Label = "Calculs",
            Description = isValid
                ? "Calculs vérifiés"
                : "Incohérence dans les calculs",
            Status = isValid ? "VALID" : "ERROR",
            IsBlocking = true,
            Field = "totals"
        };
    }

    #endregion
}
