namespace FactuTrust.Domain.Enums;

/// <summary>
/// Régime de TVA du client, au sens du code tunisien de la TVA.
///
/// ⚠️ À ne pas confondre avec <see cref="VatRate"/>, qui reste le TAUX porté par la ligne.
/// Le régime est un attribut du CLIENT : il décide si la TVA s'applique, et sous quelle
/// justification. Le taux, lui, reste celui du produit.
///
/// Jusqu'ici, exonération, suspension et export étaient tous amalgamés dans « 0 % » :
/// impossible de distinguer un client totalement exportateur d'un client exonéré, ni de
/// justifier une suspension en contrôle fiscal.
/// </summary>
public enum ClientVatRegime
{
    /// <summary>Assujetti ordinaire : la TVA s'applique au taux du produit.</summary>
    Normal = 0,

    /// <summary>
    /// Exonéré de TVA. Aucune TVA facturée, sans attestation à produire — l'exonération
    /// tient à la nature de l'opération ou du bien.
    /// </summary>
    Exempt = 1,

    /// <summary>
    /// Achats en suspension de TVA (art. 11 du code de la TVA). Suppose une ATTESTATION
    /// valide, dont le numéro et la validité doivent figurer sur la facture.
    /// </summary>
    Suspension = 2,

    /// <summary>
    /// Client à l'export : opération hors champ, TVA à 0 %. Distinct de l'exonération —
    /// le droit à déduction en amont n'est pas le même.
    /// </summary>
    Export = 3,

    /// <summary>
    /// Assujetti partiel : la TVA s'applique, mais le prorata de déduction du client diffère.
    /// Sans effet sur la facturation de vente ; conservé pour la qualification du tiers.
    /// </summary>
    PartiallyLiable = 4
}

public static class ClientVatRegimeExtensions
{
    /// <summary>Vrai lorsque le régime interdit de facturer de la TVA.</summary>
    public static bool SuppressesVat(this ClientVatRegime regime) =>
        regime is ClientVatRegime.Exempt or ClientVatRegime.Suspension or ClientVatRegime.Export;

    /// <summary>Vrai lorsqu'une attestation en cours de validité est exigée.</summary>
    public static bool RequiresCertificate(this ClientVatRegime regime) =>
        regime == ClientVatRegime.Suspension;

    /// <summary>
    /// Mention légale à porter sur la facture. <c>null</c> pour un assujetti ordinaire.
    /// </summary>
    public static string? LegalMention(this ClientVatRegime regime) => regime switch
    {
        ClientVatRegime.Exempt => "Opération exonérée de TVA",
        ClientVatRegime.Suspension => "Vente en suspension de TVA — article 11 du code de la TVA",
        ClientVatRegime.Export => "Exportation — TVA non applicable",
        _ => null
    };

    public static string ToDisplayString(this ClientVatRegime regime) => regime switch
    {
        ClientVatRegime.Normal => "Assujetti (régime normal)",
        ClientVatRegime.Exempt => "Exonéré",
        ClientVatRegime.Suspension => "Suspension de TVA (art. 11)",
        ClientVatRegime.Export => "Export",
        ClientVatRegime.PartiallyLiable => "Assujetti partiel",
        _ => throw new ArgumentOutOfRangeException(nameof(regime))
    };
}
