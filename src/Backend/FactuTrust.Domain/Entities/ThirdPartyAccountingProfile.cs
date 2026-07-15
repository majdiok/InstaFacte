using System.Text.RegularExpressions;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Fiche comptable d'un tiers (plan tiers unifié) : code auxiliaire lisible (ex. C0001/F0001,
/// exporté en CompAuxNum du FEC), compte collectif de rattachement et conditions de règlement.
/// Table séparée : les entités Client/Supplier restent intactes (jointure par ThirdPartyId).
/// </summary>
public sealed class ThirdPartyAccountingProfile : Entity
{
    private static readonly Regex CodePattern = new("^[A-Z0-9-]{1,20}$", RegexOptions.Compiled);

    public ThirdPartyKind Kind { get; private set; }
    public Guid ThirdPartyId { get; private set; }

    /// <summary>Code auxiliaire lisible et unique (CompAuxNum du FEC).</summary>
    public string AuxiliaryCode { get; private set; } = null!;

    /// <summary>Compte collectif de rattachement (411x clients / 401x fournisseurs).</summary>
    public string CollectiveAccountNumber { get; private set; } = null!;

    /// <summary>Délai de règlement en jours (facultatif, 0-365).</summary>
    public int? PaymentTermDays { get; private set; }

    public string? AccountingNotes { get; private set; }

    private ThirdPartyAccountingProfile() { }

    public static Result<ThirdPartyAccountingProfile> Create(
        ThirdPartyKind kind, Guid thirdPartyId, string auxiliaryCode,
        string collectiveAccountNumber, int? paymentTermDays = null, string? accountingNotes = null)
    {
        if (kind is not (ThirdPartyKind.Client or ThirdPartyKind.Supplier))
            return Result.Failure<ThirdPartyAccountingProfile>(Error.Validation(
                "Kind", "Le tiers doit être un client ou un fournisseur."));
        if (thirdPartyId == Guid.Empty)
            return Result.Failure<ThirdPartyAccountingProfile>(Error.Validation("ThirdPartyId", "Tiers obligatoire."));

        var profile = new ThirdPartyAccountingProfile { Kind = kind, ThirdPartyId = thirdPartyId };
        var apply = profile.Apply(auxiliaryCode, collectiveAccountNumber, paymentTermDays, accountingNotes);
        if (apply.IsFailure)
            return Result.Failure<ThirdPartyAccountingProfile>(apply.Error);

        return Result.Success(profile);
    }

    public Result Update(string auxiliaryCode, string collectiveAccountNumber, int? paymentTermDays, string? accountingNotes)
        => Apply(auxiliaryCode, collectiveAccountNumber, paymentTermDays, accountingNotes);

    /// <summary>Compte collectif par défaut selon le type de tiers (SCE : 4111 clients, 4011 fournisseurs).</summary>
    public static string DefaultCollectiveAccount(ThirdPartyKind kind) =>
        kind == ThirdPartyKind.Supplier ? "4011" : "4111";

    /// <summary>Préfixe de code auxiliaire par défaut (C clients, F fournisseurs).</summary>
    public static string CodePrefix(ThirdPartyKind kind) =>
        kind == ThirdPartyKind.Supplier ? "F" : "C";

    private Result Apply(string auxiliaryCode, string collectiveAccountNumber, int? paymentTermDays, string? accountingNotes)
    {
        auxiliaryCode = auxiliaryCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!CodePattern.IsMatch(auxiliaryCode))
            return Result.Failure(Error.Validation(
                "AuxiliaryCode", "Code auxiliaire invalide : 1 à 20 caractères A-Z, 0-9 ou tiret."));

        collectiveAccountNumber = collectiveAccountNumber?.Trim() ?? string.Empty;
        if (collectiveAccountNumber.Length is 0 or > 20 || !collectiveAccountNumber.All(char.IsAsciiDigit))
            return Result.Failure(Error.Validation(
                "CollectiveAccountNumber", "Compte collectif invalide : 1 à 20 chiffres attendus."));

        if (paymentTermDays is < 0 or > 365)
            return Result.Failure(Error.Validation("PaymentTermDays", "Délai de règlement entre 0 et 365 jours."));

        accountingNotes = string.IsNullOrWhiteSpace(accountingNotes) ? null : accountingNotes.Trim();
        if (accountingNotes is { Length: > 500 })
            return Result.Failure(Error.Validation("AccountingNotes", "Notes limitées à 500 caractères."));

        AuxiliaryCode = auxiliaryCode;
        CollectiveAccountNumber = collectiveAccountNumber;
        PaymentTermDays = paymentTermDays;
        AccountingNotes = accountingNotes;
        return Result.Success();
    }
}
