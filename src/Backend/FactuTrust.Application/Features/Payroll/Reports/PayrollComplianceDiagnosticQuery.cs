using System.Globalization;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Accounting;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Reports;

/// <summary>
/// Diagnostic de conformité paie (plan §5.4) — lecture seule, par tenant. Implémente les 7 contrôles
/// et rend un résultat scoré destiné à la page firm-only « Diagnostic conformité paie ». Aucune
/// mutation : les corrections appartiennent aux outils de reclassement / dé-solde / report d'exposition.
/// </summary>
public sealed record PayrollComplianceDiagnosticQuery : IRequest<Result<PayrollComplianceDiagnosticDto>>;

public sealed class PayrollComplianceDiagnosticQueryHandler
    : IRequestHandler<PayrollComplianceDiagnosticQuery, Result<PayrollComplianceDiagnosticDto>>
{
    private const string PayrollRunSource = "PayrollRun";

    private readonly IPayrollRunRepository _runs;
    private readonly IJournalEntryRepository _journalEntries;
    private readonly IEmployeeAdvanceRepository _advances;
    private readonly IEmployeeLoanRepository _loans;
    private readonly IEmployeeRepository _employees;
    private readonly IPayrollParametersRepository _parameters;

    public PayrollComplianceDiagnosticQueryHandler(
        IPayrollRunRepository runs,
        IJournalEntryRepository journalEntries,
        IEmployeeAdvanceRepository advances,
        IEmployeeLoanRepository loans,
        IEmployeeRepository employees,
        IPayrollParametersRepository parameters)
    {
        _runs = runs;
        _journalEntries = journalEntries;
        _advances = advances;
        _loans = loans;
        _employees = employees;
        _parameters = parameters;
    }

    public async Task<Result<PayrollComplianceDiagnosticDto>> Handle(
        PayrollComplianceDiagnosticQuery request,
        CancellationToken cancellationToken)
    {
        var runs = await _runs.ListAsync(null, cancellationToken);
        var runById = runs.ToDictionary(r => r.Id);
        var periodByRun = runs.ToDictionary(r => r.Id, r => (r.Year, r.Month));

        var entries = await _journalEntries.ListActiveBySourceTypesAsync(PayrollSourcedEntryGuard.PayrollSourceTypes, cancellationToken);
        var runEntries = entries.Where(e => e.SourceEntityType == PayrollRunSource).ToList();

        var advances = await _advances.GetAllAsync(cancellationToken);
        var loans = await _loans.ListAllWithInstallmentsAsync(cancellationToken);
        var employees = await _employees.GetAllAsync(cancellationToken);
        var parameters = await _parameters.ListAsync(cancellationToken);

        var checks = new List<PayrollDiagnosticCheckDto>
        {
            await CheckMisclassificationAsync(runEntries, runById, cancellationToken),
            CheckAuxiliaryBalances(runEntries, advances, loans),
            await CheckWronglySettledAsync(advances, loans, periodByRun, cancellationToken),
            CheckRunsWithoutEntry(runs, runEntries),
            CheckDuplicateEntries(entries),
            CheckBadPresetFingerprints(parameters),
            CheckAuxiliaryCollisions(employees)
        };

        var issueCount = checks.Sum(c => c.FindingCount);
        // Score : 100 - pénalité plafonnée. Chaque anomalie compte, mais on plafonne pour qu'un
        // tenant très actif avec quelques écarts historiques ne tombe pas à 0.
        var penalty = Math.Min(100, issueCount * 5);
        var score = Math.Max(0, 100 - penalty);

        return Result.Success(new PayrollComplianceDiagnosticDto
        {
            GeneratedAt = DateTime.UtcNow,
            Score = score,
            IssueCount = issueCount,
            Checks = checks
        });
    }

    // ── 1. Imputations erronées (641, TFP/FOPROLOS en 647/432, CSS pat en 432, AN en 421) ──

    private async Task<PayrollDiagnosticCheckDto> CheckMisclassificationAsync(
        List<JournalEntry> runEntries,
        Dictionary<Guid, PayrollRun> runById,
        CancellationToken cancellationToken)
    {
        var findings = new List<PayrollDiagnosticFindingDto>();

        foreach (var entry in runEntries.OrderBy(e => e.EntryDate))
        {
            if (entry.SourceEntityId is not { } runId || !runById.TryGetValue(runId, out var run))
                continue;

            var lines = entry.Lines.ToList();
            var indemnites641 = R(lines.Where(l => l.AccountNumber == "641").Sum(l => l.DebitAmount.Amount));
            var posted421Credits = R(lines.Where(l => l.AccountNumber == "421").Sum(l => l.CreditAmount.Amount));
            var tfp = R(run.TotalTfp);
            var foprolos = R(run.TotalFoprolos);
            var cssPat = R(run.TotalCssEmployer);

            var taxesIn647Or432 = tfp != 0m || foprolos != 0m;   // TFP/FOPROLOS bookés en 647/432 (Legacy)
            var cssPatIn432 = cssPat != 0m;                       // CSS patronale créditée en 432 (Legacy)
            var has641Posted = indemnites641 != 0m;               // indemnités comptabilisées au 641
            var has421Posted = posted421Credits != 0m;            // crédit 421 (avances et/ou compensation AN)

            // Pas d'écart potentiel → on épargne la lecture des bulletins.
            if (!taxesIn647Or432 && !cssPatIn432 && !has641Posted && !has421Posted)
                continue;

            // M1 : la ventilation (rupture vs ordinaires, part AN vs avances) se déduit des lignes
            // figées des bulletins (EarningKind), non des sommes brutes des comptes legacy. Sous Legacy
            // le 641 mêlait ordinaires et rupture, et le 421 mêlait compensation AN et avances. Sans
            // payslips chargés (cycle sans bulletins / repli), on retombe sur 0 → ancien calcul brut.
            var runWithPayslips = await _runs.GetByIdWithPayslipsAsync(runId, cancellationToken);
            var terminationFromRun = runWithPayslips is null
                ? 0m
                : R(PayrollJournalEntryBuilder.ResolveTerminationIndemnities(runWithPayslips, PayrollAccountProfile.Sce2026));
            var inKindOffsetFromRun = runWithPayslips is null
                ? 0m
                : R(PayrollJournalEntryBuilder.ResolveInKindBenefitOffset(runWithPayslips));

            // On ne peut pas reclasser plus que ce qui est effectivement comptabilisé au 641 / au 421.
            var terminationToReclass = R(Math.Min(terminationFromRun, indemnites641));
            var ordinaryToReclass = R(Math.Max(0m, indemnites641 - terminationToReclass));
            var inKindOffsetToReclass = R(Math.Min(inKindOffsetFromRun, posted421Credits));

            var has641 = indemnites641 != 0m;               // indemnités à reclasser (ordinaires + rupture)
            var hasInKind421 = inKindOffsetToReclass != 0m; // seule la part AN est erronée (les avances au 421 sont licites)

            if (!taxesIn647Or432 && !cssPatIn432 && !has641 && !hasInKind421)
                continue;

            var taxes = R(tfp + foprolos);
            var preview = new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                ["debit_6611"] = tfp,
                ["debit_6612"] = foprolos,
                ["credit_647"] = taxes,                  // retrait de 647 du TFP/FOPROLOS
                ["debit_432"] = R(taxes + cssPat),       // retrait de 432 des taxes + CSS pat
                ["credit_437"] = R(taxes + cssPat),
                ["debit_640"] = ordinaryToReclass,       // indemnités ordinaires 641 → 640
                ["debit_64602"] = terminationToReclass,  // indemnités de rupture 641 → 64602
                ["credit_641"] = indemnites641,
                ["debit_421"] = inKindOffsetToReclass,   // retrait de la seule compensation AN (pas les avances)
                ["credit_4386"] = inKindOffsetToReclass
            };

            var axes = new List<string>();
            if (taxesIn647Or432) axes.Add("TFP/FOPROLOS en 647/432");
            if (cssPatIn432) axes.Add("CSS patronale en 432");
            if (has641) axes.Add("Indemnités en 641");
            if (hasInKind421) axes.Add("Compensation AN en 421");

            findings.Add(new PayrollDiagnosticFindingDto
            {
                Period = $"{run.Month:D2}/{run.Year}",
                Label = $"OD #{entry.EntryNumber} — {string.Join(", ", axes)}",
                EntityType = "PayrollRun",
                EntityId = runId,
                Amounts = preview,
                Detail = "Reclassement SCE proposé (débit/crédit de correction)."
            });
        }

        return Check("misclassification", "Imputations comptables erronées (SCE)", findings);
    }

    // ── 2. Soldes créditeurs 421 / 421.1 et composition ──

    private static PayrollDiagnosticCheckDto CheckAuxiliaryBalances(
        List<JournalEntry> runEntries,
        IReadOnlyList<EmployeeAdvance> advances,
        IReadOnlyList<EmployeeLoan> loans)
    {
        var findings = new List<PayrollDiagnosticFindingDto>();

        // Mouvement 421 / 421.1 issu des écritures paie (la compensation AN en 421, sous Legacy,
        // accumule un solde créditeur fictif — R-05).
        foreach (var account in new[] { "421", "421.1" })
        {
            var credit = R(runEntries.Sum(e => e.Lines.Where(l => l.AccountNumber == account).Sum(l => l.CreditAmount.Amount)));
            var debit = R(runEntries.Sum(e => e.Lines.Where(l => l.AccountNumber == account).Sum(l => l.DebitAmount.Amount)));
            var net = R(credit - debit);

            var composition = new List<string>();
            if (account == "421")
            {
                var outstanding = advances.Where(a => !a.IsSettled).ToList();
                if (outstanding.Count > 0)
                    composition.Add($"{outstanding.Count} avance(s) non soldée(s) — {R(outstanding.Sum(a => a.Amount))} TND");
            }
            else
            {
                var installments = loans.SelectMany(l => l.Installments.Where(i => !i.IsSettled)).ToList();
                if (installments.Count > 0)
                    composition.Add($"{installments.Count} échéance(s) de prêt non soldée(s) — {R(installments.Sum(i => i.Amount))} TND");
            }

            if (net != 0m || composition.Count > 0)
            {
                findings.Add(new PayrollDiagnosticFindingDto
                {
                    Label = $"Compte {account}",
                    Amount = net,
                    Amounts = new Dictionary<string, decimal>(StringComparer.Ordinal)
                    {
                        ["credit"] = credit,
                        ["debit"] = debit,
                        ["net"] = net
                    },
                    Detail = composition.Count > 0 ? string.Join(" ; ", composition) : "Solde créditeur issu des écritures paie."
                });
            }
        }

        return Check("auxiliary_balances", "Soldes 421 / 421.1 et composition", findings);
    }

    // ── 3. Avances / échéances réglées sans ligne de bulletin correspondante (R-06) ──

    private async Task<PayrollDiagnosticCheckDto> CheckWronglySettledAsync(
        IReadOnlyList<EmployeeAdvance> advances,
        IReadOnlyList<EmployeeLoan> loans,
        Dictionary<Guid, (int Year, int Month)> periodByRun,
        CancellationToken cancellationToken)
    {
        var settledAdvances = advances.Where(a => a.IsSettled && a.SettledInPayrollRunId.HasValue).ToList();
        var settledInstallments = loans
            .SelectMany(l => l.Installments.Where(i => i.IsSettled && i.SettledInPayrollRunId.HasValue)
                .Select(i => (Loan: l, Installment: i)))
            .ToList();

        var runIds = settledAdvances.Select(a => a.SettledInPayrollRunId!.Value)
            .Concat(settledInstallments.Select(x => x.Installment.SettledInPayrollRunId!.Value))
            .Distinct()
            .ToHashSet();

        // Lignes de retenue par cycle (chargement ciblé — un cycle par run réglé).
        var deductionLinesByRun = new Dictionary<Guid, List<(Guid EmployeeId, DeductionKind Kind, Guid? SourceId, decimal Amount)>>();
        foreach (var runId in runIds)
        {
            var run = await _runs.GetByIdWithPayslipsAsync(runId, cancellationToken);
            if (run is null) continue;
            var rows = run.Payslips
                .SelectMany(p => p.Lines
                    .Where(l => l.Kind == PayslipLineKind.Deduction && l.DeductionKind.HasValue)
                    .Select(l => (p.EmployeeId, l.DeductionKind!.Value, l.SourceEntityId, l.Amount)))
                .ToList();
            deductionLinesByRun[runId] = rows;
        }

        var findings = new List<PayrollDiagnosticFindingDto>();

        foreach (var advance in settledAdvances)
        {
            var runId = advance.SettledInPayrollRunId!.Value;
            if (!deductionLinesByRun.TryGetValue(runId, out var rows)) rows = new();
            var matched = rows.Any(r => r.Kind == DeductionKind.Advance
                && (r.SourceId == advance.Id
                    || (r.SourceId is null && r.EmployeeId == advance.EmployeeId && r.Amount == advance.Amount)));
            if (!matched)
            {
                periodByRun.TryGetValue(runId, out var p);
                findings.Add(WronglySettledFinding("Advance", advance.Id, advance.EmployeeId, advance.Amount, runId, p));
            }
        }

        foreach (var (loan, installment) in settledInstallments)
        {
            var runId = installment.SettledInPayrollRunId!.Value;
            if (!deductionLinesByRun.TryGetValue(runId, out var rows)) rows = new();
            var matched = rows.Any(r => r.Kind == DeductionKind.Loan
                && (r.SourceId == installment.Id
                    || (r.SourceId is null && r.EmployeeId == loan.EmployeeId && r.Amount == installment.Amount)));
            if (!matched)
            {
                periodByRun.TryGetValue(runId, out var p);
                findings.Add(WronglySettledFinding("LoanInstallment", installment.Id, loan.EmployeeId, installment.Amount, runId, p));
            }
        }

        return Check("wrongly_settled", "Avances / échéances réglées sans retenue correspondante", findings);
    }

    private static PayrollDiagnosticFindingDto WronglySettledFinding(
        string itemType, Guid itemId, Guid employeeId, decimal amount, Guid runId, (int Year, int Month) period)
    {
        return new PayrollDiagnosticFindingDto
        {
            Period = period.Month > 0 ? $"{period.Month:D2}/{period.Year}" : null,
            Label = itemType == "Advance" ? "Avance réglée sans retenue" : "Échéance de prêt réglée sans retenue",
            EntityType = itemType,
            EntityId = itemId,
            Amount = R(amount),
            Amounts = new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                ["settled_in_run"] = 0m,
                ["run_id_marker"] = 0m
            },
            Detail = $"Cycle {runId} — l'item a été marqué réglé sans ligne de bulletin correspondante (R-06)."
        };
    }

    // ── 4. Cycles Validé/Clôturé sans écriture active ──

    private static PayrollDiagnosticCheckDto CheckRunsWithoutEntry(
        IReadOnlyList<PayrollRun> runs,
        List<JournalEntry> runEntries)
    {
        var withEntry = runEntries
            .Where(e => e.SourceEntityId.HasValue)
            .Select(e => e.SourceEntityId!.Value)
            .ToHashSet();

        var findings = runs
            .Where(r => r.Status is PayrollRunStatus.Validated or PayrollRunStatus.Closed && !withEntry.Contains(r.Id))
            .OrderBy(r => r.Year).ThenBy(r => r.Month)
            .Select(r => new PayrollDiagnosticFindingDto
            {
                Period = $"{r.Month:D2}/{r.Year}",
                Label = $"Cycle {r.Status.ToDisplayString()} sans écriture comptable",
                EntityType = "PayrollRun",
                EntityId = r.Id,
                Detail = "Aucune OD paie active (R-07/R-08/R-18) — régénérable via l'outil dédié."
            })
            .ToList();

        return Check("missing_entry", "Cycles validés sans écriture comptable", findings);
    }

    // ── 5. Doublons d'écritures actives par source (prérequis index unique WS-4) ──

    private static PayrollDiagnosticCheckDto CheckDuplicateEntries(IReadOnlyList<JournalEntry> entries)
    {
        var findings = entries
            .Where(e => e.SourceEntityType is not null && e.SourceEntityId.HasValue)
            .GroupBy(e => (Type: e.SourceEntityType!, Id: e.SourceEntityId!.Value))
            .Where(g => g.Count() > 1)
            .Select(g => new PayrollDiagnosticFindingDto
            {
                Label = $"{g.Key.Type} — {g.Count()} écritures actives",
                EntityType = g.Key.Type,
                EntityId = g.Key.Id,
                Amount = g.Count(),
                Detail = "Doublon actif (prérequis index unique WS-4) : " + string.Join(", ", g.Select(e => $"#{e.EntryNumber}"))
            })
            .ToList();

        return Check("duplicate_entries", "Doublons d'écritures actives par source", findings);
    }

    // ── 6. Empreintes de presets périmés (CNSS 9,18/16,57 ; barème 2024 fantôme ; SMIG 528,320 ; SmigPortion) ──

    private static PayrollDiagnosticCheckDto CheckBadPresetFingerprints(IReadOnlyList<PayrollYearParameters> parameters)
    {
        var findings = new List<PayrollDiagnosticFindingDto>();

        foreach (var p in parameters.OrderBy(p => p.FiscalYear))
        {
            var drifts = new List<string>();

            if (p.CnssEmployeeRate == 9.18m && p.CnssEmployerRate == 16.57m)
                drifts.Add("CNSS 9,18 %/16,57 % (obsolète — RSNA 9,68/17,07)");
            if (p.FiscalYear == 2026 && p.MonthlySmig == 528320m)
                drifts.Add("SMIG 2026 = 528,320 (obsolète — légal 554,736)");
            if (p.SmigIrppExemptionMode == SmigIrppExemptionMode.SmigPortion)
                drifts.Add("Mode SmigPortion actif (sans base légale vérifiée)");

            // Barème 2024 fantôme : comparaison au preset légal corrigé (IrppLegacy5Brackets).
            if (p.FiscalYear == 2024)
            {
                var preset = PayrollParameterDefaults.CreateDefaults(2024);
                if (preset.IsSuccess && BracketsDiffer(p, preset.Value))
                    drifts.Add("Barème IRPP 2024 fantôme (non conforme au barème légal 5 tranches)");
            }

            if (drifts.Count > 0)
            {
                findings.Add(new PayrollDiagnosticFindingDto
                {
                    Period = p.FiscalYear.ToString(CultureInfo.InvariantCulture),
                    Label = $"Exercice {p.FiscalYear}",
                    EntityType = "PayrollYearParameters",
                    EntityId = p.Id,
                    Detail = string.Join(" ; ", drifts)
                });
            }
        }

        return Check("bad_preset", "Empreintes de paramètres périmés", findings);
    }

    private static bool BracketsDiffer(PayrollYearParameters actual, PayrollYearParameters preset)
    {
        var a = actual.IrppBrackets.OrderBy(b => b.LowerBound).Select(b => (b.LowerBound, b.Rate)).ToList();
        var s = preset.IrppBrackets.OrderBy(b => b.LowerBound).Select(b => (b.LowerBound, b.Rate)).ToList();
        if (a.Count != s.Count) return true;
        for (var i = 0; i < a.Count; i++)
            if (a[i].LowerBound != s[i].LowerBound || a[i].Rate != s[i].Rate) return true;
        return false;
    }

    // ── 7. Collisions de comptes auxiliaires 425 parmi les salariés actifs ──

    private static PayrollDiagnosticCheckDto CheckAuxiliaryCollisions(IReadOnlyList<Employee> employees)
    {
        var active = employees
            .Where(e => e.IsActive && !string.IsNullOrWhiteSpace(e.EmployeeNumber))
            .ToList();

        // Une fiche sans compte auxiliaire fait échouer la validation du cycle : on la signale ici,
        // où c'est encore corrigeable, plutôt que de laisser l'utilisateur le découvrir au moment
        // d'arrêter la paie.
        var findings = active
            .Where(e => string.IsNullOrWhiteSpace(e.AuxiliaryAccountNumber))
            .Select(e => new PayrollDiagnosticFindingDto
            {
                Label = $"Compte auxiliaire manquant — {e.FullName}",
                EntityType = "Employee",
                EntityId = e.Id,
                Detail = $"Le salarié « {e.EmployeeNumber} » n'a pas de compte auxiliaire 425 sur sa "
                    + "fiche : il lui en sera alloué un à la validation du prochain cycle."
            })
            .ToList();

        // Collision résiduelle : deux fiches reprises depuis des bulletins figés par l'ancienne
        // dérivation du matricule peuvent porter le même compte. L'allocation séquentielle, elle,
        // est unique par construction.
        findings.AddRange(active
            .Where(e => !string.IsNullOrWhiteSpace(e.AuxiliaryAccountNumber))
            .GroupBy(e => e.AuxiliaryAccountNumber!.Trim(), StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => new PayrollDiagnosticFindingDto
            {
                Label = $"Auxiliaire {g.Key} — {g.Count()} salariés",
                Amount = g.Count(),
                Detail = string.Join(", ", g.Select(e => $"{e.FullName} ({e.EmployeeNumber})"))
            }));

        return Check("auxiliary_collisions", "Collisions de comptes auxiliaires 425", findings);
    }

    private static PayrollDiagnosticCheckDto Check(string code, string title, List<PayrollDiagnosticFindingDto> findings)
    {
        return new PayrollDiagnosticCheckDto
        {
            Code = code,
            Title = title,
            Passed = findings.Count == 0,
            FindingCount = findings.Count,
            Findings = findings,
            Summary = findings.Count == 0 ? "Conforme." : $"{findings.Count} anomalie(s)."
        };
    }

    private static decimal R(decimal value) => PayrollReportHelpers.Round(value);
}

