using FactuTrust.Application.Common.Interfaces;
using QRCoder;

namespace FactuTrust.Infrastructure.Services.Studio;

/// <summary>QR PNG generator backed by the QRCoder package already used for TOTP / fiscal invoice QR.</summary>
public sealed class StudioQrGenerator : IStudioQrGenerator
{
    private const int MaxTextLength = 2000;

    public byte[]? GeneratePng(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxTextLength)
            return null;

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(8); // 8 pixels per module
    }
}
