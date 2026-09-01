namespace FactuTrust.Domain.Enums;

public enum ThirdPartyKind
{
    None = 0,
    Client = 1,
    Supplier = 2,
    /// <summary>Salarié (compte auxiliaire 425xxxx, rattaché au collectif 425 « Personnel - rémunérations dues »).</summary>
    Employee = 3
}
