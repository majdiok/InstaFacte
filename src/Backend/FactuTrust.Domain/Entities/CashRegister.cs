using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Physical or logical cash drawer. Several per warehouse, one default.
/// </summary>
public sealed class CashRegister : AggregateRoot
{
    public const int CodeMaxLength = 20;
    public const int NameMaxLength = 100;

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public Guid WarehouseId { get; private set; }
    public Warehouse Warehouse { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public bool IsDefault { get; private set; }

    private CashRegister() { }

    public static Result<CashRegister> Create(string code, string name, Guid warehouseId, bool isDefault = true)
    {
        if (warehouseId == Guid.Empty)
            return Result.Failure<CashRegister>(
                Error.Validation("WarehouseId", "L'entrepôt est obligatoire"));

        if (string.IsNullOrWhiteSpace(code))
            return Result.Failure<CashRegister>(
                Error.Validation("Code", "Le code caisse est obligatoire"));

        var codeTrimmed = code.Trim().ToUpperInvariant();
        if (codeTrimmed.Length > CodeMaxLength)
            return Result.Failure<CashRegister>(
                Error.Validation("Code", $"Le code caisse ne peut pas dépasser {CodeMaxLength} caractères"));

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<CashRegister>(
                Error.Validation("Name", "Le nom de la caisse est obligatoire"));

        var nameTrimmed = name.Trim();
        if (nameTrimmed.Length > NameMaxLength)
            return Result.Failure<CashRegister>(
                Error.Validation("Name", $"Le nom de la caisse ne peut pas dépasser {NameMaxLength} caractères"));

        var register = new CashRegister
        {
            Code = codeTrimmed,
            Name = nameTrimmed,
            WarehouseId = warehouseId,
            IsActive = true,
            IsDefault = isDefault
        };

        return Result.Success(register);
    }

    /// <summary>
    /// Default register code for a warehouse: WH-{code}, truncated to <see cref="CodeMaxLength"/>.
    /// </summary>
    public static string DefaultCodeForWarehouse(string warehouseCode)
    {
        var suffix = (warehouseCode ?? string.Empty).Trim().ToUpperInvariant();
        var raw = string.IsNullOrEmpty(suffix) ? "WH-DEFAULT" : $"WH-{suffix}";
        return raw.Length <= CodeMaxLength ? raw : raw[..CodeMaxLength];
    }

    public static string DefaultNameForWarehouse(string warehouseName)
    {
        var name = (warehouseName ?? string.Empty).Trim();
        var raw = string.IsNullOrEmpty(name) ? "Caisse" : $"Caisse {name}";
        return raw.Length <= NameMaxLength ? raw : raw[..NameMaxLength];
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void MarkDefault() => IsDefault = true;

    public void ClearDefault() => IsDefault = false;
}
