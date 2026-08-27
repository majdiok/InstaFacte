using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a cash desk operation: either an outflow (Debit) or an inflow (Credit).
/// Generalises the former CashExpense entity to support both directions.
/// </summary>
public sealed class CashOperation : AggregateRoot
{
    public CashOperationNumber Number { get; private set; } = null!;

    public CashOperationType OperationType { get; private set; }

    public DateTime OperationDate { get; private set; }

    public PaymentMethod Method { get; private set; }

    public Money Amount { get; private set; } = null!;

    public string Label { get; private set; } = null!;

    /// <summary>
    /// Expense category — required for Debit operations, null for Credit.
    /// </summary>
    public CashExpenseCategory? Category { get; private set; }

    /// <summary>
    /// Revenue category — required for Credit operations, null for Debit.
    /// </summary>
    public CashRevenueCategory? RevenueCategory { get; private set; }

    public string? Reference { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>
    /// Taux de TVA optionnel sur un encaissement « ventes au comptant » (Credit + CashSalesReceipt
    /// uniquement). <c>null</c> = TVA non renseignée ; <see cref="Enums.VatRate.Exempt"/> = exonération
    /// explicite. Les deux produisent une écriture à 2 lignes (cf. <c>CashOperationVatCalculator</c>
    /// et <c>AccountingService.GenerateCashOperationEntryAsync</c>).
    /// </summary>
    public VatRate? VatRate { get; private set; }

    public CashOperationStatus Status { get; private set; }
    public CashOperationOrigin Origin { get; private set; }
    public string? SourceType { get; private set; }
    public Guid? SourceId { get; private set; }

    /// <summary>Optional POS cash-register session (vacation) linked to this cash movement.</summary>
    public Guid? CashRegisterSessionId { get; private set; }

    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    private CashOperation() { }

    public static Result<CashOperation> Create(
        CashOperationNumber number,
        CashOperationType operationType,
        DateTime operationDate,
        PaymentMethod method,
        Money amount,
        string label,
        CashExpenseCategory? category = null,
        CashRevenueCategory? revenueCategory = null,
        string? reference = null,
        string? notes = null,
        VatRate? vatRate = null)
    {
        if (amount.Amount <= 0)
            return Result.Failure<CashOperation>(Error.Validation("Amount", "Le montant doit être positif"));

        if (operationDate > DateTime.UtcNow.AddDays(1))
            return Result.Failure<CashOperation>(Error.Validation("OperationDate", "La date de l'opération ne peut pas être dans le futur"));

        if (string.IsNullOrWhiteSpace(label))
            return Result.Failure<CashOperation>(Error.Validation("Label", "Le libellé est obligatoire"));

        if (label.Trim().Length > 500)
            return Result.Failure<CashOperation>(Error.Validation("Label", "Le libellé ne peut pas dépasser 500 caractères"));

        var methodIsDefined = Enum.IsDefined(typeof(PaymentMethod), method);
        if (!methodIsDefined)
            return Result.Failure<CashOperation>(Error.Validation("Method", "Le mode de paiement est invalide"));

        if (method == PaymentMethod.Traite)
            return Result.Failure<CashOperation>(Error.Validation(
                "Method",
                "Le mode de paiement « traite » n'est pas autorisé pour une opération de caisse : utilisez le règlement d'effet (413/403)."));

        var operationTypeIsDefined = Enum.IsDefined(typeof(CashOperationType), operationType);
        if (!operationTypeIsDefined)
            return Result.Failure<CashOperation>(Error.Validation("OperationType", "Le type d'opération est invalide"));

        if (operationType == CashOperationType.Debit)
        {
            if (category is null)
                return Result.Failure<CashOperation>(Error.Validation("Category", "La catégorie de dépense est obligatoire pour un débit"));

            if (!Enum.IsDefined(typeof(CashExpenseCategory), category.Value))
                return Result.Failure<CashOperation>(Error.Validation("Category", "La catégorie de dépense est invalide"));
        }

        if (operationType == CashOperationType.Credit)
        {
            if (revenueCategory is null)
                return Result.Failure<CashOperation>(Error.Validation("RevenueCategory", "La catégorie de revenu est obligatoire pour un crédit"));

            if (!Enum.IsDefined(typeof(CashRevenueCategory), revenueCategory.Value))
                return Result.Failure<CashOperation>(Error.Validation("RevenueCategory", "La catégorie de revenu est invalide"));
        }

        if (vatRate is not null)
        {
            if (!Enum.IsDefined(typeof(VatRate), vatRate.Value))
                return Result.Failure<CashOperation>(Error.Validation("VatRate", "Le taux de TVA est invalide"));

            if (operationType == CashOperationType.Debit)
                return Result.Failure<CashOperation>(Error.Validation("VatRate", "Le taux de TVA ne s'applique qu'aux encaissements"));

            // Le type est forcément Credit ici (le cas Debit a déjà retourné ci-dessus).
            if (revenueCategory != CashRevenueCategory.CashSalesReceipt)
                return Result.Failure<CashOperation>(Error.Validation("VatRate", "Le taux de TVA ne s'applique qu'aux encaissements « ventes au comptant »"));
        }

        var referenceTrimmed = reference?.Trim();
        if (string.IsNullOrWhiteSpace(referenceTrimmed))
            referenceTrimmed = null;

        if (referenceTrimmed != null && referenceTrimmed.Length > 100)
            return Result.Failure<CashOperation>(Error.Validation("Reference", "La référence ne peut pas dépasser 100 caractères"));

        var notesTrimmed = notes?.Trim();
        if (string.IsNullOrWhiteSpace(notesTrimmed))
            notesTrimmed = null;

        if (notesTrimmed != null && notesTrimmed.Length > 500)
            return Result.Failure<CashOperation>(Error.Validation("Notes", "Les notes ne peuvent pas dépasser 500 caractères"));

        var operation = new CashOperation
        {
            Number = number,
            OperationType = operationType,
            OperationDate = operationDate.Date,
            Method = method,
            Amount = amount,
            Label = label.Trim(),
            Category = operationType == CashOperationType.Debit ? category : null,
            RevenueCategory = operationType == CashOperationType.Credit ? revenueCategory : null,
            Reference = referenceTrimmed,
            Notes = notesTrimmed,
            Status = CashOperationStatus.Terminee,
            Origin = CashOperationOrigin.Manual,
            VatRate = vatRate
        };

        return Result.Success(operation);
    }

    public static Result<CashOperation> CreateFromInvoicePayment(
        CashOperationNumber number,
        Guid paymentId,
        string invoiceNumber,
        Money amount,
        DateTime paymentDate,
        string? reference = null,
        string? notes = null)
    {
        if (paymentId == Guid.Empty)
            return Result.Failure<CashOperation>(Error.Validation("PaymentId", "Le paiement source est invalide"));

        var label = string.IsNullOrWhiteSpace(invoiceNumber)
            ? "Encaissement facture"
            : $"Encaissement facture {invoiceNumber.Trim()}";

        var createResult = Create(
            number: number,
            operationType: CashOperationType.Credit,
            operationDate: paymentDate,
            method: PaymentMethod.Cash,
            amount: amount,
            label: label,
            revenueCategory: CashRevenueCategory.ClientReceivablesReceipt,
            reference: reference,
            notes: notes);

        if (createResult.IsFailure)
            return createResult;

        var operation = createResult.Value;
        operation.Origin = CashOperationOrigin.InvoicePayment;
        operation.SourceType = "Payment";
        operation.SourceId = paymentId;
        return Result.Success(operation);
    }

    /// <summary>
    /// Creates a debit cash operation when a credit note (facture d'avoir) is refunded
    /// in cash to the customer (physical outflow from the cash drawer).
    /// </summary>
    public static Result<CashOperation> CreateFromInvoiceRefund(
        CashOperationNumber number,
        Guid paymentId,
        string invoiceNumber,
        Money amount,
        DateTime paymentDate,
        string? reference = null,
        string? notes = null)
    {
        if (paymentId == Guid.Empty)
            return Result.Failure<CashOperation>(Error.Validation("PaymentId", "Le paiement source est invalide"));

        var label = string.IsNullOrWhiteSpace(invoiceNumber)
            ? "Remboursement avoir"
            : $"Remboursement avoir {invoiceNumber.Trim()}";

        var createResult = Create(
            number: number,
            operationType: CashOperationType.Debit,
            operationDate: paymentDate,
            method: PaymentMethod.Cash,
            amount: amount,
            label: label,
            category: CashExpenseCategory.Other,
            reference: reference,
            notes: notes);

        if (createResult.IsFailure)
            return createResult;

        var operation = createResult.Value;
        operation.Origin = CashOperationOrigin.InvoicePayment;
        operation.SourceType = "Payment";
        operation.SourceId = paymentId;
        return Result.Success(operation);
    }

    /// <summary>
    /// Creates a debit cash operation when a supplier invoice is paid in cash (physical outflow from the cash drawer).
    /// </summary>
    public static Result<CashOperation> CreateFromSupplierPayment(
        CashOperationNumber number,
        Guid supplierPaymentId,
        string supplierInvoiceNumber,
        Money amount,
        DateTime paymentDate,
        string? reference = null,
        string? notes = null)
    {
        if (supplierPaymentId == Guid.Empty)
            return Result.Failure<CashOperation>(Error.Validation("SupplierPaymentId", "Le paiement source est invalide"));

        var label = string.IsNullOrWhiteSpace(supplierInvoiceNumber)
            ? "Paiement facture fournisseur"
            : $"Paiement facture fournisseur {supplierInvoiceNumber.Trim()}";

        var createResult = Create(
            number: number,
            operationType: CashOperationType.Debit,
            operationDate: paymentDate,
            method: PaymentMethod.Cash,
            amount: amount,
            label: label,
            category: CashExpenseCategory.SupplierInvoicePayment,
            reference: reference,
            notes: notes);

        if (createResult.IsFailure)
            return createResult;

        var operation = createResult.Value;
        operation.Origin = CashOperationOrigin.SupplierPayment;
        operation.SourceType = "SupplierPayment";
        operation.SourceId = supplierPaymentId;
        return Result.Success(operation);
    }

    public bool CanBeCancelledDirectly() => Origin == CashOperationOrigin.Manual;

    public Result Cancel(string cancellationReason)
    {
        if (Status == CashOperationStatus.Annulee)
            return Result.Failure(Error.Validation("Status", "Cette opération a déjà été annulée"));

        if (!CanBeCancelledDirectly())
            return Result.Failure(Error.Validation("Origin", "Cette opération est gérée via sa source et ne peut pas être annulée ici"));

        if (string.IsNullOrWhiteSpace(cancellationReason))
            return Result.Failure(Error.Validation("CancellationReason", "Le motif d'annulation est obligatoire"));

        var trimmed = cancellationReason.Trim();
        if (trimmed.Length > 500)
            return Result.Failure(Error.Validation("CancellationReason", "Le motif ne peut pas dépasser 500 caractères"));

        Status = CashOperationStatus.Annulee;
        CancelledAt = DateTime.UtcNow;
        CancellationReason = trimmed;

        IncrementVersion();
        return Result.Success();
    }

    public void AssignCashRegisterSession(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("L'identifiant de session caisse est requis.", nameof(sessionId));

        CashRegisterSessionId = sessionId;
    }
}
