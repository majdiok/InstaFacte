namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Prépare les images (photos de documents) avant OCR : redimensionnement, niveaux de gris, contraste.
/// </summary>
public interface IImagePreprocessingService
{
    byte[] PreprocessForOcr(byte[] imageBytes);
}
