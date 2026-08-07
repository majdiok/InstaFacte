using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Automatic journal posting (SCE Tunisia) with idempotency per source entity.
/// </summary>
public interface IAccountingService
{
    Task<Result> GenerateInvoiceSaleEntryAsync(Invoice invoice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts the journal entry for a credit note (facture d'avoir / AVO):
    /// debit revenue, debit VAT collectée, credit client — the mirror of the sale entry.
    /// Idempotent per <c>SourceInvoiceCreditNote</c> + invoice id.
    /// </summary>
    Task<Result> GenerateInvoiceCreditNoteEntryAsync(Invoice invoice, CancellationToken cancellationToken = default);

    Task<Result> GenerateClientPaymentEntryAsync(Payment payment, CancellationToken cancellationToken = default);
    Task<Result> ReverseInvoiceSaleEntryAsync(Guid invoiceId, string invoiceNumber, CancellationToken cancellationToken = default);
    Task<Result> GenerateSupplierInvoiceEntryAsync(SupplierInvoice invoice, CancellationToken cancellationToken = default);
    Task<Result> GenerateSupplierPaymentEntryAsync(SupplierPayment payment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Écriture d'encaissement/impayé d'un effet de commerce client à échéance (2ᵉ volet).
    /// Encaissé : Débit 532 / Crédit 412 (JB). Impayé : Débit 4111 / Crédit 412 (JOD, créance réouverte).
    /// Idempotent per <c>SourceEffetSettlement</c> + payment id.
    /// </summary>
    Task<Result> GenerateClientEffetSettlementEntryAsync(Payment payment, EffetStatus outcome, CancellationToken cancellationToken = default);

    /// <summary>
    /// Écriture de paiement d'un effet de commerce fournisseur à échéance (2ᵉ volet) :
    /// Débit 403 / Crédit 532 (JB). Idempotent per <c>SourceEffetSettlement</c> + payment id.
    /// </summary>
    Task<Result> GenerateSupplierEffetSettlementEntryAsync(SupplierPayment payment, CancellationToken cancellationToken = default);
    Task<Result> GenerateBankDepositEntryAsync(BankDeposit deposit, CancellationToken cancellationToken = default);
    Task<Result> GenerateCashOperationEntryAsync(CashOperation operation, CancellationToken cancellationToken = default);
    Task<Result> GenerateSupplierInvoiceWithholdingEntryAsync(SupplierInvoice invoice, CancellationToken cancellationToken = default);
    Task<Result> ReverseSupplierInvoiceEntryAsync(Guid supplierInvoiceId, string invoiceNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Contre-passe (extourne) MANUELLEMENT une écriture validée : crée une écriture inverse
    /// (débit/crédit permutés) dans la période d'origine si elle est ouverte, sinon à la date du jour,
    /// et marque l'écriture d'origine comme extournée. Refusé si l'écriture est en brouillon, déjà
    /// extournée, ou si le workflow d'extourne manuelle est désactivé. Retourne l'id de la contre-passation.
    /// </summary>
    Task<Result<Guid>> ReverseJournalEntryAsync(Guid entryId, string reason, CancellationToken cancellationToken = default);

    /// <summary>Nombre de lignes d'écriture (périodes OUVERTES) portant ce compte — pour l'aperçu du remplacement.</summary>
    Task<int> CountReplaceableLinesAsync(string accountNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remplace un compte par un autre sur les lignes d'écriture des périodes OUVERTES. Valide l'existence
    /// des deux comptes (nouveau actif), refuse un compte système ou l'égalité. Retourne le nombre de lignes modifiées.
    /// </summary>
    Task<Result<int>> ReplaceAccountAsync(string oldAccountNumber, string newAccountNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates opening balance entries (écritures d'à-nouveau) for the new fiscal year
    /// by carrying forward balance sheet account balances (classes 1-5) and closing
    /// P&amp;L accounts (classes 6-7) into the result account.
    /// </summary>
    Task<Result<Guid>> GenerateOpeningEntriesAsync(int closedFiscalYear, CancellationToken cancellationToken = default);

    Task<Result> GenerateFixedAssetAcquisitionEntryAsync(FixedAsset asset, CancellationToken cancellationToken = default);

    Task<Result> GenerateFixedAssetDepreciationEntryAsync(
        FixedAsset asset,
        DepreciationScheduleLine scheduleLine,
        CancellationToken cancellationToken = default);

    Task<Result> GenerateFixedAssetDisposalEntryAsync(FixedAsset asset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Écriture comptable de paie sur validation d'un cycle (640/647, 421, 432, 453).
    /// </summary>
    Task<Result> GeneratePayrollRunEntryAsync(PayrollRun payrollRun, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalide l'écriture OD d'un cycle lors de sa réouverture : suppression si elle est encore
    /// au brouillon, extourne sinon. Sans cela, un cycle rouvert puis recalculé conserverait
    /// silencieusement l'écriture aux anciens montants (la génération est idempotente par source).
    /// </summary>
    Task<Result> ReversePayrollRunEntryAsync(
        Guid payrollRunId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Écriture de décaissement paie (débit 421 / crédit trésorerie) sur enregistrement d'un paiement.
    /// </summary>
    Task<Result> GeneratePayrollPaymentEntryAsync(
        PayrollPayment payment,
        PayrollRun payrollRun,
        BankAccount? bankAccount,
        CancellationToken cancellationToken = default);

    /// <summary>Extourne l'écriture de paiement paie lors de l'annulation d'un paiement.</summary>
    Task<Result> ReversePayrollPaymentEntryAsync(
        Guid payrollPaymentId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>Écriture de décaissement CNSS (débit 453 / crédit trésorerie).</summary>
    Task<Result> GenerateCnssContributionPaymentEntryAsync(
        CnssContributionPayment payment,
        BankAccount? bankAccount,
        CancellationToken cancellationToken = default);

    /// <summary>Extourne l'écriture de versement CNSS lors de l'annulation.</summary>
    Task<Result> ReverseCnssContributionPaymentEntryAsync(
        Guid cnssContributionPaymentId,
        string reason,
        CancellationToken cancellationToken = default);
}
