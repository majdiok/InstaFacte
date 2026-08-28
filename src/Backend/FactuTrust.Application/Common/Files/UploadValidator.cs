namespace FactuTrust.Application.Common.Files;

/// <summary>
/// Shared upload validation (CWE-434 hardening) for attachment endpoints that accept arbitrary
/// user-supplied files (project attachments, honoraires billing attachments, etc.).
///
/// Defense layers, in order:
/// 1. File name normalization via <see cref="Path.GetFileName(string)"/> (strips any directory
///    component — callers MUST use the returned <see cref="ValidationResult.SafeFileName"/>, never
///    the raw client-supplied name, when building storage paths).
/// 2. Double-extension rejection (e.g. <c>invoice.pdf.exe</c>, <c>report.csv.php</c>): any
///    intermediate "extension-shaped" segment that matches a known allow-listed or known-dangerous
///    extension is rejected, even if the final extension alone would be allowed.
/// 3. Extension + declared Content-Type allow-list (both must match one of the configured pairs).
/// 4. Size cap.
/// 5. Magic-byte signature check for binary formats that have a reliable signature: PDF (<c>%PDF</c>),
///    PNG, JPEG, GIF, WEBP, and ZIP/Office-Open-XML (<c>docx</c>/<c>xlsx</c>/<c>pptx</c>/<c>zip</c>,
///    all ZIP containers starting with <c>PK</c>). This catches a renamed/spoofed binary (e.g. an
///    executable saved with a <c>.pdf</c> extension and a forged Content-Type header) that steps
///    1-4 alone would not detect.
///
/// IMPORTANT — formats with NO reliable magic-byte signature (<c>.csv</c>, <c>.txt</c>: plain text,
/// no fixed header) are validated on name + declared Content-Type ONLY. This is a known, accepted
/// limitation for those two extensions, not an oversight: a malicious payload that is valid UTF-8/
/// ASCII text cannot be told apart from a legitimate CSV/TXT by signature alone. Callers that store
/// such files should still avoid ever executing or including them as script/HTML.
/// </summary>
public static class UploadValidator
{
    public const long DefaultMaxSizeBytes = 10 * 1024 * 1024; // 10 MB

    /// <summary>Number of leading bytes callers should read from the upload stream for <see cref="Validate"/>.</summary>
    public const int RequiredHeaderBytes = 16;

    public readonly record struct ValidationResult(bool IsValid, string? ErrorMessage, string? SafeFileName)
    {
        public static ValidationResult Success(string safeFileName) => new(true, null, safeFileName);
        public static ValidationResult Failure(string message) => new(false, message, null);
    }

    /// <summary>Allow-listed extension → declared Content-Type(s) accepted for that extension.</summary>
    private static readonly IReadOnlyDictionary<string, string[]> AllowedExtensionsToContentTypes =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = new[] { "application/pdf" },
            [".png"] = new[] { "image/png" },
            [".jpg"] = new[] { "image/jpeg" },
            [".jpeg"] = new[] { "image/jpeg" },
            [".gif"] = new[] { "image/gif" },
            [".webp"] = new[] { "image/webp" },
            [".doc"] = new[] { "application/msword" },
            [".docx"] = new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
            [".xls"] = new[] { "application/vnd.ms-excel" },
            [".xlsx"] = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
            [".ppt"] = new[] { "application/vnd.ms-powerpoint" },
            [".pptx"] = new[] { "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
            [".zip"] = new[] { "application/zip", "application/x-zip-compressed" },
            [".csv"] = new[] { "text/csv", "application/vnd.ms-excel", "text/plain" },
            [".txt"] = new[] { "text/plain" },
        };

