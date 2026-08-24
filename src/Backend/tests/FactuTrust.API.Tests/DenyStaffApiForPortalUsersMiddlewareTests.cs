using System.Text;
using FactuTrust.API.Middleware;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace FactuTrust.API.Tests;

public sealed class DenyStaffApiForPortalUsersMiddlewareTests
{
    [Fact]
    public async Task Portal_user_is_denied_staff_invoice_api()
    {
        var (status, nextCalled, body) = await InvokeAsync("/api/invoices", isAuthenticated: true, isPortal: true);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Contains("Accès refusé", body);
    }

    [Fact]
    public async Task Portal_user_can_call_portal_api()
    {
        var (status, nextCalled, _) = await InvokeAsync("/api/portal/invoices", isAuthenticated: true, isPortal: true);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, status);
    }

    [Fact]
    public async Task Unauthenticated_request_is_not_blocked_here()
    {
        var (_, nextCalled, _) = await InvokeAsync("/api/invoices", isAuthenticated: false, isPortal: false);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Staff_user_keeps_staff_invoice_api()
    {
        var (_, nextCalled, _) = await InvokeAsync("/api/invoices", isAuthenticated: true, isPortal: false);
        Assert.True(nextCalled);
    }

    private static async Task<(int StatusCode, bool NextCalled, string Body)> InvokeAsync(
        string path,
        bool isAuthenticated,
        bool isPortal)
    {
        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var middleware = new DenyStaffApiForPortalUsersMiddleware(next);
        var context = new DefaultHttpContext
        {
            Request = { Path = path, Method = "GET" }
        };
        context.Response.Body = new MemoryStream();

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(isAuthenticated);
        currentUser.SetupGet(x => x.IsClientPortal).Returns(isPortal);
        currentUser.SetupGet(x => x.Role).Returns(isPortal ? UserRole.Client : UserRole.Accountant);

        await middleware.InvokeAsync(context, currentUser.Object);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
        return (context.Response.StatusCode, nextCalled, body);
    }
}
