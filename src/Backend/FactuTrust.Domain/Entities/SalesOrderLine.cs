using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Ligne de commande client.
///
/// Elle suit TROIS quantités indépendantes, et c'est tout l'intérêt de la commande :
/// commandée, livrée, facturée. Le bon de livraison ne connaît que « commandé / livré » et
/// perd son reliquat une fois clos ; la facture ne connaît que ce qu'elle facture. Seule la
/// commande porte le reste à livrer ET le reste à facturer dans la durée.
///
/// Le moteur de calcul est rigoureusement celui d'<see cref="InvoiceLine"/> et de
/// <see cref="QuoteLine"/> — remise → FODEC → base TVA, arrondi au millime à chaque étape —
/// pour que Devis → Commande → BL → Facture affichent le même montant de bout en bout.
/// </summary>
public sealed class SalesOrderLine : Entity
{
    public Guid SalesOrderId { get; private set; }
    public SalesOrder SalesOrder { get; private set; } = null!;

    public int LineNumber { get; private set; }

    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;

    // Instantané produit figé à la création : la commande reste lisible même si le catalogue bouge.
    public string ProductCode { get; private set; } = null!;
    public string ProductName { get; private set; } = null!;
    public string? ProductDescription { get; private set; }
    public string? Unit { get; private set; }

    /// <summary>Quantité commandée par le client.</summary>
    public decimal Quantity { get; private set; }

    /// <summary>Quantité effectivement livrée, cumulée sur tous les bons de livraison.</summary>
    public decimal DeliveredQuantity { get; private set; }

    /// <summary>Quantité effectivement facturée, cumulée sur toutes les factures.</summary>
    public decimal InvoicedQuantity { get; private set; }

    public Money UnitPrice { get; private set; } = null!;
    public VatRate VatRate { get; private set; }

    public decimal? DiscountPercent { get; private set; }
    public Money DiscountAmount { get; private set; } = null!;

    public bool IsFodecApplicable { get; private set; }
    public decimal FodecRatePercent { get; private set; }
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

    public string? Notes { get; private set; }

    /// <summary>Taux FODEC par défaut (1 %), aligné sur le reste de la chaîne documentaire.</summary>
    public const decimal DefaultFodecRatePercent = 1.0m;

    /// <summary>Reste à livrer. C'est le reliquat que le bon de livraison seul ne sait pas porter.</summary>
    public decimal PendingDeliveryQuantity => Math.Max(0m, Quantity - DeliveredQuantity);

    /// <summary>Reste à facturer.</summary>
    public decimal PendingInvoiceQuantity => Math.Max(0m, Quantity - InvoicedQuantity);

    /// <summary>Quantité livrée mais pas encore facturée — assiette de la facturation périodique.</summary>
    public decimal DeliveredNotInvoicedQuantity => Math.Max(0m, DeliveredQuantity - InvoicedQuantity);

    public bool IsFullyDelivered => DeliveredQuantity >= Quantity;
    public bool IsFullyInvoiced => InvoicedQuantity >= Quantity;

    private SalesOrderLine() { }

    internal static Result<SalesOrderLine> Create(
        SalesOrder salesOrder,
        int lineNumber,
        Product product,
        decimal quantity,
        Money unitPrice,
        decimal? discountPercent = null,
        decimal fodecRatePercent = DefaultFodecRatePercent,
        string? notes = null)
    {
        if (product is null)
            return Result.Failure<SalesOrderLine>(Error.Validation("Product", "Le produit est obligatoire"));

        if (quantity <= 0)
            return Result.Failure<SalesOrderLine>(Error.Validation("Quantity", "La quantité doit être supérieure à zéro"));

        if (discountPercent.HasValue && (discountPercent.Value < 0 || discountPercent.Value > 100))
            return Result.Failure<SalesOrderLine>(Error.Validation("DiscountPercent", "La remise doit être comprise entre 0% et 100%"));

        var line = new SalesOrderLine
        {
            SalesOrderId = salesOrder.Id,
            SalesOrder = salesOrder,
            LineNumber = lineNumber,
            ProductId = product.Id,
            Product = product,
            ProductCode = product.Code,
            ProductName = product.Name,
            ProductDescription = product.Description,
            Unit = product.Unit,
            Quantity = quantity,
            DeliveredQuantity = 0m,
            InvoicedQuantity = 0m,
            UnitPrice = unitPrice,
            VatRate = product.VatRate,
            DiscountPercent = discountPercent,
            IsFodecApplicable = product.IsFodecApplicable,
            FodecRatePercent = fodecRatePercent,
            Notes = notes?.Trim()
        };

        line.Calculate();
        return Result.Success(line);
    }

