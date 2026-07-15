namespace FactuTrust.Application.DTOs;

// ── Plan tiers unifié : répertoire clients + fournisseurs + fiche comptable ──

/// <summary>Ligne du répertoire des tiers (kind : 1 = client, 2 = fournisseur).</summary>
public sealed record ThirdPartyDirectoryRowDto
{
    public Guid ThirdPartyId { get; init; }
    public int Kind { get; init; }
    public string Name { get; init; } = null!;
    public string? Email { get; init; }
    /// <summary>Code auxiliaire (CompAuxNum FEC) — null tant que la fiche n'existe pas.</summary>
    public string? AuxiliaryCode { get; init; }
    public string CollectiveAccountNumber { get; init; } = null!;
    public int? PaymentTermDays { get; init; }
    public bool IsActive { get; init; }
    /// <summary>Solde courant du tiers (toutes écritures) : présenté débit OU crédit.</summary>
    public decimal BalanceDebit { get; init; }
    public decimal BalanceCredit { get; init; }
}

public sealed record ThirdPartyDirectoryResultDto
{
    public IReadOnlyList<ThirdPartyDirectoryRowDto> Items { get; init; } = Array.Empty<ThirdPartyDirectoryRowDto>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

/// <summary>Fiche comptable d'un tiers (valeurs par défaut si la fiche n'existe pas encore).</summary>
public sealed record ThirdPartyProfileDto
{
    public int Kind { get; init; }
    public Guid ThirdPartyId { get; init; }
    public string ThirdPartyName { get; init; } = null!;
    public string? AuxiliaryCode { get; init; }
    public string CollectiveAccountNumber { get; init; } = null!;
    public int? PaymentTermDays { get; init; }
    public string? AccountingNotes { get; init; }
    /// <summary>Faux si la fiche n'a jamais été enregistrée (valeurs par défaut affichées).</summary>
    public bool HasProfile { get; init; }
}

public sealed record UpsertThirdPartyProfileRequest
{
    public string AuxiliaryCode { get; init; } = null!;
    public string CollectiveAccountNumber { get; init; } = null!;
    public int? PaymentTermDays { get; init; }
    public string? AccountingNotes { get; init; }
}
