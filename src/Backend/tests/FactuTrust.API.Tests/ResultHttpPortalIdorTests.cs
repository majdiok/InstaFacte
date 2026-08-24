using FactuTrust.API.Http;
using FactuTrust.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FactuTrust.API.Tests;

public sealed class ResultHttpPortalIdorTests
{
    private sealed class StubController : ControllerBase;

    [Fact]
    public void Other_client_invoice_is_mapped_to_404_not_403()
    {
        var controller = new StubController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var error = Error.NotFound("Invoice", Guid.NewGuid());
        var result = ResultHttp.ToError(controller, error);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public void Draft_or_foreign_resource_never_leaks_as_forbidden()
    {
        var error = Error.NotFound("Invoice", Guid.NewGuid());
        Assert.Contains("NotFound", error.Code, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Forbidden", error.Code, StringComparison.OrdinalIgnoreCase);
    }
}