    internal Result Update(decimal quantity, Money? customUnitPrice = null, decimal? discountPercent = null, string? notes = null)
    {
        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité doit être supérieure à zéro"));

        if (discountPercent.HasValue && (discountPercent.Value < 0 || discountPercent.Value > 100))
            return Result.Failure(Error.Validation("DiscountPercent", "La remise doit être comprise entre 0% et 100%"));

        Quantity = quantity;

        if (customUnitPrice is not null)
            UnitPrice = customUnitPrice;

        DiscountPercent = discountPercent;
        Notes = notes?.Trim();

        Calculate();
        return Result.Success();
    }

    /// <summary>
    /// Impute une livraison sur cette ligne. Le cumul livré ne peut jamais dépasser le commandé :
    /// livrer plus que commandé n'est pas un reliquat négatif, c'est une erreur de saisie.
    /// </summary>
    internal Result RecordDelivery(decimal deliveredQuantity)
    {
        if (deliveredQuantity <= 0)
            return Result.Failure(Error.Validation("DeliveredQuantity", "La quantité livrée doit être positive"));

        if (DeliveredQuantity + deliveredQuantity > Quantity)
            return Result.Failure(Error.Validation("DeliveredQuantity",
                $"Livraison supérieure au reste à livrer sur la ligne {LineNumber} (reste : {PendingDeliveryQuantity})"));

        DeliveredQuantity += deliveredQuantity;
        return Result.Success();
    }

    /// <summary>
    /// Impute une facturation sur cette ligne. Le cumul facturé ne peut pas dépasser le commandé.
    /// On n'exige PAS que la quantité soit déjà livrée : la facturation d'avance (acompte,
    /// vente sur commande) est un usage légitime, arbitré au niveau applicatif.
    /// </summary>
    internal Result RecordInvoiced(decimal invoicedQuantity)
    {
        if (invoicedQuantity <= 0)
            return Result.Failure(Error.Validation("InvoicedQuantity", "La quantité facturée doit être positive"));

        if (InvoicedQuantity + invoicedQuantity > Quantity)
            return Result.Failure(Error.Validation("InvoicedQuantity",
                $"Facturation supérieure au reste à facturer sur la ligne {LineNumber} (reste : {PendingInvoiceQuantity})"));

        InvoicedQuantity += invoicedQuantity;
        return Result.Success();
    }

    /// <summary>Annule une imputation de livraison (bon de livraison annulé).</summary>
    internal Result ReverseDelivery(decimal quantity)
    {
        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité doit être positive"));

        if (quantity > DeliveredQuantity)
            return Result.Failure(Error.Validation("Quantity",
                $"Impossible d'annuler plus que la quantité livrée sur la ligne {LineNumber}"));

        DeliveredQuantity -= quantity;
        return Result.Success();
    }

    /// <summary>Annule une imputation de facturation (facture annulée ou avoir total).</summary>
    internal Result ReverseInvoiced(decimal quantity)
    {
        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité doit être positive"));

        if (quantity > InvoicedQuantity)
            return Result.Failure(Error.Validation("Quantity",
                $"Impossible d'annuler plus que la quantité facturée sur la ligne {LineNumber}"));

        InvoicedQuantity -= quantity;
        return Result.Success();
    }

    internal void SetLineNumber(int lineNumber) => LineNumber = lineNumber;

    /// <summary>
    /// Identique à <c>InvoiceLine.Calculate()</c> et <c>QuoteLine.Calculate()</c> :
    /// remise → FODEC → base TVA. Ne jamais faire diverger ces trois implémentations.
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
