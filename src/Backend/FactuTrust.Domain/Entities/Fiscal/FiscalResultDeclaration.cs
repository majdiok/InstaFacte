using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Fiscal;

/// <summary>
/// Feuille de détermination du résultat fiscal d'un exercice (liasse fiscale) : passage du résultat
/// comptable au résultat fiscal (réintégrations / déductions), imputation des déficits et amortissements
/// différés reportables, et données d'entrée du calcul de l'impôt (CA local TTC, acomptes, RS subies,
/// crédit antérieur). Le calcul lui-même est réalisé par un service pur — cet agrégat ne stocke que les
/// entrées éditables. Une seule feuille par exercice.
/// </summary>
public sealed class FiscalResultDeclaration : AggregateRoot
{
    public int FiscalYear { get; private set; }
    public TaxpayerKind TaxpayerKind { get; private set; }
    public FiscalDeclarationStatus Status { get; private set; }

    /// <summary>Résultat comptable net de l'exercice (point de départ du passage fiscal ; l'IS est réintégré).</summary>
    public decimal AccountingResult { get; private set; }
    /// <summary>Taux IS appliqué (fraction, ex. 0,15). Ignoré pour l'IRPP (barème).</summary>
    public decimal AppliedIsRate { get; private set; }
    /// <summary>Chiffre d'affaires local TTC servant d'assiette au minimum d'impôt.</summary>
    public decimal LocalTurnoverTtc { get; private set; }
    /// <summary>
    /// Régime de minimum d'impôt applicable : droit commun, réduit, ou exonéré (société nouvellement
    /// créée, ZDR, totalement exportatrice…).
    /// </summary>
    public MinimumTaxRegime MinimumTaxRegime { get; private set; }
    /// <summary>Total des acomptes provisionnels versés au titre de l'exercice.</summary>
    public decimal AcomptesPaid { get; private set; }
    /// <summary>Retenues à la source subies (créditées sur l'impôt).</summary>
    public decimal WithholdingSuffered { get; private set; }
    /// <summary>Crédit d'impôt reporté d'un exercice antérieur.</summary>
    public decimal PriorTaxCredit { get; private set; }

    public DateTime? FinalizedAt { get; private set; }
    public string? FinalizedBy { get; private set; }

    private readonly List<FiscalAdjustmentLine> _adjustments = new();
    public IReadOnlyCollection<FiscalAdjustmentLine> Adjustments => _adjustments.AsReadOnly();

    private readonly List<FiscalCarryForwardItem> _carryForwards = new();
    public IReadOnlyCollection<FiscalCarryForwardItem> CarryForwards => _carryForwards.AsReadOnly();

    private FiscalResultDeclaration() { }

    public static FiscalResultDeclaration Create(int fiscalYear, TaxpayerKind taxpayerKind)
    {
        if (fiscalYear < 2000 || fiscalYear > 2100)
            throw new ArgumentOutOfRangeException(nameof(fiscalYear));

        return new FiscalResultDeclaration
        {
            FiscalYear = fiscalYear,
            TaxpayerKind = taxpayerKind,
            Status = FiscalDeclarationStatus.Draft
        };
    }

    private void EnsureEditable()
    {
        if (Status == FiscalDeclarationStatus.Finalized)
            throw new InvalidOperationException("La feuille de détermination du résultat fiscal est finalisée et non modifiable.");
    }

    /// <param name="accountingNetResult">
    /// Résultat comptable <b>net (après impôt)</b> — l'IS comptabilisé est réintégré par une ligne
    /// d'ajustement, ne pas fournir ici un résultat avant impôt (double comptage).
    /// </param>
    public void UpdateInputs(
        TaxpayerKind taxpayerKind,
        decimal accountingNetResult,
        decimal appliedIsRate,
        decimal localTurnoverTtc,
        decimal acomptesPaid,
        decimal withholdingSuffered,
        decimal priorTaxCredit,
        MinimumTaxRegime minimumTaxRegime = MinimumTaxRegime.Standard)
    {
        EnsureEditable();
        TaxpayerKind = taxpayerKind;
        MinimumTaxRegime = minimumTaxRegime;
        AccountingResult = Math.Round(accountingNetResult, 3);
        AppliedIsRate = Math.Round(appliedIsRate, 5);
        LocalTurnoverTtc = NonNeg(localTurnoverTtc);
        AcomptesPaid = NonNeg(acomptesPaid);
        WithholdingSuffered = NonNeg(withholdingSuffered);
        PriorTaxCredit = NonNeg(priorTaxCredit);
    }

    public void ReplaceAdjustments(IEnumerable<FiscalAdjustmentLine> lines)
    {
        EnsureEditable();
        _adjustments.Clear();
        foreach (var l in lines ?? Enumerable.Empty<FiscalAdjustmentLine>())
        {
            l.AssignParentId(Id);
            _adjustments.Add(l);
        }
    }

