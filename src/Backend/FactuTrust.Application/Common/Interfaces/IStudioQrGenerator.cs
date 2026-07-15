namespace FactuTrust.Application.Common.Interfaces;

/// <summary>Generates a QR-code PNG for a text value (used by Studio QrCode display fields).</summary>
public interface IStudioQrGenerator
{
    /// <summary>Returns a PNG image of the QR encoding <paramref name="text"/>, or null if text is empty/too long.</summary>
    byte[]? GeneratePng(string? text);
}
