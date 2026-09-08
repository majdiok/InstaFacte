using FactuTrust.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Studio;

/// <summary>
/// Mappage Result → IActionResult pour les NOUVEAUX endpoints Studio (workbench P0 et suivants).
/// Les actions historiques gardent leur comportement d'origine (404/400) et n'utilisent pas ce
/// helper. Codes reconnus : <c>Conflict</c> ⇒ 409, <c>Unauthorized</c> ⇒ 401, <c>Forbidden</c>
/// ⇒ 403, <c>*.NotFound</c> ⇒ 404, tout le reste (dont <c>Validation.*</c>) ⇒ 400.
/// </summary>
internal static class StudioErrorMapping
{
    public static IActionResult ToActionResult<T>(
        ControllerBase controller, Result<T> result, Func<T, IActionResult> ok) =>
        result.IsSuccess ? ok(result.Value) : Map(controller, result.Error);

    public static IActionResult Map(ControllerBase controller, Error error) => error.Code switch
    {
        "Conflict" => controller.Conflict(ApiResponse<string>.Fail(error.Description)),
        "Unauthorized" => controller.StatusCode(
            StatusCodes.Status401Unauthorized, ApiResponse<string>.Fail(error.Description)),
        "Forbidden" => controller.StatusCode(
            StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(error.Description)),
        var code when code.EndsWith(".NotFound", StringComparison.Ordinal)
            || string.Equals(code, "NotFound", StringComparison.Ordinal) =>
            controller.NotFound(ApiResponse<string>.Fail(error.Description)),
        _ => controller.BadRequest(ApiResponse<string>.Fail(error.Description)),
    };
}