    public void ReplaceCarryForwards(IEnumerable<FiscalCarryForwardItem> items)
    {
        EnsureEditable();
        _carryForwards.Clear();
        foreach (var i in items ?? Enumerable.Empty<FiscalCarryForwardItem>())
        {
            i.AssignParentId(Id);
            _carryForwards.Add(i);
        }
    }

    public void Finalize(string userId)
    {
        if (Status == FiscalDeclarationStatus.Finalized)
            return;
        Status = FiscalDeclarationStatus.Finalized;
        FinalizedAt = DateTime.UtcNow;
        FinalizedBy = userId;
    }

    public void Reopen()
    {
        Status = FiscalDeclarationStatus.Draft;
        FinalizedAt = null;
        FinalizedBy = null;
    }

    private static decimal NonNeg(decimal v) => v < 0 ? 0 : Math.Round(v, 3);
}

/// <summary>Ligne de réintégration ou de déduction du passage résultat comptable → résultat fiscal.</summary>
public sealed class FiscalAdjustmentLine : Entity
{
    public Guid FiscalResultDeclarationId { get; private set; }
    public FiscalAdjustmentKind Kind { get; private set; }
    /// <summary>Code catalogue (ligne standard) ou null pour une ligne libre.</summary>
    public string? CatalogCode { get; private set; }
    public string Label { get; private set; } = null!;
    public decimal Amount { get; private set; }
    /// <summary>Ligne suggérée automatiquement à partir des comptes (à valider par le comptable).</summary>
    public bool IsAutoSuggested { get; private set; }

    private FiscalAdjustmentLine() { }

    public static FiscalAdjustmentLine Create(FiscalAdjustmentKind kind, string? catalogCode, string label, decimal amount, bool isAutoSuggested = false)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Le libellé de la ligne est obligatoire.", nameof(label));

        return new FiscalAdjustmentLine
        {
            Kind = kind,
            CatalogCode = string.IsNullOrWhiteSpace(catalogCode) ? null : catalogCode.Trim(),
            Label = label.Trim(),
            Amount = amount < 0 ? 0 : Math.Round(amount, 3),
            IsAutoSuggested = isAutoSuggested
        };
    }

    internal void AssignParentId(Guid declarationId)
    {
        if (declarationId == Guid.Empty)
            throw new ArgumentException("L'identifiant de la déclaration est obligatoire.", nameof(declarationId));
        FiscalResultDeclarationId = declarationId;
    }
}

/// <summary>Élément reportable (déficit ou amortissement différé) imputable sur le résultat fiscal.</summary>
public sealed class FiscalCarryForwardItem : Entity
{
    public Guid FiscalResultDeclarationId { get; private set; }
    public FiscalCarryForwardKind Kind { get; private set; }
    public int OriginYear { get; private set; }
    /// <summary>Montant initial reportable (stock à l'ouverture pour cet exercice).</summary>
    public decimal InitialAmount { get; private set; }
    /// <summary>Montant imputé sur le résultat fiscal de l'exercice courant.</summary>
    public decimal ImputedThisYear { get; private set; }
    /// <summary>Dernier exercice d'imputation possible (null = illimité, ex. amortissements différés).</summary>
    public int? ExpiryYear { get; private set; }

    private FiscalCarryForwardItem() { }

    /// <param name="deficitCarryForwardYears">
    /// Durée de report des déficits ordinaires (défaut 5). Sert à déduire automatiquement
    /// <see cref="ExpiryYear"/> lorsqu'il n'est pas fourni. Les amortissements réputés différés sont
    /// reportables sans limite : leur échéance reste nulle.
    /// </param>
    public static FiscalCarryForwardItem Create(
        FiscalCarryForwardKind kind, int originYear, decimal initialAmount, decimal imputedThisYear, int? expiryYear,
        int deficitCarryForwardYears = 5)
    {
        var initial = initialAmount < 0 ? 0 : Math.Round(initialAmount, 3);
        var imputed = imputedThisYear < 0 ? 0 : Math.Round(imputedThisYear, 3);

        // Invariant : on ne peut pas imputer plus que le stock reportable disponible.
        if (imputed > initial)
            imputed = initial;

        // Déficit ordinaire : échéance déduite de l'exercice d'origine si non fournie.
        // Amortissement réputé différé : report illimité (échéance nulle).
        var expiry = expiryYear;
        if (expiry is null && kind == FiscalCarryForwardKind.Deficit && deficitCarryForwardYears > 0)
            expiry = originYear + deficitCarryForwardYears;

        return new FiscalCarryForwardItem
        {
            Kind = kind,
            OriginYear = originYear,
            InitialAmount = initial,
            ImputedThisYear = imputed,
            ExpiryYear = kind == FiscalCarryForwardKind.DeferredDepreciation ? null : expiry
        };
    }

    internal void AssignParentId(Guid declarationId)
    {
        if (declarationId == Guid.Empty)
            throw new ArgumentException("L'identifiant de la déclaration est obligatoire.", nameof(declarationId));
        FiscalResultDeclarationId = declarationId;
    }
}
