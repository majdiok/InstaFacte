using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Accounting.Services;

/// <summary>
/// Builds a descriptive journal label for automatic JC entries from cash desk operations.
/// Output is capped to fit <see cref="MaxJournalLabelLength"/> (DB constraint).
/// </summary>
public static class CashOperationJournalLabelBuilder
{
    public const int MaxJournalLabelLength = 500;

    private const string Separator = " · ";

    public static string BuildJournalLabel(CashOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var typeWord = operation.OperationType == CashOperationType.Credit
            ? "Encaissement"
            : "Décaissement";

        var methodDisplay = operation.Method.ToDisplayString();

        var categoryDisplay = operation.OperationType == CashOperationType.Debit
            ? operation.Category?.ToDisplayString()
            : operation.RevenueCategory?.ToDisplayString();

        var userLabel = operation.Label.Trim();
        var doc = operation.Number.Value;
        var referencePart = string.IsNullOrWhiteSpace(operation.Reference)
            ? null
            : $"Réf. {operation.Reference.Trim()}";

        return ComposeAndTruncate(
            typeWord,
            methodDisplay,
            categoryDisplay,
            userLabel,
            doc,
            referencePart,
            MaxJournalLabelLength);
    }

    /// <summary>
    /// Exposed for unit tests. Preserves type, method and document number; drops or shortens lower-priority segments first.
    /// </summary>
    internal static string ComposeAndTruncate(
        string typeWord,
        string methodDisplay,
        string? categoryDisplay,
        string userLabel,
        string doc,
        string? referencePart,
        int maxLength)
    {
        if (maxLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength));

        string? reference = referencePart;
        var user = userLabel.Trim();
        string? category = string.IsNullOrWhiteSpace(categoryDisplay) ? null : categoryDisplay.Trim();

        while (true)
        {
            var text = Join(typeWord, methodDisplay, category, user, doc, reference);
            if (text.Length <= maxLength)
                return text.Length == 0 ? typeWord : text;

            if (reference is not null)
            {
                reference = null;
                continue;
            }

            var withoutUser = Join(typeWord, methodDisplay, category, string.Empty, doc, null);
            var maxUser = maxLength - withoutUser.Length - Separator.Length;
            if (maxUser >= 1 && user.Length > 0)
            {
                user = TruncateToFit(user, maxUser);
                continue;
            }

            if (user.Length > 0)
            {
                user = string.Empty;
                continue;
            }

            var withoutCategory = Join(typeWord, methodDisplay, null, string.Empty, doc, null);
            var maxCat = maxLength - withoutCategory.Length - Separator.Length;
            if (maxCat >= 1 && category is not null)
            {
                category = TruncateToFit(category, maxCat);
                continue;
            }

            if (category is not null)
            {
                category = null;
                continue;
            }

            var minimal = Join(typeWord, methodDisplay, null, string.Empty, doc, null);
            return minimal.Length <= maxLength ? minimal : minimal[..maxLength];
        }
    }

    /// <summary>
    /// Suffixe une ligne de ventilation (707 « — HT », 436711 « — TVA {taux}% ») dérivée du libellé
    /// d'entête d'une écriture caisse à 3 lignes, en respectant la limite de
    /// <see cref="MaxJournalLabelLength"/> caractères : c'est la BASE qui est tronquée si nécessaire,
    /// jamais le suffixe (sinon le suffixe — seule information distinguant les lignes — disparaîtrait
    /// en priorité sur les libellés les plus longs).
    /// </summary>
    public static string BuildLineLabel(string baseLabel, string suffix)
    {
        ArgumentNullException.ThrowIfNull(baseLabel);
        ArgumentNullException.ThrowIfNull(suffix);

        var combined = baseLabel + suffix;
        if (combined.Length <= MaxJournalLabelLength)
            return combined;

        var maxBaseLength = MaxJournalLabelLength - suffix.Length;
        if (maxBaseLength <= 0)
            return suffix[..MaxJournalLabelLength];

        return baseLabel[..maxBaseLength] + suffix;
    }

    private static string TruncateToFit(string value, int maxLength)
    {
        const string ellipsis = "…";
        if (value.Length <= maxLength)
            return value;

        if (maxLength <= ellipsis.Length)
            return ellipsis[..maxLength];

        return value[..(maxLength - ellipsis.Length)].TrimEnd() + ellipsis;
    }

    private static string Join(
        string typeWord,
        string methodDisplay,
        string? category,
        string user,
        string doc,
        string? reference)
    {
        var parts = new List<string>(7)
        {
            typeWord,
            methodDisplay
        };

        if (!string.IsNullOrEmpty(category))
            parts.Add(category);

        if (!string.IsNullOrWhiteSpace(user))
            parts.Add(user.Trim());

        parts.Add(doc);

        if (!string.IsNullOrEmpty(reference))
            parts.Add(reference);

        return string.Join(Separator, parts);
    }
}
