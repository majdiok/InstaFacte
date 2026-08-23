using System.Security.Claims;
using FactuTrust.API.Middleware;
using FactuTrust.Domain.Auth;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace FactuTrust.API.Tests;

public sealed class DelegatedAccessMiddlewareTests
{
    private static async Task<(int StatusCode, bool NextCalled)> InvokeAsync(
        string method,
        string path,
        string? accessMode,
        bool isAuthenticated = true)
    {
        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };
        var middleware = new DelegatedAccessMiddleware(next);

        var context = new DefaultHttpContext
        {
            Request = { Method = method, Path = path }
        };

        if (isAuthenticated)
        {
            var identity = new ClaimsIdentity("Bearer");
            if (accessMode is not null)
            {
                identity.AddClaim(new Claim(AuthClaimTypes.AccessMode, accessMode));
            }
            context.User = new ClaimsPrincipal(identity);
        }

        await middleware.InvokeAsync(context);
        return (context.Response.StatusCode, nextCalled);
    }

    [Theory]
    [InlineData("POST", "/api/firm/context/clear")]
    [InlineData("POST", "/api/firm/context/switch")]
    [InlineData("GET", "/api/firm/context")]
    [InlineData("POST", "/api/auth/refresh")]
    [InlineData("POST", "/api/auth/logout")]
    [InlineData("POST", "/api/notifications/a94fc082-160a-42f4-a854-0c95b881914d/read")]
    [InlineData("POST", "/api/notifications/read-all")]
    [InlineData("GET", "/api/notifications")]
    [InlineData("PATCH", "/api/me/onboarding")]
    [InlineData("GET", "/api/me/onboarding")]
    public async Task Delegated_mode_allows_firm_context_and_auth_session_writes(string method, string path)
    {
        var (statusCode, nextCalled) = await InvokeAsync(method, path, "delegated");

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
    }

    [Theory]
    [InlineData("POST", "/api/ai/chat")]
    [InlineData("POST", "/api/ai/warm-up")]
    [InlineData("POST", "/api/ai/document-extract")]
    [InlineData("DELETE", "/api/ai/conversations/a94fc082-160a-42f4-a854-0c95b881914d")]
    public async Task Delegated_mode_allows_ai_writes(string method, string path)
    {
        var (statusCode, nextCalled) = await InvokeAsync(method, path, "delegated");

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
    }

    [Fact]
    public async Task Native_mode_allows_ai_chat()
    {
        var (statusCode, nextCalled) = await InvokeAsync("POST", "/api/ai/chat", "native");

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
    }

    [Theory]
    [InlineData("POST", "/api/accounting/journal-entries")]
    [InlineData("GET", "/api/accounting/chart")]
    public async Task Delegated_mode_allows_accounting_routes(string method, string path)
    {
        var (statusCode, nextCalled) = await InvokeAsync(method, path, "delegated");

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
    }

    [Theory]
    [InlineData("POST", "/api/firm/users")]
    [InlineData("POST", "/api/invoices")]
    [InlineData("POST", "/api/clients")]
    [InlineData("POST", "/api/suppliers")]
    [InlineData("PUT", "/api/suppliers/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/exchanges/62b05d91-67a3-4069-817b-da87005c30e8/messages")]
    public async Task Delegated_mode_blocks_non_accounting_writes(string method, string path)
    {
        var (statusCode, nextCalled) = await InvokeAsync(method, path, "delegated");

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
    }

    [Theory]
    [InlineData("POST", "/api/cash-desk/operations")]
    [InlineData("POST", "/api/bankaccount")]
    [InlineData("POST", "/api/invoices/00000000-0000-0000-0000-000000000001/record-payment")]
    public async Task Delegated_mode_blocks_treasury_and_payment_writes(string method, string path)
    {
        var (statusCode, nextCalled) = await InvokeAsync(method, path, "delegated");

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
    }

    [Theory]
    [InlineData("GET", "/api/cash-desk/balances")]
    [InlineData("GET", "/api/bankaccount")]
    public async Task Delegated_mode_allows_treasury_reads(string method, string path)
    {
        var (statusCode, nextCalled) = await InvokeAsync(method, path, "delegated");

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
    }

    [Fact]
    public async Task Native_mode_allows_firm_context_clear()
    {
        var (statusCode, nextCalled) = await InvokeAsync("POST", "/api/firm/context/clear", "native");

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
    }

    [Fact]
    public async Task Unauthenticated_requests_pass_through()
    {
        var (statusCode, nextCalled) = await InvokeAsync("POST", "/api/invoices", accessMode: null, isAuthenticated: false);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
    }

    [Theory]
    [InlineData("GET", "/api/quotes")]
    [InlineData("GET", "/api/suppliers")]
    [InlineData("GET", "/api/clients")]
    [InlineData("GET", "/api/purchaseorders")]
    public async Task Delegated_mode_allows_operational_reads(string method, string path)
    {
        var (statusCode, nextCalled) = await InvokeAsync(method, path, "delegated");

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
    }

    [Theory]
    [InlineData("GET", "/api/invoices")]
    [InlineData("GET", "/api/supplierinvoices")]
    public async Task Delegated_mode_allows_invoice_reads(string method, string path)
    {
        var (statusCode, nextCalled) = await InvokeAsync(method, path, "delegated");

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
    }
}
