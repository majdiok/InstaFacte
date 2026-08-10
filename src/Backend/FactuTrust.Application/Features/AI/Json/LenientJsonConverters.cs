using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Application.Common;

namespace FactuTrust.Application.Features.AI.Json;

// ============================================================================
// Convertisseurs tolérants pour les sorties de LLM.
//
// CONTRAT COMMUN, valable pour les quatre :
//   1. Ne JAMAIS lever. Une valeur illisible devient null / vide, jamais une exception —
//      sans quoi un seul champ fantaisiste annule l'extraction entière (le bug de production :
//      « 19.0 » sur un int? faisait perdre les 30 autres champs correctement extraits).
//   2. TOUJOURS consommer intégralement le jeton courant. Un convertisseur qui rend la main sans
//      avoir consommé un StartObject/StartArray décale le lecteur et corrompt tout le reste du
//      document. D'où les reader.Skip() / JsonDocument.ParseValue systématiques.
// ============================================================================

/// <summary>
/// <c>int?</c> tolérant : accepte le décimal (« 19.0 », « 19.000 » — le cas observé en production),
/// la chaîne (« 19 », « 19 % », « 19,0 »), et neutralise tout le reste.
/// </summary>
public sealed class LenientNullableInt32Converter : JsonConverter<int?>
{
    public override bool HandleNull => true;

    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var exact))
                    return exact;
                if (reader.TryGetDecimal(out var dec))
                    return TunisianNumberParsing.ToInt32OrNull(dec);
                if (reader.TryGetDouble(out var dbl)
                    && double.IsFinite(dbl)
                    && dbl >= int.MinValue
                    && dbl <= int.MaxValue)
                {
                    return (int)Math.Round(dbl, MidpointRounding.AwayFromZero);
                }
                return null;

            case JsonTokenType.String:
                return TunisianNumberParsing.ParseInt32Lenient(reader.GetString());

            case JsonTokenType.True:
            case JsonTokenType.False:
                return null;

            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteNumberValue(value.Value);
    }
}

/// <summary>
/// <c>decimal?</c> tolérant : le nombre JSON reste exact (chemin nominal, aucune perte), la chaîne
/// passe par le parseur tunisien (« 1,000 », « 1 234,567 », « 1,250.000 », « 278,000 TND »).
/// Une valeur hors bornes <c>decimal</c> devient <c>null</c> : un montant fantaisiste serait plus
/// nocif qu'un montant absent, que le mapping aval sait déjà traiter.
/// </summary>
public sealed class LenientNullableDecimalConverter : JsonConverter<decimal?>
{
    public override bool HandleNull => true;

    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.Number:
                return reader.TryGetDecimal(out var dec) ? dec : null;

            case JsonTokenType.String:
                return TunisianNumberParsing.ParseDecimalLenient(reader.GetString());

            case JsonTokenType.True:
            case JsonTokenType.False:
                return null;

            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteNumberValue(value.Value);
    }
}

/// <summary>
/// <c>string?</c> tolérant : accepte le nombre (« confidence: 0.95 », observé en production),
/// le booléen, et l'objet/tableau dont il extrait un texte lisible.
/// </summary>
public sealed class LenientStringConverter : JsonConverter<string?>
{
    /// <summary>Borne de sécurité : un LLM en boucle peut produire des chaînes très longues.</summary>
    private const int MaxLength = 2_000;

    /// <summary>Clés portant habituellement le texte utile d'un objet renvoyé à la place d'une chaîne.</summary>
    private static readonly string[] HumanKeys =
        ["message", "text", "warning", "libelle", "label", "description", "value"];

    public override bool HandleNull => true;

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ReadLenient(ref reader);

    /// <summary>Partagé avec <see cref="LenientStringListConverter"/> pour lire un élément.</summary>
    internal static string? ReadLenient(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.String:
                return Clean(reader.GetString());

            case JsonTokenType.Number:
                // Littéral brut : « 19.000 » reste « 19.000 ». Passer par GetDouble().ToString()
                // dépendrait de la culture et perdrait les zéros significatifs.
                // BuffersExtensions.ToArray est qualifié : sans cela le compilateur le confond
                // avec ImmutableArrayExtensions.ToArray et échoue à inférer le type.
                return Clean(reader.HasValueSequence
                    ? Encoding.UTF8.GetString(BuffersExtensions.ToArray(reader.ValueSequence))
                    : Encoding.UTF8.GetString(reader.ValueSpan));

            case JsonTokenType.True:
                return "true";

            case JsonTokenType.False:
                return "false";

            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
            {
                // ParseValue consomme le sous-arbre et laisse le lecteur sur EndObject/EndArray.
                using var doc = JsonDocument.ParseValue(ref reader);
                return Clean(Humanize(doc.RootElement));
            }

            default:
                reader.Skip();
                return null;
        }
    }

    private static string? Humanize(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in HumanKeys)
            {
                if (element.TryGetProperty(key, out var property)
                    && property.ValueKind == JsonValueKind.String)
                {
                    return property.GetString();
                }
            }
        }
        return element.GetRawText();
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length <= MaxLength ? trimmed : trimmed[..MaxLength];
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);

    // Sans ces deux surcharges, un Dictionary<string, …> ajouté plus tard à un DTO lèverait
    // NotSupportedException : System.Text.Json exige un convertisseur de nom de propriété.
    public override string ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString()!;

    public override void WriteAsPropertyName(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value);
}

/// <summary>
/// <c>List&lt;string&gt;?</c> tolérant : accepte un tableau d'éléments hétérogènes (le cas
/// « warnings: [{ message: … }] » observé en production), une chaîne nue à la place d'un tableau,
/// ou un objet unique. Les entrées vides sont écartées silencieusement.
/// </summary>
public sealed class LenientStringListConverter : JsonConverter<List<string>?>
{
    private const int MaxItems = 50;

    public override bool HandleNull => true;

    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        var items = new List<string>();

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                var item = LenientStringConverter.ReadLenient(ref reader);
                if (!string.IsNullOrWhiteSpace(item) && items.Count < MaxItems)
                    items.Add(item);
            }
            return items;
        }

        // Le modèle a oublié le tableau : on enveloppe la valeur unique.
        var single = LenientStringConverter.ReadLenient(ref reader);
        if (!string.IsNullOrWhiteSpace(single))
            items.Add(single);
        return items;
    }

    public override void Write(Utf8JsonWriter writer, List<string>? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (var item in value)
            writer.WriteStringValue(item);
        writer.WriteEndArray();
    }
}
