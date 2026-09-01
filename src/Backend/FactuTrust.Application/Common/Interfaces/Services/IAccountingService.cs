using FactuTrust.Application.DTOs;
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
    /// Encaissé : Débit 532 / Crédit 413 (JB). Impayé : Débit 4111 / Crédit 413 (JOD, créance réouverte).
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

    /// <summary>
    /// Diagnostic (lecture seule) de toutes les écritures d'à-nouveau ACTIVES (non extournées) :
    /// recalcule pour chacune les soldes attendus (ancrés, T7/T8) et rend les écarts ligne à
    /// ligne. Aucune mutation. T8, point 3.
    /// </summary>
    Task<Result<IReadOnlyList<OpeningEntryDiagnosticDto>>> DiagnoseOpeningEntriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Répare l'à-nouveau contaminé de l'exercice clôturé <paramref name="closedFiscalYear"/> : si
    /// l'écriture active est en écart avec les soldes attendus, la supprime (si brouillon) ou
    /// l'extourne (<c>SourceOpeningBalanceReversal</c>, statut <c>Validee</c>, patron
    /// <see cref="ReverseSupplierInvoiceEntryAsync"/>) puis régénère via
    /// <see cref="GenerateOpeningEntriesAsync"/>. Idempotente : aucun écart détecté → no-op succès.
    /// À exécuter dans une transaction (<c>ITenantUnitOfWork</c>) —
    /// voir <c>RepairOpeningEntriesCommandHandler</c>. T8, point 4.
    /// </summary>
    Task<Result> RepairOpeningEntriesAsync(int closedFiscalYear, CancellationToken cancellationToken = default);

    Task<Result> GenerateFixedAssetAcquisitionEntryAsync(FixedAsset asset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Poste la dotation d'amortissement d'une ligne d'échéancier. <paramref name="entryDate"/>
    /// permet d'imposer une date d'écriture explicite (flux de cession — T3/T4) ; non fourni, le
    /// comportement historique est conservé : datée au 31/12 de l'exercice (clé) de la ligne. Pour
    /// un dossier à exercice décalé (plan « Exercices décalés », P3), le run annuel
    /// (<c>PostDepreciationRunCommandHandler</c>) calcule la fin d'exercice (ex. 30/06/N+1) et la
    /// passe explicitement — ce défaut 31/12 reste donc le chemin civil (et un repli défensif).
    /// </summary>
    Task<Result> GenerateFixedAssetDepreciationEntryAsync(
        FixedAsset asset,
        DepreciationScheduleLine scheduleLine,
        DateTime? entryDate = null,
        CancellationToken cancellationToken = default);

    Task<Result> GenerateFixedAssetDisposalEntryAsync(FixedAsset asset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Écriture d'impôt sur le résultat de la finalisation de la liasse fiscale (T10, Bug E) :
    /// débit 691 = impôt dû, débit 6912 = CSS (omise si 0), crédit 4343 = impôt total dû — au journal
    /// JOD, date 31/12/N, statut <c>Validee</c> forcé (jamais <c>NewEntryStatus</c>, sinon l'impôt
    /// resterait invisible des états NCT en brouillard). Idempotente par <c>SourceFiscalTax</c> +
    /// <paramref name="declarationId"/>. Montants négatifs refusés ; impôt total nul → no-op succès.
    /// À exécuter dans une transaction (<c>ITenantUnitOfWork</c>) couvrant l'alignement de la feuille.
    /// </summary>
    Task<Result<Guid>> GenerateFiscalTaxEntryAsync(
        int fiscalYear,
        decimal taxDue,
        decimal cssDue,
        Guid declarationId,
        CancellationToken cancellationToken = default);

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
    /// R-33 (lifecycle) : valide (poste) l'écriture OD d'un cycle encore au brouillon. Appelé à la
    /// clôture en mode Brouillard pour qu'un cycle clôturé ne laisse pas une OD brouillon modifiable
    /// et non reopenable. No-op si l'écriture est déjà validée ou inexistante.
    /// </summary>
    Task<Result> EnsurePayrollRunEntryPostedAsync(
        Guid payrollRunId,
        string validatedBy,
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

    /// <summary>
    /// Écriture de décaissement d'une avance sur salaire : débit 421 « Personnel - avances et
    /// acomptes » / crédit trésorerie.
    /// </summary>
    /// <remarks>
    /// Sans cette écriture, le versement de l'avance ne laisse aucune trace comptable : la retenue
    /// opérée le mois suivant crédite alors 421 sans contrepartie, et ce compte d'actif reste
    /// durablement créditeur. Idempotente par source (<c>EmployeeAdvance</c> + id de l'avance).
    /// No-op quand le décaissement automatique est désactivé pour le dossier.
    /// </remarks>
    Task<Result> GenerateEmployeeAdvanceDisbursementEntryAsync(
        EmployeeAdvance advance,
        string? employeeName,
        PaymentMethod method,
        BankAccount? bankAccount,
        CancellationToken cancellationToken = default);

    /// <summary>Extourne l'écriture de décaissement d'une avance lors de sa suppression.</summary>
    Task<Result> ReverseEmployeeAdvanceDisbursementEntryAsync(
        Guid advanceId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Écriture de décaissement d'un prêt salarié : débit du compte de prêts (421.1 par défaut) /
    /// crédit trésorerie. Mêmes garanties que l'avance (idempotence par source, no-op si désactivé).
    /// </summary>
    Task<Result> GenerateEmployeeLoanDisbursementEntryAsync(
        EmployeeLoan loan,
        string? employeeName,
        DateTime disbursementDate,
        PaymentMethod method,
        BankAccount? bankAccount,
        CancellationToken cancellationToken = default);

    /// <summary>Extourne l'écriture de décaissement d'un prêt lors de son annulation.</summary>
    Task<Result> ReverseEmployeeLoanDisbursementEntryAsync(
        Guid loanId,
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

    /// <summary>
    /// OD de reclassement SCE d'un cycle paie (plan §5.2.1 / WS-5) : UNE écriture de correction par
    /// période, journal JOD, libellé « Reclassement paie MM/YYYY (migration SCE) ». Débit 6611/6612
    /// &amp; crédit 647 (TFP/FOPROLOS), débit 432 &amp; crédit 437 (TFP+FOPROLOS+CSS pat), 640↔641
    /// (indemnités ordinaires) et 421↔4386 (compensation avantage en nature). Montants déterministes
    /// issus des totaux figés du cycle + lignes de l'OD legacy. Idempotente par
    /// <c>SourceEntityType="PayrollReclassification"</c> + <c>SourceEntityId=runId</c> : refuse de
    /// s'exécuter deux fois tant qu'une écriture active existe. À exécuter dans une transaction
    /// (<c>ITenantUnitOfWork</c>). Retourne l'id de l'écriture créée.
    /// </summary>
    Task<Result<Guid>> GeneratePayrollReclassificationEntryAsync(
        PayrollRun payrollRun,
        CancellationToken cancellationToken = default);
}
