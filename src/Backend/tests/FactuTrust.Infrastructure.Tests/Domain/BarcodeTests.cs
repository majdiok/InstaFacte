using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Vague 1, lot 8 — code-barres EAN.
///
/// La clé de contrôle est vérifiée à la SAISIE : un code mal recopié est refusé au catalogue
/// plutôt que de dormir jusqu'à ce qu'un scan tombe à côté. C'est un défaut de sûreté, pas de
/// confort : le scan du point de vente cherchait auparavant dans le code produit interne avec
/// repli sur une correspondance approchée, et pouvait donc encaisser un autre article.
/// </summary>
public sealed class BarcodeTests
{
    [Theory]
    [InlineData("4006381333931")] // EAN-13 de référence
    [InlineData("5901234123457")] // EAN-13 de référence
    [InlineData("96385074")]      // EAN-8 de référence
    [InlineData("73513537")]      // EAN-8 de référence
    public void ValidEanIsAccepted(string value)
    {
        var result = Barcode.Create(value);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(value, result.Value.Value);
    }

    [Theory]
    [InlineData("4006381333932")] // dernier chiffre faussé
    [InlineData("5901234123456")]
    [InlineData("96385075")]
    public void WrongCheckDigitIsRejected(string value)
    {
        var result = Barcode.Create(value);

        Assert.True(result.IsFailure);
        Assert.Contains("Clé de contrôle", result.Error.Description);
    }

    [Theory]
    [InlineData("123456789012")]   // 12 chiffres : ni EAN-8 ni EAN-13
    [InlineData("123456")]
    [InlineData("40063813339311")] // 14 chiffres
    public void WrongLengthIsRejected(string value)
    {
        var result = Barcode.Create(value);

        Assert.True(result.IsFailure);
        Assert.Contains("8 ou 13", result.Error.Description);
    }

    [Theory]
    [InlineData("400638133393A")]
    [InlineData("4006-38133393")]
    [InlineData(" 400638133393")]
    public void NonDigitIsRejected(string value)
    {
        Assert.True(Barcode.Create(value).IsFailure);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyIsRejected(string? value)
    {
        Assert.True(Barcode.Create(value).IsFailure);
    }

    [Fact]
    public void SurroundingWhitespaceIsTrimmed()
    {
        var result = Barcode.Create("  4006381333931  ");

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal("4006381333931", result.Value.Value);
    }

    [Theory]
    [InlineData("400638133393", "4006381333931")] // 12 -> EAN-13
    [InlineData("9638507", "96385074")]           // 7  -> EAN-8
    public void CheckDigitIsComputedFromBody(string body, string expected)
    {
        var result = Barcode.FromBody(body);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(expected, result.Value.Value);
    }

    [Fact]
    public void GeneratedBarcodeIsAlwaysValid()
    {
        // Tout code engendré doit repasser la validation : c'est ce qui rend sûre la
        // génération d'un code interne pour un article sans EAN fournisseur.
        for (var i = 0; i < 200; i++)
        {
            var body = (100000000000L + i * 7919).ToString()[..12];
            var generated = Barcode.FromBody(body);

            Assert.True(generated.IsSuccess, $"corps {body} : {generated.Error?.Description}");
            Assert.True(Barcode.IsValid(generated.Value.Value));
        }
    }

    // ─────────────────────── Intégration au produit ───────────────────────

    [Fact]
    public void ProductStartsWithoutBarcode()
    {
        Assert.Null(NewProduct().Barcode);
    }

    [Fact]
    public void SettingAValidBarcodeSucceeds()
    {
        var product = NewProduct();

        var result = product.SetBarcode("4006381333931");

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal("4006381333931", product.Barcode!.Value);
    }

    [Fact]
    public void SettingAnInvalidBarcodeIsRejectedAndLeavesTheProductUntouched()
    {
        var product = NewProduct();
        Assert.True(product.SetBarcode("4006381333931").IsSuccess);

        var result = product.SetBarcode("4006381333932"); // clé faussée

        Assert.True(result.IsFailure);
        Assert.Equal("4006381333931", product.Barcode!.Value); // valeur précédente intacte
    }

    [Fact]
    public void SettingNullClearsTheBarcode()
    {
        var product = NewProduct();
        Assert.True(product.SetBarcode("4006381333931").IsSuccess);

        Assert.True(product.SetBarcode(null).IsSuccess);
        Assert.Null(product.Barcode);
    }

    [Fact]
    public void BarcodeIsDistinctFromTheInternalProductCode()
    {
        // Le point clé du correctif : deux identifiants distincts. Le scan doit interroger
        // le code-barres, jamais le code interne.
        var product = NewProduct();
        Assert.True(product.SetBarcode("4006381333931").IsSuccess);

        Assert.Equal("P-EAN", product.Code);
        Assert.NotEqual(product.Code, product.Barcode!.Value);
    }

    private static Product NewProduct() =>
        Product.Create(
            code: "P-EAN",
            name: "Produit scanné",
            type: ProductType.Product,
            unitPrice: Money.Create(100m),
            vatRate: VatRate.Standard,
            categoryId: Guid.NewGuid(),
            unit: "Unité").Value;
}
