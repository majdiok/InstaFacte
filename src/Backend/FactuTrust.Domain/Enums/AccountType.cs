namespace FactuTrust.Domain.Enums;

/// <summary>
/// Nature métier d'un compte du plan comptable (façon Axeane Kompta). <see cref="General"/> par défaut
/// pour préserver le comportement historique des comptes existants.
/// </summary>
public enum AccountType
{
    /// <summary>Compte général (par défaut).</summary>
    General = 0,

    /// <summary>Compte client (auxiliaire d'un compte collectif de la classe 411).</summary>
    Client = 1,

    /// <summary>Compte fournisseur (auxiliaire d'un compte collectif de la classe 401).</summary>
    Supplier = 2,

    /// <summary>Autre compte tiers/auxiliaire.</summary>
    Other = 3
}
