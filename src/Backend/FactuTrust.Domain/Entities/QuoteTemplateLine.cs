using FactuTrust.Domain.Common;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

public sealed class QuoteTemplateLine : Entity
{
    public Guid QuoteTemplateId { get; private set; }
    public QuoteTemplate QuoteTemplate { get; private set; } = null!;
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public Money? CustomUnitPrice { get; private set; }
    public decimal? DiscountPercent { get; private set; }
    public int SortOrder { get; private set; }

    private QuoteTemplateLine() { }

    public static QuoteTemplateLine Create(
        QuoteTemplate template,
        Guid productId,
        decimal quantity,
        int sortOrder,
        Money? customUnitPrice = null,
        decimal? discountPercent = null)
    {
        return new QuoteTemplateLine
        {
            QuoteTemplate = template,
            QuoteTemplateId = template.Id,
            ProductId = productId,
            Quantity = quantity > 0 ? quantity : 1,
            SortOrder = sortOrder,
            CustomUnitPrice = customUnitPrice,
            DiscountPercent = discountPercent
        };
    }
}
