using System.Globalization;
using System.Text;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Reports;

// ── Rapport d'exposition SCE (plan §5.4 item 6 / WS-5) ──
// Read-only firm-only : pour chaque exercice, recale en mémoire les bulletins des cycles Validés/
// Clôturés avec les paramètres légaux corrigés (PayrollParameterDefaults.GetPreset) et produit les
// deltas CNSS sal/pat + IRPP (barème) par salarié/mois. Aucune réécriture de l'historique ; sert à
// l'expert-comptable pour déposer les declarations rectificatives.

/// <summary>Rapport d'exposition (sous-withholding / barème) d'un exercice, recalculé vs preset légal.</summary>
public sealed record GetPayrollExposureReportQuery(int FiscalYear) : IRequest<Result<PayrollExposureReportDto>>;

public sealed class GetPayrollExposureReportQueryHandler
    : IRequestHandler<GetPayrollExposureReportQuery, Result<PayrollExposureReportDto>>
{
    private readonly IPayrollRunRepository _runs;
    private readonly IEmployeeRepository _employees;

    public GetPayrollExposureReportQueryHandler(
        IPayrollRunRepository runs,
        IEmployeeRepository employees)
    {
        _runs = runs;
        _employees = employees;
    }

    public async Task<Result<PayrollExposureReportDto>> Handle(
        GetPayrollExposureReportQuery request,
        CancellationToken cancellationToken)
    {
        var preset = PayrollParameterDefaults.GetPreset(request.FiscalYear);
        var correctedEmpRate = preset.CnssEmployeeRate;
        var correctedPatRate = preset.CnssEmployerRate;
        var brackets = preset.IrppBrackets;

        var runs = await _runs.ListByMonthRangeWithPayslipsAsync(
            request.FiscalYear, 1, 12, includeCalculated: true, cancellationToken);

        // Seuls les cycles arrêtés (Validé/Clôturé) portent une exposition déclarative.
        var settledRuns = runs
            .Where(r => r.Status is PayrollRunStatus.Validated or PayrollRunStatus.Closed)
            .OrderBy(r => r.Month)
            .ToList();

        var payslips = settledRuns.SelectMany(r => r.Payslips).ToList();
        var employeeIds = payslips.Select(p => p.EmployeeId).Distinct().ToList();
        var names = employeeIds.Count > 0
            ? await _employees.GetFullNamesByIdsAsync(employeeIds, cancellationToken)
            : new Dictionary<Guid, string>();

        var rows = new List<PayrollExposureRowDto>();
        foreach (var run in settledRuns)
        {
            foreach (var p in run.Payslips.OrderBy(p => p.EmployeeId))
            {
                // Base CNSSable réellement utilisée = montant stocké / taux appliqué (absorbe le plafond
                // CNSS sans recourir au moteur). Quand le taux appliqué == taux légal, le delta est nul.
                var correctedCnssSal = p.AppliedCnssEmployeeRate > 0m
                    ? R(p.CnssEmployee / p.AppliedCnssEmployeeRate * correctedEmpRate)
                    : R(p.CnssableGross * correctedEmpRate);
                var correctedCnssPat = p.AppliedCnssEmployerRate > 0m
                    ? R(p.CnssEmployer / p.AppliedCnssEmployerRate * correctedPatRate)
                    : R(p.CnssableGross * correctedPatRate);
                var recomputedIrppGross = ProgressiveIrpp(p.AnnualNetTaxable, brackets);

                rows.Add(new PayrollExposureRowDto
                {
                    EmployeeId = p.EmployeeId,
                    EmployeeName = names.TryGetValue(p.EmployeeId, out var n) ? n : string.Empty,
                    Year = run.Year,
                    Month = run.Month,
                    Period = $"{run.Month:D2}/{run.Year}",
                    CnssableGross = R(p.CnssableGross),
                    AppliedCnssEmployeeRate = p.AppliedCnssEmployeeRate,
                    AppliedCnssEmployerRate = p.AppliedCnssEmployerRate,
                    StoredCnssSal = R(p.CnssEmployee),
                    CorrectedCnssSal = correctedCnssSal,
                    DeltaCnssSal = R(correctedCnssSal - p.CnssEmployee),
                    StoredCnssPat = R(p.CnssEmployer),
                    CorrectedCnssPat = correctedCnssPat,
                    DeltaCnssPat = R(correctedCnssPat - p.CnssEmployer),
                    AnnualNetTaxable = R(p.AnnualNetTaxable),
                    StoredIrppBeforeExemption = R(p.IrppBeforeSmigExemption),
                    RecomputedIrppGross = recomputedIrppGross,
                    DeltaIrppGross = R(recomputedIrppGross - p.IrppBeforeSmigExemption),
                    StoredIrpp = R(p.Irpp),
                    RateMismatch = p.AppliedCnssEmployeeRate != correctedEmpRate
                        || p.AppliedCnssEmployerRate != correctedPatRate
                });
            }
        }

        return Result.Success(new PayrollExposureReportDto
        {
            FiscalYear = request.FiscalYear,
            GeneratedAt = DateTime.UtcNow,
            CorrectedCnssEmployeeRate = correctedEmpRate,
            CorrectedCnssEmployerRate = correctedPatRate,
            RowCount = rows.Count,
            TotalDeltaCnssSal = R(rows.Sum(r => r.DeltaCnssSal)),
            TotalDeltaCnssPat = R(rows.Sum(r => r.DeltaCnssPat)),
            TotalDeltaIrppGross = R(rows.Sum(r => r.DeltaIrppGross)),
            Rows = rows,
            Caveat = "Deltas CNSS = taux légal × assiette CNSSable stockée − montant stocké. " +
                     "Delta IRPP = barème légal appliqué à la base nette annuelle stockée (effet barème seul, " +
                     "hors impact CNSS sur l'assiette et hors mode d'exonération SMIG — recale moteur complet requis)."
        });
    }

    private static decimal R(decimal v) => Math.Round(v, 3, MidpointRounding.AwayFromZero);

    /// <summary>IRPP progressif par tranches (LowerBound, Rate) : chaque tranche taxe la part au-dessus de son seuil.</summary>
    private static decimal ProgressiveIrpp(decimal annualNetTaxable, IReadOnlyList<(decimal LowerBound, decimal Rate)> brackets)
    {
        if (annualNetTaxable <= 0m || brackets.Count == 0)
            return 0m;

        var ordered = brackets.OrderBy(b => b.LowerBound).ToList();
        decimal tax = 0m;
        for (int i = 0; i < ordered.Count; i++)
        {
            var lower = ordered[i].LowerBound;
            if (annualNetTaxable <= lower)
                break;
            var upper = i + 1 < ordered.Count ? ordered[i + 1].LowerBound : decimal.MaxValue;
            tax += (Math.Min(annualNetTaxable, upper) - lower) * ordered[i].Rate;
        }
        return R(tax);
    }
}

