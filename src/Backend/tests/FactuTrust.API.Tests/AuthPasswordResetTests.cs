using System.Net;
using System.Net.Http.Json;
using FactuTrust.Application.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FactuTrust.API.Tests;

/// <summary>
/// Password reset endpoint contract tests (anonymous, always generic forgot-password response).
/// </summary>
[Collection("SqlServerIntegration")]
public sealed class AuthPasswordResetTests : IClassFixture<ChannelsDisabledWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthPasswordResetTests(ChannelsDisabledWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Forgot_password_with_empty_email_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordDto { Email = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Forgot_password_with_valid_email_returns_200_generic_message()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordDto { Email = "unknown-user@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Contains("Si un compte existe", body.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reset_password_with_mismatched_confirm_returns_400()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordDto
            {
                Email = "user@example.com",
                Token = "invalid-token",
                NewPassword = "SecurePass123!",
                ConfirmNewPassword = "DifferentPass123!"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reset_password_with_invalid_token_returns_400()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordDto
            {
                Email = "user@example.com",
                Token = "invalid-token",
                NewPassword = "SecurePass123!",
                ConfirmNewPassword = "SecurePass123!"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
