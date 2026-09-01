using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Audit;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.AccountingAudit.Rules;

internal static class PayrollRuleConstants
{
    /// <summary>Tolérance sur les taux appliqués, en points. Absorbe les arrondis de paramétrage.</summary>
    internal const decimal RateTolerance = 0.01m;

    internal const int MaxDetailLines = 200;

    /// <summary>
    /// Cycles de paie exploitables pour un contrôle : calculés au minimum. Un brouillon n'engage
    /// rien et sera recalculé — le signaler serait du bruit.
    /// </summary>
    internal static bool IsSettled(PayrollRunStatus status) =>
        status is PayrollRunStatus.Calculated or PayrollRunStatus.Validated or PayrollRunStatus.Closed;
}

/// <summary>
/// Régime social du contrat incohérent avec la CNSS réellement retenue sur le bulletin.
///
/// <para>Trois dérives, toutes coûteuses. Un contrat SIVP est <b>exonéré</b> de CNSS : lui retenir
/// des cotisations prélève indûment le salarié. À l'inverse, un contrat RSNA sans retenue crée une
/// dette CNSS que l'entreprise devra régulariser avec pénalités. Et un contrat RSA calculé au taux
/// RSNA — ou l'inverse — fausse l'assiette déclarée.</para>
///
/// <para>La règle ne recalcule pas la paie : elle confronte le <b>régime du contrat en vigueur au
/// mois du bulletin</b> au taux effectivement appliqué, tel que le bulletin l'a figé dans
/// <c>AppliedCnssEmployeeRate</c>. C'est une comparaison, pas une simulation — donc reproductible.</para>
/// </summary>
public sealed class PayrollCnssRegimeMismatchAuditRule : AccountingAuditRuleBase
{
    public override string Code => "payroll-cnss-regime-mismatch";
    public override string ModuleCode => "payroll";
    public override int Category => (int)AnomalyCategory.Paie;
    public override int DefaultSeverity => (int)PreClosingSeverity.Blocking;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();

