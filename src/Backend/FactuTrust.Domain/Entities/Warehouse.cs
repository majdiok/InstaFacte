using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a warehouse or storage location.
/// </summary>
public sealed class Warehouse : AggregateRoot
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Address { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }

    private Warehouse() { }

    public static Result<Warehouse> Create(
        string code,
        string name,
        string? address = null,
        bool isDefault = false)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result.Failure<Warehouse>(Error.Validation("Code", "Le code entrepôt est obligatoire"));

        if (code.Length > 20)
            return Result.Failure<Warehouse>(Error.Validation("Code", "Le code entrepôt ne peut pas dépasser 20 caractères"));

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Warehouse>(Error.Validation("Name", "Le nom de l'entrepôt est obligatoire"));

        if (name.Length > 100)
            return Result.Failure<Warehouse>(Error.Validation("Name", "Le nom de l'entrepôt ne peut pas dépasser 100 caractères"));

        var warehouse = new Warehouse
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Address = address?.Trim(),
            IsDefault = isDefault,
            IsActive = true
        };

        return Result.Success(warehouse);
    }

    public void Update(string name, string? address)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Name = name.Trim();

        Address = address?.Trim();
    }

    public void SetAsDefault()
    {
        IsDefault = true;
    }

    public void RemoveDefault()
    {
        IsDefault = false;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
