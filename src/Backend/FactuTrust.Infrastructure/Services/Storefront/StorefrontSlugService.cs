using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;

namespace FactuTrust.Infrastructure.Services.Storefront;

public sealed partial class StorefrontSlugService : IStorefrontSlugService
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "api", "admin", "auth", "settings", "swagger", "health", "platform", "public", "street",
        "storefront", "storefronts", "visite-virtuelle", "login", "register", "legal", "pos"
    };

    private readonly IStorefrontProfileRepository _profiles;

    public StorefrontSlugService(IStorefrontProfileRepository profiles)
    {
        _profiles = profiles;
    }

    [GeneratedRegex("[^a-z0-9\\-]+", RegexOptions.Compiled)]
    private static partial Regex NonSlugChars();

    public async Task<string> GenerateUniqueAsync(
        string proposed,
        Guid? excludingStorefrontId = null,
        CancellationToken cancellationToken = default)
    {
        var baseSlug = Normalize(proposed);
        if (string.IsNullOrEmpty(baseSlug))
            baseSlug = "magasin";

        var candidate = baseSlug;
        for (var i = 0; i < 200; i++)
        {
            if (!await _profiles.SlugExistsAsync(candidate, excludingStorefrontId, cancellationToken))
                return candidate;
            candidate = i == 0 ? $"{baseSlug}-2" : $"{baseSlug}-{i + 2}";
        }

        return $"{baseSlug}-{Guid.NewGuid():N}"[..60];
    }

    public bool IsValid(string slug, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(slug))
        {
            errorMessage = "Le slug est obligatoire.";
            return false;
        }

        var s = slug.Trim().ToLowerInvariant();
        if (s.Length is < 3 or > 60)
        {
            errorMessage = "Le slug doit contenir entre 3 et 60 caractères.";
            return false;
        }

        if (!SlugSyntax().IsMatch(s))
        {
            errorMessage = "Le slug ne peut contenir que des lettres minuscules, chiffres et tirets.";
            return false;
        }

        if (Reserved.Contains(s))
        {
            errorMessage = "Ce slug est réservé.";
            return false;
        }

        return true;
    }

    [GeneratedRegex("^[a-z0-9]([a-z0-9-]{1,58}[a-z0-9])?$", RegexOptions.Compiled)]
    private static partial Regex SlugSyntax();

    private static string Normalize(string proposed)
    {
        var s = proposed.Trim().ToLowerInvariant();
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(c);
        }

        var ascii = sb.ToString().Normalize(NormalizationForm.FormC);
        ascii = ascii.Replace(' ', '-');
        ascii = NonSlugChars().Replace(ascii, "-");
        ascii = Regex.Replace(ascii, "-{2,}", "-").Trim('-');
        return ascii;
    }
}