    /// <summary>
    /// Extensions never accepted, used only to detect double-extension smuggling
    /// (e.g. <c>invoice.pdf.php</c>) — these are NOT part of the allow-list above.
    /// </summary>
    private static readonly IReadOnlySet<string> KnownDangerousExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".msi", ".bat", ".cmd", ".sh", ".ps1", ".vbs", ".js",
        ".php", ".php3", ".php4", ".php5", ".phtml", ".pht",
        ".asp", ".aspx", ".jsp", ".jspx", ".cgi", ".htm", ".html", ".svg",
    };

    /// <summary>Extensions with a fixed, reliable magic-byte signature we can verify.</summary>
    private static readonly IReadOnlyDictionary<string, Func<ReadOnlyMemory<byte>, bool>> MagicByteCheckers =
        new Dictionary<string, Func<ReadOnlyMemory<byte>, bool>>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = header => StartsWith(header, 0x25, 0x50, 0x44, 0x46), // %PDF
            [".png"] = header => StartsWith(header, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
            [".jpg"] = IsJpeg,
            [".jpeg"] = IsJpeg,
            [".gif"] = header => StartsWith(header, 0x47, 0x49, 0x46, 0x38), // "GIF8"
            [".webp"] = IsWebp,
            // ZIP / Office Open XML (docx/xlsx/pptx are ZIP containers) — "PK\x03\x04" (also empty/spanned archives).
            [".zip"] = IsZip,
            [".docx"] = IsZip,
            [".xlsx"] = IsZip,
            [".pptx"] = IsZip,
        };

    /// <summary>Extensions intentionally left without a magic-byte check (see class doc comment).</summary>
    public static readonly IReadOnlySet<string> ExtensionsWithoutMagicByteCheck = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".csv", ".txt", ".doc", ".xls", ".ppt", // legacy binary Office (OLE/CFBF) not checked here
    };

    private static bool IsJpeg(ReadOnlyMemory<byte> header) => StartsWith(header, 0xFF, 0xD8, 0xFF);

    private static bool IsWebp(ReadOnlyMemory<byte> header)
    {
        if (header.Length < 12) return false;
        var span = header.Span;
        return span[0] == (byte)'R' && span[1] == (byte)'I' && span[2] == (byte)'F' && span[3] == (byte)'F'
            && span[8] == (byte)'W' && span[9] == (byte)'E' && span[10] == (byte)'B' && span[11] == (byte)'P';
    }

    private static bool IsZip(ReadOnlyMemory<byte> header)
    {
        if (header.Length < 4) return false;
        var span = header.Span;
        if (span[0] != 0x50 || span[1] != 0x4B) return false; // "PK"
        return span[2] is 0x03 or 0x05 or 0x07; // local file header / empty archive / spanned archive
    }

    /// <summary>
    /// True only if <paramref name="extension"/> has a known magic-byte signature and
    /// <paramref name="header"/> matches it — fails closed for unknown extensions. Shared by
    /// callers (e.g. Studio file storage) that do their own extension/MIME allow-listing but
    /// still need the signature check.
    /// </summary>
    public static bool MatchesMagicBytes(string extension, ReadOnlyMemory<byte> header) =>
        MagicByteCheckers.TryGetValue(extension, out var checker) && checker(header);

    private static bool StartsWith(ReadOnlyMemory<byte> header, params byte[] signature)
    {
        if (header.Length < signature.Length) return false;
        var span = header.Span;
        for (var i = 0; i < signature.Length; i++)
        {
            if (span[i] != signature[i]) return false;
        }
        return true;
    }

    /// <summary>
    /// Validates an upload's name, declared extension/Content-Type pair, size, and (where a reliable
    /// signature exists) magic bytes. Does not read the whole stream — callers should pass at least
    /// <see cref="RequiredHeaderBytes"/> bytes read from the START of the stream in <paramref name="header"/>
    /// (fewer is fine for small files; the checkers below tolerate shorter spans by failing closed).
    /// </summary>
    public static ValidationResult Validate(
        string? fileName,
        string? contentType,
        long sizeBytes,
        ReadOnlyMemory<byte> header,
        long maxSizeBytes = DefaultMaxSizeBytes)
    {
        var safeName = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(safeName))
            return ValidationResult.Failure("Le nom du fichier est obligatoire.");

        if (sizeBytes <= 0)
            return ValidationResult.Failure("Le fichier est vide.");
        if (sizeBytes > maxSizeBytes)
            return ValidationResult.Failure($"Fichier trop volumineux (max {maxSizeBytes / (1024 * 1024)} Mo).");

        var doubleExtensionError = CheckForDoubleExtension(safeName);
        if (doubleExtensionError is not null)
            return ValidationResult.Failure(doubleExtensionError);

        var ext = Path.GetExtension(safeName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensionsToContentTypes.TryGetValue(ext, out var allowedTypes))
            return ValidationResult.Failure("Extension de fichier non autorisée.");

        if (string.IsNullOrWhiteSpace(contentType) || !allowedTypes.Contains(contentType.Trim()))
            return ValidationResult.Failure("Type de contenu (Content-Type) non autorisé pour cette extension.");

        if (MagicByteCheckers.TryGetValue(ext, out var checker) && !checker(header))
            return ValidationResult.Failure("Le contenu du fichier ne correspond pas à l'extension déclarée.");

        return ValidationResult.Success(safeName);
    }

    /// <summary>
    /// Rejects names with an intermediate "extension-shaped" segment that is itself allow-listed or
    /// known-dangerous, e.g. <c>invoice.pdf.exe</c> or <c>report.csv.php</c> — the attacker relies on
    /// some consumer looking only at the LAST extension while a web server executes the SECOND-to-last.
    /// </summary>
    private static string? CheckForDoubleExtension(string safeName)
    {
        var parts = safeName.Split('.');
        if (parts.Length <= 2) return null; // "name.ext" — nothing to check

        for (var i = 1; i < parts.Length - 1; i++)
        {
            var candidate = "." + parts[i];
            if (AllowedExtensionsToContentTypes.ContainsKey(candidate) || KnownDangerousExtensions.Contains(candidate))
                return "Nom de fichier avec double extension non autorisé.";
        }

        return null;
    }
}
