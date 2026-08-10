using System.Text.Json;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Json;
using FactuTrust.Application.Features.Accounting.DocumentImport;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Tolérance de la frontière LLM → DTO.
///
/// Chaque cas provient d'un échec RÉEL observé dans les journaux de production
/// (factutrust-20260809.log et antérieurs) : le modèle produisait un JSON syntaxiquement valide
/// mais mal typé, et une seule propriété fautive annulait l'extraction complète.
/// </summary>
public sealed class LlmJsonToleranceTests
{
    private static LlmAccountingDocument Accounting(string json) =>
        JsonSerializer.Deserialize<LlmAccountingDocument>(json, LlmJsonOptions.Tolerant)!;

    private static LlmAccountingLine SingleLine(string lineJson) =>
        Accounting($$"""{"lines":[{{lineJson}}]}""").Lines![0];

    // ========================================================================
    // vatRatePercent : LE défaut de production (20 occurrences sur 20)
    // ========================================================================

    [Theory]
    [InlineData("19.0", 19)]      // valeur exacte du log : BytePositionInLine 28
    [InlineData("19.000", 19)]    // valeur exacte du log : BytePositionInLine 30
    [InlineData("19", 19)]
    [InlineData("7.0", 7)]
    [InlineData("13.00", 13)]
    [InlineData("0.0", 0)]
    public void VatRatePercent_DecimalLiteral_CoercesToInt(string literal, int expected)
    {
        var line = SingleLine($$"""{"designation":"A","vatRatePercent":{{literal}}}""");
        Assert.Equal(expected, line.VatRatePercent);
    }

    [Theory]
    [InlineData("\"19\"", 19)]
    [InlineData("\"19 %\"", 19)]
    [InlineData("\"19%\"", 19)]
    [InlineData("\"19,0\"", 19)]
    [InlineData("\"TVA 19\"", 19)]
    public void VatRatePercent_QuotedForms_CoerceToInt(string literal, int expected)
    {
        var line = SingleLine($$"""{"designation":"A","vatRatePercent":{{literal}}}""");
        Assert.Equal(expected, line.VatRatePercent);
    }

    [Theory]
    [InlineData("18.6", 19)]
    [InlineData("6.5", 7)]
    [InlineData("12.5", 13)]
    public void VatRatePercent_RoundsAwayFromZero(string literal, int expected)
    {
        var line = SingleLine($$"""{"designation":"A","vatRatePercent":{{literal}}}""");
        Assert.Equal(expected, line.VatRatePercent);
    }

    [Theory]
    [InlineData("\"n/a\"")]
    [InlineData("\"\"")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("[19]")]
    public void VatRatePercent_Unusable_YieldsNullWithoutThrowing(string literal)
    {
        var line = SingleLine($$"""{"designation":"A","vatRatePercent":{{literal}}}""");
        Assert.Null(line.VatRatePercent);
    }

    /// <summary>
    /// Le test le plus important du fichier : il prouve que le convertisseur CONSOMME bien le
    /// jeton objet. S'il rendait la main sans consommer, le lecteur se décalerait et toutes les
    /// propriétés suivantes seraient perdues ou corrompues.
    /// </summary>
    [Fact]
    public void VatRatePercent_Object_YieldsNull_AndFollowingPropertiesStillParse()
    {
        var line = SingleLine(
            """{"designation":"Article","vatRatePercent":{"value":19},"unitPriceHt":278.000,"unit":"pcs"}""");

        Assert.Null(line.VatRatePercent);
        Assert.Equal(278.000m, line.UnitPriceHt);
        Assert.Equal("pcs", line.Unit);
        Assert.Equal("Article", line.Designation);
    }

    [Fact]
    public void VatBreakdown_RatePercent_DecimalLiteral_CoercesToInt()
    {
        var doc = Accounting(
            """{"vatBreakdown":[{"ratePercent":7.0,"baseAmount":61.682,"vatAmount":4.318}]}""");

        var bucket = Assert.Single(doc.VatBreakdown!);
        Assert.Equal(7, bucket.RatePercent);
        Assert.Equal(61.682m, bucket.BaseAmount);
        Assert.Equal(4.318m, bucket.VatAmount);
    }

    // ========================================================================
    // confidence : le modèle renvoie un nombre au lieu de "high"/"medium"/"low"
    // ========================================================================

