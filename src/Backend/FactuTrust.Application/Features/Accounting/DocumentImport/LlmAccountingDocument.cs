using FactuTrust.Application.Features.AI;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Features.Accounting.DocumentImport;

// ============================================================================
// DTOs de SORTIE du LLM.
//
// Le caractère nullable protège contre les champs ABSENTS — jamais contre les champs de MAUVAIS
// TYPE. Un modèle local écrit « 19.0 » pour un int, « 0.95 » pour une chaîne, un objet pour un
// élément de List<string> : c'est la désérialisation via LlmJsonOptions.Tolerant, et elle seule,
// qui absorbe ces écarts.
//
// RÈGLE : toute propriété ajoutée ici doit être nullable ET son type doit être couvert par un
// convertisseur de LlmJsonOptions.Tolerant. LlmDtoContractTests le vérifie par réflexion.
// ============================================================================

public sealed class LlmAccountingDocument
{
    public string? DocumentType { get; set; }
    public string? DocumentNumber { get; set; }
    public string? IssueDate { get; set; }
    public string? DueDate { get; set; }
    public string? DocumentStatus { get; set; }
    public string? Currency { get; set; }
    public LlmAccountingParty? Seller { get; set; }
    public LlmAccountingParty? Buyer { get; set; }
    public List<LlmAccountingLine>? Lines { get; set; }
    public List<LlmVatBucket>? VatBreakdown { get; set; }
    public decimal? TotalHt { get; set; }
    public decimal? TotalVat { get; set; }
    public decimal? FodecAmount { get; set; }
    public decimal? FiscalStampAmount { get; set; }
    public decimal? WithholdingAmount { get; set; }
    public decimal? TotalTtc { get; set; }
    public string? Confidence { get; set; }
    public List<string>? Warnings { get; set; }
}

public sealed class LlmAccountingParty
{
    public string? Name { get; set; }
    public string? Nif { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Governorate { get; set; }
}

public sealed class LlmAccountingLine
{
    public string? Designation { get; set; }
    public string? Reference { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? UnitPriceHt { get; set; }
    public decimal? DiscountPercent { get; set; }
    public int? VatRatePercent { get; set; }
}

public sealed class LlmVatBucket
{
    public int? RatePercent { get; set; }
    public decimal? BaseAmount { get; set; }
    public decimal? VatAmount { get; set; }
}

/// <summary>
/// Normalise la sortie brute du LLM vers <see cref="AccountingDocumentExtractionDto"/> :
/// nettoyage, bornage des taux de TVA, dates ISO, et reconstruction de la ventilation TVA
/// lorsque le document ne l'imprime pas.
/// </summary>
public static class AccountingDocumentMapping
{
    public static AccountingDocumentExtractionDto FromLlm(
        LlmAccountingDocument parsed,
        string extractionMethod,
        bool ocrApplied,
        bool textTruncated)
    {
        ArgumentNullException.ThrowIfNull(parsed);

        var warnings = new List<string>();
        if (parsed.Warnings is { Count: > 0 })
        {
            warnings.AddRange(parsed.Warnings
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Select(w => w.Trim()));
        }

        if (textTruncated)
            warnings.Add("Le document est volumineux : seule une partie a été analysée. Vérifiez que rien ne manque.");

        var lines = (parsed.Lines ?? [])
            .Select(MapLine)
            .Where(l => l is not null)
            .Select(l => l!)
            .ToList();

        var breakdown = MapBreakdown(parsed.VatBreakdown);
        var recomputed = false;

        if (breakdown.Count == 0 && lines.Count > 0)
        {
            breakdown = RebuildBreakdownFromLines(lines);
            recomputed = breakdown.Count > 0;
            if (recomputed)
            {
                warnings.Add(
                    "La ventilation de la TVA n'était pas lisible sur la pièce : elle a été reconstituée "
                    + "à partir des lignes. Vérifiez les bases imposables.");
            }
        }

        var totalVat = parsed.TotalVat
            ?? (breakdown.Count > 0 ? breakdown.Sum(b => b.VatAmount) : null);
        var totalHt = parsed.TotalHt
            ?? (breakdown.Count > 0 ? breakdown.Sum(b => b.BaseAmount) : null);

        if (parsed.WithholdingAmount is { } rs && rs != 0m)
        {
            warnings.Add(
                "Retenue à la source détectée sur la pièce : elle n'est pas intégrée à l'écriture de facture "
                + "(elle est comptabilisée séparément au paiement).");
        }

        return new AccountingDocumentExtractionDto
        {
            DocumentType = InvoiceImportParsing.NormalizeDocumentType(parsed.DocumentType),
            DocumentNumber = InvoiceImportParsing.CleanOrNull(parsed.DocumentNumber),
            IssueDate = InvoiceImportParsing.ParseDate(parsed.IssueDate),
            DueDate = InvoiceImportParsing.ParseDate(parsed.DueDate),
            DocumentStatus = InvoiceImportParsing.CleanOrNull(parsed.DocumentStatus),
            Currency = InvoiceImportParsing.NormalizeCurrency(parsed.Currency),
            Seller = MapParty(parsed.Seller),
            Buyer = MapParty(parsed.Buyer),
            Lines = lines,
            VatBreakdown = breakdown,
            TotalHt = Round(totalHt),
            TotalVat = Round(totalVat),
            FodecAmount = Round(parsed.FodecAmount),
            FiscalStampAmount = Round(parsed.FiscalStampAmount),
            WithholdingAmount = Round(parsed.WithholdingAmount),
            TotalTtc = Round(parsed.TotalTtc),
            Confidence = InvoiceImportParsing.NormalizeConfidence(parsed.Confidence),
            Warnings = warnings,
            ExtractionMethod = extractionMethod,
            OcrApplied = ocrApplied,
            VatBreakdownRecomputed = recomputed
        };
    }