        var parameters = await c.Db.Set<PayrollYearParameters>().AsNoTracking()
            .Where(p => p.FiscalYear == ctx.FiscalYear)
            .Select(p => new
            {
                p.CnssEmployeeRate,
                p.CnssEmployeeRateRsa
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Sans paramètres d'exercice, aucun taux de référence : la règle s'abstient.
        if (parameters is null) return Array.Empty<AnomalyCandidate>();

        var payslips = await c.Db.Set<Payslip>().AsNoTracking()
            .Join(c.Db.Set<PayrollRun>().AsNoTracking(),
                p => p.PayrollRunId, r => r.Id, (p, r) => new { Slip = p, Run = r })
            .Where(x => x.Slip.Year == ctx.FiscalYear)
            .Select(x => new
            {
                x.Slip.Id,
                x.Slip.EmployeeId,
                x.Slip.EmployeeName,
                x.Slip.EmployeeNumber,
                x.Slip.Year,
                x.Slip.Month,
                x.Slip.CnssEmployee,
                x.Slip.AppliedCnssEmployeeRate,
                x.Run.Status
            })
            .ToListAsync(cancellationToken);

        var settled = payslips.Where(p => PayrollRuleConstants.IsSettled(p.Status)).ToList();
        if (settled.Count == 0) return Array.Empty<AnomalyCandidate>();

        var employeeIds = settled.Select(p => p.EmployeeId).Distinct().ToList();

        var contracts = await c.Db.Set<EmploymentContract>().AsNoTracking()
            .Where(k => employeeIds.Contains(k.EmployeeId))
            .Select(k => new { k.EmployeeId, k.Regime, k.Type, k.StartDate, k.EndDate })
            .ToListAsync(cancellationToken);

        var findings = new List<(string Employee, int Year, int Month, string Reason, decimal Amount)>();

        foreach (var slip in settled)
        {
            var month = new DateTime(slip.Year, slip.Month, 1);

            // Contrat en vigueur au mois du bulletin. Un salarié peut avoir changé de régime en
            // cours d'année : comparer au contrat courant produirait des anomalies fausses sur les
            // mois antérieurs.
            var contract = contracts
                .Where(k => k.EmployeeId == slip.EmployeeId
                            && k.StartDate <= month.AddMonths(1).AddDays(-1)
                            && (k.EndDate == null || k.EndDate >= month))
                .OrderByDescending(k => k.StartDate)
                .FirstOrDefault();

            if (contract is null) continue;

            var subjectToCnss = contract.Regime.IsSubjectToCnss();
            var appliedRate = slip.AppliedCnssEmployeeRate;

            string? reason = null;

            if (!subjectToCnss && slip.CnssEmployee > 0)
            {
                reason = $"régime {contract.Regime} exonéré de CNSS, mais {slip.CnssEmployee:N3} TND retenus";
            }
            else if (subjectToCnss && slip.CnssEmployee == 0)
            {
                reason = $"régime {contract.Regime} soumis à CNSS, mais aucune retenue";
            }
            else if (subjectToCnss)
            {
                var expected = contract.Regime == SocialRegime.Rsa
                    ? parameters.CnssEmployeeRateRsa
                    : parameters.CnssEmployeeRate;

                if (Math.Abs(appliedRate - expected) > PayrollRuleConstants.RateTolerance)
                    reason = $"taux {appliedRate:N2} % appliqué au lieu de {expected:N2} % " +
                             $"pour le régime {contract.Regime}";
            }

            if (reason is not null)
                findings.Add(($"{slip.EmployeeNumber} — {slip.EmployeeName}", slip.Year, slip.Month,
                    reason, slip.CnssEmployee));
        }

        if (findings.Count == 0) return Array.Empty<AnomalyCandidate>();

        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "CNSS incohérente avec le régime du contrat",
                $"{findings.Count} bulletin(s) dont la retenue CNSS ne correspond pas au régime social " +
                "du contrat en vigueur.",
                "Assiette CNSS déclarée fausse : régularisation et pénalités à prévoir.",
                accountRef: "453",
                amount: MillimeRounding.Round(findings.Sum(f => f.Amount)),
                periodFrom: null,
                periodTo: null,
                lines: findings.Take(PayrollRuleConstants.MaxDetailLines).Select(f => new AnomalyLineCandidate(
                    null, null, new DateTime(f.Year, f.Month, 1), "453",
                    $"{f.Employee} — {f.Reason}", f.Amount, 0,
                    $"{f.Month:D2}/{f.Year}", null)).ToList(),
                recommendations:
                [
                    "Vérifier le régime social du contrat et les taux de l'exercice.",
                    "Recalculer les bulletins concernés après correction du paramétrage."
                ],
                deepLinkRoute: "/payroll/runs")
        ];
    }
}

/// <summary>
/// Parent à charge déclaré sans pièce nominative, ou réclamé deux fois.
///
/// <para>La déduction pour parent à charge exige l'identité du parent. Deux situations la rendent
/// indéfendable : un compteur renseigné sans aucune déclaration nominative, et un même CIN parent
/// réclamé par deux salariés — l'un des deux perdra la déduction en contrôle, et l'entreprise aura
/// sous-retenu l'IRPP.</para>
///
/// <para>Ces deux cas correspondent exactement à <c>ParentClaimsStatus.Incomplete</c> et
/// <c>Conflict</c>, déjà modélisés dans le domaine. La règle les recalcule ici pour rester dans son
/// contrat de lecture — elle ne lit que par <c>ctx.Db</c>.</para>
/// </summary>
public sealed class PayrollDependentWithoutProofAuditRule : AccountingAuditRuleBase
{
    public override string Code => "payroll-dependent-no-proof";
    public override string ModuleCode => "payroll";
    public override int Category => (int)AnomalyCategory.Paie;
    public override int DefaultSeverity => (int)PreClosingSeverity.Warning;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();

        var employees = await c.Db.Set<Employee>().AsNoTracking()
            .Where(e => e.IsActive && e.DependentParents > 0)
            .Select(e => new { e.Id, e.EmployeeNumber, e.FirstName, e.LastName, e.DependentParents })
            .ToListAsync(cancellationToken);

        var allClaims = await c.Db.Set<EmployeeDependentParent>().AsNoTracking()
            .Select(p => new { p.EmployeeId, p.ParentCin })
            .ToListAsync(cancellationToken);

        var claimsByEmployee = allClaims
            .GroupBy(p => p.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Count());

        var results = new List<AnomalyCandidate>();

