using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IBankReconciliationService
{
    Task<Result<BankStatementDto>> ImportStatementAsync(ImportBankStatementRequest request, CancellationToken cancellationToken = default);
    Task<Result<BankStatementDto>> GetStatementAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BankStatementDto>>> GetStatementsAsync(string? accountNumber, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<Result> ReconcileLineAsync(ReconcileLineRequest request, CancellationToken cancellationToken = default);
    Task<Result> UnreconcileLineAsync(Guid bankStatementLineId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyse un fichier de relevé bancaire (CSV/Excel/PDF/image) sans rien persister.
    /// </summary>
    Task<Result<BankStatementFilePreviewDto>> PreviewStatementFileAsync(
        byte[] content,
        BankStatementFileFormat format,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Association automatique : pour chaque ligne non rapprochée du relevé, propose l'écriture
    /// correspondante (même compte banque, même montant, bon sens, fenêtre de dates). NE PERSISTE
    /// RIEN — les propositions certaines sont pré-sélectionnées mais appliquées via <see cref="ApplyAssociationsAsync"/>.
    /// </summary>
    Task<Result<AutoAssociationResultDto>> AutoAssociateAsync(Guid statementId, CancellationToken cancellationToken = default);

    /// <summary>Rapproche en lot les paires (ligne de relevé ↔ ligne d'écriture) confirmées par l'utilisateur.</summary>
    Task<Result<ApplyAssociationsResultDto>> ApplyAssociationsAsync(Guid statementId, ApplyAssociationsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Comptabilise une ligne de relevé non rapprochée : crée l'écriture 532 ↔ contrepartie et
    /// rapproche la ligne sur la ligne du compte banque créée.
    /// </summary>
    Task<Result> CreateEntryForLineAsync(Guid bankStatementLineId, CreateEntryForLineRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// État de rapprochement d'un relevé : confronte le solde comptable ancré du compte banque au
    /// solde du relevé, liste les suspens des deux côtés et calcule l'écart (qui doit être nul).
    /// Lecture seule — aucune écriture ni persistance.
    /// </summary>
    Task<Result<BankReconciliationStatementDto>> GetReconciliationStatementAsync(
        Guid statementId, CancellationToken cancellationToken = default);
}
