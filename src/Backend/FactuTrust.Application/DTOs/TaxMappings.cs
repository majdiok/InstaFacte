using System.Linq;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

/// <summary>
/// Maps domain tax entities to DTOs and display strings.
/// </summary>
public static class TaxMappings
{
    public static TaxDto ToDto(Tax tax)
    {
        return new TaxDto
        {
            Id = tax.Id,
            Name = tax.Name,
            Type = (int)tax.Type,
            TypeDisplay = ToTypeDisplay(tax.Type),
            ValueType = (int)tax.ValueType,
            ValueTypeDisplay = ToValueTypeDisplay(tax.ValueType),
            Value = tax.Value,
            Context = (int)tax.Context,
            ContextDisplay = ToContextDisplay(tax.Context),
            IsAppliedToProducts = tax.IsAppliedToProducts,
            IsActive = tax.IsActive,
            IsSystem = tax.IsSystem,
            DisplayOrder = tax.DisplayOrder
        };
    }

    public static IReadOnlyList<VatRateOptionDto> ToVatRateOptions(IReadOnlyList<Tax> vatTaxes)
    {
        return vatTaxes.Select(t => new VatRateOptionDto
        {
            Id = t.Id,
            Percent = (int)t.Value,
            Label = $"{(int)t.Value} % — {t.Name}"
        }).ToList();
    }

    public static string ToTypeDisplay(TaxType type) => type switch
    {
        TaxType.VAT => "TVA",
        TaxType.Stamp => "Timbre",
        TaxType.FODEC => "FODEC",
        TaxType.Consumption => "Droit de consommation",
        TaxType.Other => "Autre",
        _ => type.ToString()
    };

    public static string ToValueTypeDisplay(TaxValueType vt) => vt switch
    {
        TaxValueType.Percentage => "Pourcentage",
        TaxValueType.FixedAmount => "Montant fixe",
        _ => vt.ToString()
    };

    public static string ToContextDisplay(TaxContext ctx) => ctx switch
    {
        TaxContext.All => "Tous",
        TaxContext.Sales => "Ventes",
        TaxContext.Purchases => "Achats",
        _ => ctx.ToString()
    };
}
