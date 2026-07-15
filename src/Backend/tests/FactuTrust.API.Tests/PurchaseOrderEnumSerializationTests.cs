using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.API.Tests;

public sealed class PurchaseOrderEnumSerializationTests
{
    [Fact]
    public void PurchaseOrderStatus_ShouldSerializeAsCamelCase()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        var payload = new { Status = PurchaseOrderStatus.Confirmed };
        var json = JsonSerializer.Serialize(payload, options);

        Assert.Contains("\"status\":\"confirmed\"", json);
    }
}