// ── DTOs de sortie (dédiés au diagnostic — plan §5.4) ──

public sealed record PayrollComplianceDiagnosticDto
{
    public DateTime GeneratedAt { get; init; }
    /// <summary>0–100, 100 = aucun écart détecté.</summary>
    public int Score { get; init; }
    public int IssueCount { get; init; }
    public IReadOnlyList<PayrollDiagnosticCheckDto> Checks { get; init; } = Array.Empty<PayrollDiagnosticCheckDto>();
}

public sealed record PayrollDiagnosticCheckDto
{
    public string Code { get; init; } = null!;
    public string Title { get; init; } = null!;
    public bool Passed { get; init; }
    public int FindingCount { get; init; }
    public string Summary { get; init; } = null!;
    public IReadOnlyList<PayrollDiagnosticFindingDto> Findings { get; init; } = Array.Empty<PayrollDiagnosticFindingDto>();
}

public sealed record PayrollDiagnosticFindingDto
{
    public string? Period { get; init; }
    public string? Label { get; init; }
    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public decimal? Amount { get; init; }
    public string? Detail { get; init; }
    /// <summary>Montants nommés (aperçu de reclassement, soldes, compteurs).</summary>
    public IReadOnlyDictionary<string, decimal>? Amounts { get; init; }
}
