namespace FactuTrust.Domain.Enums;

public enum WithholdingCategory
{
    Loyers = 0,
    Honoraires = 1,
    Commissions = 2,
    Achats = 3,
    RevenusCapitaux = 4,
    Dividendes = 5,
    PlusValues = 6,
    Salaires = 7,
    NonResidents = 8,
    TVA = 9,
    Jeux = 10,
    ImmobilierFoncier = 11,
    Autres = 12
}

public static class WithholdingCategoryExtensions
{
    public static string ToDisplayString(this WithholdingCategory category) => category switch
    {
        WithholdingCategory.Loyers => "Loyers",
        WithholdingCategory.Honoraires => "Honoraires",
        WithholdingCategory.Commissions => "Commissions et courtages",
        WithholdingCategory.Achats => "Achats ≥ 1 000 TND TTC",
        WithholdingCategory.RevenusCapitaux => "Revenus de capitaux mobiliers",
        WithholdingCategory.Dividendes => "Dividendes",
        WithholdingCategory.PlusValues => "Plus-values",
        WithholdingCategory.Salaires => "Salaires et traitements",
        WithholdingCategory.NonResidents => "Non-résidents",
        WithholdingCategory.TVA => "Retenue TVA",
        WithholdingCategory.Jeux => "Jeux et loteries",
        WithholdingCategory.ImmobilierFoncier => "Immobilier / Fonds de commerce",
        WithholdingCategory.Autres => "Autres retenues",
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };
}

public enum CertificateStatus
{
    Draft = 0,
    Validated = 1,
    Submitted = 2,
    Cancelled = 3,
    CorrectionPending = 4
}

public static class CertificateStatusExtensions
{
    public static string ToDisplayString(this CertificateStatus status) => status switch
    {
        CertificateStatus.Draft => "Brouillon",
        CertificateStatus.Validated => "Validé",
        CertificateStatus.Submitted => "Soumis à TEJ",
        CertificateStatus.Cancelled => "Annulé",
        CertificateStatus.CorrectionPending => "Correction en attente",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static bool CanBeEdited(this CertificateStatus status) =>
        status is CertificateStatus.Draft or CertificateStatus.CorrectionPending;

    public static bool CanBeValidated(this CertificateStatus status) =>
        status is CertificateStatus.Draft or CertificateStatus.CorrectionPending;

    public static bool CanBeCancelled(this CertificateStatus status) =>
        status is CertificateStatus.Draft or CertificateStatus.Validated or CertificateStatus.CorrectionPending;
}

public enum IdentificationType
{
    MatriculeFiscal = 1,
    CIN = 2,
    Passeport = 3,
    CarteSejour = 4,
    AutreIdentifiant = 5
}

public static class IdentificationTypeExtensions
{
    public static string ToDisplayString(this IdentificationType type) => type switch
    {
        IdentificationType.MatriculeFiscal => "Matricule fiscal",
        IdentificationType.CIN => "Carte d'identité nationale",
        IdentificationType.Passeport => "Passeport",
        IdentificationType.CarteSejour => "Carte de séjour",
        IdentificationType.AutreIdentifiant => "Autre identifiant",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static string ToTejCode(this IdentificationType type) => ((int)type).ToString();
}

public enum TejSubmissionType
{
    Initial = 0,
    Rectificative = 1
}

public enum BeneficiaryCategory
{
    PersonnePhysique = 0,
    PersonneMorale = 1
}

public static class BeneficiaryCategoryExtensions
{
    public static string ToDisplayString(this BeneficiaryCategory cat) => cat switch
    {
        BeneficiaryCategory.PersonnePhysique => "Personne physique",
        BeneficiaryCategory.PersonneMorale => "Personne morale",
        _ => throw new ArgumentOutOfRangeException(nameof(cat))
    };

    public static string ToTejCode(this BeneficiaryCategory cat) => cat switch
    {
        BeneficiaryCategory.PersonnePhysique => "PP",
        BeneficiaryCategory.PersonneMorale => "PM",
        _ => throw new ArgumentOutOfRangeException(nameof(cat))
    };
}

public enum BeneficiaryType
{
    Client = 0,
    Supplier = 1,
    Employee = 2,
    Other = 3
}

public enum TejCategory
{
    DGE = 0,
    DME = 1,
    Teledeclaration = 2,
    Autre = 3
}

public static class TejCategoryExtensions
{
    public static string ToDisplayString(this TejCategory cat) => cat switch
    {
        TejCategory.DGE => "Direction des Grandes Entreprises",
        TejCategory.DME => "Direction des Moyennes Entreprises",
        TejCategory.Teledeclaration => "Télédéclaration",
        TejCategory.Autre => "Autre",
        _ => throw new ArgumentOutOfRangeException(nameof(cat))
    };
}
