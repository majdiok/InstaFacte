using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.ValueObjects;

/// <summary>
/// Attestation d'achat en suspension de TVA (article 11 du code de la TVA).
///
/// Sans elle, une vente en suspension n'est pas justifiable en contrôle : la validité est
/// donc portée par l'objet lui-même, et vérifiée à la date d'émission de la facture — non à
/// la date du jour, sans quoi une facture ancienne deviendrait irrégulière avec le temps.
/// </summary>
public sealed class VatExemptionCertificate : ValueObject
{
    public string Number { get; }
    public DateTime ValidFrom { get; }
    public DateTime ValidUntil { get; }

    private VatExemptionCertificate(string number, DateTime validFrom, DateTime validUntil)
    {
        Number = number;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
    }

    // Requis par EF Core
    private VatExemptionCertificate()
    {
        Number = string.Empty;
    }

    public static Result<VatExemptionCertificate> Create(string? number, DateTime validFrom, DateTime validUntil)
    {
        if (string.IsNullOrWhiteSpace(number))
            return Result.Failure<VatExemptionCertificate>(
                Error.Validation("CertificateNumber", "Le numéro d'attestation est obligatoire"));

        var normalized = number.Trim();

        if (normalized.Length > 50)
            return Result.Failure<VatExemptionCertificate>(
                Error.Validation("CertificateNumber", "Le numéro d'attestation ne peut pas dépasser 50 caractères"));

        if (validUntil.Date < validFrom.Date)
            return Result.Failure<VatExemptionCertificate>(
                Error.Validation("ValidUntil", "La fin de validité ne peut pas précéder le début"));

        return Result.Success(new VatExemptionCertificate(normalized, validFrom.Date, validUntil.Date));
    }

    /// <summary>
    /// Vrai si l'attestation couvre la date donnée — typiquement la date d'émission du
    /// document, et non la date du jour : une facture régulière à son émission le reste.
    /// </summary>
    public bool CoversDate(DateTime date)
    {
        var d = date.Date;
        return d >= ValidFrom && d <= ValidUntil;
    }

    /// <summary>Vrai si l'attestation est expirée à la date du jour.</summary>
    public bool IsExpired => DateTime.UtcNow.Date > ValidUntil;

    public override string ToString() => $"{Number} ({ValidFrom:dd/MM/yyyy} — {ValidUntil:dd/MM/yyyy})";

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Number;
        yield return ValidFrom;
        yield return ValidUntil;
    }
}