    [Fact]
    public void Confidence_NumericLiteral_BecomesRawString()
    {
        Assert.Equal("0.95", Accounting("""{"confidence":0.95}""").Confidence);
    }

    [Fact]
    public void Confidence_PreservesSignificantZeros()
    {
        // Passer par GetDouble().ToString() rendrait « 19 » et dépendrait de la culture.
        Assert.Equal("19.000", Accounting("""{"confidence":19.000}""").Confidence);
    }

    [Fact]
    public void Confidence_Boolean_BecomesString()
    {
        Assert.Equal("true", Accounting("""{"confidence":true}""").Confidence);
    }

    // ========================================================================
    // warnings : le modèle renvoie des objets au lieu de chaînes
    // ========================================================================

    [Fact]
    public void Warnings_ArrayOfObjects_KeepsReadableText()
    {
        var doc = Accounting("""{"warnings":[{"message":"TVA douteuse"},{"text":"Total illisible"}]}""");
        Assert.Equal(["TVA douteuse", "Total illisible"], doc.Warnings);
    }

    [Fact]
    public void Warnings_MixedArray_DropsUnusableEntries()
    {
        var doc = Accounting("""{"warnings":["ok",3,null,"",{"autre":1}]}""");

        Assert.Contains("ok", doc.Warnings!);
        Assert.Contains("3", doc.Warnings!);
        Assert.DoesNotContain(doc.Warnings!, string.IsNullOrWhiteSpace);
    }

    [Fact]
    public void Warnings_BareString_BecomesSingleElementList()
    {
        Assert.Equal(["attention"], Accounting("""{"warnings":"attention"}""").Warnings);
    }

    [Fact]
    public void Warnings_Null_StaysNull()
    {
        Assert.Null(Accounting("""{"warnings":null}""").Warnings);
    }

    // ========================================================================
    // Montants : formats tunisiens et français en chaîne
    // ========================================================================

    [Theory]
    [InlineData("\"1,000\"", 1.000)]
    [InlineData("\"1 234,567\"", 1234.567)]
    [InlineData("\"1,250.000\"", 1250.000)]
    [InlineData("\"278,000\"", 278.000)]
    [InlineData("\"+3700.000\"", 3700.000)]
    [InlineData("\"-1.000\"", -1.000)]
    [InlineData("\"1 000,50 TND\"", 1000.50)]
    public void Amounts_TunisianAndFrenchStrings_Parse(string literal, double expected)
    {
        var doc = Accounting($$"""{"fodecAmount":{{literal}}}""");
        Assert.Equal((decimal)expected, doc.FodecAmount);
    }

    [Fact]
    public void Amounts_OutOfDecimalRange_YieldsNull()
    {
        // Cas de production : « could not be converted to Nullable<Decimal> » sur $.fodecAmount.
        Assert.Null(Accounting("""{"fodecAmount":1e40}""").FodecAmount);
    }

    [Fact]
    public void Amounts_NumericLiterals_AreUnchanged()
    {
        // Non-régression du chemin nominal : aucune perte de précision sur les millimes.
        var doc = Accounting("""{"totalHt":273.682,"totalVat":4.318,"totalTtc":279.000,"fiscalStampAmount":1.000}""");

        Assert.Equal(273.682m, doc.TotalHt);
        Assert.Equal(4.318m, doc.TotalVat);
        Assert.Equal(279.000m, doc.TotalTtc);
        Assert.Equal(1.000m, doc.FiscalStampAmount);
    }

    // ========================================================================
    // Robustesse générale
    // ========================================================================

    [Fact]
    public void AllFieldsNull_StillDeserializes()
    {
        var doc = Accounting(
            """
            {"documentType":null,"documentNumber":null,"issueDate":null,"dueDate":null,
             "documentStatus":null,"currency":null,"seller":null,"buyer":null,"lines":null,
             "vatBreakdown":null,"totalHt":null,"totalVat":null,"fodecAmount":null,
             "fiscalStampAmount":null,"withholdingAmount":null,"totalTtc":null,
             "confidence":null,"warnings":null}
            """);

        Assert.NotNull(doc);
        Assert.Null(doc.TotalHt);
        Assert.Null(doc.Confidence);
    }

