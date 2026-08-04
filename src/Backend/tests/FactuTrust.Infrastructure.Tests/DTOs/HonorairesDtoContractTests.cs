using FactuTrust.Application.DTOs;
using System.Text.Json;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.DTOs;

public sealed class HonorairesDtoContractTests
{
    [Fact]
    public void HonorairesLineWriteDto_SerializesActivityCode()
    {
        var dto = new HonorairesLineWriteDto
        {
            ActivityCode = "TENUE",
            Designation = "Tenue comptable / saisie",
            Description = "Janvier 2026",
            Quantity = 1,
            UnitPrice = 700m,
            VatRate = 19
        };

        var json = JsonSerializer.Serialize(dto);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("TENUE", doc.RootElement.GetProperty("ActivityCode").GetString());

        var roundTrip = JsonSerializer.Deserialize<HonorairesLineWriteDto>(json);
        Assert.NotNull(roundTrip);
        Assert.Equal("TENUE", roundTrip!.ActivityCode);
        Assert.Equal("Janvier 2026", roundTrip.Description);
    }

    [Fact]
    public void HonorairesLineDto_ActivityCodeOptionalForLegacy()
    {
        var json = """{"Id":"00000000-0000-0000-0000-000000000001","LineNumber":1,"Designation":"Mission","Quantity":1,"UnitPrice":100,"VatRate":19,"DiscountAmount":0,"SubTotal":100,"VatAmount":19,"Total":119}""";
        var dto = JsonSerializer.Deserialize<HonorairesLineDto>(json);
        Assert.NotNull(dto);
        Assert.Null(dto!.ActivityCode);
        Assert.Equal("Mission", dto.Designation);
    }
}
