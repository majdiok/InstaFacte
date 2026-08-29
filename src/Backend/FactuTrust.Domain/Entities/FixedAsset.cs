using System.Text.RegularExpressions;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Fixed asset register entry (immobilisation) — SCE Tunisia.
/// </summary>
public sealed class FixedAsset : AggregateRoot
{
    /// <summary>
    /// Format minimal partagé d'un numéro de compte comptable (chiffres uniquement, 2 à 20
    /// caractères). Invariant de dernier recours au niveau du domaine : les règles complètes
    /// (préfixes NCT, cohérence corporel/incorporel) vivent dans
    /// <c>FixedAssetAccountRules</c> (Application), appliquées en amont par les handlers.
    /// </summary>
    private static readonly Regex AccountNumberFormat = new("^\\d{2,20}$", RegexOptions.Compiled);

    /// <summary>
    /// Format du numéro d'inventaire (plan T5, B2) : <c>IMMO-{année}-{séquence sur 4 chiffres}</c>.
    /// </summary>
    private static readonly Regex InventoryNumberFormat = new("^IMMO-\\d{4}-\\d{4}$", RegexOptions.Compiled);

    /// <summary>
    /// Coefficients d'amortissement accéléré autorisés par le Décret 2008-492 art. 2 :
    /// 1,5 pour deux équipes (matériel industriel ; taux 15 % → 22,5 %) et 2 pour trois équipes (→ 30 %).
    /// </summary>
    public static readonly decimal[] AllowedAccelerationCoefficients = new[] { 1.5m, 2m };

    /// <summary>
    /// Valeur maximale d'un bien immobilisé de faible valeur éligible à l'amortissement intégral
    /// au titre de l'année de sa mise en service (Décret 2008-492 art. 4).
    /// </summary>
    public const decimal LowValueAssetCeilingTnd = 200m;

    public string InventoryNumber { get; private set; } = null!;
    public string Label { get; private set; } = null!;
    public string? Description { get; private set; }
    public string AssetAccountNumber { get; private set; } = null!;
    public string DepreciationAccountNumber { get; private set; } = null!;
    public string ExpenseAccountNumber { get; private set; } = null!;
    public decimal AcquisitionCost { get; private set; }
    public decimal CapitalizedFees { get; private set; }
    public decimal ResidualValue { get; private set; }
    public decimal VatAmount { get; private set; }
    public bool VatCapitalized { get; private set; }
    public DateTime AcquisitionDate { get; private set; }
    public DateTime? InServiceDate { get; private set; }
    public DateTime? DisposalDate { get; private set; }
    public Guid DepreciationRateCategoryId { get; private set; }
    public DepreciationRateCategory? DepreciationRateCategory { get; private set; }
    public decimal DepreciationRatePercent { get; private set; }
    public decimal UsefulLifeYears { get; private set; }
    public DepreciationMethod DepreciationMethod { get; private set; }
    public decimal AccelerationCoefficient { get; private set; } = 1m;
    public FixedAssetStatus Status { get; private set; }
    public Guid? SupplierId { get; private set; }
    public Guid? SupplierInvoiceId { get; private set; }
    public Guid? SupplierInvoiceLineId { get; private set; }
    public string? Location { get; private set; }
    public decimal AccumulatedDepreciation { get; private set; }
    public decimal NetBookValue { get; private set; }
    public string? CreditAccountNumber { get; private set; }
    public decimal? DisposalProceeds { get; private set; }
    public string? DisposalTreasuryAccount { get; private set; }

    private readonly List<DepreciationScheduleLine> _scheduleLines = new();
    public IReadOnlyCollection<DepreciationScheduleLine> ScheduleLines => _scheduleLines.AsReadOnly();

    private readonly List<FixedAssetEvent> _events = new();
    public IReadOnlyCollection<FixedAssetEvent> Events => _events.AsReadOnly();

    private FixedAsset() { }

    public decimal DepreciableBase => Math.Max(0, TotalCapitalizedCost - ResidualValue);

    public decimal TotalCapitalizedCost => AcquisitionCost + CapitalizedFees + (VatCapitalized ? VatAmount : 0);

