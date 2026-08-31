using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Phase 2 — sector classification propagation on <see cref="UserDto"/> (plan §WP-B8, D8).
/// The serialization facts run without SQL (pure unit); the <c>Me</c> endpoint fact is SQL-gated
/// (mirrors <c>RegisterSectorConfigurationSqlTests</c>).
/// </summary>
public sealed class UserDtoSectorFieldsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void UserDto_serializes_null_sector_fields_for_legacy_tenants()
    {
        // A legacy/firm tenant has no sector classification — both fields are null. The new fields
        // must be PRESENT in the JSON (as null), not omitted, so the additive contract is stable and
        // a frontend reading them gets an explicit null rather than a missing key.
        var dto = new UserDto
        {
            Id = Guid.NewGuid(),
            Email = "legacy@example.com",
            FirstName = "Legacy",
            LastName = "Tenant",
            Role = UserRole.Accountant,
            RoleDisplay = "Comptable",
            TenantId = Guid.NewGuid(),
            CompanyName = "Société Legacy"
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("companySegment", out var seg), "companySegment must be present in the JSON even when null.");
        Assert.True(seg.ValueKind == JsonValueKind.Null, "companySegment must serialize as null for a legacy tenant.");

        Assert.True(doc.RootElement.TryGetProperty("businessDomain", out var dom), "businessDomain must be present in the JSON even when null.");
        Assert.True(dom.ValueKind == JsonValueKind.Null, "businessDomain must serialize as null for a legacy tenant.");
    }

    [Fact]
    public void UserDto_serializes_populated_sector_fields()
    {
        var dto = new UserDto
        {
            Id = Guid.NewGuid(),
            Email = "sector@example.com",
            FirstName = "Sector",
            LastName = "Tenant",
            Role = UserRole.Accountant,
            RoleDisplay = "Comptable",
            TenantId = Guid.NewGuid(),
            CompanyName = "Société Sector",
            CompanySegment = "commerce",
            BusinessDomain = "artisanat"
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("commerce", doc.RootElement.GetProperty("companySegment").GetString());
        Assert.Equal("artisanat", doc.RootElement.GetProperty("businessDomain").GetString());
    }
}

/// <summary>
/// SQL-gated integration fact for the <c>GET /api/auth/me</c> endpoint (plan §WP-B8): after a
/// sector-aware registration, <c>/me</c> must return the tenant's persisted
/// <c>companySegment</c>/<c>businessDomain</c> on the <see cref="UserDto"/>. Requires SQL Server —
/// set <c>RUN_SECTOR_REGISTRATION_SQL_TESTS=1</c> to execute.
/// </summary>
[Collection("SqlServerIntegration")]
public sealed class UserDtoMeEndpointSectorFieldsTests : IClassFixture<ChannelsDisabledWebApplicationFactory>
{
    private static bool ShouldRun => Environment.GetEnvironmentVariable("RUN_SECTOR_REGISTRATION_SQL_TESTS") == "1";

    private readonly ChannelsDisabledWebApplicationFactory _factory;

    public UserDtoMeEndpointSectorFieldsTests(ChannelsDisabledWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Me_endpoint_returns_sector_classification()
    {
        if (!ShouldRun) return;

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var unique = Guid.NewGuid().ToString("N")[..12];
        var nifDigits = (Convert.ToUInt64(unique, 16) % 10_000_000_000UL).ToString("D10");
        var dto = new RegisterDto
        {
            Email = $"me-sector-{unique}@example.com",
            Password = "SecurePass123!",
            ConfirmPassword = "SecurePass123!",
            FirstName = "Me",
            LastName = "Sector",
            CompanyName = $"Société Me Sector {unique}",
            Nif = $"{nifDigits[..7]}/A/B/C/{nifDigits[7..]}",
            TaxRegime = TaxRegime.RealRegime,
            Street = "1 rue Test",
            City = "Tunis",
            PostalCode = "1000",
            Governorate = "Tunis",
            CompanyEmail = $"contact-me-sector-{unique}@example.com",
            Phone = "71123456",
            CompanySegment = CompanySegments.Commerce,
            BusinessDomain = BusinessDomains.Autre
        };

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", dto, TenantUsersTestSupport.ApiJsonOptions);
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(TenantUsersTestSupport.ApiJsonOptions);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.NotNull(registerBody?.Data);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registerBody!.Data!.AccessToken);

        var meResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var meBody = await meResponse.Content.ReadFromJsonAsync<ApiResponse<UserDto>>(TenantUsersTestSupport.ApiJsonOptions);
        Assert.NotNull(meBody?.Data);
        Assert.Equal(CompanySegments.Commerce, meBody!.Data!.CompanySegment);
        Assert.Equal(BusinessDomains.Autre, meBody.Data.BusinessDomain);
    }
}
