using System.Text.Json.Serialization;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

public record WithholdingTaxTypeDto(
    Guid Id,
    string Code,
    WithholdingCategory Category,
    string CategoryLabel,
    string Label,
    string? LabelAr,
    decimal DefaultRate,
    string? ArticleReference,
    bool ApplicableToResident,
    bool ApplicableToNonResident,
    decimal? MinimumThreshold,
    bool IsActive,
    bool IsSystem,
    int DisplayOrder);

/// <param name="WithholdingRateOverride">When set (facture/fournisseur), remplace le taux légal déduit du code d'opération.</param>
/// <param name="Rs7TtcThresholdTnd">Seuil TTC RS7 pour l'exercice concerné ; null = défaut légal en code (1000 TND).</param>
public record WithholdingCalculationRequest(
    decimal AmountHT,
    decimal? VatRate,
    string OperationCode,
    bool IsResident,
    bool HasCNPC,
    bool HasPriseEnCharge,
    decimal? WithholdingRateOverride = null,
    decimal? Rs7TtcThresholdTnd = null);

public record WithholdingCalculationResultDto(
    decimal GrossAmountHT,
    decimal VatAmount,
    decimal AmountTTC,
    decimal WithholdingRate,
    decimal WithholdingAmount,
    decimal NetAmountPaid,
    decimal? VatWithholdingAmount);

public record TejXmlExportRequest(
    [property: JsonPropertyName("exerciceYear")] int Year,
    [property: JsonPropertyName("month")] int Month,
    TejSubmissionType SubmissionType);

/// <summary>One TEJ <c>Operation</c> line (amounts in TND; millimes at XML serialization).</summary>
public record TejRsCertificatLinePayload(
    string OperationCode,
    decimal AmountHT,
    decimal VatRate,
    decimal AmountTVA,
    decimal AmountTTC,
    decimal WithholdingRate,
    decimal AmountWithheld,
    decimal NetAmountPaid,
    string? Currency,
    decimal? ExchangeRate,
    decimal? AmountWithheldForeignCurrency,
    decimal? AmountTTCForeignCurrency,
    decimal? NetAmountPaidForeignCurrency);

/// <summary>Payload for one TEJ <c>Certificat</c> element (independent of persisted certificate entity).</summary>
public record TejRsCertificatPayload(
    string RefCertifChezDeclarant,
    DateTime PaymentDate,
    int BillingYear,
    bool HasCNPC,
    bool HasPriseEnCharge,
    BeneficiaryCategory BeneficiaryCategory,
    IdentificationType IdentificationType,
    string IdentificationNumber,
    bool IsResident,
    string? CountryCode,
    DateTime? DateOfBirth,
    string BeneficiaryName,
    string? BeneficiaryAddress,
    string? BeneficiaryEmail,
    string? BeneficiaryPhone,
    string? BeneficiaryActivity,
    IReadOnlyList<TejRsCertificatLinePayload> Lines,
    decimal TotalAmountHT,
    decimal TotalAmountTVA,
    decimal TotalAmountTTC,
    decimal TotalAmountWithheld,
    decimal TotalNetPaid);

public record TejXmlExportResultDto(
    string FileName,
    byte[] XmlContent,
    List<string> ValidationErrors,
    int CertificateCount,
    bool IsValid);

/// <summary>Retenues subies sur encaissements clients (hors TEJ déclarant).</summary>
public record ClientWithholdingSubieMonthDto(int Month, decimal TotalSubie, int PaymentCount);

/// <summary>Dashboard scoped to <see cref="DashboardYear"/> (payment date year).</summary>
public record WithholdingDashboardDto(
    int DashboardYear,
    decimal TotalWithheldForYear,
    int TotalCertificatesForYear,
    int DraftCertificates,
    int ValidatedCertificates,
    int SubmittedCertificates,
    DateTime? NextTejDeadline,
    List<WithholdingDashboardMonthBreakdownDto> MonthlyBreakdown,
    List<string> Alerts,
    decimal TotalClientWithholdingSubieForYear,
    List<ClientWithholdingSubieMonthDto> ClientWithholdingSubieByMonth);

public record WithholdingDashboardMonthBreakdownDto(
    int Month,
    int CertificateCount,
    decimal TotalHT,
    decimal TotalWithheld,
    decimal TotalNetPaid);

public record WithholdingMonthlyTotalDto(
    int Year,
    int Month,
    decimal TotalWithheld,
    int CertificateCount);

public record WithholdingMonthlyReportDto(
    int Year,
    int Month,
    List<WithholdingReportByCategoryDto> ByCategory,
    decimal GrandTotalHT,
    decimal GrandTotalWithheld,
    int TotalCertificates);

public record WithholdingReportByCategoryDto(
    WithholdingCategory Category,
    string CategoryLabel,
    decimal TotalHT,
    decimal TotalWithheld,
    int CertificateCount);

public record TejXmlExportLogListItemDto(
    Guid Id,
    DateTime CreatedAt,
    string FileName,
    int Year,
    int Month,
    int SubmissionType,
    string Sha256Hex,
    int CertificateCount,
    bool IsValid,
    string? ExportedByEmail);
