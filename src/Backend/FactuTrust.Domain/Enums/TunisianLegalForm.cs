namespace FactuTrust.Domain.Enums;

/// <summary>Formes juridiques tunisiennes courantes.</summary>
public enum TunisianLegalForm
{
    Sarl = 0,
    Suarl = 1,
    Sa = 2,
    Snc = 3,
    Scs = 4,
    EntrepriseIndividuelle = 5,
    Other = 99
}

public static class TunisianLegalFormExtensions
{
    public static string ToDisplayString(this TunisianLegalForm form) => form switch
    {
        TunisianLegalForm.Sarl => "SARL",
        TunisianLegalForm.Suarl => "SUARL",
        TunisianLegalForm.Sa => "SA",
        TunisianLegalForm.Snc => "SNC",
        TunisianLegalForm.Scs => "SCS",
        TunisianLegalForm.EntrepriseIndividuelle => "Entreprise individuelle",
        TunisianLegalForm.Other => "Autre",
        _ => form.ToString()
    };
}
