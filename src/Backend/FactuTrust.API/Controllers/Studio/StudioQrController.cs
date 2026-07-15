using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Studio;

/// <summary>
/// Renders a text value as a QR-code PNG for Studio "QrCode" display fields. Read-only; available
/// to any user who can read custom records.
/// </summary>
[ApiController]
[Route("api/studio/qr")]
[Authorize(Policy = PermissionPolicies.CustomRecordsRead)]
public sealed class StudioQrController : ControllerBase
{
    private readonly IStudioQrGenerator _qr;

    public StudioQrController(IStudioQrGenerator qr) => _qr = qr;

    [HttpGet]
    public IActionResult Get([FromQuery] string? text)
    {
        var png = _qr.GeneratePng(text);
        return png is null ? NotFound() : File(png, "image/png");
    }
}
