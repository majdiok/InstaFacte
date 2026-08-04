using System.Linq;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// CRUD des taxes et TVA (catalogue tenant).
/// </summary>
[ApiController]
[Route("api/tax")]
[Authorize]
public class TaxController : ControllerBase
{
    private readonly ITaxRepository _taxRepository;
    private readonly ILogger<TaxController> _logger;

    public TaxController(ITaxRepository taxRepository, ILogger<TaxController> logger)
    {
        _taxRepository = taxRepository;
        _logger = logger;
    }

    /// <summary>
    /// Liste toutes les taxes (filtres optionnels).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.SettingsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TaxDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTaxes(
        [FromQuery] int? type,
        [FromQuery] int? context,
        [FromQuery] bool? activeOnly,
        CancellationToken cancellationToken = default)
    {
        TaxType? typeFilter = null;
        TaxContext? contextFilter = null;

        if (type.HasValue)
        {
            if (!Enum.IsDefined(typeof(TaxType), type.Value))
                return BadRequest(ApiResponse<IReadOnlyList<TaxDto>>.Fail("Type de taxe invalide"));
            typeFilter = (TaxType)type.Value;
        }

        if (context.HasValue)
        {
            if (!Enum.IsDefined(typeof(TaxContext), context.Value))
                return BadRequest(ApiResponse<IReadOnlyList<TaxDto>>.Fail("Contexte invalide"));
            contextFilter = (TaxContext)context.Value;
        }

        var list = await _taxRepository.GetAllAsync(typeFilter, contextFilter, activeOnly, cancellationToken);
        var dtos = list.Select(TaxMappings.ToDto).ToList();
        return Ok(ApiResponse<IReadOnlyList<TaxDto>>.Ok(dtos));
    }

    /// <summary>
    /// Détail d'une taxe.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SettingsRead)]
    [ProducesResponseType(typeof(ApiResponse<TaxDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTax(Guid id, CancellationToken cancellationToken = default)
    {
        var tax = await _taxRepository.GetByIdAsync(id, cancellationToken);
        if (tax == null)
            return NotFound(ApiResponse<TaxDto>.Fail("Taxe introuvable"));

        return Ok(ApiResponse<TaxDto>.Ok(TaxMappings.ToDto(tax)));
    }

    /// <summary>
    /// Taux de TVA actifs (pour listes déroulantes produits / factures).
    /// </summary>
    [HttpGet("vat-rates")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<VatRateOptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVatRates(CancellationToken cancellationToken = default)
    {
        var list = await _taxRepository.GetActiveVatRatesAsync(cancellationToken);
        var options = TaxMappings.ToVatRateOptions(list);
        return Ok(ApiResponse<IReadOnlyList<VatRateOptionDto>>.Ok(options));
    }

    /// <summary>
    /// Créer une taxe.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.SettingsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<TaxDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTax([FromBody] CreateTaxDto dto, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(typeof(TaxType), dto.Type))
            return BadRequest(ApiResponse<TaxDto>.Fail("Type de taxe invalide"));
        if (!Enum.IsDefined(typeof(TaxValueType), dto.ValueType))
            return BadRequest(ApiResponse<TaxDto>.Fail("Type de valeur invalide"));
        if (!Enum.IsDefined(typeof(TaxContext), dto.Context))
            return BadRequest(ApiResponse<TaxDto>.Fail("Contexte invalide"));

        var type = (TaxType)dto.Type;
        var valueType = (TaxValueType)dto.ValueType;
        var context = (TaxContext)dto.Context;

        var created = Tax.Create(
            dto.Name,
            type,
            valueType,
            dto.Value,
            context,
            dto.IsAppliedToProducts,
            isSystem: false,
            dto.DisplayOrder);

        if (created.IsFailure)
            return BadRequest(ApiResponse<TaxDto>.Fail(created.Error.Description));

        var tax = await _taxRepository.AddAsync(created.Value, cancellationToken);
        _logger.LogInformation("Tax created {TaxId}", tax.Id);

        return Ok(ApiResponse<TaxDto>.Ok(TaxMappings.ToDto(tax), "Taxe créée"));
    }

    /// <summary>
    /// Mettre à jour une taxe (les taxes système : champs limités côté domaine).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SettingsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<TaxDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTax(
        Guid id,
        [FromBody] UpdateTaxDto dto,
        CancellationToken cancellationToken = default)
    {
        var tax = await _taxRepository.GetByIdAsync(id, cancellationToken);
        if (tax == null)
            return NotFound(ApiResponse<TaxDto>.Fail("Taxe introuvable"));

        if (!Enum.IsDefined(typeof(TaxType), dto.Type))
            return BadRequest(ApiResponse<TaxDto>.Fail("Type de taxe invalide"));
        if (!Enum.IsDefined(typeof(TaxValueType), dto.ValueType))
            return BadRequest(ApiResponse<TaxDto>.Fail("Type de valeur invalide"));
        if (!Enum.IsDefined(typeof(TaxContext), dto.Context))
            return BadRequest(ApiResponse<TaxDto>.Fail("Contexte invalide"));

        var type = (TaxType)dto.Type;
        var valueType = (TaxValueType)dto.ValueType;
        var context = (TaxContext)dto.Context;

        var result = tax.Update(
            dto.Name,
            type,
            valueType,
            dto.Value,
            context,
            dto.IsAppliedToProducts,
            dto.DisplayOrder);

        if (result.IsFailure)
            return BadRequest(ApiResponse<TaxDto>.Fail(result.Error.Description));

        await _taxRepository.UpdateAsync(tax, cancellationToken);

        return Ok(ApiResponse<TaxDto>.Ok(TaxMappings.ToDto(tax), "Taxe mise à jour"));
    }

    /// <summary>
    /// Activer ou désactiver une taxe.
    /// </summary>
    [HttpPut("{id:guid}/toggle")]
    [Authorize(Policy = PermissionPolicies.SettingsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<TaxDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleTax(
        Guid id,
        [FromBody] SetTaxActiveDto dto,
        CancellationToken cancellationToken = default)
    {
        var tax = await _taxRepository.GetByIdAsync(id, cancellationToken);
        if (tax == null)
            return NotFound(ApiResponse<TaxDto>.Fail("Taxe introuvable"));

        tax.SetActive(dto.IsActive);
        await _taxRepository.UpdateAsync(tax, cancellationToken);

        return Ok(ApiResponse<TaxDto>.Ok(TaxMappings.ToDto(tax), "État de la taxe mis à jour"));
    }

    /// <summary>
    /// Supprimer une taxe (interdit pour les taxes système).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SettingsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTax(Guid id, CancellationToken cancellationToken = default)
    {
        var tax = await _taxRepository.GetByIdAsync(id, cancellationToken);
        if (tax == null)
            return NotFound(ApiResponse<object>.Fail("Taxe introuvable"));

        if (tax.IsSystem)
            return BadRequest(ApiResponse<object>.Fail("Impossible de supprimer une taxe système"));

        await _taxRepository.DeleteAsync(tax, cancellationToken);
        _logger.LogInformation("Tax deleted {TaxId}", id);

        return Ok(ApiResponse<object>.Ok(null!, "Taxe supprimée"));
    }
}
