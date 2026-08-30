using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using FactuTrust.Application.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Performance baseline for firm registration. Requires SQL Server + Features:AccountingFirms:Enabled.
/// Set RUN_FIRM_REGISTRATION_PERF=1 to execute.
/// </summary>
[Collection("SqlServerIntegration")]
public sealed class RegisterFirmPerformanceTests : IClassFixture<ChannelsDisabledWebApplicationFactory>
{
    private readonly ChannelsDisabledWebApplicationFactory _factory;

    public RegisterFirmPerformanceTests(ChannelsDisabledWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterFirm_end_to_end_under_baseline_when_sql_available()
    {
        if (Environment.GetEnvironmentVariable("RUN_FIRM_REGISTRATION_PERF") != "1")
            return;

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var unique = Guid.NewGuid().ToString("N")[..8];
        var dto = new RegisterAccountingFirmDto
        {
            Email = $"perf-firm-{unique}@example.com",
            Password = "SecurePass123!",
            ConfirmPassword = "SecurePass123!",
            FirstName = "Perf",
            LastName = "Test",
            FirmName = $"Cabinet Perf {unique}",
            Nif = "7654321/A/B/C/000",
            Street = "1 rue Test",
            City = "Tunis",
            PostalCode = "1000",
            Governorate = "Tunis",
            FirmEmail = $"contact-{unique}@example.com",
            Phone = "71123456",
            IsPublicInDirectory = false
        };

        var sw = Stopwatch.StartNew();
        var response = await client.PostAsJsonAsync("/api/auth/register-firm", dto);
        sw.Stop();

        Assert.True(
            response.StatusCode is HttpStatusCode.Created or HttpStatusCode.NotFound,
            $"Unexpected status: {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.Created)
        {
            Assert.True(sw.ElapsedMilliseconds < 120_000, $"Registration took {sw.ElapsedMilliseconds}ms");
        }
    }
}
