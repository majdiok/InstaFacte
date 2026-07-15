using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.API.Tests;

public sealed class SupplierInvoiceEnumSerializationTests
{
    [Fact]
    public void SupplierInvoiceStatus_ShouldSerializeAsCamelCase()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        var payload = new { Status = SupplierInvoiceStatus.PartiallyPaid };
        var json = JsonSerializer.Serialize(payload, options);

        Assert.Contains("\"status\":\"partiallyPaid\"", json);
    }
}