/// <summary>Export CSV du rapport d'exposition (pour dépôt de declarations rectificatives).</summary>
public sealed record ExportPayrollExposureReportQuery(int Year) : IRequest<Result<PayrollReportFileDto>>;

public sealed class ExportPayrollExposureReportQueryHandler
    : IRequestHandler<ExportPayrollExposureReportQuery, Result<PayrollReportFileDto>>
{
    private readonly IMediator _mediator;

    public ExportPayrollExposureReportQueryHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<Result<PayrollReportFileDto>> Handle(
        ExportPayrollExposureReportQuery request,
        CancellationToken cancellationToken)
    {
        var generated = await _mediator.Send(
            new GetPayrollExposureReportQuery(request.Year), cancellationToken);
        if (generated.IsFailure)
            return Result.Failure<PayrollReportFileDto>(generated.Error);

        var report = generated.Value;
        var sb = new StringBuilder();
        sb.AppendLine("Annee;Mois;Periode;EmployeId;Employe;AssietteCNSS;TauxCNSSSalApplique;TauxCNSSPatApplique;" +
                      "CNSSSalStocke;CNSSSalRecale;DeltaCNSSSal;CNSSPatStocke;CNSSPatRecale;DeltaCNSSPat;" +
                      "BaseNetteAnnuelle;IRPPBrutStocke;IRPPBrutRecale;DeltaIRPPBrut;IRPPFinalStocke;TauxDivergent");

        var culture = CultureInfo.InvariantCulture;
        string F(decimal v) => v.ToString("0.###", culture);
        foreach (var r in report.Rows)
        {
            sb.AppendLine(string.Join(';',
                r.Year.ToString(culture),
                r.Month.ToString("00", culture),
                r.Period,
                r.EmployeeId.ToString(),
                CsvEscape(r.EmployeeName),
                F(r.CnssableGross),
                F(r.AppliedCnssEmployeeRate),
                F(r.AppliedCnssEmployerRate),
                F(r.StoredCnssSal),
                F(r.CorrectedCnssSal),
                F(r.DeltaCnssSal),
                F(r.StoredCnssPat),
                F(r.CorrectedCnssPat),
                F(r.DeltaCnssPat),
                F(r.AnnualNetTaxable),
                F(r.StoredIrppBeforeExemption),
                F(r.RecomputedIrppGross),
                F(r.DeltaIrppGross),
                F(r.StoredIrpp),
                r.RateMismatch ? "oui" : "non"));
        }

        var content = Encoding.UTF8.GetBytes(sb.ToString());
        return Result.Success(new PayrollReportFileDto(
            content,
            $"exposition_paie_{request.Year}.csv",
            "text/csv; charset=utf-8"));
    }

    private static string CsvEscape(string value)
        => value.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}

public sealed record PayrollExposureReportDto
{
    public int FiscalYear { get; init; }
    public DateTime GeneratedAt { get; init; }
    public decimal CorrectedCnssEmployeeRate { get; init; }
    public decimal CorrectedCnssEmployerRate { get; init; }
    public int RowCount { get; init; }
    public decimal TotalDeltaCnssSal { get; init; }
    public decimal TotalDeltaCnssPat { get; init; }
    public decimal TotalDeltaIrppGross { get; init; }
    public string Caveat { get; init; } = string.Empty;
    public IReadOnlyList<PayrollExposureRowDto> Rows { get; init; } = Array.Empty<PayrollExposureRowDto>();
}

public sealed record PayrollExposureRowDto
{
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public int Year { get; init; }
    public int Month { get; init; }
    public string Period { get; init; } = string.Empty;
    public decimal CnssableGross { get; init; }
    public decimal AppliedCnssEmployeeRate { get; init; }
    public decimal AppliedCnssEmployerRate { get; init; }
    public decimal StoredCnssSal { get; init; }
    public decimal CorrectedCnssSal { get; init; }
    public decimal DeltaCnssSal { get; init; }
    public decimal StoredCnssPat { get; init; }
    public decimal CorrectedCnssPat { get; init; }
    public decimal DeltaCnssPat { get; init; }
    public decimal AnnualNetTaxable { get; init; }
    public decimal StoredIrppBeforeExemption { get; init; }
    public decimal RecomputedIrppGross { get; init; }
    public decimal DeltaIrppGross { get; init; }
    public decimal StoredIrpp { get; init; }
    public bool RateMismatch { get; init; }
}
