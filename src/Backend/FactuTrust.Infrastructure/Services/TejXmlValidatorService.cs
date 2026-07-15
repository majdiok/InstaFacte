using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using FactuTrust.Application.Common.Interfaces.Services;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Validates TEJ XML: structural rules (always) + optional official XSD when embedded under Resources/TejSchemas/*.xsd.
/// À rapprocher régulièrement des XSD / guide publiés par l’administration (noms d’éléments, formats date et montants).
/// </summary>
public partial class TejXmlValidatorService : ITejXmlValidatorService
{
    private static readonly Regex DatePattern = DateRegex();

    public List<string> Validate(byte[] xmlContent)
    {
        var errors = new List<string>();

        XDocument doc;
        try
        {
            using var ms = new MemoryStream(xmlContent);
            doc = XDocument.Load(ms);
        }
        catch (Exception ex)
        {
            errors.Add($"Le fichier XML n'est pas valide : {ex.Message}");
            return errors;
        }

        ValidateStructure(doc, errors);
        ValidateEmbeddedSchemas(xmlContent, errors);

        return errors;
    }

    private static void ValidateEmbeddedSchemas(byte[] xmlContent, List<string> errors)
    {
        var assembly = typeof(TejXmlValidatorService).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(n => n.Contains("TejSchemas", StringComparison.OrdinalIgnoreCase)
                && n.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (names.Count == 0)
            return;

        var set = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };

        foreach (var name in names)
        {
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null)
                continue;

            try
            {
                using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
                set.Add(null, reader);
            }
            catch (XmlSchemaException ex)
            {
                errors.Add($"Schéma embarqué '{name}' : {ex.Message}");
                return;
            }
        }

        try
        {
            set.Compile();
        }
        catch (XmlSchemaException ex)
        {
            errors.Add($"Compilation des schémas XSD : {ex.Message}");
            return;
        }

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = set,
            ValidationFlags = XmlSchemaValidationFlags.ProcessInlineSchema
                | XmlSchemaValidationFlags.ReportValidationWarnings
        };

        void OnValidation(object? _, ValidationEventArgs e)
        {
            var loc = e.Exception?.LineNumber > 0 ? $"ligne {e.Exception.LineNumber} " : string.Empty;
            errors.Add($"XSD ({e.Severity}) {loc}: {e.Message}");
        }

        settings.ValidationEventHandler += OnValidation;

        try
        {
            using var ms = new MemoryStream(xmlContent);
            using var r = XmlReader.Create(ms, settings);
            while (r.Read())
            {
            }
        }
        catch (XmlException ex)
        {
            errors.Add($"XSD : {ex.Message}");
        }
    }

    private static void ValidateStructure(XDocument doc, List<string> errors)
    {
        var root = doc.Root;
        if (root is null || root.Name.LocalName != "DeclarationsRS")
        {
            errors.Add("L'élément racine doit être 'DeclarationsRS'");
            return;
        }

        var version = root.Attribute("VersionSchema")?.Value;
        if (version != "1.0")
            errors.Add("L'attribut VersionSchema doit être '1.0'");

        ValidateDeclarant(root, errors);
        ValidateReferenceDeclaration(root, errors);
        ValidateCertificats(root, errors);
    }

    private static void ValidateDeclarant(XElement root, List<string> errors)
    {
        var declarant = root.Element("Declarant");
        if (declarant is null)
        {
            errors.Add("L'élément 'Declarant' est obligatoire");
            return;
        }

        RequireElement(declarant, "TypeIdentifiant", errors, "Declarant");
        RequireElement(declarant, "Identifiant", errors, "Declarant");
        RequireElement(declarant, "CategorieContribuable", errors, "Declarant");
    }

    private static void ValidateReferenceDeclaration(XElement root, List<string> errors)
    {
        var refDecl = root.Element("ReferenceDeclaration");
        if (refDecl is null)
        {
            errors.Add("L'élément 'ReferenceDeclaration' est obligatoire");
            return;
        }

        RequireElement(refDecl, "ActeDepot", errors, "ReferenceDeclaration");
        RequireElement(refDecl, "AnneeDepot", errors, "ReferenceDeclaration");
        RequireElement(refDecl, "MoisDepot", errors, "ReferenceDeclaration");
    }

    private static void ValidateCertificats(XElement root, List<string> errors)
    {
        var ajouter = root.Element("AjouterCertificats");
        var modifier = root.Element("ModifierCertificats");
        var annuler = root.Element("AnnulerCertificats");

        if (ajouter is null && modifier is null && annuler is null)
        {
            errors.Add("Au moins un bloc 'AjouterCertificats', 'ModifierCertificats' ou 'AnnulerCertificats' est obligatoire");
            return;
        }

        var certElements = new List<XElement>();
        if (ajouter is not null) certElements.AddRange(ajouter.Elements("Certificat"));
        if (modifier is not null) certElements.AddRange(modifier.Elements("Certificat"));

        if (certElements.Count == 0 && annuler is null)
        {
            errors.Add("Au moins un certificat est requis dans le fichier");
            return;
        }

        for (var i = 0; i < certElements.Count; i++)
            ValidateCertificat(certElements[i], i + 1, errors);
    }

    private static void ValidateCertificat(XElement cert, int index, List<string> errors)
    {
        var prefix = $"Certificat #{index}";

        if (cert.Element("Beneficiaire") is null)
            errors.Add($"{prefix} : l'élément 'Beneficiaire' est obligatoire");

        var datePayement = cert.Element("DatePayement")?.Value;
        if (string.IsNullOrWhiteSpace(datePayement))
            errors.Add($"{prefix} : la date de paiement est obligatoire");
        else if (!DatePattern.IsMatch(datePayement))
            errors.Add($"{prefix} : le format de date doit être DD/MM/YYYY");

        RequireElement(cert, "Ref_certif_chez_declarant", errors, prefix);

        var operations = cert.Element("ListeOperations");
        if (operations is null || !operations.Elements("Operation").Any())
        {
            errors.Add($"{prefix} : au moins une opération est requise");
        }
        else
        {
            var opIndex = 1;
            foreach (var op in operations.Elements("Operation"))
            {
                ValidateOperation(op, prefix, opIndex++, errors);
            }
        }

        var total = cert.Element("TotalPayement");
        if (total is null)
            errors.Add($"{prefix} : l'élément 'TotalPayement' est obligatoire");
    }

    private static void ValidateOperation(XElement op, string certPrefix, int index, List<string> errors)
    {
        var prefix = $"{certPrefix}, Opération #{index}";

        var idType = op.Attribute("IdTypeOperation")?.Value;
        if (string.IsNullOrWhiteSpace(idType))
            errors.Add($"{prefix} : l'attribut 'IdTypeOperation' est obligatoire");

        RequireElement(op, "AnneeFacturation", errors, prefix);
        RequireElement(op, "CNPC", errors, prefix);
        RequireElement(op, "P_Charge", errors, prefix);

        ValidateMillimeAmount(op, "MontantHT", prefix, errors);
        RequireElement(op, "TauxRS", errors, prefix);
        ValidateMillimeAmount(op, "MontantTTC", prefix, errors);
        ValidateMillimeAmount(op, "MontantRS", prefix, errors);
        ValidateMillimeAmount(op, "MontantNetServi", prefix, errors);
    }

    private static void RequireElement(XElement parent, string elementName, List<string> errors, string context)
    {
        if (parent.Element(elementName) is null)
            errors.Add($"{context} : l'élément '{elementName}' est obligatoire");
    }

    private static void ValidateMillimeAmount(XElement parent, string elementName, string context, List<string> errors)
    {
        var element = parent.Element(elementName);
        if (element is null)
        {
            errors.Add($"{context} : l'élément '{elementName}' est obligatoire");
            return;
        }

        if (!long.TryParse(element.Value, out _))
            errors.Add($"{context} : '{elementName}' doit être un entier (montant en millimes)");
    }

    [GeneratedRegex(@"^\d{2}/\d{2}/\d{4}$")]
    private static partial Regex DateRegex();
}