        // ── Compteur sans déclaration nominative ────────────────────────────────────────────
        var incomplete = employees
            .Where(e => claimsByEmployee.GetValueOrDefault(e.Id) == 0)
            .Take(PayrollRuleConstants.MaxDetailLines)
            .ToList();

        if (incomplete.Count > 0)
        {
            results.Add(SingleGroup(
                Code, ModuleCode, Category, DefaultSeverity,
                "Parent à charge sans pièce nominative",
                $"{incomplete.Count} salarié(s) déclarent un parent à charge sans identité renseignée.",
                "Déduction IRPP non justifiable : redressement probable en contrôle.",
                accountRef: "432",
                amount: 0m,
                periodFrom: null,
                periodTo: null,
                lines: incomplete.Select(e => new AnomalyLineCandidate(
                    null, null, null, "432",
                    $"{e.EmployeeNumber} — {e.FirstName} {e.LastName} ({e.DependentParents} parent(s) déclaré(s))",
                    0, 0, e.EmployeeNumber, "Absente")).ToList(),
                recommendations:
                [
                    "Saisir le CIN et l'identité de chaque parent à charge.",
                    "Retirer la déduction tant que la pièce n'est pas fournie."
                ],
                deepLinkRoute: "/payroll/employees",
                discriminator: "incomplete"));
        }

        // ── Même CIN parent réclamé par plusieurs salariés ──────────────────────────────────
        var conflicts = allClaims
            .Where(p => !string.IsNullOrWhiteSpace(p.ParentCin))
            .GroupBy(p => p.ParentCin.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(x => x.EmployeeId).Distinct().Count() > 1)
            .ToList();

        if (conflicts.Count > 0)
        {
            var employeeLabels = await c.Db.Set<Employee>().AsNoTracking()
                .Select(e => new { e.Id, e.EmployeeNumber, e.FirstName, e.LastName })
                .ToDictionaryAsync(
                    e => e.Id, e => $"{e.EmployeeNumber} — {e.FirstName} {e.LastName}", cancellationToken);

            results.Add(SingleGroup(
                Code, ModuleCode, Category, (int)PreClosingSeverity.Blocking,
                "Parent à charge réclamé par plusieurs salariés",
                $"{conflicts.Count} parent(s) sont déclarés à charge par plus d'un salarié.",
                "Un seul salarié peut prétendre à la déduction : l'IRPP a été sous-retenu.",
                accountRef: "432",
                amount: 0m,
                periodFrom: null,
                periodTo: null,
                lines: conflicts.Take(PayrollRuleConstants.MaxDetailLines).Select(g => new AnomalyLineCandidate(
                    null, null, null, "432",
                    $"CIN {g.Key} réclamé par : " + string.Join(", ", g.Select(x => x.EmployeeId)
                        .Distinct()
                        .Select(id => employeeLabels.GetValueOrDefault(id, "salarié inconnu"))),
                    0, 0, g.Key, null)).ToList(),
                recommendations:
                [
                    "Trancher entre les salariés concernés et retirer la déduction aux autres.",
                    "Régulariser l'IRPP des mois déjà arrêtés."
                ],
                deepLinkRoute: "/payroll/employees",
                discriminator: "conflict"));
        }

        return results;
    }
}

/// <summary>
/// Heure supplémentaire à un taux incompatible avec le régime hebdomadaire du contrat.
///
/// <para>Le taux de 175 % est le taux légal du régime 48 heures ; l'appliquer sous un régime 40
/// heures sans l'option « taux étendus » n'a pas de fondement. La règle <b>rejoue le validateur du
/// domaine</b> — <c>OvertimeRatePercentExtensions.IsValid(rate, extended, regime)</c> — sur
/// l'historique, plutôt que de réécrire la règle légale : une seule définition, un seul endroit à
/// corriger si la loi change.</para>
/// </summary>
public sealed class PayrollOvertimeOutOfRegimeAuditRule : AccountingAuditRuleBase
{
    public override string Code => "payroll-overtime-out-of-regime";
    public override string ModuleCode => "payroll";
    public override int Category => (int)AnomalyCategory.Paie;
    public override int DefaultSeverity => (int)PreClosingSeverity.Warning;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();

