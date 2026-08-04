namespace FactuTrust.Domain.Enums;

/// <summary>Discriminates a firm honoraires invoice from a credit note (avoir).</summary>
public enum HonorairesDocumentType
{
    Invoice = 0,
    CreditNote = 1
}
