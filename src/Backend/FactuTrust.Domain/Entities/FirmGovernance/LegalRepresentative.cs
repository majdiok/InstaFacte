using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.FirmGovernance;

public sealed class LegalRepresentative : Entity
{
    public Guid PermanentFileId { get; private set; }
    public string LastName { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string? Cin { get; private set; }
    public string? Nationality { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? CnssNumber { get; private set; }
    public string Role { get; private set; } = "Gérant";
    public bool IsActive { get; private set; } = true;
    public bool HasProSpace { get; private set; }

    private LegalRepresentative() { }

    public static Result<LegalRepresentative> Create(
        Guid permanentFileId,
        string lastName,
        string firstName,
        string role = "Gérant")
    {
        if (permanentFileId == Guid.Empty)
            return Result.Failure<LegalRepresentative>(Error.Validation("PermanentFileId", "Dossier permanent requis"));
        if (string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(firstName))
            return Result.Failure<LegalRepresentative>(Error.Validation("Name", "Nom et prénom obligatoires"));

        return Result.Success(new LegalRepresentative
        {
            PermanentFileId = permanentFileId,
            LastName = lastName.Trim(),
            FirstName = firstName.Trim(),
            Role = string.IsNullOrWhiteSpace(role) ? "Gérant" : role.Trim()
        });
    }

    public void UpdateContact(string? email, string? phone, string? cin, string? nationality, string? cnssNumber)
    {
        Email = email?.Trim();
        Phone = phone?.Trim();
        Cin = cin?.Trim();
        Nationality = nationality?.Trim();
        CnssNumber = cnssNumber?.Trim();
    }

    public Result Update(string lastName, string firstName, string role, string? email, string? phone, string? cin, string? nationality, string? cnssNumber)
    {
        if (!IsActive)
            return Result.Failure(Error.NotFound("LegalRepresentative", Id));
        if (string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(firstName))
            return Result.Failure(Error.Validation("Name", "Nom et prénom obligatoires"));

        LastName = lastName.Trim();
        FirstName = firstName.Trim();
        SetRole(string.IsNullOrWhiteSpace(role) ? "Gérant" : role);
        UpdateContact(email, phone, cin, nationality, cnssNumber);
        return Result.Success();
    }

    public void SetRole(string role) => Role = role.Trim();
    public void Deactivate() => IsActive = false;
    public void EnableProSpace() => HasProSpace = true;
}