    [Fact]
    public void PartyFields_WrongTypes_DegradeIndividually()
    {
        var doc = Accounting(
            """{"seller":{"name":"Ste Bonjour","nif":130893,"phone":{"mobile":"99949365"},"city":"Ariana"}}""");

        Assert.Equal("Ste Bonjour", doc.Seller!.Name);
        Assert.Equal("130893", doc.Seller.Nif);
        Assert.Equal("""{"mobile":"99949365"}""", doc.Seller.Phone);
        Assert.Equal("Ariana", doc.Seller.City);
    }

    // ========================================================================
    // Le payload de production, sur LES DEUX chemins d'import
    // ========================================================================

    /// <summary>Reproduction fidèle de la réponse qui échouait 20 fois sur 20.</summary>
    private const string ProductionPayload = """
        {
          "documentType": "INVOICE",
          "documentNumber": "68",
          "issueDate": "2024-03-16",
          "currency": "TND",
          "seller": { "name": "E-info", "nif": "130893/B" },
          "buyer": { "name": "Mohamed", "phone": "99949365" },
          "lines": [
            { "designation": "SOURIS RAMITECH USB TB220", "quantity": 1, "unitPriceHt": 7.477, "vatRatePercent": 19.0 },
            { "designation": "CARTES MEMOIRES 32G", "quantity": 1, "unitPriceHt": 19.626, "vatRatePercent": 7.000 }
          ],
          "vatBreakdown": [ { "ratePercent": 7.0, "baseAmount": 61.682, "vatAmount": 4.318 } ],
          "totalHt": 273.682,
          "totalVat": 4.318,
          "fiscalStampAmount": 1.000,
          "totalTtc": 279.000,
          "confidence": 0.95,
          "warnings": []
        }
        """;

    [Fact]
    public void ProductionPayload_AccountingPath_Deserializes()
    {
        var doc = Accounting(ProductionPayload);

        Assert.Equal("68", doc.DocumentNumber);
        Assert.Equal(19, doc.Lines![0].VatRatePercent);
        Assert.Equal(7, doc.Lines[1].VatRatePercent);
        Assert.Equal(7, doc.VatBreakdown![0].RatePercent);
        Assert.Equal(279.000m, doc.TotalTtc);
        Assert.Equal("0.95", doc.Confidence);
    }

    /// <summary>
    /// Jumeau du test précédent sur le DTO du wizard de facturation. La paire échoue si un seul
    /// des deux chemins d'import est corrigé — c'est le garde-fou contre une redivergence.
    /// </summary>
    [Fact]
    public void ProductionPayload_WizardPath_Deserializes()
    {
        var wizardPayload = """
            {
              "documentType": "INVOICE",
              "invoiceNumber": "68",
              "currency": "TND",
              "client": { "name": "Mohamed" },
              "lines": [
                { "designation": "SOURIS RAMITECH USB TB220", "quantity": 1, "unitPriceHT": 7.477, "vatRatePercent": 19.0 }
              ],
              "totals": { "totalHT": 273.682, "totalVat": 4.318, "totalTTC": 279.000 },
              "confidence": 0.95,
              "warnings": [{ "message": "montant douteux" }]
            }
            """;

        var doc = JsonSerializer.Deserialize<LlmInvoiceExtraction>(wizardPayload, LlmJsonOptions.Tolerant)!;

        Assert.Equal("68", doc.InvoiceNumber);
        Assert.Equal(19, doc.Lines![0].VatRatePercent);
        Assert.Equal(279.000m, doc.Totals!.TotalTTC);
        Assert.Equal("0.95", doc.Confidence);
        Assert.Equal(["montant douteux"], doc.Warnings);
    }

    /// <summary>Le filet aval reste actif : un taux hors barème tunisien est ramené par ClampVatRate.</summary>
    [Fact]
    public void Mapping_StillClampsOutOfRangeVatRate()
    {
        var doc = Accounting("""{"lines":[{"designation":"A","vatRatePercent":17.4}]}""");
        var mapped = AccountingDocumentMapping.FromLlm(
            doc, AccountingDocumentExtractionMethods.LlmVision, ocrApplied: true, textTruncated: false);

        Assert.Equal(19, mapped.Lines[0].VatRatePercent);
    }
}
