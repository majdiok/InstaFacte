using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Préférence de modèle visuel d'impression d'un tenant pour un type de document donné.
/// Calquée sur <see cref="DocumentNumberingScheme"/> (entité tenant simple, clé unique
/// (TenantId, DocumentType)). En l'absence de ligne, le résolveur applique le modèle par défaut.
/// </summary>
public sealed class DocumentTemplatePreference
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public PrintableDocumentType DocumentType { get; private set; }
    public string TemplateKey { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    private DocumentTemplatePreference() { }

    public static DocumentTemplatePreference Create(Guid tenantId, PrintableDocumentType documentType, string templateKey)
    {
        return new DocumentTemplatePreference
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentType = documentType,
            TemplateKey = templateKey,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void SetTemplate(string templateKey)
    {
        TemplateKey = templateKey;
        UpdatedAt = DateTime.UtcNow;
    }
}
