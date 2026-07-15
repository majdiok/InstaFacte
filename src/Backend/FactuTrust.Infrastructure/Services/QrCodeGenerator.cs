using FactuTrust.Application.Common.Interfaces.Services;
using QRCoder;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Générateur de QR PNG à usage général, adossé à QRCoder (déjà utilisé pour TOTP / factures /
/// Studio). Même approche que <c>Studio.StudioQrGenerator</c>.
/// </summary>
public sealed class QrCodeGenerator : IQrCodeGenerator
{
    // Un QR WhatsApp Web tient largement en dessous de cette borne ; garde-fou anti-abus.
    private const int MaxTextLength = 4096;

    public byte[]? GeneratePng(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxTextLength)
            return null;

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(8); // 8 pixels par module
    }
}
