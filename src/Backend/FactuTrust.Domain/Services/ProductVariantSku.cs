using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Services;

public static class ProductVariantSku
{
    public const int MaxCodeLength = 50;

    public static Result<string> Build(string parentCode, IReadOnlyList<string> valueCodes, int? collisionSuffix = null)
    {
        if (string.IsNullOrWhiteSpace(parentCode))
            return Result.Failure<string>(Error.Validation("Code", "Le code parent est obligatoire"));

        var parts = new List<string> { parentCode.Trim().ToUpperInvariant() };
        parts.AddRange(valueCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant()));

        if (collisionSuffix is > 0)
            parts.Add(collisionSuffix.Value.ToString("D2"));

        var code = string.Join("-", parts);
        if (code.Length <= MaxCodeLength)
            return Result.Success(code);

        // Truncate from the left of the suffix chain while keeping parent prefix.
        var parent = parts[0];
        var rest = string.Join("-", parts.Skip(1));
        var budget = MaxCodeLength - parent.Length - 1;
        if (budget < 4)
        {
            var truncatedParent = parent.Length > MaxCodeLength - 4
                ? parent[..(MaxCodeLength - 4)]
                : parent;
            code = $"{truncatedParent}-{Guid.NewGuid().ToString("N")[..3]}".ToUpperInvariant();
            return Result.Success(code[..Math.Min(code.Length, MaxCodeLength)]);
        }

        rest = rest.Length <= budget ? rest : rest[..budget];
        return Result.Success($"{parent}-{rest}");
    }
}
