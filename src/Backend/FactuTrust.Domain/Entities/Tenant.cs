using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a company/tenant in the multi-tenant system.
/// Each tenant has its own dedicated database.
/// </summary>
public sealed class Tenant : AggregateRoot
{
    public string CompanyName { get; private set; } = null!;
    public NIF NIF { get; private set; } = null!;
    public Address Address { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public PhoneNumber Phone { get; private set; } = null!;
    public string? Website { get; private set; }
    public string? LogoUrl { get; private set; }
    
    public TaxRegime TaxRegime { get; private set; }
    public TenantKind Kind { get; private set; } = TenantKind.Company;
    public string DatabaseName { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime? DeactivatedAt { get; private set; }

    /// <summary>
    /// Cabinet comptable qui a créé et gère ce dossier client (société sans compte plateforme).
    /// Null pour les tenants auto-inscrits.
    /// </summary>
    public Guid? ManagedByFirmTenantId { get; private set; }

    public bool IsFirmManaged => ManagedByFirmTenantId.HasValue;

    private Tenant() { }

    public static Result<Tenant> Create(
        string companyName,
        NIF nif,
        Address address,
        Email email,
        PhoneNumber phone,
        TaxRegime taxRegime,
        string? website = null)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            return Result.Failure<Tenant>(Error.Validation("CompanyName", "La raison sociale est obligatoire"));

        if (companyName.Length > 200)
            return Result.Failure<Tenant>(Error.Validation("CompanyName", "La raison sociale ne peut pas dépasser 200 caractères"));

        var tenant = new Tenant
        {
            CompanyName = companyName.Trim(),
            NIF = nif,
            Address = address,
            Email = email,
            Phone = phone,
            TaxRegime = taxRegime,
            Kind = TenantKind.Company,
            Website = website?.Trim(),
            DatabaseName = GenerateDatabaseName(),
            IsActive = true
        };

        tenant.AddDomainEvent(new TenantCreatedEvent(tenant.Id, tenant.CompanyName, tenant.DatabaseName));

        return Result.Success(tenant);
    }

    /// <summary>
    /// Crée un tenant société géré par un cabinet comptable (client sans compte utilisateur sur la plateforme).
    /// </summary>
    public static Result<Tenant> CreateFirmManaged(
        Guid managedByFirmTenantId,
        string companyName,
        NIF nif,
        Address address,
        Email email,
        PhoneNumber phone,
        TaxRegime taxRegime,
        string? website = null)
    {
        if (managedByFirmTenantId == Guid.Empty)
            return Result.Failure<Tenant>(Error.Validation("ManagedByFirmTenantId", "Le cabinet gestionnaire est obligatoire"));

        var result = Create(companyName, nif, address, email, phone, taxRegime, website);
        if (result.IsFailure)
            return result;

        result.Value.ManagedByFirmTenantId = managedByFirmTenantId;
        return result;
    }

    public static Result<Tenant> CreateAccountingFirm(
        string firmName,
        NIF nif,
        Address address,
        Email email,
        PhoneNumber phone,
        string? website = null)
    {
        if (string.IsNullOrWhiteSpace(firmName))
            return Result.Failure<Tenant>(Error.Validation("CompanyName", "La raison sociale du cabinet est obligatoire"));

        if (firmName.Length > 200)
            return Result.Failure<Tenant>(Error.Validation("CompanyName", "La raison sociale ne peut pas dépasser 200 caractères"));

        var tenant = new Tenant
        {
            CompanyName = firmName.Trim(),
            NIF = nif,
            Address = address,
            Email = email,
            Phone = phone,
            TaxRegime = TaxRegime.RealRegime,
            Kind = TenantKind.AccountingFirm,
            Website = website?.Trim(),
            DatabaseName = GenerateDatabaseName(),
            IsActive = true
        };

        tenant.AddDomainEvent(new TenantCreatedEvent(tenant.Id, tenant.CompanyName, tenant.DatabaseName));

        return Result.Success(tenant);
    }

    public void UpdateCompanyInfo(
        string companyName,
        Address address,
        PhoneNumber phone,
        string? website,
        string? logoUrl)
    {
        if (!string.IsNullOrWhiteSpace(companyName))
            CompanyName = companyName.Trim();

        Address = address;
        Phone = phone;
        Website = website?.Trim();
        LogoUrl = logoUrl?.Trim();

        AddDomainEvent(new TenantUpdatedEvent(Id, CompanyName));
    }

    public void UpdateTaxRegime(TaxRegime newRegime)
    {
        TaxRegime = newRegime;
    }

    /// <summary>Sync raison sociale depuis le dossier permanent cabinet (usage interne gouvernance).</summary>
    public void UpdateCompanyNameFromPermanentFile(string companyName)
    {
        if (!string.IsNullOrWhiteSpace(companyName))
            CompanyName = companyName.Trim();
    }

    /// <summary>Sync NIF depuis le dossier permanent cabinet (usage interne gouvernance).</summary>
    public Result UpdateNifFromPermanentFile(string nifValue)
    {
        var nif = NIF.Create(nifValue);
        if (nif.IsFailure)
            return Result.Failure(nif.Error);
        NIF = nif.Value;
        return Result.Success();
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;
        DeactivatedAt = DateTime.UtcNow;
        AddDomainEvent(new TenantDeactivatedEvent(Id, CompanyName));
    }

    public void Reactivate()
    {
        if (IsActive)
            return;

        IsActive = true;
        DeactivatedAt = null;
        AddDomainEvent(new TenantReactivatedEvent(Id, CompanyName));
    }

    private static string GenerateDatabaseName()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"FactuTrust_Tenant_{uniqueId}";
    }
}

/// <summary>
/// Tunisian tax regimes.
/// </summary>
public enum TaxRegime
{
    /// <summary>
    /// Régime réel - Full VAT accounting.
    /// </summary>
    RealRegime = 0,

    /// <summary>
    /// Régime forfaitaire - Simplified taxation for small businesses.
    /// </summary>
    FlatRateRegime = 1,

    /// <summary>
    /// Exonéré - VAT exempt.
    /// </summary>
    Exempt = 2
}

public static class TaxRegimeExtensions
{
    public static string ToDisplayString(this TaxRegime regime) => regime switch
    {
        TaxRegime.RealRegime => "Régime réel",
        TaxRegime.FlatRateRegime => "Régime forfaitaire",
        TaxRegime.Exempt => "Exonéré",
        _ => throw new ArgumentOutOfRangeException(nameof(regime))
    };
}
