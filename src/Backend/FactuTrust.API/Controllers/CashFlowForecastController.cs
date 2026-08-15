using System.Globalization;
using System.Text;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Treasury;
using FactuTrust.Application.Features.Treasury.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Trésorerie prévisionnelle par IA : projection du solde, flux attendus, scénarios, engagements
/// récurrents et seuils.
/// </summary>
/// <remarks>
/// Toutes les routes répondent <b>503</b> quand <c>TreasuryForecast:Enabled</c> est faux, comme le
/// module Prévisions IA. Le front doit basculer son propre flag conjointement.
/// </remarks>
[ApiController]
[Route("api/treasury/cash-forecast")]
[Authorize]
public sealed class CashFlowForecastController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUser _currentUser;
    private readonly TreasuryForecastOptions _options;

    public CashFlowForecastController(
        IMediator mediator,
        ICurrentUser currentUser,
        IOptions<TreasuryForecastOptions> options)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _options = options.Value;
    }

    private IActionResult? GuardEnabled() =>
        _options.Enabled
            ? null
            : StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail(
                "Le module Trésorerie prévisionnelle est désactivé. " +
                "Activez TreasuryForecast:Enabled pour cet espace.",
                "ModuleDisabled"));

    // ──────────────────────────── Projection ────────────────────────────

    /// <summary>Projection complète pour l'écran.</summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.TreasuryForecastView)]
    [ProducesResponseType(typeof(ApiResponse<CashFlowForecastDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetForecast(
        [FromQuery] int? horizonMonths,
        CancellationToken cancellationToken = default)
    {
        if (GuardEnabled() is { } guard) return guard;

        var horizon = horizonMonths ?? _options.DefaultHorizonMonths;
        if (horizon < 1 || horizon > _options.MaxHorizonMonths)
        {
            return BadRequest(ApiResponse<object>.Fail(
                $"L'horizon doit être compris entre 1 et {_options.MaxHorizonMonths} mois.",
                "InvalidHorizon"));
        }

        var result = await _mediator.Send(new GetCashFlowForecastQuery(horizon), cancellationToken);

        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, "ForecastFailed"))
            : Ok(ApiResponse<CashFlowForecastDto>.Ok(result.Value));
    }

    /// <summary>Recalcul explicite. Plafonné par <c>MaxRecomputeRunsPerDay</c>.</summary>
    [HttpPost("recompute")]
    [Authorize(Policy = PermissionPolicies.TreasuryForecastManage)]
    [ProducesResponseType(typeof(ApiResponse<CashFlowForecastDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Recompute(
        [FromQuery] int? horizonMonths,
        CancellationToken cancellationToken = default)
    {
        if (GuardEnabled() is { } guard) return guard;

        var userId = _currentUser.UserId;
        if (userId is null)
            return Unauthorized(ApiResponse<object>.Fail("Utilisateur non identifié.", "Unauthorized"));

        var horizon = horizonMonths ?? _options.DefaultHorizonMonths;
        var result = await _mediator.Send(
            new RecomputeCashFlowForecastCommand(horizon, userId.Value),
            cancellationToken);

        if (result.IsSuccess)
            return Ok(ApiResponse<CashFlowForecastDto>.Ok(result.Value, "Projection recalculée."));

        // Le plafond journalier se traduit par un 429 pour que le front réutilise le dialogue
        // existant de limitation de débit au lieu d'un toast d'erreur générique. Le test porte sur
        // le code d'erreur, jamais sur le libellé : celui-ci changera.
        return result.Error.Code == CashFlowForecastErrors.RateLimitedCode
            ? StatusCode(StatusCodes.Status429TooManyRequests, ApiResponse<object>.Fail(result.Error.Description, "RateLimited"))
            : BadRequest(ApiResponse<object>.Fail(result.Error.Description, "RecomputeFailed"));
    }

    // ──────────────────────────── Flux ────────────────────────────

    /// <summary>Flux attendus d'une projection, paginés.</summary>
    [HttpGet("{runId:guid}/lines")]
    [Authorize(Policy = PermissionPolicies.TreasuryForecastView)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CashFlowLineDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLines(
        Guid runId,
        [FromQuery] string? direction,
        [FromQuery] string? sourceType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (GuardEnabled() is { } guard) return guard;

        try
        {
            var result = await _mediator.Send(
                new GetCashFlowLinesQuery(runId, direction, sourceType, from, to, search, page, pageSize),
                cancellationToken);

            return result.IsFailure
                ? NotFound(ApiResponse<object>.Fail(result.Error.Description, "NotFound"))
                : Ok(ApiResponse<PagedResult<CashFlowLineDto>>.Ok(result.Value));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message, "InvalidArgument"));
        }
    }

    /// <summary>Export CSV des flux d'une projection (UTF-8 BOM, séparateur « ; »).</summary>
    [HttpGet("{runId:guid}/export")]
    [Authorize(Policy = PermissionPolicies.TreasuryForecastView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(
        Guid runId,
        [FromQuery] string? direction,
        [FromQuery] string? sourceType,
        CancellationToken cancellationToken = default)
    {
        if (GuardEnabled() is { } guard) return guard;

        try
        {
            var result = await _mediator.Send(
                new GetCashFlowLinesQuery(runId, direction, sourceType, null, null, null, 1, 200),
                cancellationToken);

            if (result.IsFailure)
                return NotFound(ApiResponse<object>.Fail(result.Error.Description, "NotFound"));

            var csv = BuildCsv(result.Value.Items);
            var fileName = $"tresorerie-previsionnelle-{DateTime.UtcNow:yyyyMMdd}.csv";

            // BOM explicite : sans lui, Excel en environnement francophone lit les accents de
            // travers, ce qui a déjà été corrigé sur les autres exports du produit.
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message, "InvalidArgument"));
        }
    }

    // ──────────────────── Engagements récurrents ────────────────────

    [HttpGet("commitments")]
    [Authorize(Policy = PermissionPolicies.TreasuryForecastView)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RecurringCashCommitmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCommitments(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        if (GuardEnabled() is { } guard) return guard;

        var result = await _mediator.Send(new GetRecurringCashCommitmentsQuery(includeInactive), cancellationToken);

        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<IReadOnlyList<RecurringCashCommitmentDto>>.Ok(result.Value));
    }

    [HttpPost("commitments")]
    [Authorize(Policy = PermissionPolicies.TreasuryForecastManage)]
    [ProducesResponseType(typeof(ApiResponse<RecurringCashCommitmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateCommitment(
        [FromBody] SaveRecurringCashCommitmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (GuardEnabled() is { } guard) return guard;

        try
        {
            var result = await _mediator.Send(
                new CreateRecurringCashCommitmentCommand(request),
                cancellationToken);

            return result.IsFailure
                ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
                : Ok(ApiResponse<RecurringCashCommitmentDto>.Ok(result.Value, "Engagement enregistré."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message, "InvalidArgument"));
        }
    }

    [HttpPut("commitments/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.TreasuryForecastManage)]
    [ProducesResponseType(typeof(ApiResponse<RecurringCashCommitmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCommitment(
        Guid id,
        [FromBody] SaveRecurringCashCommitmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (GuardEnabled() is { } guard) return guard;

        try
        {
            var result = await _mediator.Send(
                new UpdateRecurringCashCommitmentCommand(id, request),
                cancellationToken);

            return result.IsFailure
                ? NotFound(ApiResponse<object>.Fail(result.Error.Description, "NotFound"))
                : Ok(ApiResponse<RecurringCashCommitmentDto>.Ok(result.Value, "Engagement mis à jour."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message, "InvalidArgument"));
        }
    }

    [HttpDelete("commitments/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.TreasuryForecastManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeactivateCommitment(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (GuardEnabled() is { } guard) return guard;

        var result = await _mediator.Send(new DeactivateRecurringCashCommitmentCommand(id), cancellationToken);

        return result.IsFailure
            ? NotFound(ApiResponse<object>.Fail(result.Error.Description, "NotFound"))
            : NoContent();
    }

    // ──────────────────────────── Seuils ────────────────────────────

    [HttpGet("settings")]
    [Authorize(Policy = PermissionPolicies.TreasuryForecastView)]
    [ProducesResponseType(typeof(ApiResponse<CashFlowThresholdsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken = default)
    {
        if (GuardEnabled() is { } guard) return guard;

        var result = await _mediator.Send(new GetCashFlowThresholdsQuery(), cancellationToken);

        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<CashFlowThresholdsDto>.Ok(result.Value));
    }

    [HttpPut("settings")]
    [Authorize(Policy = PermissionPolicies.TreasuryForecastManage)]
    [ProducesResponseType(typeof(ApiResponse<CashFlowThresholdsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveSettings(
        [FromBody] SaveCashFlowThresholdsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (GuardEnabled() is { } guard) return guard;

        try
        {
            var result = await _mediator.Send(new SaveCashFlowThresholdsCommand(request), cancellationToken);

            return result.IsFailure
                ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
                : Ok(ApiResponse<CashFlowThresholdsDto>.Ok(result.Value, "Seuils enregistrés."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message, "InvalidArgument"));
        }
    }

    // ──────────────────────────── CSV ────────────────────────────

    private static string BuildCsv(IReadOnlyList<CashFlowLineDto> lines)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Sens;Source;Reference;Libelle;Tiers;Date contractuelle;Date attendue;Montant;Probabilite %;Montant pondere");

        foreach (var line in lines)
        {
            sb.Append(Escape(line.Direction == "inflow" ? "Encaissement" : "Décaissement")).Append(';')
              .Append(Escape(line.SourceType)).Append(';')
              .Append(Escape(line.SourceReference)).Append(';')
              .Append(Escape(line.Label)).Append(';')
              .Append(Escape(line.ThirdPartyName)).Append(';')
              .Append(line.ContractualDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)).Append(';')
              .Append(line.ExpectedDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)).Append(';')
              .Append(line.Amount.ToString("0.000", CultureInfo.InvariantCulture)).Append(';')
              .Append(line.ProbabilityPercent.ToString("0.##", CultureInfo.InvariantCulture)).Append(';')
              .Append(line.WeightedAmount.ToString("0.000", CultureInfo.InvariantCulture))
              .AppendLine();
        }

        return sb.ToString();
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Contains(';') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