        var lines = await c.Db.Set<PayrollOvertimeLine>().AsNoTracking()
            .Where(l => l.Year == ctx.FiscalYear && l.Hours > 0)
            .Select(l => new
            {
                l.Id,
                l.EmployeeId,
                l.Year,
                l.Month,
                l.Hours,
                l.RatePercent,
                Amount = l.IsOverridden ? (l.OverrideAmount ?? l.ComputedAmount) : l.ComputedAmount
            })
            .ToListAsync(cancellationToken);

        if (lines.Count == 0) return Array.Empty<AnomalyCandidate>();

        var employeeIds = lines.Select(l => l.EmployeeId).Distinct().ToList();

        var contracts = await c.Db.Set<EmploymentContract>().AsNoTracking()
            .Where(k => employeeIds.Contains(k.EmployeeId))
            .Select(k => new { k.EmployeeId, k.WeeklyRegime, k.StartDate, k.EndDate })
            .ToListAsync(cancellationToken);

        var employees = await c.Db.Set<Employee>().AsNoTracking()
            .Where(e => employeeIds.Contains(e.Id))
            .Select(e => new { e.Id, e.EmployeeNumber, e.FirstName, e.LastName })
            .ToDictionaryAsync(
                e => e.Id, e => $"{e.EmployeeNumber} — {e.FirstName} {e.LastName}", cancellationToken);

        var invalid = new List<(string Employee, int Month, decimal Rate, string Regime, decimal Amount)>();

        foreach (var line in lines)
        {
            var month = new DateTime(line.Year, line.Month, 1);
            var contract = contracts
                .Where(k => k.EmployeeId == line.EmployeeId
                            && k.StartDate <= month.AddMonths(1).AddDays(-1)
                            && (k.EndDate == null || k.EndDate >= month))
                .OrderByDescending(k => k.StartDate)
                .FirstOrDefault();

            if (contract is null) continue;

            // Validateur du domaine, option « taux étendus » à faux : on contrôle la conformité
            // légale de base, pas une configuration permissive du dossier.
            if (OvertimeRatePercentExtensions.IsValid(
                    line.RatePercent, enableExtendedOvertimeRates: false, contract.WeeklyRegime))
                continue;

            invalid.Add((
                employees.GetValueOrDefault(line.EmployeeId, "salarié inconnu"),
                line.Month, line.RatePercent, contract.WeeklyRegime.ToString(), line.Amount));
        }

        if (invalid.Count == 0) return Array.Empty<AnomalyCandidate>();

        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Heures supplémentaires hors régime",
                $"{invalid.Count} ligne(s) d'heures supplémentaires à un taux incompatible avec le " +
                "régime hebdomadaire du contrat.",
                "Majoration sans fondement légal : rappel de salaire ou redressement possible.",
                // NCT 01 6401 « Heures supplémentaires » (sous-compte de 640). L'ancrage précédent
                // sur 641 visait le compte des indemnités de rupture — sans rapport avec les HS.
                accountRef: "6401",
                amount: MillimeRounding.Round(invalid.Sum(i => i.Amount)),
                periodFrom: null,
                periodTo: null,
                lines: invalid.Take(PayrollRuleConstants.MaxDetailLines).Select(i => new AnomalyLineCandidate(
                    null, null, new DateTime(ctx.FiscalYear, i.Month, 1), "6401",
                    $"{i.Employee} — {i.Rate:N0} % sous régime {i.Regime}", i.Amount, 0,
                    $"{i.Month:D2}/{ctx.FiscalYear}", null)).ToList(),
                recommendations:
                [
                    "Corriger le taux ou le régime hebdomadaire du contrat.",
                    "Activer l'option « taux étendus » si le dossier le justifie."
                ],
                deepLinkRoute: "/payroll/runs")
        ];
    }
}

/// <summary>
/// Rémunération inférieure au SMIG.
///
/// <para>Contrôle le <b>brut reconstitué à temps plein</b>, pas le brut versé : un salarié entré en
/// cours de mois, en congé sans solde ou en suspension touche légitimement moins que le SMIG. Ne
/// pas neutraliser le prorata produirait une anomalie sur chaque embauche — la règle serait
/// désactivée dans la semaine.</para>
///
/// <para>Le SMIG vient de <c>PayrollYearParameters.MonthlySmig</c> de l'exercice ; à zéro ou absent,
/// la règle s'abstient : aucun seuil légal n'est codé en dur.</para>
/// </summary>
public sealed class PayrollBelowSmigAuditRule : AccountingAuditRuleBase
{
    /// <summary>Jours ouvrés de référence d'un mois plein, servant à reconstituer le temps plein.</summary>
    private const decimal ReferenceMonthDays = 26m;

