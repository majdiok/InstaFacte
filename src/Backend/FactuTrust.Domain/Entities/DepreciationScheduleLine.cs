using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// One depreciation period line (tableau d'amortissement — format CP17).
/// </summary>
public sealed class DepreciationScheduleLine : Entity
{
    public Guid FixedAssetId { get; private set; }
    public FixedAsset? FixedAsset { get; private set; }
    public int FiscalYear { get; private set; }
    public int? PeriodMonth { get; private set; }
    public decimal OpeningNbv { get; private set; }
    public decimal NormalAnnualAmount { get; private set; }
    public decimal PriorAccumulatedDepreciation { get; private set; }
    public decimal DepreciationAmount { get; private set; }
    public decimal AccumulatedDepreciation { get; private set; }
    public decimal ClosingNbv { get; private set; }
    public bool IsPosted { get; private set; }
    public Guid? JournalEntryId { get; private set; }
    public Guid? AccountingPeriodId { get; private set; }

    private DepreciationScheduleLine() { }

    public static Result<DepreciationScheduleLine> Create(
        Guid fixedAssetId,
        int fiscalYear,
        int? periodMonth,
        decimal openingNbv,
        decimal normalAnnualAmount,
        decimal priorAccumulatedDepreciation,
        decimal depreciationAmount,
        decimal accumulatedDepreciation,
        decimal closingNbv)
    {
        if (fixedAssetId == Guid.Empty)
            return Result.Failure<DepreciationScheduleLine>(Error.Validation("FixedAssetId", "L'immobilisation est obligatoire"));
        if (fiscalYear < 1900)
            return Result.Failure<DepreciationScheduleLine>(Error.Validation("FiscalYear", "Exercice invalide"));
        if (periodMonth is < 1 or > 12)
            return Result.Failure<DepreciationScheduleLine>(Error.Validation("PeriodMonth", "Mois invalide"));
        if (depreciationAmount < 0 || accumulatedDepreciation < 0 || openingNbv < 0 || closingNbv < 0)
            return Result.Failure<DepreciationScheduleLine>(Error.Validation("Amount", "Les montants ne peuvent pas être négatifs"));

        return Result.Success(new DepreciationScheduleLine
        {
            FixedAssetId = fixedAssetId,
            FiscalYear = fiscalYear,
            PeriodMonth = periodMonth,
            OpeningNbv = openingNbv,
            NormalAnnualAmount = normalAnnualAmount,
            PriorAccumulatedDepreciation = priorAccumulatedDepreciation,
            DepreciationAmount = depreciationAmount,
            AccumulatedDepreciation = accumulatedDepreciation,
            ClosingNbv = closingNbv,
            IsPosted = false
        });
    }

    public void MarkPosted(Guid journalEntryId, Guid accountingPeriodId)
    {
        IsPosted = true;
        JournalEntryId = journalEntryId;
        AccountingPeriodId = accountingPeriodId;
    }

    public void Unpost()
    {
        IsPosted = false;
        JournalEntryId = null;
        AccountingPeriodId = null;
    }

    /// <summary>
    /// Met à jour en place les montants (et le mois) d'une ligne non comptabilisée — utilisé par
    /// le merge du tableau (T4/T13) afin de préserver l'<see cref="Entity.Id"/> et le lien d'audit
    /// <see cref="JournalEntryId"/>. Refus si la ligne est déjà comptabilisée (garde défensive).
    /// Mêmes validations que <see cref="Create"/>.
    /// </summary>
    public Result UpdateAmounts(
        decimal openingNbv,
        decimal normalAnnualAmount,
        decimal priorAccumulatedDepreciation,
        decimal depreciationAmount,
        decimal accumulatedDepreciation,
        decimal closingNbv,
        int? periodMonth)
    {
        if (IsPosted)
            return Result.Failure(Error.Validation("Schedule", "Une dotation comptabilisée ne peut pas être modifiée."));
        if (periodMonth is < 1 or > 12)
            return Result.Failure(Error.Validation("PeriodMonth", "Mois invalide"));
        if (depreciationAmount < 0 || accumulatedDepreciation < 0 || openingNbv < 0 || closingNbv < 0)
            return Result.Failure(Error.Validation("Amount", "Les montants ne peuvent pas être négatifs"));

        OpeningNbv = openingNbv;
        NormalAnnualAmount = normalAnnualAmount;
        PriorAccumulatedDepreciation = priorAccumulatedDepreciation;
        DepreciationAmount = depreciationAmount;
        AccumulatedDepreciation = accumulatedDepreciation;
        ClosingNbv = closingNbv;
        PeriodMonth = periodMonth;
        return Result.Success();
    }
}