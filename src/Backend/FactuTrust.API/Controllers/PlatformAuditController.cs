using System.Globalization;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Lot B3 — Audit Log Viewer plateforme.
///
/// Les audit logs sont stockés <b>par tenant</b> (chaque base tenant a sa propre table
/// <c>AuditLogs</c>). Cet endpoint expose donc les actions d'audit <b>d'un tenant ciblé</b> :
/// le frontend doit toujours fournir le <c>tenantId</c>.
///
/// Sécurité : permission <see cref="PlatformPermissions.AuditRead"/> requise. Le controller
/// reconfigure temporairement le <see cref="ITenantContext"/> avec le tenant ciblé pour que
/// le service tenant-scoped <see cref="IAuditLogQueryService"/> puisse résoudre la base.
///
/// Format d'export PDF : QuestPDF avec footer HMAC SHA-256 du contenu pour traçabilité
/// (le checksum est imprimé en bas, permettant de vérifier l'intégrité du document a posteriori).
/// </summary>
[ApiController]
[Route("api/platform/audit")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformAuditController : ControllerBase
{
    private const int MaxExportRows = 5000;

    private readonly IAuditLogQueryService _auditLogs;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantService _tenantService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PlatformAuditController> _logger;

    public PlatformAuditController(
        IAuditLogQueryService auditLogs,
        ITenantContext tenantContext,
        ITenantService tenantService,
        IConfiguration configuration,
        ILogger<PlatformAuditController> logger)
    {
        _auditLogs = auditLogs;
        _tenantContext = tenantContext;
        _tenantService = tenantService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Liste paginée d'entrées audit pour un tenant donné.</summary>
    [HttpGet("tenants/{tenantId:guid}/logs")]
    [Authorize(Policy = "perm:" + PlatformPermissions.AuditRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AuditLogEntryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListLogs(
        Guid tenantId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? action,
        [FromQuery] Guid? userId,
        [FromQuery] string? entityType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareTenantContextAsync(tenantId, cancellationToken);
        if (prepared is { } notFound)
            return notFound;

        try
        {
            var r = await _auditLogs.GetLogsAsync(from, to, action, userId, entityType, page, pageSize, cancellationToken);
            if (r.IsFailure)
                return BadRequest(ApiResponse<object>.Fail(r.Error.Description, r.Error.Code));
            return Ok(ApiResponse<PagedResult<AuditLogEntryDto>>.Ok(r.Value));
        }
        finally
        {
            _tenantContext.Clear();
        }
    }

    /// <summary>Détail d'une entrée audit (avec OldValues/NewValues + chaîne de hashes).</summary>
    [HttpGet("tenants/{tenantId:guid}/logs/{logId:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.AuditRead)]
    [ProducesResponseType(typeof(ApiResponse<AuditLogDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid tenantId,
        Guid logId,
        CancellationToken cancellationToken)
    {
        var prepared = await PrepareTenantContextAsync(tenantId, cancellationToken);
        if (prepared is { } notFound)
            return notFound;

        try
        {
            var r = await _auditLogs.GetByIdAsync(logId, cancellationToken);
            if (r.IsFailure)
                return r.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
                    ? NotFound(ApiResponse<object>.Fail(r.Error.Description, r.Error.Code))
                    : BadRequest(ApiResponse<object>.Fail(r.Error.Description, r.Error.Code));
            return Ok(ApiResponse<AuditLogDetailDto>.Ok(r.Value));
        }
        finally
        {
            _tenantContext.Clear();
        }
    }

    /// <summary>Vérifie l'intégrité de la chaîne SHA-256 sur l'ensemble des audit logs du tenant.</summary>
    [HttpGet("tenants/{tenantId:guid}/integrity-report")]
    [Authorize(Policy = "perm:" + PlatformPermissions.AuditRead)]
    [ProducesResponseType(typeof(ApiResponse<AuditChainVerificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> IntegrityReport(Guid tenantId, CancellationToken cancellationToken)
    {
        var prepared = await PrepareTenantContextAsync(tenantId, cancellationToken);
        if (prepared is { } notFound)
            return notFound;

        try
        {
            var r = await _auditLogs.VerifyChainAsync(cancellationToken);
            if (r.IsFailure)
                return BadRequest(ApiResponse<object>.Fail(r.Error.Description, r.Error.Code));
            return Ok(ApiResponse<AuditChainVerificationDto>.Ok(r.Value));
        }
        finally
        {
            _tenantContext.Clear();
        }
    }

    /// <summary>
    /// Exporte les audit logs au format CSV ou PDF (limité à <see cref="MaxExportRows"/>).
    /// CSV : BOM UTF-8 + séparateur `;` (Excel-friendly).
    /// PDF : QuestPDF A4 portrait + footer HMAC SHA-256 (signature du contenu pour traçabilité).
    /// </summary>
    [HttpGet("tenants/{tenantId:guid}/export")]
    [Authorize(Policy = "perm:" + PlatformPermissions.AuditRead)]
    [Produces(MediaTypeNames.Text.Plain, "text/csv", "application/pdf")]
    public async Task<IActionResult> Export(
        Guid tenantId,
        [FromQuery] string format = "csv",
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? action = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? entityType = null,
        CancellationToken cancellationToken = default)
    {
        format = (format ?? string.Empty).Trim().ToLowerInvariant();
        if (format is not ("csv" or "pdf"))
            return BadRequest(ApiResponse<object>.Fail("Format invalide. Valeurs acceptées : csv, pdf."));

        var prepared = await PrepareTenantContextAsync(tenantId, cancellationToken);
        if (prepared is { } notFound)
            return notFound;

        try
        {
            // Charge jusqu'à MaxExportRows lignes correspondant aux filtres
            var r = await _auditLogs.GetLogsAsync(from, to, action, userId, entityType, page: 1, pageSize: MaxExportRows, cancellationToken);
            if (r.IsFailure)
                return BadRequest(ApiResponse<object>.Fail(r.Error.Description, r.Error.Code));

            var rows = r.Value.Items;
            var nowUtc = DateTime.UtcNow;
            var fileBaseName = $"audit-log-{tenantId:N}-{nowUtc:yyyyMMdd-HHmm}";

            _logger.LogInformation(
                "Platform admin {ActorId} exported {Count} audit log entries for tenant {TenantId} as {Format}",
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, rows.Count, tenantId, format);

            if (format == "csv")
            {
                var bytes = BuildCsv(rows);
                return File(bytes, "text/csv; charset=utf-8", $"{fileBaseName}.csv");
            }
            else
            {
                var bytes = BuildPdf(rows, tenantId, nowUtc, BuildExportHmac(rows));
                return File(bytes, MediaTypeNames.Application.Pdf, $"{fileBaseName}.pdf");
            }
        }
        finally
        {
            _tenantContext.Clear();
        }
    }

    // ============================================================================
    //  Helpers privés
    // ============================================================================

    /// <summary>Configure le TenantContext pour que IAuditLogQueryService puisse résoudre la BD du tenant.</summary>
    private async Task<IActionResult?> PrepareTenantContextAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var connStr = await _tenantService.GetConnectionStringAsync(tenantId, cancellationToken);
        if (string.IsNullOrEmpty(connStr))
            return NotFound(ApiResponse<object>.Fail($"Tenant {tenantId} introuvable ou sans base de données."));

        _tenantContext.SetTenant(tenantId, connStr);
        return null;
    }

    /// <summary>Compose un CSV BOM UTF-8 avec séparateur `;` (Excel-FR friendly).</summary>
    private static byte[] BuildCsv(IReadOnlyList<AuditLogEntryDto> rows)
    {
        var sb = new StringBuilder();
        // En-tête
        sb.AppendLine("Date;Heure;Utilisateur;Email;Action;Type d'entité;ID entité;Libellé");
        foreach (var r in rows)
        {
            var date = r.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var time = r.CreatedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            sb.Append(date).Append(';');
            sb.Append(time).Append(';');
            sb.Append(EscapeCsv(r.UserId?.ToString() ?? "")).Append(';');
            sb.Append(EscapeCsv(r.UserEmail)).Append(';');
            sb.Append(EscapeCsv(r.Action)).Append(';');
            sb.Append(EscapeCsv(r.EntityType)).Append(';');
            sb.Append(EscapeCsv(r.EntityId?.ToString() ?? "")).Append(';');
            sb.AppendLine(EscapeCsv(r.EntityLabel ?? ""));
        }

        var encoding = new UTF8Encoding(true); // BOM
        return encoding.GetBytes(sb.ToString());
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }

    /// <summary>Calcule un HMAC SHA-256 du contenu exporté pour traçabilité (basé sur la clé JWT).</summary>
    private string BuildExportHmac(IReadOnlyList<AuditLogEntryDto> rows)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var key = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"] ?? "fallback-key");

        var data = new StringBuilder();
        foreach (var r in rows)
        {
            data.Append(r.Id).Append('|')
                .Append(r.CreatedAt.ToString("O", CultureInfo.InvariantCulture)).Append('|')
                .Append(r.Action).Append('|')
                .Append(r.UserEmail).Append('\n');
        }

        using var hmac = new HMACSHA256(key);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data.ToString()));
        return Convert.ToHexString(hashBytes);
    }

    private static byte[] BuildPdf(
        IReadOnlyList<AuditLogEntryDto> rows,
        Guid tenantId,
        DateTime exportedAtUtc,
        string hmacHex)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Portrait());
                page.Margin(36);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(s => s.FontSize(9).FontFamily(Fonts.Calibri));

                // Header
                page.Header().Column(col =>
                {
                    col.Item().Text("FactuTrust — Journal d'audit").FontSize(16).Bold();
                    col.Item().Text(t =>
                    {
                        t.Span($"Tenant : ").SemiBold();
                        t.Span(tenantId.ToString());
                    });
                    col.Item().Text(t =>
                    {
                        t.Span($"Exporté le : ").SemiBold();
                        t.Span(exportedAtUtc.ToString("dd/MM/yyyy HH:mm:ss") + " UTC");
                    });
                    col.Item().Text(t =>
                    {
                        t.Span($"Total entrées : ").SemiBold();
                        t.Span(rows.Count.ToString(CultureInfo.InvariantCulture));
                    });
                    col.Item().PaddingTop(8).LineHorizontal(0.6f).LineColor(Colors.Grey.Lighten1);
                });

                // Content (tableau)
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(1.6f);
                        c.RelativeColumn(2);
                    });

                    table.Header(h =>
                    {
                        var bg = Colors.Grey.Lighten3;
                        h.Cell().Background(bg).Padding(4).Text("Date / Heure").SemiBold();
                        h.Cell().Background(bg).Padding(4).Text("Utilisateur").SemiBold();
                        h.Cell().Background(bg).Padding(4).Text("Action").SemiBold();
                        h.Cell().Background(bg).Padding(4).Text("Entité").SemiBold();
                        h.Cell().Background(bg).Padding(4).Text("ID").SemiBold();
                    });

                    foreach (var r in rows)
                    {
                        table.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2).Padding(3)
                            .Text(r.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture));
                        table.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2).Padding(3)
                            .Text(r.UserEmail);
                        table.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2).Padding(3)
                            .Text(r.Action);
                        table.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2).Padding(3)
                            .Text(r.EntityType + (string.IsNullOrEmpty(r.EntityLabel) ? "" : " — " + r.EntityLabel));
                        table.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2).Padding(3)
                            .Text(r.EntityId?.ToString() ?? "—").FontSize(7);
                    }
                });

                // Footer (avec HMAC + numéro de page)
                page.Footer().Column(col =>
                {
                    col.Item().PaddingTop(6).LineHorizontal(0.6f).LineColor(Colors.Grey.Lighten1);
                    col.Item().Text(t =>
                    {
                        t.Span("Checksum HMAC SHA-256 : ").SemiBold().FontSize(7);
                        t.Span(hmacHex).FontSize(7).FontFamily(Fonts.Consolas);
                    });
                    col.Item().AlignRight().Text(t =>
                    {
                        t.Span("Page ").FontSize(8);
                        t.CurrentPageNumber().FontSize(8);
                        t.Span(" / ").FontSize(8);
                        t.TotalPages().FontSize(8);
                    });
                });
            });
        });

        return document.GeneratePdf();
    }
}
