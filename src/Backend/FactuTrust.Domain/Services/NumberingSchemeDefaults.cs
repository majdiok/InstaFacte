using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Services;

public static class NumberingSchemeDefaults
{
    public static IReadOnlyList<NumberingFormatBlock> GetDefaultBlocks(NumberingDocumentType documentType)
    {
        var prefix = documentType.DefaultFreeText();

        if (documentType == NumberingDocumentType.PhysicalInventory)
        {
            return new List<NumberingFormatBlock>
            {
                new(NumberingBlockType.FreeText, prefix, 0),
                new(NumberingBlockType.Separator, "-", 1),
                new(NumberingBlockType.DocumentNumberPadded6, null, 2)
            };
        }

        return new List<NumberingFormatBlock>
        {
            new(NumberingBlockType.FreeText, prefix, 0),
            new(NumberingBlockType.Separator, "-", 1),
            new(NumberingBlockType.Year4, null, 2),
            new(NumberingBlockType.Separator, "-", 3),
            new(NumberingBlockType.DocumentNumberPadded6, null, 4)
        };
    }

    // Case-insensitive so a format persisted with PascalCase keys ("Type"/"Value"/"Order") — as written
    // by the BootstrapDocumentNumberingSchemesFromLegacy migration — deserializes correctly against the
    // camelCase [JsonPropertyName] of NumberingFormatBlock. Without this, every block would silently fall
    // back to its default (Type = FreeText), dropping the document-number block and breaking numbering.
    private static readonly System.Text.Json.JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string SerializeBlocks(IReadOnlyList<NumberingFormatBlock> blocks) =>
        System.Text.Json.JsonSerializer.Serialize(blocks);

    public static IReadOnlyList<NumberingFormatBlock> DeserializeBlocks(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<NumberingFormatBlock>();

        var blocks = System.Text.Json.JsonSerializer.Deserialize<List<NumberingFormatBlock>>(json, DeserializeOptions);
        return blocks ?? new List<NumberingFormatBlock>();
    }
}
