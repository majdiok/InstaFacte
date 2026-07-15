namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Générateur de QR code en PNG (usage général). Réutilise QRCoder côté Infrastructure, comme le
/// fait déjà <c>StudioQrGenerator</c> pour les QR Studio / factures / TOTP.
/// </summary>
public interface IQrCodeGenerator
{
    /// <summary>Rend le texte en PNG (octets), ou <c>null</c> si vide ou trop long.</summary>
    byte[]? GeneratePng(string? text);
}
