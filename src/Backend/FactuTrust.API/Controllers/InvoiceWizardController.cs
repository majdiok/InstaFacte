using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.InvoiceWizard.Commands;
using FactuTrust.Application.Features.InvoiceWizard.Queries;
using FactuTrust.Application.Features.InvoiceWizard.Validators;
using FactuTrust.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// API controller for invoice wizard operations.
/// Handles draft management, validation, and invoice submission.
/// </summary>
[ApiController]
[Route("api/invoices/wizard")]
[Authorize]
[Produces("application/json")]
public class InvoiceWizardController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IInvoiceComplianceValidator _complianceValidator;
    private readonly ILogger<InvoiceWizardController> _logger;

    public InvoiceWizardController(
        IMediator mediator,
        IInvoiceComplianceValidator complianceValidator,
        ILogger<InvoiceWizardController> logger)
    {
        _mediator = mediator;
        _complianceValidator = complianceValidator;
        _logger = logger;
    }

    #region Draft Management

    /// <summary>
    /// Creates a new draft or updates an existing one.
    /// Supports partial saves for progressive wizard completion.
    /// </summary>
    /// <param name="request">Draft data to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated draft with calculated totals</returns>
    [HttpPost("drafts")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<DraftResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveDraft(
        [FromBody] SaveDraftRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Saving draft {DraftId} at step {Step}",
            request.DraftId, request.CurrentStep);

        var command = new SaveDraftCommand(request);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }

        return Ok(ApiResponse<DraftResponseDto>.Ok(
            result.Value, 
            "Brouillon enregistré"));
    }

    /// <summary>
    /// Gets a draft by ID.
    /// </summary>
    /// <param name="id">Draft ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Draft with all step data and calculated totals</returns>
    [HttpGet("drafts/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.InvoicesRead)]
    [ProducesResponseType(typeof(ApiResponse<DraftResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDraft(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetDraftQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<DraftResponseDto>.Ok(result.Value));
    }

    /// <summary>
    /// Lists user's drafts.
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20)</param>
    /// <param name="includeExpired">Include expired drafts</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of draft summaries</returns>
    [HttpGet("drafts")]
    [Authorize(Policy = PermissionPolicies.InvoicesRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DraftSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDrafts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeExpired = false,
        CancellationToken cancellationToken = default)
    {
        var query = new ListDraftsQuery(page, pageSize, includeExpired);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(ApiResponse<PagedResult<DraftSummaryDto>>.Ok(result));
    }

    /// <summary>
    /// Deletes a draft.
    /// </summary>
    /// <param name="id">Draft ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpDelete("drafts/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.InvoicesUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDraft(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteDraftCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));

        return NoContent();
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validates a draft step.
    /// </summary>
    /// <param name="request">Step data to validate</param>
    /// <param name="step">Step number (0-5)</param>
    /// <returns>Validation result</returns>
    [HttpPost("validate/step/{step:int}")]
    [Authorize(Policy = PermissionPolicies.InvoicesRead)]
    [ProducesResponseType(typeof(ApiResponse<StepValidationResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public IActionResult ValidateStep(
        [FromBody] object request,
        int step)
    {
        IValidator? validator = step switch
        {
            0 when request is WizardStepMetadataDto metadata => 
                new WizardStepMetadataValidator(),
            2 when request is WizardStepClientDto client => 
                new WizardStepClientValidator(),
            3 when request is List<WizardStepLineDto> lines => 
                new WizardStepLinesValidator(),
            4 when request is WizardStepPaymentLegalDto payment => 
                new WizardStepPaymentLegalValidator(),
            _ => null
        };

        if (validator == null)
        {
            return Ok(ApiResponse<StepValidationResultDto>.Ok(
                new StepValidationResultDto { IsValid = true, Errors = new() }));
        }

        var context = new ValidationContext<object>(request);
        var validationResult = validator.Validate(context);

        var result = new StepValidationResultDto
        {
            IsValid = validationResult.IsValid,
            Errors = validationResult.Errors
                .Select(e => new ValidationErrorDto
                {
                    Field = e.PropertyName,
                    Message = e.ErrorMessage,
                    ErrorCode = e.ErrorCode
                })
                .ToList()
        };

        return Ok(ApiResponse<StepValidationResultDto>.Ok(result));
    }

    /// <summary>
    /// Performs full compliance validation on a draft.
    /// </summary>
    /// <param name="id">Draft ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detailed compliance validation result</returns>
    [HttpPost("drafts/{id:guid}/validate")]
    [Authorize(Policy = PermissionPolicies.InvoicesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<WizardValidationResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ValidateDraft(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating draft {DraftId}", id);

        // Get the draft
        var query = new GetDraftQuery(id);
        var draftResult = await _mediator.Send(query, cancellationToken);

        if (draftResult.IsFailure)
            return NotFound(ApiResponse<object>.Fail(draftResult.Error.Description));

        // Load draft entity for validation
        var validateCommand = new ValidateDraftCommand(id);
        var validationResult = await _mediator.Send(validateCommand, cancellationToken);

        if (validationResult.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(validationResult.Error.Description));

        return Ok(ApiResponse<WizardValidationResultDto>.Ok(
            validationResult.Value,
            validationResult.Value.IsValid 
                ? "Le brouillon est conforme" 
                : "Des corrections sont nécessaires"));
    }

    #endregion

    #region Invoice Number

    /// <summary>
    /// Gets the next invoice number preview.
    /// </summary>
    /// <param name="prefix">Invoice prefix (FAC for invoice, AVO for credit note)</param>
    /// <param name="year">Fiscal year (default: current year)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Next invoice number</returns>
    [HttpGet("next-number")]
    [Authorize(Policy = PermissionPolicies.InvoicesRead)]
    [ProducesResponseType(typeof(ApiResponse<NextInvoiceNumberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetNextNumber(
        [FromQuery] string prefix = "FAC",
        [FromQuery] int? year = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Getting next invoice number for Prefix {Prefix}, Year {Year}",
            prefix, year);

        var query = new GetNextInvoiceNumberQuery(prefix, year);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning(
                "Failed to get next invoice number: {Error}",
                result.Error.Description);
            
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }

        return Ok(ApiResponse<NextInvoiceNumberDto>.Ok(result.Value));
    }

    #endregion

    #region Submission

    /// <summary>
    /// Submits a draft to create a final invoice.
    /// Uses idempotency key to prevent double submissions.
    /// </summary>
    /// <param name="id">Draft ID</param>
    /// <param name="request">Submission request with idempotency key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created invoice details</returns>
    [HttpPost("drafts/{id:guid}/submit")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<InvoiceCreatedResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitDraft(
        Guid id,
        [FromBody] SubmitDraftRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Submitting draft {DraftId} with idempotency key {IdempotencyKey}",
            id, request.IdempotencyKey);

        // Validate request
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || 
            request.IdempotencyKey.Length < 32)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "La clé d'idempotence doit contenir au moins 32 caractères"));
        }

        var command = new SubmitInvoiceCommand(id, request.IdempotencyKey);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            
            if (result.Error.Code == "Conflict")
                return Conflict(ApiResponse<object>.Fail(result.Error.Description));
            
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }

        _logger.LogInformation(
            "Invoice {InvoiceNumber} created from draft {DraftId}",
            result.Value.InvoiceNumber, id);

        return CreatedAtRoute(
            "GetInvoice",
            new { id = result.Value.InvoiceId },
            ApiResponse<InvoiceCreatedResultDto>.Ok(
                result.Value, 
                "Facture créée avec succès"));
    }

    #endregion
}

#region Supporting DTOs

/// <summary>
/// Request for submitting a draft.
/// </summary>
public sealed record SubmitDraftRequest
{
    /// <summary>
    /// Unique key to prevent double submissions.
    /// Should be generated client-side and stored until success.
    /// </summary>
    public string IdempotencyKey { get; init; } = null!;
}

/// <summary>
/// Result of step validation.
/// </summary>
public sealed record StepValidationResultDto
{
    public bool IsValid { get; init; }
    public List<ValidationErrorDto> Errors { get; init; } = new();
}

/// <summary>
/// Validation error details.
/// </summary>
public sealed record ValidationErrorDto
{
    public string Field { get; init; } = null!;
    public string Message { get; init; } = null!;
    public string? ErrorCode { get; init; }
}

#endregion

#region Additional Commands

/// <summary>
/// Command to delete a draft.
/// </summary>
public sealed record DeleteDraftCommand(Guid DraftId) : IRequest<Domain.Common.Result>;

/// <summary>
/// Handler for DeleteDraftCommand.
/// </summary>
public sealed class DeleteDraftCommandHandler : IRequestHandler<DeleteDraftCommand, Domain.Common.Result>
{
    private readonly IInvoiceDraftRepository _draftRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteDraftCommandHandler(
        IInvoiceDraftRepository draftRepository,
        IUnitOfWork unitOfWork)
    {
        _draftRepository = draftRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Domain.Common.Result> Handle(
        DeleteDraftCommand command, 
        CancellationToken cancellationToken)
    {
        var draft = await _draftRepository.GetByIdAsync(command.DraftId, cancellationToken);

        if (draft is null)
            return Domain.Common.Result.Failure(
                Domain.Common.Error.NotFound("Draft", command.DraftId));

        if (draft.IsConverted)
            return Domain.Common.Result.Failure(
                Domain.Common.Error.Conflict("Impossible de supprimer un brouillon converti"));

        await _draftRepository.DeleteAsync(draft, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Domain.Common.Result.Success();
    }
}

/// <summary>
/// Command to validate a draft (compliance check).
/// </summary>
public sealed record ValidateDraftCommand(Guid DraftId) : IRequest<Domain.Common.Result<WizardValidationResultDto>>;

/// <summary>
/// Handler for ValidateDraftCommand.
/// </summary>
public sealed class ValidateDraftCommandHandler 
    : IRequestHandler<ValidateDraftCommand, Domain.Common.Result<WizardValidationResultDto>>
{
    private readonly IInvoiceDraftRepository _draftRepository;
    private readonly IInvoiceComplianceValidator _complianceValidator;

    public ValidateDraftCommandHandler(
        IInvoiceDraftRepository draftRepository,
        IInvoiceComplianceValidator complianceValidator)
    {
        _draftRepository = draftRepository;
        _complianceValidator = complianceValidator;
    }

    public async Task<Domain.Common.Result<WizardValidationResultDto>> Handle(
        ValidateDraftCommand command, 
        CancellationToken cancellationToken)
    {
        var draft = await _draftRepository.GetByIdAsync(command.DraftId, cancellationToken);

        if (draft is null)
            return Domain.Common.Result.Failure<WizardValidationResultDto>(
                Domain.Common.Error.NotFound("Draft", command.DraftId));

        var validation = await _complianceValidator.ValidateAsync(draft, cancellationToken);

        return Domain.Common.Result.Success(validation);
    }
}

#endregion
