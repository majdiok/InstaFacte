using System.Globalization;
using System.Text.RegularExpressions;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Parse les montants des relevés bancaires tunisiens (BIAT, STB…) : point = milliers, virgule = décimales TND.
/// </summary>
public static class TunisianBankAmountParsing
{
  /// <summary>Format BIAT : 17.837,479 | 3.000,000 | 2,202 | 0,417</summary>
  public const string StrictAmountPattern = @"\d{1,3}(?:\.\d{3})*,\d{3}|\d{1,6},\d{3}";

  private static readonly Regex StrictAmountFullRegex = new(
    $"^(?:{StrictAmountPattern})$",
    RegexOptions.Compiled | RegexOptions.CultureInvariant);

  public static readonly Regex StrictAmountTokenRegex = new(
    StrictAmountPattern,
    RegexOptions.Compiled | RegexOptions.CultureInvariant);

  public static bool IsPlausibleOperationAmount(decimal amount, decimal max = 50_000_000m)
    => amount > 0 && amount <= max;

  public static bool TryParseStrictAmount(string? value, out decimal amount)
  {
    amount = 0m;
    value = value?.Trim() ?? string.Empty;
    if (string.IsNullOrEmpty(value) || !StrictAmountFullRegex.IsMatch(value))
      return false;

    return TryParseTunisianBankAmount(value, out amount);
  }

  public static bool TryParseTunisianBankAmount(string? value, out decimal amount)
  {
    amount = 0m;
    value = value?.Trim() ?? string.Empty;
    if (string.IsNullOrEmpty(value))
      return false;

    value = value.Replace(" ", string.Empty).Replace("\u00A0", string.Empty);

    var hasDot = value.Contains('.');
    var hasComma = value.Contains(',');

    if (hasDot && hasComma)
    {
      var lastComma = value.LastIndexOf(',');
      var intPart = value[..lastComma].Replace(".", string.Empty);
      var fracPart = value[(lastComma + 1)..];
      value = intPart + "." + fracPart;
    }
    else if (hasComma)
    {
      value = value.Replace(',', '.');
    }

    return decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowLeadingSign,
      CultureInfo.InvariantCulture, out amount);
  }
}