    /// <summary>Marge sous le SMIG avant de signaler, en dinars. Absorbe les arrondis de prorata.</summary>
    private const decimal SmigTolerance = 1m;

    public override string Code => "payroll-below-smig";
    public override string ModuleCode => "payroll";
    public override int Category => (int)AnomalyCategory.Paie;
    public override int DefaultSeverity => (int)PreClosingSeverity.Blocking;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();

        var smig = await c.Db.Set<PayrollYearParameters>().AsNoTracking()
            .Where(p => p.FiscalYear == ctx.FiscalYear)
            .Select(p => (decimal?)p.MonthlySmig)
            .FirstOrDefaultAsync(cancellationToken);

        if (smig is not { } monthlySmig || monthlySmig <= 0) return Array.Empty<AnomalyCandidate>();

        var payslips = await c.Db.Set<Payslip>().AsNoTracking()
            .Join(c.Db.Set<PayrollRun>().AsNoTracking(),
                p => p.PayrollRunId, r => r.Id, (p, r) => new { Slip = p, Run = r })
            .Where(x => x.Slip.Year == ctx.FiscalYear)
            .Select(x => new
            {
                x.Slip.EmployeeName,
                x.Slip.EmployeeNumber,
                x.Slip.Year,
                x.Slip.Month,
                x.Slip.GrossSalary,
                x.Slip.ProrataWorkedDays,
                x.Slip.ProrataNonWorkedDays,
                x.Run.Status
            })
            .ToListAsync(cancellationToken);

        var below = new List<(string Employee, int Month, decimal FullTime, decimal Gross)>();

        foreach (var slip in payslips.Where(p => PayrollRuleConstants.IsSettled(p.Status)))
        {
            // Reconstitution du temps plein. Sans information de prorata, le brut est déjà celui
            // d'un mois complet.
            var workedDays = slip.ProrataWorkedDays;
            var totalDays = workedDays + slip.ProrataNonWorkedDays;

            var fullTimeGross = totalDays > 0 && workedDays > 0
                ? MillimeRounding.Round(slip.GrossSalary / workedDays * ReferenceMonthDays)
                : slip.GrossSalary;

            // Un mois entièrement non travaillé ne dit rien du niveau de rémunération.
            if (workedDays == 0 && totalDays > 0) continue;

            if (fullTimeGross >= monthlySmig - SmigTolerance) continue;

            below.Add((
                $"{slip.EmployeeNumber} — {slip.EmployeeName}",
                slip.Month, fullTimeGross, slip.GrossSalary));
        }

        if (below.Count == 0) return Array.Empty<AnomalyCandidate>();

        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Rémunération inférieure au SMIG",
                $"{below.Count} bulletin(s) dont le brut reconstitué à temps plein est inférieur au " +
                $"SMIG de l'exercice ({monthlySmig:N3} TND).",
                "Infraction au salaire minimum : rappel de salaire et sanction possibles.",
                // NCT 01 640 « Salaires et compléments de salaires » : l'écart porte sur la
                // rémunération elle-même, pas sur les indemnités de rupture (ex-ancrage 641).
                accountRef: "640",
                amount: MillimeRounding.Round(below.Sum(b => monthlySmig - b.FullTime)),
                periodFrom: null,
                periodTo: null,
                lines: below.Take(PayrollRuleConstants.MaxDetailLines).Select(b => new AnomalyLineCandidate(
                    null, null, new DateTime(ctx.FiscalYear, b.Month, 1), "640",
                    $"{b.Employee} — {b.FullTime:N3} TND reconstitués (versé {b.Gross:N3})",
                    b.Gross, 0, $"{b.Month:D2}/{ctx.FiscalYear}", null)).ToList(),
                recommendations:
                [
                    "Relever le salaire de base au niveau du SMIG de l'exercice.",
                    "Vérifier le prorata si le salarié n'a pas travaillé le mois complet."
                ],
                deepLinkRoute: "/payroll/runs")
        ];
    }
}
