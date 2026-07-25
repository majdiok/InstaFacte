using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Fiscal;

/// <summary>
/// Paramètres de l'impôt sur le revenu/les sociétés versionnés par exercice (taux IS, minimum d'impôt,
/// CSS, acomptes, report des déficits, barème IRPP). Les valeurs par défaut sont initialisées pour
/// chaque année ; la loi de finances peut imposer des mises à jour <b>sans modifier le code</b>.
///
/// IMPORTANT : les taux ci-dessous sont des valeurs par défaut indicatives à valider par un
/// expert-comptable pour chaque exercice ; le taux réellement appliqué est choisi sur la déclaration.
/// </summary>
public sealed class IncomeTaxYearParameter : Entity
{
    public int FiscalYear { get; private set; }

    // ── Taux IS ────────────────────────────────────────────────────────────────
    /// <summary>Taux IS de droit commun (défaut 15 %).</summary>
    public decimal IsStandardRate { get; private set; }
    /// <summary>Taux IS réduit (ex. certaines activités — défaut 10 %).</summary>
    public decimal IsReducedRate { get; private set; }
    /// <summary>Taux IS majoré sectoriel (banques, assurances, télécoms… — défaut 35 %).</summary>
    public decimal IsSectorRate { get; private set; }

    // ── Minimum d'impôt ──────────────────────────────────────────────────────────
    /// <summary>Taux du minimum d'impôt sur CA local TTC (défaut 0,2 %).</summary>
    public decimal MinTaxRate { get; private set; }
    /// <summary>Taux réduit du minimum d'impôt (défaut 0,1 %).</summary>
    public decimal MinTaxReducedRate { get; private set; }
    /// <summary>Plancher du minimum d'impôt en TND (défaut 500).</summary>
    public decimal MinTaxFloorTnd { get; private set; }
    /// <summary>Plancher du minimum d'impôt au régime réduit, en TND (défaut 300).</summary>
    public decimal MinTaxFloorReducedTnd { get; private set; }

    // ── Contribution sociale de solidarité (CSS) ─────────────────────────────────
    /// <summary>La CSS s'applique-t-elle pour cet exercice ?</summary>
    public bool CssApplies { get; private set; }
    /// <summary>Taux CSS appliqué au résultat imposable (défaut 0 — à configurer selon l'exercice).</summary>
    public decimal CssRate { get; private set; }
    /// <summary>Plancher CSS en TND (défaut 0).</summary>
    public decimal CssFloorTnd { get; private set; }

    // ── Acomptes provisionnels ───────────────────────────────────────────────────
    /// <summary>Taux d'un acompte provisionnel sur l'IS N-1 (défaut 30 %).</summary>
    public decimal AcompteRate { get; private set; }
    /// <summary>Nombre d'acomptes provisionnels (défaut 3).</summary>
    public int AcompteCount { get; private set; }

    // ── Reports ──────────────────────────────────────────────────────────────────
    /// <summary>Nombre d'exercices de report des déficits ordinaires (défaut 5).</summary>
    public int DeficitCarryForwardYears { get; private set; }

    /// <summary>Barème IRPP progressif sérialisé en JSON : tableau de { "lower": borne, "rate": taux % }.</summary>
    public string IrppBracketsJson { get; private set; } = "[]";

    // ── Présentation / conformité déclarative ────────────────────────────────────
    /// <summary>
    /// Arrondir l'assiette imposable au dinar inférieur avant application du taux/barème
    /// (pratique des déclarations fiscales tunisiennes). Défaut : vrai.
    /// </summary>
    public bool RoundTaxableToDinar { get; private set; }

    /// <summary>
    /// Vrai dès que le comptable a modifié ces paramètres via l'écran de paramétrage.
    /// L'initialiseur ne rafraîchit JAMAIS une ligne marquée comme modifiée par l'utilisateur.
    /// </summary>
    public bool IsUserModified { get; private set; }

