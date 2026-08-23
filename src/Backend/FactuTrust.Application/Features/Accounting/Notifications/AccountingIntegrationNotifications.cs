using MediatR;

namespace FactuTrust.Application.Features.Accounting.Notifications;

/// <summary>
/// Published after a client invoice payment is persisted (partial or full).
/// </summary>
public sealed record InvoicePaymentRecordedNotification(Guid PaymentId) : INotification;

/// <summary>
/// Published after a supplier invoice is created from a purchase order.
/// </summary>
public sealed record SupplierInvoiceCreatedForAccountingNotification(Guid SupplierInvoiceId) : INotification;

/// <summary>
/// Published after a supplier invoice payment is recorded.
/// </summary>
public sealed record SupplierPaymentRecordedForAccountingNotification(Guid SupplierPaymentId) : INotification;

/// <summary>
/// Published after a bank deposit (and its cash operation) is persisted.
/// </summary>
public sealed record BankDepositCreatedForAccountingNotification(Guid BankDepositId) : INotification;

/// <summary>
/// Published after a standalone cash desk operation is created.
/// </summary>
public sealed record CashOperationCreatedForAccountingNotification(Guid CashOperationId) : INotification;

/// <summary>
/// Published after a supplier invoice becomes fully paid; generates RS withholding journal entry when applicable.
/// </summary>
public sealed record SupplierInvoiceWithholdingAccountingNotification(Guid SupplierInvoiceId) : INotification;

/// <summary>
/// Published after a client effet (traite) is settled at maturity (encaissé ou impayé).
/// Génère le 2ᵉ volet comptable (532/413 ou 4111/413).
/// </summary>
public sealed record ClientEffetSettledNotification(Guid PaymentId) : INotification;

/// <summary>
/// Published after a supplier effet (traite) is paid at maturity.
/// Génère le 2ᵉ volet comptable (403/532).
/// </summary>
public sealed record SupplierEffetSettledNotification(Guid SupplierPaymentId) : INotification;
