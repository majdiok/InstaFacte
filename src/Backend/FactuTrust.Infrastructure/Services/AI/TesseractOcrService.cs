using System.Collections.Concurrent;
using FactuTrust.Application.Common.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Tesseract;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>
/// OCR Tesseract. Les fichiers de langue (*.traineddata) doivent être présents
/// dans le dossier Resources/Tessdata (copié au build via le .csproj) ou dans
/// le chemin configuré (option). Si tessdata n'est pas disponible, le service
/// marque IsAvailable=false et RecognizeAsync renvoie une chaîne vide.
/// </summary>
public sealed class TesseractOcrService : IAiOcrService, IDisposable
{
    private readonly ILogger<TesseractOcrService> _logger;
    private readonly string _tessDataPath;
    private readonly string _defaultLanguages;
    private readonly ConcurrentDictionary<string, Lazy<TesseractEngine?>> _engineByLang;
    private readonly bool _available;

    public TesseractOcrService(ILogger<TesseractOcrService> logger)
    {
        _logger = logger;
        _tessDataPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Tessdata");
        _defaultLanguages = "fra+eng";
        _engineByLang = new ConcurrentDictionary<string, Lazy<TesseractEngine?>>(StringComparer.OrdinalIgnoreCase);
        _available = Directory.Exists(_tessDataPath)
            && Directory.EnumerateFiles(_tessDataPath, "*.traineddata", SearchOption.TopDirectoryOnly).Any();
        if (!_available)
        {
            _logger.LogInformation(
                "Tesseract OCR indisponible : aucun fichier *.traineddata trouvé dans {TessDataPath}. " +
                "L'OCR sera désactivé jusqu'à ce que les fichiers de langue soient déposés.",
                _tessDataPath);
        }
    }

    public bool IsAvailable => _available;

    public Task<string> RecognizeAsync(byte[] imageBytes, string? languages = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_available || imageBytes is null || imageBytes.Length == 0)
            return Task.FromResult(string.Empty);

        var langs = NormalizeLanguages(languages);
        var engine = GetEngine(langs);
        if (engine is null)
            return Task.FromResult(string.Empty);

        PageSegMode[] modes = [PageSegMode.Auto, PageSegMode.SparseText, PageSegMode.SingleBlock];
        string? best = null;
        var bestScore = 0;

        try
        {
            using var img = Pix.LoadFromMemory(imageBytes);
            foreach (var mode in modes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var page = engine.Process(img, mode);
                var text = (page.GetText() ?? string.Empty).Trim();
                var score = ScoreOcrText(text);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = text;
                }
                if (score >= 40)
                    break;
            }

            return Task.FromResult(best ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tesseract OCR processing failed (lang={Langs})", langs);
            return Task.FromResult(string.Empty);
        }
    }

    private static int ScoreOcrText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        var alnum = text.Count(char.IsLetterOrDigit);
        return alnum + Math.Min(text.Length / 4, 30);
    }

    private TesseractEngine? GetEngine(string langs)
    {
        var lazy = _engineByLang.GetOrAdd(langs, key => new Lazy<TesseractEngine?>(() =>
        {
            try
            {
                return new TesseractEngine(_tessDataPath, key, EngineMode.LstmOnly);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Tesseract engine init failed (path={Path}, lang={Lang}). " +
                    "Vérifiez la présence des fichiers .traineddata correspondants.",
                    _tessDataPath, key);
                return null;
            }
        }));
        return lazy.Value;
    }

    private string NormalizeLanguages(string? requested)
    {
        var langs = string.IsNullOrWhiteSpace(requested) ? _defaultLanguages : requested;
        var parts = langs.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var present = new List<string>();
        foreach (var p in parts)
        {
            var file = Path.Combine(_tessDataPath, p + ".traineddata");
            if (File.Exists(file))
            {
                present.Add(p);
            }
        }
        return present.Count > 0 ? string.Join("+", present) : "eng";
    }

    public void Dispose()
    {
        foreach (var kv in _engineByLang)
        {
            try
            {
                kv.Value.Value?.Dispose();
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
