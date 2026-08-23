using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Projects;

/// <summary>
/// BTP work situation (situation de travaux). Validated rows are immutable.
/// </summary>
public sealed class ProjectSituation : Entity
{
    public Guid ProjectId { get; private set; }
    public int Number { get; private set; }
    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }
    public decimal CumulativePercent { get; private set; }
    public decimal GrossAmountHt { get; private set; }
    public decimal RetainageAmountHt { get; private set; }
    public int VatRatePercent { get; private set; }
    public ProjectSituationStatus Status { get; private set; }
    public Guid? InvoiceId { get; private set; }

    public decimal NetAmountHt => decimal.Round(GrossAmountHt - RetainageAmountHt, 3);
    public decimal VatAmount => decimal.Round(NetAmountHt * VatRatePercent / 100m, 3);
    public decimal TotalTtc => decimal.Round(NetAmountHt + VatAmount, 3);

    private ProjectSituation() { }

    public static Result<ProjectSituation> Create(
        Guid projectId,
        int number,
        DateTime periodStart,
        DateTime periodEnd,
        decimal cumulativePercent,
        decimal grossAmountHt,
        decimal retainageAmountHt,
        int vatRatePercent,
        decimal previousValidatedCumulativePercent)
    {
        if (projectId == Guid.Empty)
            return Result.Failure<ProjectSituation>(Error.Validation("ProjectId", "Le projet est obligatoire"));
        if (number < 1)
            return Result.Failure<ProjectSituation>(Error.Validation("Number", "Le numéro de situation doit être positif"));

        var dates = EnsurePeriod(periodStart, periodEnd);
        if (dates.IsFailure)
            return Result.Failure<ProjectSituation>(dates.Error);

        var progress = EnsureMonotonicPercent(previousValidatedCumulativePercent, cumulativePercent);
        if (progress.IsFailure)
            return Result.Failure<ProjectSituation>(progress.Error);

        var amounts = EnsureAmounts(grossAmountHt, retainageAmountHt, vatRatePercent);
        if (amounts.IsFailure)
            return Result.Failure<ProjectSituation>(amounts.Error);

        return Result.Success(new ProjectSituation
        {
            ProjectId = projectId,
            Number = number,
            PeriodStart = periodStart.Date,
            PeriodEnd = periodEnd.Date,
            CumulativePercent = decimal.Round(cumulativePercent, 2),
            GrossAmountHt = decimal.Round(grossAmountHt, 3),
            RetainageAmountHt = decimal.Round(retainageAmountHt, 3),
            VatRatePercent = vatRatePercent,
            Status = ProjectSituationStatus.Draft
        });
    }

    public Result UpdateDraft(
        DateTime periodStart,
        DateTime periodEnd,
        decimal cumulativePercent,
        decimal grossAmountHt,
        decimal retainageAmountHt,
        int vatRatePercent,
        decimal previousValidatedCumulativePercent)
    {
        if (Status != ProjectSituationStatus.Draft)
            return Result.Failure(Error.Validation("Status", "Une situation validée n'est pas modifiable (avoir ou situation corrective)"));

        var dates = EnsurePeriod(periodStart, periodEnd);
        if (dates.IsFailure)
            return dates;

        var progress = EnsureMonotonicPercent(previousValidatedCumulativePercent, cumulativePercent);
        if (progress.IsFailure)
            return progress;

        var amounts = EnsureAmounts(grossAmountHt, retainageAmountHt, vatRatePercent);
        if (amounts.IsFailure)
            return amounts;

        PeriodStart = periodStart.Date;
        PeriodEnd = periodEnd.Date;
        CumulativePercent = decimal.Round(cumulativePercent, 2);
        GrossAmountHt = decimal.Round(grossAmountHt, 3);
        RetainageAmountHt = decimal.Round(retainageAmountHt, 3);
        VatRatePercent = vatRatePercent;
        return Result.Success();
    }

    public Result Validate(decimal previousValidatedCumulativePercent)
    {
        if (Status != ProjectSituationStatus.Draft)
            return Result.Failure(Error.Validation("Status", "Cette situation est déjà validée"));

        var progress = EnsureMonotonicPercent(previousValidatedCumulativePercent, CumulativePercent);
        if (progress.IsFailure)
            return progress;

        Status = ProjectSituationStatus.Validated;
        return Result.Success();
    }

    public Result AttachInvoice(Guid invoiceId)
    {
        if (Status != ProjectSituationStatus.Validated)
            return Result.Failure(Error.Validation("Status", "Validez la situation avant de la facturer"));
        if (InvoiceId.HasValue)
            return Result.Failure(Error.Validation("InvoiceId", "Cette situation est déjà facturée"));
        if (invoiceId == Guid.Empty)
            return Result.Failure(Error.Validation("InvoiceId", "La facture est obligatoire"));
        InvoiceId = invoiceId;
        return Result.Success();
    }

    public static Result EnsureMonotonicPercent(decimal previousValidatedCumulative, decimal newCumulative)
    {
        if (newCumulative is < 0 or > 100)
            return Result.Failure(Error.Validation("CumulativePercent", "Le pourcentage cumulé doit être compris entre 0 et 100"));
        if (newCumulative < previousValidatedCumulative)
            return Result.Failure(Error.Validation("CumulativePercent", "Le pourcentage cumulé doit être monotone (jamais inférieur à la dernière situation validée)"));
        return Result.Success();
    }

    public static Result EnsureAmounts(decimal grossAmountHt, decimal retainageAmountHt, int vatRatePercent)
    {
        if (grossAmountHt < 0)
            return Result.Failure(Error.Validation("GrossAmountHt", "Le montant brut ne peut pas être négatif"));
        if (retainageAmountHt < 0)
            return Result.Failure(Error.Validation("RetainageAmountHt", "La retenue de garantie ne peut pas être négative"));
        if (retainageAmountHt > grossAmountHt)
            return Result.Failure(Error.Validation("RetainageAmountHt", "La retenue de garantie ne peut pas dépasser le montant brut"));
        if (vatRatePercent is not (0 or 7 or 13 or 19))
            return Result.Failure(Error.Validation("VatRatePercent", "Taux de TVA invalide"));
        return Result.Success();
    }

    public static Result EnsurePeriod(DateTime periodStart, DateTime periodEnd)
    {
        if (periodEnd.Date < periodStart.Date)
            return Result.Failure(Error.Validation("PeriodEnd", "La fin de période doit être postérieure au début"));
        return Result.Success();
    }
}
