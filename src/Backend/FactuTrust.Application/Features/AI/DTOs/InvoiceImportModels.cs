namespace FactuTrust.Application.Features.AI.DTOs;

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

/// <summary>Objet racine renvoyé par le LLM lors de l'extraction d'une facture.</summary>
public sealed class LlmInvoiceExtraction
{
    public string? DocumentType { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? IssueDate { get; set; }
    public string? DueDate { get; set; }
    public string? Currency { get; set; }
    public LlmParty? Seller { get; set; }
    public LlmParty? Client { get; set; }
    public List<LlmInvoiceLine>? Lines { get; set; }
    public LlmTotals? Totals { get; set; }
    public string? Confidence { get; set; }
    public List<string>? Warnings { get; set; }
}

public sealed class LlmParty
{
    public string? Name { get; set; }
    public string? Nif { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public LlmAddress? Address { get; set; }
}

public sealed class LlmAddress
{
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Governorate { get; set; }
}

public sealed class LlmInvoiceLine
{
    public string? Designation { get; set; }
    public string? Description { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? UnitPriceHT { get; set; }
    public decimal? DiscountPercent { get; set; }
    public int? VatRatePercent { get; set; }
}

public sealed class LlmTotals
{
    public decimal? TotalHT { get; set; }
    public decimal? TotalVat { get; set; }
    public decimal? TotalTTC { get; set; }
}

// ============================================================================
// DTOs de RÉPONSE API (renvoyés au frontend après validation et rapprochement).
// ============================================================================

/// <summary>Résultat structuré de l'import d'une facture depuis un fichier.</summary>
public sealed record InvoiceImportResultDto
{
    /// <summary>INVOICE, CREDIT_NOTE, DELIVERY_NOTE ou PROFORMA.</summary>
    public string DocumentType { get; init; } = "INVOICE";
    public string? InvoiceNumber { get; init; }
    public DateOnly? IssueDate { get; init; }
    public DateOnly? DueDate { get; init; }
    /// <summary>"TND", "EUR" ou "USD".</summary>
    public string Currency { get; init; } = "TND";
    public InvoiceImportClientDto Client { get; init; } = new();
    public IReadOnlyList<InvoiceImportLineDto> Lines { get; init; } = Array.Empty<InvoiceImportLineDto>();
    public InvoiceImportTotalsDto? Totals { get; init; }
    /// <summary>Auto-évaluation du LLM : "high", "medium" ou "low".</summary>
    public string Confidence { get; init; } = "medium";
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    /// <summary>Format détecté du fichier source ("pdf", "image", "docx", "xlsx", "csv", "txt").</summary>
    public string ExtractionFormat { get; init; } = "";
    public bool OcrApplied { get; init; }
}

public sealed record InvoiceImportClientDto
{
    /// <summary>Renseigné si le client a été rapproché d'un client existant en base.</summary>
    public Guid? MatchedClientId { get; init; }
    public string? MatchedClientName { get; init; }
    /// <summary>True si aucun client existant n'a été rapproché (mode "nouveau client" dans le wizard).</summary>
    public bool IsNewClient { get; init; } = true;
    public string? Name { get; init; }
    public string? Nif { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Street { get; init; }
    public string? City { get; init; }
    public string? PostalCode { get; init; }
    public string? Governorate { get; init; }
    /// <summary>"TAX_SUBJECT", "NON_TAX_SUBJECT" ou "TAX_EXEMPT".</summary>
    public string TaxType { get; init; } = "NON_TAX_SUBJECT";
}

public sealed record InvoiceImportLineDto
{
    /// <summary>Renseigné si la ligne a été rapprochée d'un produit existant en base.</summary>
    public Guid? MatchedProductId { get; init; }
    public string? MatchedProductCode { get; init; }
    public string Designation { get; init; } = "";
    public string? Description { get; init; }
    public decimal Quantity { get; init; } = 1m;
    public string? Unit { get; init; }
    public decimal UnitPriceHT { get; init; }
    public decimal? DiscountPercent { get; init; }
    /// <summary>Taux de TVA tunisien : 0, 7, 13 ou 19.</summary>
    public int VatRatePercent { get; init; } = 19;
}

public sealed record InvoiceImportTotalsDto
{
    public decimal? TotalHT { get; init; }
    public decimal? TotalVat { get; init; }
    public decimal? TotalTTC { get; init; }
}
