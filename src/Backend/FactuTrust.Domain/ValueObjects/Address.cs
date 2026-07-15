using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.ValueObjects;

/// <summary>
/// Represents a Tunisian address.
/// </summary>
public sealed class Address : ValueObject
{
    public string Street { get; private set; }
    public string? StreetLine2 { get; private set; }
    public string City { get; private set; }
    public string? PostalCode { get; private set; }
    public string Governorate { get; private set; }
    public string Country { get; private set; }

    // Required for EF Core
    private Address()
    {
        Street = string.Empty;
        City = string.Empty;
        Governorate = string.Empty;
        Country = string.Empty;
    }

    private Address(
        string street, 
        string? streetLine2, 
        string city, 
        string? postalCode, 
        string governorate,
        string country)
    {
        Street = street;
        StreetLine2 = streetLine2;
        City = city;
        PostalCode = postalCode;
        Governorate = governorate;
        Country = country;
    }

    public static Result<Address> Create(
        string street,
        string city,
        string governorate,
        string? streetLine2 = null,
        string? postalCode = null,
        string country = "Tunisie")
    {
        var errors = new List<Error>();

        if (string.IsNullOrWhiteSpace(street))
            errors.Add(Error.Validation("Address.Street", "L'adresse est obligatoire"));

        if (string.IsNullOrWhiteSpace(city))
            errors.Add(Error.Validation("Address.City", "La ville est obligatoire"));

        if (string.IsNullOrWhiteSpace(governorate))
            errors.Add(Error.Validation("Address.Governorate", "Le gouvernorat est obligatoire"));

        if (errors.Count > 0)
            return Result.Failure<Address>(errors.First());

        return Result.Success(new Address(
            street.Trim(),
            streetLine2?.Trim(),
            city.Trim(),
            postalCode?.Trim(),
            governorate.Trim(),
            country.Trim()));
    }

    public string ToSingleLine()
    {
        var parts = new List<string> { Street };
        
        if (!string.IsNullOrWhiteSpace(StreetLine2))
            parts.Add(StreetLine2);
        
        parts.Add(City);
        
        if (!string.IsNullOrWhiteSpace(PostalCode))
            parts.Add(PostalCode);
        
        parts.Add(Governorate);
        parts.Add(Country);

        return string.Join(", ", parts);
    }

    public string ToMultiLine()
    {
        var lines = new List<string> { Street };
        
        if (!string.IsNullOrWhiteSpace(StreetLine2))
            lines.Add(StreetLine2);
        
        var cityLine = City;
        if (!string.IsNullOrWhiteSpace(PostalCode))
            cityLine = $"{PostalCode} {City}";
        lines.Add(cityLine);
        
        lines.Add(Governorate);
        lines.Add(Country);

        return string.Join(Environment.NewLine, lines);
    }

    public override string ToString() => ToSingleLine();

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Street;
        yield return StreetLine2 ?? string.Empty;
        yield return City;
        yield return PostalCode ?? string.Empty;
        yield return Governorate;
        yield return Country;
    }
}

/// <summary>
/// Tunisian governorates (Gouvernorats).
/// </summary>
public static class TunisianGovernorates
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "Ariana", "Béja", "Ben Arous", "Bizerte", "Gabès", "Gafsa",
        "Jendouba", "Kairouan", "Kasserine", "Kébili", "Le Kef", "Mahdia",
        "La Manouba", "Médenine", "Monastir", "Nabeul", "Sfax", "Sidi Bouzid",
        "Siliana", "Sousse", "Tataouine", "Tozeur", "Tunis", "Zaghouan"
    };

    public static bool IsValid(string governorate)
    {
        return All.Contains(governorate, StringComparer.OrdinalIgnoreCase);
    }
}
