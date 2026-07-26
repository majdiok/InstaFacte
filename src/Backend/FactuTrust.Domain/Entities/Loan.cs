using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Accounting;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Emprunt bancaire (compte 16 « Emprunts et dettes assimilées ») et son échéancier.
/// <para>
/// <b>Portée : édition.</b> L'agrégat porte le registre et le tableau d'amortissement ; les
/// échéances ne sont PAS comptabilisées automatiquement (aucun lien vers une écriture) — c'est un
/// état, pas un traitement.
/// </para>
/// </summary>
public sealed class Loan : AggregateRoot
{
    public string LoanNumber { get; private set; } = null!;
    public string Label { get; private set; } = null!;
    public string LenderName { get; private set; } = null!;
    public decimal Principal { get; private set; }
    public decimal AnnualRatePercent { get; private set; }
    public DateTime StartDate { get; private set; }
    public int InstallmentCount { get; private set; }
    public LoanPeriodicity Periodicity { get; private set; }
    public LoanAmortizationMethod Method { get; private set; }

    /// <summary>Compte de dette (classe 16).</summary>
    public string LoanAccountNumber { get; private set; } = null!;
    /// <summary>Compte de charges d'intérêts (classe 65).</summary>
    public string InterestAccountNumber { get; private set; } = null!;
    /// <summary>Compte de trésorerie de décaissement des échéances (classe 53/532).</summary>
    public string BankAccountNumber { get; private set; } = null!;

    public LoanStatus Status { get; private set; }
    public string? Notes { get; private set; }

    private readonly List<LoanScheduleLine> _scheduleLines = new();
    public IReadOnlyCollection<LoanScheduleLine> ScheduleLines => _scheduleLines.AsReadOnly();

    private Loan() { }

    /// <summary>Total des intérêts de l'échéancier (coût du crédit).</summary>
    public decimal TotalInterest => _scheduleLines.Sum(l => l.InterestAmount);

    /// <summary>Total remboursé = capital + intérêts.</summary>
    public decimal TotalRepayment => Principal + TotalInterest;

    public static Result<Loan> Create(
        string loanNumber,
        string label,
        string lenderName,
        decimal principal,
        decimal annualRatePercent,
        DateTime startDate,
        int installmentCount,
        LoanPeriodicity periodicity,
        LoanAmortizationMethod method,
        string loanAccountNumber,
        string interestAccountNumber,
        string bankAccountNumber,
        string? notes = null)
    {
        loanNumber = loanNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(loanNumber))
            return Result.Failure<Loan>(Error.Validation("LoanNumber", "Le numéro d'emprunt est obligatoire."));
        if (loanNumber.Length > 32)
            return Result.Failure<Loan>(Error.Validation("LoanNumber", "Le numéro d'emprunt ne peut pas dépasser 32 caractères."));

        label = label?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(label))
            return Result.Failure<Loan>(Error.Validation("Label", "Le libellé est obligatoire."));

        lenderName = lenderName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(lenderName))
            return Result.Failure<Loan>(Error.Validation("LenderName", "L'organisme prêteur est obligatoire."));

        if (principal <= 0m)
            return Result.Failure<Loan>(Error.Validation("Principal", "Le capital emprunté doit être strictement positif."));
        if (annualRatePercent < 0m || annualRatePercent > 100m)
            return Result.Failure<Loan>(Error.Validation("AnnualRatePercent", "Le taux annuel doit être compris entre 0 et 100 %."));
        if (installmentCount < 1 || installmentCount > 600)
            return Result.Failure<Loan>(Error.Validation("InstallmentCount", "Le nombre d'échéances doit être compris entre 1 et 600."));

        var accounts = new[]
        {
            (Value: loanAccountNumber, Field: "LoanAccountNumber", Name: "compte d'emprunt"),
            (Value: interestAccountNumber, Field: "InterestAccountNumber", Name: "compte d'intérêts"),
            (Value: bankAccountNumber, Field: "BankAccountNumber", Name: "compte de trésorerie")
        };
        foreach (var (value, field, name) in accounts)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Result.Failure<Loan>(Error.Validation(field, $"Le {name} est obligatoire."));
        }

        var loan = new Loan
        {
            LoanNumber = loanNumber,
            Label = label,
            LenderName = lenderName,
            Principal = MillimeRounding.Round(principal),
            AnnualRatePercent = annualRatePercent,
            StartDate = startDate.Date,
            InstallmentCount = installmentCount,
            Periodicity = periodicity,
            Method = method,
            LoanAccountNumber = loanAccountNumber.Trim(),
            InterestAccountNumber = interestAccountNumber.Trim(),
            BankAccountNumber = bankAccountNumber.Trim(),
            Status = LoanStatus.Active,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };

        loan.GenerateSchedule();
        return Result.Success(loan);
    }

    /// <summary>
    /// (Re)génère l'échéancier depuis les paramètres de l'emprunt. Appelé à la création ;
    /// idempotent (les lignes existantes sont remplacées).
    /// </summary>
    public void GenerateSchedule()
    {
        _scheduleLines.Clear();
        var installments = LoanScheduleCalculator.Build(
            Principal, AnnualRatePercent, InstallmentCount, Periodicity, Method, StartDate);

        foreach (var i in installments)
            _scheduleLines.Add(LoanScheduleLine.Create(Id, i));
    }

    /// <summary>Marque l'emprunt comme intégralement remboursé.</summary>
    public Result MarkRepaid()
    {
        if (Status == LoanStatus.Cancelled)
            return Result.Failure(Error.Validation("Status", "Un emprunt annulé ne peut pas être marqué remboursé."));
        Status = LoanStatus.Repaid;
        return Result.Success();
    }

    /// <summary>Annule l'emprunt (saisie erronée). Sans effet s'il est déjà annulé.</summary>
    public Result Cancel()
    {
        if (Status == LoanStatus.Repaid)
            return Result.Failure(Error.Validation("Status", "Un emprunt remboursé ne peut pas être annulé."));
        Status = LoanStatus.Cancelled;
        return Result.Success();
    }
}
