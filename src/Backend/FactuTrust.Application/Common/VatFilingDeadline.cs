namespace FactuTrust.Application.Common;

/// <summary>
/// Date limite de dépôt de la déclaration TVA mensuelle (Tunisie) : 22 du mois suivant la période.
/// </summary>
public static class VatFilingDeadline
{
    public static DateTime ForPeriod(int year, int month)
    {
        var following = new DateTime(year, month, 1).AddMonths(1);
        var day = Math.Min(22, DateTime.DaysInMonth(following.Year, following.Month));
        return new DateTime(following.Year, following.Month, day);
    }
}
