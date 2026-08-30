namespace FactuTrust.Application.DTOs;

/// <summary>
/// DTO for company/tenant information.
/// </summary>
public sealed record CompanyDto
{
    public Guid Id { get; init; }
    public string CompanyName { get; init; } = null!;
    public string? TradeName { get; init; }
    public string Nif { get; init; } = null!;
    public string? CommerceRegistry { get; init; }
    public int TaxRegime { get; init; }
    public string TaxRegimeDisplay { get; init; } = null!;
    
    public AddressDto Address { get; init; } = null!;
    public string Email { get; init; } = null!;
    public string Phone { get; init; } = null!;
    public string? Website { get; init; }
    public string? LogoUrl { get; init; }
    
    public string? BankName { get; init; }
    public string? Rib { get; init; }
    public string? Iban { get; init; }
    
    public string? InvoicePrefix { get; init; }
    public string? DefaultPaymentTerms { get; init; }
    public string? InvoiceFooter { get; init; }
    public string? WarehouseName { get; init; }
    public string? CnssEmployerNumber { get; init; }
    public bool ClientPortalEnabled { get; init; } = true;

    // Sector-aware registration wizard (plan §6.1 B7) — read-only in Phase 1, populated from the
    // master Tenant row; null for tenants registered before this feature or via register-firm.
    public string? CompanySegment { get; init; }
    public string? BusinessDomain { get; init; }
}

/// <summary>
/// DTO for updating company information.
/// </summary>
public sealed record UpdateCompanyDto
{
    public string CompanyName { get; init; } = null!;
    public string? TradeName { get; init; }
    public string Nif { get; init; } = null!;
    public string? CommerceRegistry { get; init; }
    public int TaxRegime { get; init; }
    
    public string Street { get; init; } = null!;
    public string? StreetLine2 { get; init; }
    public string City { get; init; } = null!;
    public string? PostalCode { get; init; }
    public string Governorate { get; init; } = null!;
    
    public string Email { get; init; } = null!;
    public string Phone { get; init; } = null!;
    public string? Website { get; init; }
    public string? LogoUrl { get; init; }
    
    public string? BankName { get; init; }
    public string? Rib { get; init; }
    public string? Iban { get; init; }
    
    public string? InvoicePrefix { get; init; }
    public string? DefaultPaymentTerms { get; init; }
    public string? InvoiceFooter { get; init; }
    public string? WarehouseName { get; init; }
    public string? CnssEmployerNumber { get; init; }
    public bool? ClientPortalEnabled { get; init; }
}