    public static Result<FixedAsset> Create(
        string inventoryNumber,
        string label,
        Guid depreciationRateCategoryId,
        decimal depreciationRatePercent,
        decimal usefulLifeYears,
        string assetAccountNumber,
        string depreciationAccountNumber,
        string expenseAccountNumber,
        decimal acquisitionCost,
        decimal capitalizedFees,
        decimal residualValue,
        DateTime acquisitionDate,
        string? description = null,
        decimal vatAmount = 0,
        string? location = null,
        Guid? supplierId = null,
        DepreciationMethod depreciationMethod = DepreciationMethod.Linear,
        decimal accelerationCoefficient = 1m,
        bool vatCapitalized = false)
    {
        inventoryNumber = inventoryNumber?.Trim() ?? string.Empty;
        label = label?.Trim() ?? string.Empty;
        assetAccountNumber = assetAccountNumber?.Trim() ?? string.Empty;
        depreciationAccountNumber = depreciationAccountNumber?.Trim() ?? string.Empty;
        expenseAccountNumber = expenseAccountNumber?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(inventoryNumber))
            return Result.Failure<FixedAsset>(Error.Validation("InventoryNumber", "Le numéro d'inventaire est obligatoire"));
        if (string.IsNullOrEmpty(label))
            return Result.Failure<FixedAsset>(Error.Validation("Label", "La désignation est obligatoire"));
        if (depreciationRateCategoryId == Guid.Empty)
            return Result.Failure<FixedAsset>(Error.Validation("DepreciationRateCategoryId", "La catégorie d'amortissement est obligatoire"));
        if (acquisitionCost < 0 || capitalizedFees < 0 || residualValue < 0 || vatAmount < 0)
            return Result.Failure<FixedAsset>(Error.Validation("Amount", "Les montants ne peuvent pas être négatifs"));
        if (acquisitionCost + capitalizedFees <= 0)
            return Result.Failure<FixedAsset>(Error.Validation("AcquisitionCost", "Le coût d'acquisition doit être positif"));
        var totalForValidation = acquisitionCost + capitalizedFees + (vatCapitalized ? vatAmount : 0m);
        if (residualValue >= totalForValidation)
            return Result.Failure<FixedAsset>(Error.Validation("ResidualValue", "La valeur résiduelle doit être inférieure au coût total"));
        if (string.IsNullOrEmpty(assetAccountNumber) || string.IsNullOrEmpty(depreciationAccountNumber) || string.IsNullOrEmpty(expenseAccountNumber))
            return Result.Failure<FixedAsset>(Error.Validation("AccountNumber", "Les comptes comptables sont obligatoires"));
        if (!AccountNumberFormat.IsMatch(assetAccountNumber) || !AccountNumberFormat.IsMatch(depreciationAccountNumber) || !AccountNumberFormat.IsMatch(expenseAccountNumber))
            return Result.Failure<FixedAsset>(Error.Validation("AccountNumber", "Un numéro de compte comptable ne peut contenir que des chiffres (2 à 20 caractères)"));
        if (depreciationMethod == DepreciationMethod.Accelerated && !AllowedAccelerationCoefficients.Contains(accelerationCoefficient))
            return Result.Failure<FixedAsset>(Error.Validation(
                "AccelerationCoefficient",
                "Le coefficient d'amortissement accéléré doit être 1,5 (matériel industriel en deux équipes) ou 2 (trois équipes) — Décret 2008-492 art. 2"));
        if (depreciationMethod != DepreciationMethod.Accelerated && accelerationCoefficient != 1m)
            return Result.Failure<FixedAsset>(Error.Validation("AccelerationCoefficient", "Le coefficient n'est applicable qu'à la méthode accélérée"));
        if (depreciationMethod != DepreciationMethod.Linear && depreciationRatePercent <= 0)
            return Result.Failure<FixedAsset>(Error.Validation("DepreciationMethod", "Cette méthode d'amortissement nécessite un taux positif (bien amortissable)"));

        var total = acquisitionCost + capitalizedFees + (vatCapitalized ? vatAmount : 0m);
        var asset = new FixedAsset
        {
            InventoryNumber = inventoryNumber,
            Label = label,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DepreciationRateCategoryId = depreciationRateCategoryId,
            DepreciationRatePercent = depreciationRatePercent,
            UsefulLifeYears = usefulLifeYears,
            DepreciationMethod = depreciationMethod,
            AccelerationCoefficient = depreciationMethod == DepreciationMethod.Accelerated ? accelerationCoefficient : 1m,
            AssetAccountNumber = assetAccountNumber,
            DepreciationAccountNumber = depreciationAccountNumber,
            ExpenseAccountNumber = expenseAccountNumber,
            AcquisitionCost = acquisitionCost,
            CapitalizedFees = capitalizedFees,
            ResidualValue = residualValue,
            VatAmount = vatAmount,
            VatCapitalized = vatCapitalized,
            AcquisitionDate = acquisitionDate.Date,
            Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            SupplierId = supplierId,
            Status = FixedAssetStatus.Draft,
            AccumulatedDepreciation = 0,
            NetBookValue = total
        };

