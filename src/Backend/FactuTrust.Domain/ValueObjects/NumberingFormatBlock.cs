using System.Text.Json.Serialization;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.ValueObjects;

public sealed class NumberingFormatBlock
{
    [JsonPropertyName("type")]
    public NumberingBlockType Type { get; init; }

    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("order")]
    public int Order { get; init; }

    public NumberingFormatBlock() { }

    public NumberingFormatBlock(NumberingBlockType type, string? value, int order)
    {
        Type = type;
        Value = value;
        Order = order;
    }
}