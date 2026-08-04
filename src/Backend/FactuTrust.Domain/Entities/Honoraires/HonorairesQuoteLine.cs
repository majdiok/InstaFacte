using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Services.Honoraires;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities.Honoraires;

public sealed class HonorairesQuoteLine : Entity
{
    public Guid HonorairesQuoteId { get; private set; }
    public HonorairesQuote Quote { get; private set; } = null!;
    public int LineNumber { get; private set; }
    /// <summary>Code activité cabinet (snapshot chaîne, pas de FK) — ex. TENUE.</summary>
    public string? ActivityCode { get; private set; }
    public string Designation { get; private set; } = null!;
    public string? Description { get; private set; }
    public decimal Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = null!;
    public VatRate VatRate { get; private set; }
    public decimal? DiscountPercent { get; private set; }
    public Money DiscountAmount { get; private set; } = null!;
    public Money SubTotal { get; private set; } = null!;
    public Money VatAmount { get; private set; } = null!;
    public Money Total { get; private set; } = null!;

    private HonorairesQuoteLine() { }

    internal static Result<HonorairesQuoteLine> Create(
        HonorairesQuote quote,
        int lineNumber,
        string designation,
        string? description,
        decimal quantity,
        Money unitPrice,
        VatRate vatRate,
        decimal? discountPercent = null,
        string? activityCode = null)
    {
        if (string.IsNullOrWhiteSpace(designation))
            return Result.Failure<HonorairesQuoteLine>(Error.Validation("Designation", "La désignation est obligatoire"));
        if (quantity <= 0)
            return Result.Failure<HonorairesQuoteLine>(Error.Validation("Quantity", "La quantité doit être supérieure à zéro"));
        if (discountPercent is < 0 or > 100)
            return Result.Failure<HonorairesQuoteLine>(Error.Validation("DiscountPercent", "La remise doit être comprise entre 0% et 100%"));

        var line = new HonorairesQuoteLine
        {
            HonorairesQuoteId = quote.Id,
            Quote = quote,
            LineNumber = lineNumber,
            ActivityCode = NormalizeActivityCode(activityCode),
            Designation = designation.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Quantity = quantity,
            UnitPrice = unitPrice,
            VatRate = vatRate,
            DiscountPercent = discountPercent
        };
        line.Calculate();
        return Result.Success(line);
    }

    internal Result Update(
        string designation,
        string? description,
        decimal quantity,
        Money unitPrice,
        VatRate vatRate,
        decimal? discountPercent,
        string? activityCode = null)
    {
        if (string.IsNullOrWhiteSpace(designation))
            return Result.Failure(Error.Validation("Designation", "La désignation est obligatoire"));
        if (quantity <= 0)
            return Result.Failure(Error.Validation("Quantity", "La quantité doit être supérieure à zéro"));
        if (discountPercent is < 0 or > 100)
            return Result.Failure(Error.Validation("DiscountPercent", "La remise doit être comprise entre 0% et 100%"));

        ActivityCode = NormalizeActivityCode(activityCode);
        Designation = designation.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        VatRate = vatRate;
        DiscountPercent = discountPercent;
        Calculate();
        return Result.Success();
    }

    internal void SetLineNumber(int lineNumber) => LineNumber = lineNumber;

    private void Calculate()
    {
        var calc = TunisianFeeLineCalculator.Calculate(Quantity, UnitPrice.Amount, DiscountPercent, VatRate);
        var currency = UnitPrice.Currency;
        DiscountAmount = Money.Create(calc.DiscountAmount, currency);
        SubTotal = Money.Create(calc.NetHt, currency);
        VatAmount = Money.Create(calc.VatAmount, currency);
        Total = Money.Create(calc.TotalTtc, currency);
    }

    private static string? NormalizeActivityCode(string? activityCode)
    {
        var normalized = FirmActivityCode.NormalizeCode(activityCode);
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
