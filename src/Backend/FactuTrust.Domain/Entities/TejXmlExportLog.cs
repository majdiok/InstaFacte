using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>Audit trail of TEJ XML files generated from FactuTrust (hash + metadata).</summary>
public sealed class TejXmlExportLog : Entity
{
    public int Year { get; private set; }
    public int Month { get; private set; }
    public int SubmissionType { get; private set; }
    public string FileName { get; private set; } = null!;
    public string Sha256Hex { get; private set; } = null!;
    public int CertificateCount { get; private set; }
    public bool IsValid { get; private set; }
    public string? ValidationErrorSummary { get; private set; }
    public string? ExportedByEmail { get; private set; }

    private TejXmlExportLog()
    {
    }

    public static TejXmlExportLog Create(
        int year,
        int month,
        int submissionType,
        string fileName,
        byte[] sha256,
        int certificateCount,
        bool isValid,
        string? validationErrorSummary,
        string? exportedByEmail)
    {
        var log = new TejXmlExportLog
        {
            Year = year,
            Month = month,
            SubmissionType = submissionType,
            FileName = fileName,
            Sha256Hex = Convert.ToHexString(sha256).ToLowerInvariant(),
            CertificateCount = certificateCount,
            IsValid = isValid,
            ValidationErrorSummary = Truncate(validationErrorSummary, 4000),
            ExportedByEmail = exportedByEmail
        };
        return log;
    }

    private static string? Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? s : s.Length <= max ? s : s[..max];
}
