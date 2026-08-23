using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a line item on a quote.
/// </summary>
public sealed class QuoteLine : Entity
{
    public Guid QuoteId { get; private set; }
    public Quote Quote { get; private set; } = null!;
    
    public int LineNumber { get; private set; }
    
    public Guid? ProductId { get; private set; }
    public Product? Product { get; private set; }
    
    public string ProductCode { get; private set; } = null!;
    public string ProductName { get; private set; } = null!;
    public string? ProductDescription { get; private set; }
    
    public decimal Quantity { get; private set; }
    public string? Unit { get; private set; }
    public Money UnitPrice { get; private set; } = null!;
    public VatRate VatRate { get; private set; }
    
    public decimal? DiscountPercent { get; private set; }
    public Money DiscountAmount { get; private set; } = null!;

    public Guid? AppliedPromotionId { get; private set; }
    public string? AppliedPromotionName { get; private set; }

    /// <summary>
    /// Snapshot de <see cref="Product.IsFodecApplicable"/> à la création de la ligne.
    /// Le devis doit annoncer le FODEC que la facture appliquera, sans quoi le client
    /// reçoit une facture supérieure au devis qu'il a accepté.
    /// </summary>
    public bool IsFodecApplicable { get; private set; }

    /// <summary>Montant FODEC de la ligne (assiette : HT après remise).</summary>
    public Money FodecAmount { get; private set; } = null!;

    public Money SubTotal { get; private set; } = null!;

    /// <summary>
    /// Part de la remise de pied de document imputée à cette ligne, répartie au prorata de sa
    /// base HT par <see cref="FactuTrust.Domain.Services.GlobalDiscountAllocator"/>.
    ///
    /// Distincte de <c>DiscountAmount</c>, qui est la remise négociée SUR la ligne : les deux
    /// doivent rester lisibles séparément sur le document. Vaut zéro tant qu'aucune remise de
    /// pied n'est posée — la ligne se calcule alors exactement comme avant la tranche 5B.
    /// </summary>
    public Money AllocatedGlobalDiscount { get; private set; } = Money.Zero();

    /// <summary>Base HT de la ligne AVANT imputation de la remise de pied.</summary>
    public Money SubTotalBeforeGlobalDiscount => SubTotal.Add(AllocatedGlobalDiscount);

    /// <summary>Appelé par l'agrégat lors de la répartition ; recalcule la ligne dans la foulée.</summary>
    internal void SetAllocatedGlobalDiscount(Money allocated)
    {
        AllocatedGlobalDiscount = allocated;
        Calculate();
    }

    public Money VatAmount { get; private set; } = null!;
    public Money Total { get; private set; } = null!;

    /// <summary>Taux FODEC appliqué, aligné sur InvoiceLine (1 % par défaut).</summary>
    public decimal FodecRatePercent { get; private set; }

    private QuoteLine() { }

    /// <summary>Taux FODEC par défaut (1 %), aligné sur <see cref="Invoice.AddLine"/>.</summary>
    public const decimal DefaultFodecRatePercent = 1.0m;

    internal static Result<QuoteLine> Create(
        Quote quote,
        int lineNumber,
        Product product,
        decimal quantity,
        Money unitPrice,
        decimal? discountPercent = null,
        decimal fodecRatePercent = DefaultFodecRatePercent,
        Guid? appliedPromotionId = null,
        string? appliedPromotionName = null)
    {
        if (quantity <= 0)
            return Result.Failure<QuoteLine>(Error.Validation("Quantity", "La quantité doit être supérieure à zéro"));

        if (discountPercent.HasValue && (discountPercent.Value < 0 || discountPercent.Value > 100))
            return Result.Failure<QuoteLine>(Error.Validation("DiscountPercent", "La remise doit être comprise entre 0% et 100%"));

        var line = new QuoteLine
        {
            QuoteId = quote.Id,
            Quote = quote,
            LineNumber = lineNumber,
            ProductId = product.Id,
            Product = product,
            ProductCode = product.Code,
            ProductName = product.Name,
            ProductDescription = product.Description,
            Quantity = quantity,
            Unit = product.Unit,
            UnitPrice = unitPrice,
            VatRate = product.VatRate,
            DiscountPercent = discountPercent,
            AppliedPromotionId = appliedPromotionId,
            AppliedPromotionName = appliedPromotionName?.Trim(),
            IsFodecApplicable = product.IsFodecApplicable,
            FodecRatePercent = fodecRatePercent
        };

        line.Calculate();

        return Result.Success(line);
    }

