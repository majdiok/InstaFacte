using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Phase 3 — plan §3.5: (a) deferred registration fields (site web, ligne d'adresse 2, entrepôt
/// personnalisé) must remain optional while the legal minimum (email, password, companyName, NIF,
/// taxRegime, governorate) stays required — this was already true of <see cref="RegisterDto"/> and
/// <c>Address.Create</c> before this phase; the tests below pin that behavior down explicitly.
/// (b) <see cref="UserDto.TenantCreatedAtUtc"/> exposes the tenant creation date so the frontend can
/// gate onboarding nudges by tenant age.
/// </summary>
public sealed class Phase3RegisterOptionalFieldsAndTenantCreatedAtTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void UserDto_serializes_tenant_created_at_utc()
    {
        var createdAt = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var dto = new UserDto
        {
            Id = Guid.NewGuid(),
            Email = "created-at@example.com",
            FirstName = "Created",
            LastName = "AtTenant",
            Role = UserRole.Accountant,
            RoleDisplay = "Comptable",
            TenantId = Guid.NewGuid(),
            CompanyName = "Société Age",
            TenantCreatedAtUtc = createdAt
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("tenantCreatedAtUtc", out var value), "tenantCreatedAtUtc must be present in the JSON contract.");
        Assert.Equal(createdAt, value.GetDateTime());
    }
}

/// <summary>
/// SQL-gated integration fact: registration succeeds with only the legal minimum fields (no
/// website, no second address line, no custom warehouse name, no sector fields) and the response
/// exposes a fresh <c>tenantCreatedAtUtc</c>. Requires SQL Server — set
/// <c>RUN_SECTOR_REGISTRATION_SQL_TESTS=1</c> to execute (mirrors
/// <c>UserDtoMeEndpointSectorFieldsTests</c>).
/// </summary>
[Collection("SqlServerIntegration")]
public sealed class RegisterWithDeferredFieldsOmittedSqlTests : IClassFixture<ChannelsDisabledWebApplicationFactory>
{
    private static bool ShouldRun => Environment.GetEnvironmentVariable("RUN_SECTOR_REGISTRATION_SQL_TESTS") == "1";

    private readonly ChannelsDisabledWebApplicationFactory _factory;

    public RegisterWithDeferredFieldsOmittedSqlTests(ChannelsDisabledWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_succeeds_without_website_streetLine2_or_warehouseName()
    {
        if (!ShouldRun) return;

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var unique = Guid.NewGuid().ToString("N")[..12];
        var nifDigits = (Convert.ToUInt64(unique, 16) % 10_000_000_000UL).ToString("D10");
        var beforeRegister = DateTime.UtcNow;

        // Legal minimum only: email, password, companyName, NIF, taxRegime, governorate (+ the
        // other always-required identity/contact fields). Website, StreetLine2 and WarehouseName
        // are intentionally omitted (null) to prove they are deferrable.
        var dto = new RegisterDto
        {
            Email = $"minimal-{unique}@example.com",
            Password = "SecurePass123!",
            ConfirmPassword = "SecurePass123!",
            FirstName = "Minimal",
            LastName = "Register",
            CompanyName = $"Société Minimale {unique}",
            Nif = $"{nifDigits[..7]}/A/B/C/{nifDigits[7..]}",
            TaxRegime = TaxRegime.RealRegime,
            Street = "1 rue Minimale",
            City = "Tunis",
            Governorate = "Tunis",
            CompanyEmail = $"contact-minimal-{unique}@example.com",
            Phone = "71123456"
            // StreetLine2, PostalCode, Website, WarehouseName, CompanySegment, BusinessDomain: all null.
        };

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", dto, TenantUsersTestSupport.ApiJsonOptions);
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(TenantUsersTestSupport.ApiJsonOptions);

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.NotNull(registerBody?.Data);

        var afterRegister = DateTime.UtcNow;
        var tenantCreatedAt = registerBody!.Data!.User.TenantCreatedAtUtc;
        Assert.InRange(tenantCreatedAt, beforeRegister.AddSeconds(-5), afterRegister.AddSeconds(5));
    }
}