        asset._events.Add(FixedAssetEvent.Create(asset.Id, FixedAssetEventType.Created, acquisitionDate.Date, null, null));
        return Result.Success(asset);
    }

    /// <summary>
    /// Réattribue le numéro d'inventaire (plan T5, B2) — utilisé par le retry ciblé du repository
    /// en cas de violation d'unicité (<c>IX_FixedAssets_InventoryNumber</c>) : format
    /// <c>IMMO-{année}-{séquence}</c> vérifié, uniquement autorisé avant la mise en service.
    /// </summary>
    public Result AssignInventoryNumber(string inventoryNumber)
    {
        if (Status != FixedAssetStatus.Draft)
            return Result.Failure(Error.Validation("InventoryNumber", "Le numéro d'inventaire ne peut être modifié qu'avant la mise en service."));

        inventoryNumber = inventoryNumber?.Trim() ?? string.Empty;
        if (!InventoryNumberFormat.IsMatch(inventoryNumber))
            return Result.Failure(Error.Validation("InventoryNumber", "Le numéro d'inventaire doit respecter le format IMMO-AAAA-9999."));

        InventoryNumber = inventoryNumber;
        return Result.Success();
    }

    public void LinkSupplierInvoiceSource(Guid supplierInvoiceId, Guid supplierInvoiceLineId)
    {
        if (supplierInvoiceId == Guid.Empty || supplierInvoiceLineId == Guid.Empty)
            return;

        SupplierInvoiceId = supplierInvoiceId;
        SupplierInvoiceLineId = supplierInvoiceLineId;
    }

    public Result UpdateDraft(
        string label,
        string? description,
        decimal acquisitionCost,
        decimal capitalizedFees,
        decimal residualValue,
        DateTime acquisitionDate,
        decimal depreciationRatePercent,
        decimal usefulLifeYears,
        string assetAccountNumber,
        string depreciationAccountNumber,
        string expenseAccountNumber,
        string? location,
        DepreciationMethod? depreciationMethod = null,
        decimal? accelerationCoefficient = null,
        Guid? depreciationRateCategoryId = null,
        decimal? vatAmount = null,
        bool? vatCapitalized = null)
    {
        if (Status != FixedAssetStatus.Draft)
            return Result.Failure(Error.Validation("Status", "Seuls les immobilisations en brouillon peuvent être modifiées"));

        label = label?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(label))
            return Result.Failure(Error.Validation("Label", "La désignation est obligatoire"));
        if (acquisitionCost < 0 || capitalizedFees < 0 || residualValue < 0)
            return Result.Failure(Error.Validation("Amount", "Les montants ne peuvent pas être négatifs"));
        if (vatAmount is < 0)
            return Result.Failure(Error.Validation("VatAmount", "Le montant de TVA ne peut pas être négatif"));
        if (acquisitionCost + capitalizedFees <= 0)
            return Result.Failure(Error.Validation("AcquisitionCost", "Le coût d'acquisition doit être positif"));
        var effectiveVatAmount = vatAmount ?? VatAmount;
        var effectiveVatCapitalized = vatCapitalized ?? VatCapitalized;
        var totalForValidation = acquisitionCost + capitalizedFees + (effectiveVatCapitalized ? effectiveVatAmount : 0m);
        if (residualValue >= totalForValidation)
            return Result.Failure(Error.Validation("ResidualValue", "La valeur résiduelle doit être inférieure au coût total"));

        var method = depreciationMethod ?? DepreciationMethod;
        var coefficient = accelerationCoefficient ?? (method == DepreciationMethod.Accelerated ? AccelerationCoefficient : 1m);
        if (method == DepreciationMethod.Accelerated && !AllowedAccelerationCoefficients.Contains(coefficient))
            return Result.Failure(Error.Validation(
                "AccelerationCoefficient",
                "Le coefficient d'amortissement accéléré doit être 1,5 (matériel industriel en deux équipes) ou 2 (trois équipes) — Décret 2008-492 art. 2"));
        if (method != DepreciationMethod.Accelerated && coefficient != 1m)
            return Result.Failure(Error.Validation("AccelerationCoefficient", "Le coefficient n'est applicable qu'à la méthode accélérée"));
        if (method != DepreciationMethod.Linear && depreciationRatePercent <= 0)
            return Result.Failure(Error.Validation("DepreciationMethod", "Cette méthode d'amortissement nécessite un taux positif (bien amortissable)"));

        assetAccountNumber = assetAccountNumber?.Trim() ?? string.Empty;
        depreciationAccountNumber = depreciationAccountNumber?.Trim() ?? string.Empty;
        expenseAccountNumber = expenseAccountNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(assetAccountNumber) || string.IsNullOrEmpty(depreciationAccountNumber) || string.IsNullOrEmpty(expenseAccountNumber))
            return Result.Failure(Error.Validation("AccountNumber", "Les comptes comptables sont obligatoires"));
        if (!AccountNumberFormat.IsMatch(assetAccountNumber) || !AccountNumberFormat.IsMatch(depreciationAccountNumber) || !AccountNumberFormat.IsMatch(expenseAccountNumber))
            return Result.Failure(Error.Validation("AccountNumber", "Un numéro de compte comptable ne peut contenir que des chiffres (2 à 20 caractères)"));

        Label = label;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        AcquisitionCost = acquisitionCost;
        CapitalizedFees = capitalizedFees;
        ResidualValue = residualValue;
        AcquisitionDate = acquisitionDate.Date;
        DepreciationRatePercent = depreciationRatePercent;
        UsefulLifeYears = usefulLifeYears;
        DepreciationMethod = method;
        AccelerationCoefficient = method == DepreciationMethod.Accelerated ? coefficient : 1m;
        if (depreciationRateCategoryId is { } categoryId && categoryId != Guid.Empty)
            DepreciationRateCategoryId = categoryId;
        if (vatAmount is { } vat)
            VatAmount = vat;
        VatCapitalized = effectiveVatCapitalized;
        AssetAccountNumber = assetAccountNumber.Trim();
        DepreciationAccountNumber = depreciationAccountNumber.Trim();
        ExpenseAccountNumber = expenseAccountNumber.Trim();
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        NetBookValue = TotalCapitalizedCost - AccumulatedDepreciation;
        return Result.Success();
    }

    public Result PutInService(DateTime inServiceDate, string creditAccountNumber)
    {
        if (Status != FixedAssetStatus.Draft)
            return Result.Failure(Error.Validation("Status", "L'immobilisation est déjà en service ou clôturée"));

        creditAccountNumber = creditAccountNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(creditAccountNumber))
            return Result.Failure(Error.Validation("CreditAccountNumber", "Le compte de crédit est obligatoire"));

        if (inServiceDate.Date < AcquisitionDate)
            return Result.Failure(Error.Validation("InServiceDate", "La date de mise en service ne peut pas précéder l'acquisition"));

        if (DepreciationRatePercent <= 0)
        {
            Status = FixedAssetStatus.InService;
            InServiceDate = inServiceDate.Date;
            CreditAccountNumber = creditAccountNumber;
            _events.Add(FixedAssetEvent.Create(Id, FixedAssetEventType.InService, inServiceDate.Date, TotalCapitalizedCost, null));
            return Result.Success();
        }

        Status = FixedAssetStatus.InService;
        InServiceDate = inServiceDate.Date;
        CreditAccountNumber = creditAccountNumber;
        NetBookValue = TotalCapitalizedCost;
        _events.Add(FixedAssetEvent.Create(Id, FixedAssetEventType.InService, inServiceDate.Date, TotalCapitalizedCost, null));
        return Result.Success();
    }

    public Result Dispose(DateTime disposalDate, decimal disposalProceeds, string treasuryAccountNumber)
    {
        if (Status is FixedAssetStatus.Draft or FixedAssetStatus.Disposed)
            return Result.Failure(Error.Validation("Status", "Cette immobilisation ne peut pas être cédée"));

        treasuryAccountNumber = treasuryAccountNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(treasuryAccountNumber))
            return Result.Failure(Error.Validation("TreasuryAccountNumber", "Le compte de trésorerie est obligatoire"));

        if (InServiceDate is null || disposalDate.Date < InServiceDate.Value)
            return Result.Failure(Error.Validation("DisposalDate", "La date de cession ne peut pas précéder la mise en service"));

        if (disposalProceeds < 0)
            return Result.Failure(Error.Validation("DisposalProceeds", "Le prix de cession ne peut pas être négatif"));

        Status = FixedAssetStatus.Disposed;
        DisposalDate = disposalDate.Date;
        DisposalProceeds = disposalProceeds;
        DisposalTreasuryAccount = treasuryAccountNumber;
        _events.Add(FixedAssetEvent.Create(Id, FixedAssetEventType.Disposed, disposalDate.Date, disposalProceeds, null));
        return Result.Success();
    }

    public void ApplyDepreciation(decimal amount, decimal newAccumulated, decimal newNbv)
    {
        AccumulatedDepreciation = newAccumulated;
        NetBookValue = newNbv;
        if (newNbv <= ResidualValue && DepreciationRatePercent > 0)
            Status = FixedAssetStatus.FullyDepreciated;
    }

    public void AddScheduleLine(DepreciationScheduleLine line) => _scheduleLines.Add(line);

    public void AddEvent(FixedAssetEvent evt) => _events.Add(evt);

    public void ClearScheduleLines() => _scheduleLines.Clear();
}