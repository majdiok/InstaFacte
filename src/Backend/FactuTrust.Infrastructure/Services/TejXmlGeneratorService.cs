using System.Text;
using System.Xml.Linq;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Infrastructure.Services;

public class TejXmlGeneratorService : ITejXmlGeneratorService
{
    private readonly ITejXmlValidatorService _validator;

    public TejXmlGeneratorService(ITejXmlValidatorService validator)
    {
        _validator = validator;
    }

    public Task<TejXmlExportResultDto> GenerateAsync(
        Company declarant,
        IReadOnlyList<TejRsCertificatPayload> certificats,
        int year,
        int month,
        TejSubmissionType submissionType,
        CancellationToken ct = default)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            BuildRoot(declarant, certificats, year, month, submissionType));

        var xmlBytes = SerializeToUtf8(doc);
        var errors = _validator.Validate(xmlBytes);

        var nif = declarant.Nif.Value;
        var fileName = $"{nif}-{year}-{month:D2}-{(int)submissionType}.xml";

        var result = new TejXmlExportResultDto(
            fileName,
            xmlBytes,
            errors,
            certificats.Count,
            errors.Count == 0);

        return Task.FromResult(result);
    }

    private static XElement BuildRoot(
        Company declarant,
        IReadOnlyList<TejRsCertificatPayload> certificats,
        int year,
        int month,
        TejSubmissionType submissionType)
    {
        var root = new XElement("DeclarationsRS",
            new XAttribute("VersionSchema", "1.0"));

        root.Add(BuildDeclarant(declarant));
        root.Add(BuildReferenceDeclaration(year, month, submissionType));
        root.Add(BuildCertificats(certificats, submissionType));

        return root;
    }

    private static XElement BuildDeclarant(Company company)
    {
        return new XElement("Declarant",
            new XElement("TypeIdentifiant", "1"),
            new XElement("Identifiant", company.Nif.Value),
            new XElement("CategorieContribuable", "PM"));
    }

    private static XElement BuildReferenceDeclaration(int year, int month, TejSubmissionType type)
    {
        return new XElement("ReferenceDeclaration",
            new XElement("ActeDepot", ((int)type).ToString()),
            new XElement("AnneeDepot", year.ToString()),
            new XElement("MoisDepot", month.ToString("D2")));
    }

    private static XElement BuildCertificats(
        IReadOnlyList<TejRsCertificatPayload> certificats,
        TejSubmissionType submissionType)
    {
        var wrapper = submissionType == TejSubmissionType.Initial
            ? new XElement("AjouterCertificats")
            : new XElement("ModifierCertificats");

        foreach (var cert in certificats)
        {
            wrapper.Add(BuildCertificat(cert));
        }

        return wrapper;
    }

    private static XElement BuildCertificat(TejRsCertificatPayload cert)
    {
        var element = new XElement("Certificat");

        element.Add(BuildBeneficiaire(cert));
        element.Add(new XElement("DatePayement", cert.PaymentDate.ToString("dd/MM/yyyy")));
        element.Add(new XElement("Ref_certif_chez_declarant", cert.RefCertifChezDeclarant));
        element.Add(BuildListeOperations(cert));
        element.Add(BuildTotalPayement(cert));

        return element;
    }

    private static XElement BuildBeneficiaire(TejRsCertificatPayload cert)
    {
        var beneficiaire = new XElement("Beneficiaire");

        beneficiaire.Add(BuildIdTaxpayer(cert));
        beneficiaire.Add(new XElement("Resident", cert.IsResident ? "1" : "0"));
        beneficiaire.Add(new XElement("NometprenonOuRaisonsociale", cert.BeneficiaryName));
        beneficiaire.Add(new XElement("Adresse", cert.BeneficiaryAddress ?? ""));

        if (!string.IsNullOrWhiteSpace(cert.BeneficiaryActivity))
            beneficiaire.Add(new XElement("Activite", cert.BeneficiaryActivity));

        var contactInfo = new XElement("InfosContact",
            new XElement("AdresseMail", cert.BeneficiaryEmail ?? ""),
            new XElement("NumTel", cert.BeneficiaryPhone ?? ""));
        beneficiaire.Add(contactInfo);

        return beneficiaire;
    }

    private static XElement BuildIdTaxpayer(TejRsCertificatPayload cert)
    {
        var idTaxpayer = new XElement("IdTaxpayer");
        var categoryCode = cert.BeneficiaryCategory.ToTejCode();

        switch (cert.IdentificationType)
        {
            case IdentificationType.MatriculeFiscal:
                idTaxpayer.Add(new XElement("MatriculeFiscal",
                    new XElement("TypeIdentifiant", "1"),
                    new XElement("Identifiant", cert.IdentificationNumber),
                    new XElement("CategorieContribuable", categoryCode)));
                break;

            case IdentificationType.CIN:
                idTaxpayer.Add(new XElement("CIN",
                    new XElement("TypeIdentifiant", "2"),
                    new XElement("Identifiant", cert.IdentificationNumber),
                    new XElement("DateNaissance", cert.DateOfBirth?.ToString("dd/MM/yyyy") ?? ""),
                    new XElement("CategorieContribuable", "PP")));
                break;

            case IdentificationType.Passeport:
                idTaxpayer.Add(new XElement("Passeport",
                    new XElement("TypeIdentifiant", "3"),
                    new XElement("Identifiant", cert.IdentificationNumber),
                    new XElement("DateNaissance", cert.DateOfBirth?.ToString("dd/MM/yyyy") ?? ""),
                    new XElement("Pays", cert.CountryCode ?? ""),
                    new XElement("CategorieContribuable", "PP")));
                break;

            case IdentificationType.CarteSejour:
                idTaxpayer.Add(new XElement("CarteSejour",
                    new XElement("TypeIdentifiant", "4"),
                    new XElement("Identifiant", cert.IdentificationNumber),
                    new XElement("DateNaissance", cert.DateOfBirth?.ToString("dd/MM/yyyy") ?? ""),
                    new XElement("Pays", cert.CountryCode ?? ""),
                    new XElement("CategorieContribuable", "PP")));
                break;

            default:
                idTaxpayer.Add(new XElement("AutreIdentifiantFiscal",
                    new XElement("TypeIdentifiant", "5"),
                    new XElement("Identifiant", cert.IdentificationNumber),
                    new XElement("Pays", cert.CountryCode ?? ""),
                    new XElement("CategorieContribuable", categoryCode)));
                break;
        }

        return idTaxpayer;
    }

    private static XElement BuildListeOperations(TejRsCertificatPayload cert)
    {
        var liste = new XElement("ListeOperations");

        foreach (var line in cert.Lines)
        {
            var op = new XElement("Operation",
                new XAttribute("IdTypeOperation", line.OperationCode));

            op.Add(new XElement("AnneeFacturation", cert.BillingYear.ToString()));
            op.Add(new XElement("CNPC", cert.HasCNPC ? "1" : "0"));
            op.Add(new XElement("P_Charge", cert.HasPriseEnCharge ? "1" : "0"));
            op.Add(new XElement("MontantHT", ToMillimes(line.AmountHT)));

            if (line.VatRate > 0)
            {
                op.Add(new XElement("TauxTVA", line.VatRate.ToString("F2")));
                op.Add(new XElement("MontantTVA", ToMillimes(line.AmountTVA)));
            }

            op.Add(new XElement("TauxRS", line.WithholdingRate.ToString("F2")));
            op.Add(new XElement("MontantTTC", ToMillimes(line.AmountTTC)));
            op.Add(new XElement("MontantRS", ToMillimes(line.AmountWithheld)));
            op.Add(new XElement("MontantNetServi", ToMillimes(line.NetAmountPaid)));

            if (!string.IsNullOrWhiteSpace(line.Currency) && line.ExchangeRate.HasValue)
            {
                var devise = new XElement("Devise",
                    new XElement("CodeDevise", line.Currency),
                    new XElement("TauxChange", line.ExchangeRate.Value.ToString("F6")));

                if (line.AmountWithheldForeignCurrency.HasValue)
                    devise.Add(new XElement("MontantRSDevise", ToMillimes(line.AmountWithheldForeignCurrency.Value)));
                if (line.AmountTTCForeignCurrency.HasValue)
                    devise.Add(new XElement("MontantTTCDevise", ToMillimes(line.AmountTTCForeignCurrency.Value)));
                if (line.NetAmountPaidForeignCurrency.HasValue)
                    devise.Add(new XElement("MontantNetServiDevise", ToMillimes(line.NetAmountPaidForeignCurrency.Value)));

                op.Add(devise);
            }

            liste.Add(op);
        }

        return liste;
    }

    private static XElement BuildTotalPayement(TejRsCertificatPayload cert)
    {
        return new XElement("TotalPayement",
            new XElement("TotalMontantHT", ToMillimes(cert.TotalAmountHT)),
            new XElement("TotalMontantTVA", ToMillimes(cert.TotalAmountTVA)),
            new XElement("TotalMontantTTC", ToMillimes(cert.TotalAmountTTC)),
            new XElement("TotalMontantRS", ToMillimes(cert.TotalAmountWithheld)),
            new XElement("TotalMontantNetServi", ToMillimes(cert.TotalNetPaid)));
    }

    private static string ToMillimes(decimal tndAmount)
        => ((long)Math.Round(tndAmount * 1000m, 0)).ToString();

    private static byte[] SerializeToUtf8(XDocument doc)
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        doc.Save(writer);
        writer.Flush();
        return ms.ToArray();
    }
}
