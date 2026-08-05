namespace FactuTrust.Domain.Services.Payroll;

/// <summary>Génère un échéancier de prêt salarié sans intérêt.</summary>
public static class LoanScheduleGenerator
{
    public sealed record InstallmentScheduleItem(int Year, int Month, int SequenceNumber, decimal Amount);

    public static IReadOnlyList<InstallmentScheduleItem> Generate(
        decimal principal,
        int installmentCount,
        int startYear,
        int startMonth)
    {
        if (principal <= 0) throw new ArgumentOutOfRangeException(nameof(principal));
        if (installmentCount <= 0) throw new ArgumentOutOfRangeException(nameof(installmentCount));
        if (startMonth is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(startMonth));

        var baseAmount = Math.Round(principal / installmentCount, 3, MidpointRounding.AwayFromZero);
        var items = new List<InstallmentScheduleItem>(installmentCount);
        var remaining = principal;
        var year = startYear;
        var month = startMonth;

        for (var i = 1; i <= installmentCount; i++)
        {
            var amount = i == installmentCount ? R(remaining) : baseAmount;
            remaining = R(remaining - amount);
            items.Add(new InstallmentScheduleItem(year, month, i, amount));

            month++;
            if (month > 12)
            {
                month = 1;
                year++;
            }
        }

        return items;
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
