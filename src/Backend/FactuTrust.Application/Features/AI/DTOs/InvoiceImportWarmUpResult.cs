namespace FactuTrust.Application.Features.AI.DTOs;

/// <summary>Résultat du préchauffage du modèle d'import de facture.</summary>
public sealed record InvoiceImportWarmUpResult(
    bool Accepted,
    bool Ready,
    string? Model,
    string? Error,
    double? RequiredGiB,
    double? AvailableGiB);