    private static ExtractedPartyDto? MapParty(LlmAccountingParty? party)
    {
        if (party is null)
            return null;

        var dto = new ExtractedPartyDto
        {
            Name = InvoiceImportParsing.CleanOrNull(party.Name),
            Nif = InvoiceImportParsing.CleanOrNull(party.Nif),
            Email = InvoiceImportParsing.CleanOrNull(party.Email),
            Phone = InvoiceImportParsing.CleanOrNull(party.Phone),
            Street = InvoiceImportParsing.CleanOrNull(party.Street),
            City = InvoiceImportParsing.CleanOrNull(party.City),
            PostalCode = InvoiceImportParsing.CleanOrNull(party.PostalCode),
            Governorate = InvoiceImportParsing.CleanOrNull(party.Governorate)
        };

        return dto.Name is null && dto.Nif is null ? null : dto;
    }

    private static ExtractedLineDto? MapLine(LlmAccountingLine? line)
    {
        var designation = InvoiceImportParsing.CleanOrNull(line?.Designation);
        if (line is null || designation is null)
            return null;

        var quantity = line.Quantity is > 0m ? line.Quantity.Value : 1m;
        var unitPrice = line.UnitPriceHt is >= 0m ? line.UnitPriceHt.Value : 0m;
        var discount = line.DiscountPercent is > 0m and <= 100m ? line.DiscountPercent : null;

        var gross = quantity * unitPrice;
        var net = discount is { } d ? gross * (1m - d / 100m) : gross;

        return new ExtractedLineDto
        {
            Designation = designation,
            Reference = InvoiceImportParsing.CleanOrNull(line.Reference),
            Quantity = quantity,
            Unit = InvoiceImportParsing.CleanOrNull(line.Unit),
            UnitPriceHt = unitPrice,
            DiscountPercent = discount,
            VatRatePercent = InvoiceImportParsing.ClampVatRate(line.VatRatePercent),
            LineTotalHt = MillimeRounding.Round(net)
        };
    }

    private static List<ExtractedVatBucketDto> MapBreakdown(List<LlmVatBucket>? buckets)
    {
        if (buckets is null || buckets.Count == 0)
            return [];

        var merged = new Dictionary<int, (decimal Base, decimal Vat)>();
        foreach (var bucket in buckets)
        {
            if (bucket is null)
                continue;

            var rate = InvoiceImportParsing.ClampVatRate(bucket.RatePercent);
            var baseAmount = MillimeRounding.Round(bucket.BaseAmount ?? 0m);
            var vatAmount = MillimeRounding.Round(bucket.VatAmount ?? 0m);
            if (baseAmount == 0m && vatAmount == 0m)
                continue;

            merged.TryGetValue(rate, out var existing);
            merged[rate] = (existing.Base + baseAmount, existing.Vat + vatAmount);
        }

        return merged
            .OrderBy(kv => kv.Key)
            .Select(kv => new ExtractedVatBucketDto(kv.Key, kv.Value.Base, kv.Value.Vat))
            .ToList();
    }

    /// <summary>
    /// Reconstitue la ventilation depuis les lignes lorsque la pièce ne l'imprime pas.
    /// Marqué par un avertissement : cette base est un calcul, pas une lecture.
    /// </summary>
    private static List<ExtractedVatBucketDto> RebuildBreakdownFromLines(List<ExtractedLineDto> lines)
    {
        var merged = new Dictionary<int, decimal>();
        foreach (var line in lines)
        {
            var ht = line.LineTotalHt ?? MillimeRounding.Round(line.Quantity * line.UnitPriceHt);
            merged.TryGetValue(line.VatRatePercent, out var existing);
            merged[line.VatRatePercent] = existing + ht;
        }

        return merged
            .Where(kv => kv.Value != 0m)
            .OrderBy(kv => kv.Key)
            .Select(kv => new ExtractedVatBucketDto(
                kv.Key,
                MillimeRounding.Round(kv.Value),
                MillimeRounding.Round(kv.Value * kv.Key / 100m)))
            .ToList();
    }

    private static decimal? Round(decimal? value) =>
        value is null ? null : MillimeRounding.Round(value.Value);
}
