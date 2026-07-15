using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a client/customer of a company.
/// </summary>
public sealed class Client : AggregateRoot
{
    public string Name { get; private set; } = null!;
    public ClientType Type { get; private set; }
    public NIF? NIF { get; private set; }
    public Address Address { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public PhoneNumber? Phone { get; private set; }
    public string? ContactPerson { get; private set; }
    public string? Notes { get; private set; }
    public Guid? AssignedUserId { get; private set; }
    public string? AssignedUserName { get; private set; }
    public bool IsActive { get; private set; }

    // TEJ / withholding-related fields
    public IdentificationType? TejIdentificationType { get; private set; }
    public DateTime? DateOfBirth { get; private set; }
    public string? CountryCode { get; private set; }
    public bool IsResident { get; private set; } = true;
    public string? Activity { get; private set; }

    private readonly List<Invoice> _invoices = new();
    public IReadOnlyCollection<Invoice> Invoices => _invoices.AsReadOnly();

    private Client() { }

    public static Result<Client> Create(
        string name,
        ClientType type,
        Address address,
        Email email,
        NIF? nif = null,
        PhoneNumber? phone = null,
        string? contactPerson = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Client>(Error.Validation("Name", "Le nom du client est obligatoire"));

        if (name.Length > 200)
            return Result.Failure<Client>(Error.Validation("Name", "Le nom du client ne peut pas dépasser 200 caractères"));

        if (type == ClientType.Business && nif is null)
            return Result.Failure<Client>(Error.Validation("NIF", "Le NIF est obligatoire pour les clients professionnels"));

        var client = new Client
        {
            Name = name.Trim(),
            Type = type,
            NIF = nif,
            Address = address,
            Email = email,
            Phone = phone,
            ContactPerson = contactPerson?.Trim(),
            Notes = notes?.Trim(),
            IsActive = true
        };

        return Result.Success(client);
    }

    public void Update(
        string name,
        Address address,
        Email email,
        PhoneNumber? phone,
        string? contactPerson,
        string? notes)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Name = name.Trim();

        Address = address;
        Email = email;
        Phone = phone;
        ContactPerson = contactPerson?.Trim();
        Notes = notes?.Trim();
    }

    public void UpdateNIF(NIF nif)
    {
        NIF = nif;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Reactivate()
    {
        IsActive = true;
    }

    public void AssignToUser(Guid userId, string userName)
    {
        AssignedUserId = userId;
        AssignedUserName = userName?.Trim();
    }

    public void UnassignUser()
    {
        AssignedUserId = null;
        AssignedUserName = null;
    }

    public void UpdateTejInfo(
        IdentificationType? identificationType,
        DateTime? dateOfBirth,
        string? countryCode,
        bool isResident,
        string? activity)
    {
        TejIdentificationType = identificationType;
        DateOfBirth = dateOfBirth;
        CountryCode = countryCode?.Trim();
        IsResident = isResident;
        Activity = activity?.Trim();
    }
}

/// <summary>
/// Type of client.
/// </summary>
public enum ClientType
{
    /// <summary>
    /// Individual/private person.
    /// </summary>
    Individual = 0,

    /// <summary>
    /// Business/company.
    /// </summary>
    Business = 1
}

public static class ClientTypeExtensions
{
    public static string ToDisplayString(this ClientType type) => type switch
    {
        ClientType.Individual => "Particulier",
        ClientType.Business => "Entreprise",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
