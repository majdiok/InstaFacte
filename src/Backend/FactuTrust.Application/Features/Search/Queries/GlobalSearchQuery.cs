using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using MediatR;

namespace FactuTrust.Application.Features.Search.Queries;

public sealed record GlobalSearchQuery(
    string? Query,
    IReadOnlyList<SearchEntityType>? Types = null,
    int LimitPerType = 5) : IRequest<GlobalSearchResponseDto>;

public sealed class GlobalSearchQueryHandler : IRequestHandler<GlobalSearchQuery, GlobalSearchResponseDto>
{
    private const int MinQueryLength = 2;
    private const int MaxQueryLength = 100;
    private const int MaxLimitPerType = 10;

    private readonly IEnumerable<IGlobalSearchProvider> _providers;
    private readonly ICurrentUser _currentUser;

    public GlobalSearchQueryHandler(IEnumerable<IGlobalSearchProvider> providers, ICurrentUser currentUser)
    {
        _providers = providers;
        _currentUser = currentUser;
    }

    public async Task<GlobalSearchResponseDto> Handle(GlobalSearchQuery request, CancellationToken cancellationToken)
    {
        var query = request.Query?.Trim() ?? string.Empty;
        if (query.Length < MinQueryLength)
        {
            return new GlobalSearchResponseDto { Query = query };
        }

        if (query.Length > MaxQueryLength)
        {
            query = query[..MaxQueryLength];
        }

        var limit = Math.Clamp(request.LimitPerType, 1, MaxLimitPerType);
        var typeFilter = request.Types?.Count > 0 ? new HashSet<SearchEntityType>(request.Types) : null;

        var activeProviders = _providers
            .Where(p => _currentUser.HasPermission(p.RequiredPermission))
            .Where(p => typeFilter == null || typeFilter.Contains(p.EntityType))
            .ToList();

        if (activeProviders.Count == 0)
        {
            return new GlobalSearchResponseDto { Query = query };
        }

        var tasks = activeProviders.Select(async provider =>
        {
            try
            {
                var results = await provider.SearchAsync(query, limit, cancellationToken);
                return (Provider: provider, Results: results);
            }
            catch
            {
                return (Provider: provider, Results: (IReadOnlyList<GlobalSearchProviderResult>)Array.Empty<GlobalSearchProviderResult>());
            }
        });

        var providerResults = await Task.WhenAll(tasks);
        var truncatedTypes = new List<string>();
        var merged = new List<GlobalSearchResultDto>();

        foreach (var (provider, results) in providerResults)
        {
            if (results.Count >= limit)
            {
                truncatedTypes.Add(provider.EntityType.ToString());
            }

            merged.AddRange(results.Select(r => new GlobalSearchResultDto
            {
                EntityType = provider.EntityType.ToString().ToLowerInvariant(),
                Id = r.Id,
                Title = r.Title,
                Subtitle = r.Subtitle,
                Status = r.Status,
                Date = r.Date,
                Route = r.DetailRoute,
                ListRoute = r.ListRoute,
                Icon = r.Icon,
                Score = r.Score
            }));
        }

        var ordered = merged
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.Date ?? DateTime.MinValue)
            .ToList();

        return new GlobalSearchResponseDto
        {
            Query = query,
            Results = ordered,
            TruncatedTypes = truncatedTypes
        };
    }
}