    /// <summary>
    /// Creates a custom quote line without a product reference.
    /// </summary>
    internal static Result<QuoteLine> CreateCustom(
        Quote quote,
        int lineNumber,
        string designation,
        string? description,
        decimal quantity,
        string unit,
        Money unitPrice,
        VatRate vatRate,
        decimal? discountPercent = null,
        bool isFodecApplicable = false,
        decimal fodecRatePercent = DefaultFodecRatePercent)
    {
        if (quantity <= 0)
            return Result.Failure<QuoteLine>(Error.Validation("Quantity", "La quantité doit être supérieure à zéro"));

        if (discountPercent.HasValue && (discountPercent.Value < 0 || discountPercent.Value > 100))
            return Result.Failure<QuoteLine>(Error.Validation("DiscountPercent", "La remise doit être comprise entre 0% et 100%"));

        if (string.IsNullOrWhiteSpace(designation))
            return Result.Failure<QuoteLine>(Error.Validation("Designation", "La désignation est obligatoire"));

        var line = new QuoteLine
        {
            QuoteId = quote.Id,
            Quote = quote,
            LineNumber = lineNumber,
            ProductId = null,
            Product = null,
            ProductCode = "CUSTOM",
            ProductName = designation,
            ProductDescription = description,
            Quantity = quantity,
            Unit = unit,
            UnitPrice = unitPrice,
            VatRate = vatRate,
            DiscountPercent = discountPercent,
            IsFodecApplicable = isFodecApplicable,
            FodecRatePercent = fodecRatePercent
        };

        line.Calculate();

        return Result.Success(line);
    }

    internal Result Update(decimal quantity, Money? customUnitPrice = null, decimal? discountPercent = null)
    {
        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité doit être supérieure à zéro"));

        if (discountPercent.HasValue && (discountPercent.Value < 0 || discountPercent.Value > 100))
            return Result.Failure(Error.Validation("DiscountPercent", "La remise doit être comprise entre 0% et 100%"));

        Quantity = quantity;
        
        if (customUnitPrice is not null)
            UnitPrice = customUnitPrice;
        
        DiscountPercent = discountPercent;
        
        Calculate();

        return Result.Success();
    }

    internal void SetLineNumber(int lineNumber)
    {
        LineNumber = lineNumber;
    }

    /// <summary>
    /// Rigoureusement identique à <c>InvoiceLine.Calculate()</c> : remise → FODEC → base TVA.
    /// C'est cette identité qui garantit qu'un devis accepté et la facture qui en découle
    /// portent le même total.
    /// </summary>
    private void Calculate()
    {
        var grossAmount = UnitPrice.Multiply(Quantity);

        if (DiscountPercent.HasValue && DiscountPercent.Value > 0)
        {
            DiscountAmount = grossAmount.ApplyPercentage(DiscountPercent.Value);
            SubTotal = grossAmount.Subtract(DiscountAmount);
        }
        else
        {
            DiscountAmount = Money.Zero(UnitPrice.Currency);
            SubTotal = grossAmount;
        }


        // Remise de pied : imputée APRÈS la remise de ligne et AVANT le FODEC, si bien que le
        // FODEC et la TVA portent sur la base réellement facturée. Zéro tant qu'aucune remise
        // de pied n'est posée — le calcul est alors identique à celui d'avant la tranche 5B.
        if (AllocatedGlobalDiscount is { Amount: > 0 })
            SubTotal = SubTotal.Subtract(AllocatedGlobalDiscount);

        FodecAmount = IsFodecApplicable && FodecRatePercent > 0
            ? SubTotal.ApplyPercentage(FodecRatePercent)
            : Money.Zero(UnitPrice.Currency);

        var vatBase = SubTotal.Add(FodecAmount);
        VatAmount = vatBase.ApplyPercentage(VatRate.ToDecimal());
        Total = SubTotal.Add(FodecAmount).Add(VatAmount);
    }
}
