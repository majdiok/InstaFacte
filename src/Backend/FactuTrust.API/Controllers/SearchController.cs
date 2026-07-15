using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Search;
using FactuTrust.Application.Features.Search.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SearchController : ControllerBase
{
    private readonly IMediator _mediator;

    public SearchController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<GlobalSearchResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] string? types = null,
        [FromQuery] int limitPerType = 5,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SearchEntityType>? parsedTypes = null;
        if (!string.IsNullOrWhiteSpace(types))
        {
            var list = new List<SearchEntityType>();
            foreach (var part in types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Enum.TryParse<SearchEntityType>(part, ignoreCase: true, out var entityType))
                {
                    list.Add(entityType);
                }
            }
            if (list.Count > 0) parsedTypes = list;
        }

        var result = await _mediator.Send(new GlobalSearchQuery(q, parsedTypes, limitPerType), cancellationToken);
        return Ok(ApiResponse<GlobalSearchResponseDto>.Ok(result));
    }
}
