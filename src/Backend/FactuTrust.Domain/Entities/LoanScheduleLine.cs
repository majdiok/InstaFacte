using FactuTrust.Domain.Common;
using FactuTrust.Domain.Services.Accounting;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Une échéance de l'échéancier d'un emprunt : capital restant dû, intérêt, capital remboursé,
/// annuité et solde après échéance. Ligne d'ÉTAT — aucune comptabilisation associée dans ce lot
/// (pas de <c>IsPosted</c>/<c>JournalEntryId</c> : pas de colonne morte).
/// </summary>
public sealed class LoanScheduleLine : Entity
{
    public Guid LoanId { get; private set; }
    public Loan? Loan { get; private set; }

    public int InstallmentNumber { get; private set; }
    public DateTime DueDate { get; private set; }
    /// <summary>Capital restant dû avant l'échéance.</summary>
    public decimal OpeningBalance { get; private set; }
    public decimal InterestAmount { get; private set; }
    public decimal PrincipalAmount { get; private set; }
    /// <summary>Annuité = capital remboursé + intérêt.</summary>
    public decimal InstallmentAmount { get; private set; }
    /// <summary>Capital restant dû après l'échéance (0 sur la dernière).</summary>
    public decimal ClosingBalance { get; private set; }

    private LoanScheduleLine() { }

    internal static LoanScheduleLine Create(Guid loanId, LoanInstallment installment)
    {
        return new LoanScheduleLine
        {
            LoanId = loanId,
            InstallmentNumber = installment.Number,
            DueDate = installment.DueDate,
            OpeningBalance = installment.OpeningBalance,
            InterestAmount = installment.InterestAmount,
            PrincipalAmount = installment.PrincipalAmount,
            InstallmentAmount = installment.InstallmentAmount,
            ClosingBalance = installment.ClosingBalance
        };
    }
}
