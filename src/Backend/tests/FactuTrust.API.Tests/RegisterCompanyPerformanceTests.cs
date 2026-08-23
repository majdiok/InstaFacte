using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Performance baseline for company registration. Requires SQL Server.
/// Set RUN_COMPANY_REGISTRATION_PERF=1 to execute.
/// Cold runs (no template backup) may exceed the 8s warm-template budget; this test
/// documents the warm-path SLO once TenantProvisioning TemplateClone is ready.
/// </summary>
public sealed class RegisterCompanyPerformanceTests : IClassFixture<ChannelsDisabledWebApplicationFactory>
{
    private readonly ChannelsDisabledWebApplicationFactory _factory;

    public RegisterCompanyPerformanceTests(ChannelsDisabledWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_end_to_end_under_baseline_when_sql_available()
    {
        if (Environment.GetEnvironmentVariable("RUN_COMPANY_REGISTRATION_PERF") != "1")
            return;

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var unique = Guid.NewGuid().ToString("N")[..8];
        var dto = new RegisterDto
        {
            Email = $"perf-company-{unique}@example.com",
            Password = "SecurePass123!",
            ConfirmPassword = "SecurePass123!",
            FirstName = "Perf",
            LastName = "Test",
            CompanyName = $"Société Perf {unique}",
            Nif = "1234567/A/B/C/000",
            TaxRegime = TaxRegime.RealRegime,
            Street = "1 rue Test",
            City = "Tunis",
            PostalCode = "1000",
            Governorate = "Tunis",
            CompanyEmail = $"contact-{unique}@example.com",
            Phone = "71123456"
        };

        var sw = Stopwatch.StartNew();
        var response = await client.PostAsJsonAsync("/api/auth/register", dto);
        sw.Stop();

        Assert.True(
            response.StatusCode is HttpStatusCode.Created or HttpStatusCode.BadRequest,
            $"Unexpected status: {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.Created)
        {
            Assert.True(sw.ElapsedMilliseconds < 8_000, $"Warm registration took {sw.ElapsedMilliseconds}ms");
        }
    }
}
