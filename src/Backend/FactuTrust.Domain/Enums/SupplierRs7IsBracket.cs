namespace FactuTrust.Domain.Enums;

/// <summary>
/// Régime d'impôt sur les sociétés du fournisseur pour les achats soumis à RS7 (TEJ).
/// Détermine le sous-code RS7 et le taux de retenue sur HT (1,5 % / 1 % / 0,5 %).
/// </summary>
public enum SupplierRs7IsBracket
{
    /// <summary>Non utilisé : conserver le type RS explicite ou le défaut catalogue.</summary>
    Unspecified = 0,

    /// <summary>IS taux normal 25 % → code TEJ RS7_000001, retenue 1,5 %.</summary>
    Normal25 = 1,

    /// <summary>IS réduit 15 % → RS7_000002, retenue 1 %.</summary>
    Reduced15 = 2,

    /// <summary>IS réduit 10 % → RS7_000003, retenue 0,5 %.</summary>
    Reduced10 = 3
}
