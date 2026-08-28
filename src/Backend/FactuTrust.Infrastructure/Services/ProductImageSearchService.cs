using System.Text.Json;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Common.Logging;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Configuration for external image search APIs.
/// </summary>
public sealed class ProductImageSearchOptions
{
    public const string SectionName = "ExternalApis";

    public UnsplashOptions Unsplash { get; set; } = new();
    public GoogleCustomSearchOptions GoogleCustomSearch { get; set; } = new();
}

public sealed class UnsplashOptions
{
    public string AccessKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.unsplash.com";
}

public sealed class GoogleCustomSearchOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string SearchEngineId { get; set; } = string.Empty;
}

/// <summary>
/// Searches for product images using Unsplash (primary) and Google Custom Search (fallback).
/// </summary>
public sealed class ProductImageSearchService : IProductImageSearchService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ProductImageSearchService> _logger;
    private readonly ProductImageSearchOptions _options;

    public ProductImageSearchService(
        IHttpClientFactory httpClientFactory,
        IOptions<ProductImageSearchOptions> options,
        ILogger<ProductImageSearchService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string?> SearchImageUrlAsync(string searchQuery, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
            return null;

        var query = searchQuery.Trim();
        if (query.Length > 200)
            query = query[..200];

        var url = await TryUnsplashAsync(query, cancellationToken);
        if (!string.IsNullOrEmpty(url))
            return url;

        url = await TryGoogleCustomSearchAsync(query, cancellationToken);
        return url;
    }

    private async Task<string?> TryUnsplashAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Unsplash.AccessKey))
        {
            _logger.LogDebug("Unsplash AccessKey not configured, skipping.");
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("ProductImageSearch");
            var encodedQuery = Uri.EscapeDataString(query);
            var requestUrl = $"{_options.Unsplash.BaseUrl}/search/photos?query={encodedQuery}&per_page=1";

            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Add("Authorization", $"Client-ID {_options.Unsplash.AccessKey}");

            var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;
            if (!root.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                return null;

            var first = results[0];
            if (!first.TryGetProperty("urls", out var urls))
                return null;

            if (urls.TryGetProperty("regular", out var regular))
                return regular.GetString();
            if (urls.TryGetProperty("small", out var small))
                return small.GetString();
            if (urls.TryGetProperty("thumb", out var thumb))
                return thumb.GetString();

            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Unsplash API request failed for query: {Query}", LogSanitizer.Sanitize(query));
            return null;
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unsplash image search failed for query: {Query}", LogSanitizer.Sanitize(query));
            return null;
        }
    }

    private async Task<string?> TryGoogleCustomSearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.GoogleCustomSearch.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.GoogleCustomSearch.SearchEngineId))
        {
            _logger.LogDebug("Google Custom Search not configured, skipping.");
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("ProductImageSearch");
            var encodedQuery = Uri.EscapeDataString(query);
            var requestUrl = $"https://www.googleapis.com/customsearch/v1?q={encodedQuery}&searchType=image&key={_options.GoogleCustomSearch.ApiKey}&cx={_options.GoogleCustomSearch.SearchEngineId}&rights=cc_publicdomain&num=1";

            var response = await client.GetAsync(requestUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;
            if (!root.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
                return null;

            var first = items[0];
            if (first.TryGetProperty("link", out var link))
                return link.GetString();

            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Google Custom Search API request failed for query: {Query}", LogSanitizer.Sanitize(query));
            return null;
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Custom Search image search failed for query: {Query}", LogSanitizer.Sanitize(query));
            return null;
        }
    }
}
