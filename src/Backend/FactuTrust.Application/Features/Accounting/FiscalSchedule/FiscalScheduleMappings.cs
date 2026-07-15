using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Accounting.FiscalSchedule;

public static class FiscalScheduleMappings
{
    public static Result ValidatePeriodConsistency(
        FiscalObligationType obligationType,
        int? periodMonth,
        int? periodQuarter)
    {
        if (obligationType == FiscalObligationType.QuarterlyVat)
        {
            if (!periodQuarter.HasValue)
                return Result.Failure(Error.Validation("PeriodQuarter", "Le trimestre est obligatoire pour une TVA trimestrielle."));
            if (periodMonth.HasValue)
                return Result.Failure(Error.Validation("PeriodMonth", "Indiquez un mois ou un trimestre, pas les deux."));
            return Result.Success();
        }

        if (periodMonth.HasValue && periodQuarter.HasValue)
            return Result.Failure(Error.Validation("PeriodMonth", "Indiquez un mois ou un trimestre, pas les deux."));

        return Result.Success();
    }

    private static readonly string[] MonthNames =
    [
        "",
        "Janvier",
        "Fevrier",
        "Mars",
        "Avril",
        "Mai",
        "Juin",
        "Juillet",
        "Aout",
        "Septembre",
        "Octobre",
        "Novembre",
        "Decembre"
    ];

    public static FiscalScheduleEntryDto ToDto(
        FiscalScheduleEntry entry,
        DateTime today,
        Guid? companyTenantId = null,
        string? companyName = null)
    {
        var status = entry.ResolveStatus(today);
        return new FiscalScheduleEntryDto
        {
            Id = entry.Id,
            CompanyTenantId = companyTenantId,
            CompanyName = companyName,
            ObligationType = (int)entry.ObligationType,
            ObligationTypeDisplay = GetObligationDisplay(entry.ObligationType),
            ObligationLabel = entry.ObligationLabel,
            FiscalYear = entry.FiscalYear,
            PeriodMonth = entry.PeriodMonth,
            PeriodQuarter = entry.PeriodQuarter,
            PeriodStart = entry.PeriodStart,
            PeriodEnd = entry.PeriodEnd,
            PeriodDisplay = GetPeriodDisplay(entry),
            DueDate = entry.DueDate,
            EstimatedAmount = entry.EstimatedAmount,
            Currency = entry.Currency,
            Status = (int)status,
            StatusDisplay = GetStatusDisplay(status),
            SourceType = (int)entry.SourceType,
            SourceId = entry.SourceId,
            DepositDate = entry.DepositDate,
            PaymentDate = entry.PaymentDate,
            ValidatedAt = entry.ValidatedAt,
            ValidatedBy = entry.ValidatedBy,
            ResponsibleUserId = entry.ResponsibleUserId,
            ResponsibleName = entry.ResponsibleName,
            Observations = entry.Observations,
            LastReminderAt = entry.LastReminderAt,
            LastReminderChannel = entry.LastReminderChannel.HasValue ? (int)entry.LastReminderChannel.Value : null,
            AttachmentCount = entry.Attachments.Count,
            HistoryCount = entry.History.Count,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt,
            CreatedBy = entry.CreatedBy,
            UpdatedBy = entry.UpdatedBy,
            Version = entry.Version
        };
    }

    public static FiscalScheduleHistoryDto ToDto(FiscalScheduleHistoryEntry history) => new()
    {
        Id = history.Id,
        FiscalScheduleEntryId = history.FiscalScheduleEntryId,
        Action = history.Action,
        Summary = history.Summary,
        OldValuesJson = history.OldValuesJson,
        NewValuesJson = history.NewValuesJson,
        CreatedAt = history.CreatedAt,
        CreatedBy = history.CreatedBy
    };

    public static FiscalScheduleAttachmentDto ToDto(FiscalScheduleAttachment attachment) => new()
    {
        Id = attachment.Id,
        FiscalScheduleEntryId = attachment.FiscalScheduleEntryId,
        FileName = attachment.FileName,
        ContentType = attachment.ContentType,
        SizeBytes = attachment.SizeBytes,
        CreatedAt = attachment.CreatedAt,
        UploadedBy = attachment.CreatedBy
    };

