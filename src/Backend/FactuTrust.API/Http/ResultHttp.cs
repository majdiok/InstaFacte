using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Http;

internal static class ResultHttp
{
    public static IActionResult ToActionResult(this ControllerBase controller, Result result, string? successMessage = null)
    {
        if (!result.IsFailure)
            return controller.Ok(ApiResponse<object>.Ok(null!, successMessage ?? "OK"));

        return ToError(controller, result.Error);
    }

    public static IActionResult ToActionResult<T>(this ControllerBase controller, Result<T> result, string? successMessage = null)
    {
        if (!result.IsFailure)
            return controller.Ok(ApiResponse<T>.Ok(result.Value, successMessage));

        return ToError(controller, result.Error);
    }

    public static IActionResult ToError(ControllerBase controller, Error error)
    {
        var code = error.Code ?? string.Empty;
        var body = ApiResponse<object>.Fail(error.Description, code);

        if (code.Contains("NotFound", StringComparison.OrdinalIgnoreCase))
            return controller.NotFound(body);

        if (string.Equals(code, "Forbidden", StringComparison.OrdinalIgnoreCase))
            return controller.StatusCode(StatusCodes.Status403Forbidden, body);

        if (string.Equals(code, "Unauthorized", StringComparison.OrdinalIgnoreCase))
            return controller.Unauthorized(body);

        if (string.Equals(code, "Conflict", StringComparison.OrdinalIgnoreCase))
            return controller.Conflict(body);

        return controller.BadRequest(body);
    }
}
