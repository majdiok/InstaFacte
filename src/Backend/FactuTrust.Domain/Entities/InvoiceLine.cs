using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a line item on an invoice.
/// </summary>
public sealed class InvoiceLine : Entity
{
    public Guid InvoiceId { get; private set; }
    public Invoice Invoice { get; private set; } = null!;
    
    public int LineNumber { get; private set; }
    
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    
    public string ProductCode { get; private set; } = null!;
    public string ProductName { get; private set; } = null!;
    public string? ProductDescription { get; private set; }
    
    public decimal Quantity { get; private set; }
    public string? Unit { get; private set; }
    public Money UnitPrice { get; private set; } = null!;
    public VatRate VatRate { get; private set; }

    /// <summary>Snapshot: FODEC applies on this line (from product or custom line flag).</summary>
    public bool IsFodecApplicable { get; private set; }
    
    public decimal? DiscountPercent { get; private set; }
    public Money DiscountAmount { get; private set; } = null!;
    
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

    public Money FodecAmount { get; private set; } = null!;
    public Money VatAmount { get; private set; } = null!;
    public Money Total { get; private set; } = null!;

    private decimal _fodecRatePercent;

    private InvoiceLine() { }

    internal static Result<InvoiceLine> Create(
        Invoice invoice,
        int lineNumber,
        Product product,
        decimal quantity,
        Money unitPrice,
        decimal? discountPercent = null,
        decimal fodecRatePercent = 1.0m)
    {
        if (quantity <= 0)
            return Result.Failure<InvoiceLine>(Error.Validation("Quantity", "La quantité doit être supérieure à zéro"));

        if (discountPercent.HasValue && (discountPercent.Value < 0 || discountPercent.Value > 100))
            return Result.Failure<InvoiceLine>(Error.Validation("DiscountPercent", "La remise doit être comprise entre 0% et 100%"));

        var line = new InvoiceLine
        {
            InvoiceId = invoice.Id,
            Invoice = invoice,
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
            IsFodecApplicable = product.IsFodecApplicable,
            DiscountPercent = discountPercent,
            _fodecRatePercent = fodecRatePercent
        };

        line.Calculate();

        return Result.Success(line);
    }

    /// <summary>
    /// Creates a custom invoice line without a product reference.
    /// </summary>
    internal static Result<InvoiceLine> CreateCustom(
        Invoice invoice,
        int lineNumber,
        string designation,
        string? description,
        decimal quantity,
        string unit,
        Money unitPrice,
        VatRate vatRate,
        decimal? discountPercent = null,
        bool isFodecApplicable = false,
        decimal fodecRatePercent = 1.0m)
    {
        if (quantity <= 0)
            return Result.Failure<InvoiceLine>(Error.Validation("Quantity", "La quantité doit être supérieure à zéro"));

        if (discountPercent.HasValue && (discountPercent.Value < 0 || discountPercent.Value > 100))
            return Result.Failure<InvoiceLine>(Error.Validation("DiscountPercent", "La remise doit être comprise entre 0% et 100%"));

        if (string.IsNullOrWhiteSpace(designation))
            return Result.Failure<InvoiceLine>(Error.Validation("Designation", "La désignation est obligatoire"));

        var line = new InvoiceLine
        {
            InvoiceId = invoice.Id,
            Invoice = invoice,
            LineNumber = lineNumber,
            ProductId = Guid.Empty, // No product reference
            ProductCode = "CUSTOM",
            ProductName = designation,
            ProductDescription = description,
            Quantity = quantity,
            Unit = unit,
            UnitPrice = unitPrice,
            VatRate = vatRate,
            IsFodecApplicable = isFodecApplicable,
            DiscountPercent = discountPercent,
            _fodecRatePercent = fodecRatePercent
        };

        line.Calculate();

        return Result.Success(line);
    }

    internal Result Update(
        decimal quantity,
        Money? customUnitPrice = null,
        decimal? discountPercent = null,
        decimal? fodecRatePercent = null)
    {
        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité doit être supérieure à zéro"));

        if (discountPercent.HasValue && (discountPercent.Value < 0 || discountPercent.Value > 100))
            return Result.Failure(Error.Validation("DiscountPercent", "La remise doit être comprise entre 0% et 100%"));

        Quantity = quantity;
        
        if (customUnitPrice is not null)
            UnitPrice = customUnitPrice;
        
        DiscountPercent = discountPercent;

        if (fodecRatePercent.HasValue)
            _fodecRatePercent = fodecRatePercent.Value;
        
        Calculate();

        return Result.Success();
    }

    internal void SetFodecApplicable(bool value, decimal fodecRatePercent)
    {
        IsFodecApplicable = value;
        _fodecRatePercent = fodecRatePercent;
        Calculate();
    }

    internal void SetLineNumber(int lineNumber)
    {
        LineNumber = lineNumber;
    }

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

        FodecAmount = IsFodecApplicable && _fodecRatePercent > 0
            ? SubTotal.ApplyPercentage(_fodecRatePercent)
            : Money.Zero(UnitPrice.Currency);

        var vatBase = SubTotal.Add(FodecAmount);
        VatAmount = vatBase.ApplyPercentage(VatRate.ToDecimal());
        Total = SubTotal.Add(FodecAmount).Add(VatAmount);
    }
}