    public static FiscalScheduleSummaryDto BuildSummary(IReadOnlyList<FiscalScheduleEntryDto> items, DateTime today)
    {
        var activeItems = items.Where(i => i.Status != (int)FiscalScheduleStatus.Cancelled).ToList();
        var currentMonthStart = new DateTime(today.Year, today.Month, 1);
        var nextMonthStart = currentMonthStart.AddMonths(1);
        var depositedThisMonth = activeItems
            .Where(i => i.DepositDate.HasValue && i.DepositDate.Value.Date >= currentMonthStart && i.DepositDate.Value.Date < nextMonthStart)
            .ToList();

        return new FiscalScheduleSummaryDto
        {
            UpcomingWithin7DaysCount = activeItems.Count(i => i.Status == (int)FiscalScheduleStatus.UpcomingWithin7Days),
            UpcomingWithin7DaysAmount = activeItems.Where(i => i.Status == (int)FiscalScheduleStatus.UpcomingWithin7Days).Sum(i => i.EstimatedAmount),
            UpcomingAfter7DaysCount = activeItems.Count(i => i.Status == (int)FiscalScheduleStatus.UpcomingAfter7Days),
            UpcomingAfter7DaysAmount = activeItems.Where(i => i.Status == (int)FiscalScheduleStatus.UpcomingAfter7Days).Sum(i => i.EstimatedAmount),
            OverdueCount = activeItems.Count(i => i.Status == (int)FiscalScheduleStatus.Overdue),
            OverdueAmount = activeItems.Where(i => i.Status == (int)FiscalScheduleStatus.Overdue).Sum(i => i.EstimatedAmount),
            DepositedThisMonthCount = depositedThisMonth.Count,
            DepositedThisMonthAmount = depositedThisMonth.Sum(i => i.EstimatedAmount),
            TotalCount = activeItems.Count,
            TotalAmount = activeItems.Sum(i => i.EstimatedAmount)
        };
    }

    public static string GetObligationDisplay(FiscalObligationType type) => type switch
    {
        FiscalObligationType.MonthlyDeclaration => "Declaration mensuelle",
        FiscalObligationType.ProvisionalCorporateTaxInstallment => "Acompte provisionnel IS",
        FiscalObligationType.WithholdingTax => "Retenue a la source",
        FiscalObligationType.Fodec => "FODEC",
        FiscalObligationType.QuarterlyVat => "TVA trimestrielle",
        FiscalObligationType.FinancialStatements => "Etats financiers",
        FiscalObligationType.SemiAnnualFinancialStatements => "Etats financiers semestriels",
        FiscalObligationType.PersonalIncomeTaxInstallment => "IRPP - Acompte",
        FiscalObligationType.CnssDtsQuarterly => "DTS CNSS (salaires trimestriels)",
        FiscalObligationType.PayrollIrppWithholding => "Retenue IRPP salariés",
        _ => "Autre obligation"
    };

    public static string GetStatusDisplay(FiscalScheduleStatus status) => status switch
    {
        FiscalScheduleStatus.UpcomingWithin7Days => "A venir (<= 7 j)",
        FiscalScheduleStatus.UpcomingAfter7Days => "A venir (> 7 j)",
        FiscalScheduleStatus.Overdue => "En retard",
        FiscalScheduleStatus.Deposited => "Deposee",
        FiscalScheduleStatus.Paid => "Payee",
        FiscalScheduleStatus.Validated => "Validee",
        FiscalScheduleStatus.Cancelled => "Annulee",
        _ => "Inconnu"
    };

    private static string GetPeriodDisplay(FiscalScheduleEntry entry)
    {
        if (entry.PeriodMonth is >= 1 and <= 12)
            return $"{MonthNames[entry.PeriodMonth.Value]} {entry.FiscalYear}";

        if (entry.PeriodQuarter is >= 1 and <= 4)
            return $"Trimestre {entry.PeriodQuarter.Value} {entry.FiscalYear}";

        if (entry.PeriodStart.HasValue && entry.PeriodEnd.HasValue)
            return $"{entry.PeriodStart.Value:dd/MM/yyyy} - {entry.PeriodEnd.Value:dd/MM/yyyy}";

        return $"Exercice {entry.FiscalYear}";
    }
}