    private IncomeTaxYearParameter() { }

    public static IncomeTaxYearParameter Create(
        int fiscalYear,
        decimal isStandardRate,
        decimal isReducedRate,
        decimal isSectorRate,
        decimal minTaxRate,
        decimal minTaxReducedRate,
        decimal minTaxFloorTnd,
        bool cssApplies,
        decimal cssRate,
        decimal cssFloorTnd,
        decimal acompteRate,
        int acompteCount,
        int deficitCarryForwardYears,
        string irppBracketsJson,
        decimal minTaxFloorReducedTnd = 0m,
        bool roundTaxableToDinar = true)
    {
        if (fiscalYear < 2000 || fiscalYear > 2100)
            throw new ArgumentOutOfRangeException(nameof(fiscalYear));

        return new IncomeTaxYearParameter
        {
            FiscalYear = fiscalYear,
            IsStandardRate = Round(isStandardRate),
            IsReducedRate = Round(isReducedRate),
            IsSectorRate = Round(isSectorRate),
            MinTaxRate = Round(minTaxRate),
            MinTaxReducedRate = Round(minTaxReducedRate),
            MinTaxFloorTnd = Round(minTaxFloorTnd),
            MinTaxFloorReducedTnd = Round(minTaxFloorReducedTnd),
            CssApplies = cssApplies,
            CssRate = Round(cssRate),
            CssFloorTnd = Round(cssFloorTnd),
            AcompteRate = Round(acompteRate),
            AcompteCount = acompteCount < 0 ? 0 : acompteCount,
            DeficitCarryForwardYears = deficitCarryForwardYears < 0 ? 0 : deficitCarryForwardYears,
            IrppBracketsJson = string.IsNullOrWhiteSpace(irppBracketsJson) ? "[]" : irppBracketsJson,
            RoundTaxableToDinar = roundTaxableToDinar,
            IsUserModified = false
        };
    }

    /// <summary>
    /// Met à jour l'ensemble des paramètres (hors exercice).
    /// <paramref name="markUserModified"/> : vrai lors d'une saisie par le comptable (écran de
    /// paramétrage) — la ligne devient alors intouchable par l'initialiseur ; faux lors d'un
    /// rafraîchissement automatique des défauts.
    /// </summary>
    public void Update(
        decimal isStandardRate, decimal isReducedRate, decimal isSectorRate,
        decimal minTaxRate, decimal minTaxReducedRate, decimal minTaxFloorTnd,
        bool cssApplies, decimal cssRate, decimal cssFloorTnd,
        decimal acompteRate, int acompteCount, int deficitCarryForwardYears, string irppBracketsJson,
        decimal minTaxFloorReducedTnd = 0m, bool roundTaxableToDinar = true, bool markUserModified = true)
    {
        IsStandardRate = Round(isStandardRate);
        IsReducedRate = Round(isReducedRate);
        IsSectorRate = Round(isSectorRate);
        MinTaxRate = Round(minTaxRate);
        MinTaxReducedRate = Round(minTaxReducedRate);
        MinTaxFloorTnd = Round(minTaxFloorTnd);
        MinTaxFloorReducedTnd = Round(minTaxFloorReducedTnd);
        CssApplies = cssApplies;
        CssRate = Round(cssRate);
        CssFloorTnd = Round(cssFloorTnd);
        AcompteRate = Round(acompteRate);
        AcompteCount = acompteCount < 0 ? 0 : acompteCount;
        DeficitCarryForwardYears = deficitCarryForwardYears < 0 ? 0 : deficitCarryForwardYears;
        IrppBracketsJson = string.IsNullOrWhiteSpace(irppBracketsJson) ? "[]" : irppBracketsJson;
        RoundTaxableToDinar = roundTaxableToDinar;
        if (markUserModified)
            IsUserModified = true;
    }

    private static decimal Round(decimal v) => Math.Round(v, 5);
}
