namespace FactuTrust.Domain.Enums;

public enum ThirdPartyKind
{
    None = 0,
    Client = 1,
    Supplier = 2,
    /// <summary>Salarié (compte auxiliaire 421xxxx pour la paie).</summary>
    Employee = 3
}
