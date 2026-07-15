using FactuTrust.Domain.Common;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Public directory profile for an accounting firm tenant.
/// </summary>
public sealed class AccountingFirmProfile : AggregateRoot
{
    public Guid TenantId { get; private set; }
    public string DisplayName { get; private set; } = null!;
    public string? Description { get; private set; }
    public string City { get; private set; } = null!;
    public string Governorate { get; private set; } = null!;
    public bool IsPublicInDirectory { get; private set; }
    public string? ProfessionalRegistrationNumber { get; private set; }
    public string ContactEmail { get; private set; } = null!;
    public string ContactPhone { get; private set; } = null!;

    private AccountingFirmProfile() { }

    public static Result<AccountingFirmProfile> Create(
        Guid tenantId,
        string displayName,
        string city,
        string governorate,
        Email contactEmail,
        PhoneNumber contactPhone,
        string? description = null,
        string? professionalRegistrationNumber = null,
        bool isPublicInDirectory = true)
    {
        if (tenantId == Guid.Empty)
            return Result.Failure<AccountingFirmProfile>(Error.Validation("TenantId", "Identifiant cabinet invalide"));

        if (string.IsNullOrWhiteSpace(displayName))
            return Result.Failure<AccountingFirmProfile>(Error.Validation("DisplayName", "Le nom affiché est obligatoire"));

        return Result.Success(new AccountingFirmProfile
        {
            TenantId = tenantId,
            DisplayName = displayName.Trim(),
            Description = description?.Trim(),
            City = city.Trim(),
            Governorate = governorate.Trim(),
            IsPublicInDirectory = isPublicInDirectory,
            ProfessionalRegistrationNumber = professionalRegistrationNumber?.Trim(),
            ContactEmail = contactEmail.Value,
            ContactPhone = contactPhone.Value
        });
    }

    public void Update(
        string displayName,
        string city,
        string governorate,
        string contactEmail,
        string contactPhone,
        string? description,
        string? professionalRegistrationNumber,
        bool isPublicInDirectory)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
            DisplayName = displayName.Trim();

        Description = description?.Trim();
        City = city.Trim();
        Governorate = governorate.Trim();
        ContactEmail = contactEmail.Trim();
        ContactPhone = contactPhone.Trim();
        ProfessionalRegistrationNumber = professionalRegistrationNumber?.Trim();
        IsPublicInDirectory = isPublicInDirectory;
    }
}
