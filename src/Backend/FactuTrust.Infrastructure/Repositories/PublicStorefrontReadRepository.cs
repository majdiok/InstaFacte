using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Storefront.Public;
using FactuTrust.Domain.Entities.Storefront;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class PublicStorefrontReadRepository : IPublicStorefrontReadRepository
{
    private readonly MasterDbContext _db;

    public PublicStorefrontReadRepository(MasterDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PublicStreetMapEntryDto>> GetPublishedStreetMapAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.StorefrontProfiles
            .AsNoTracking()
            .Where(p => p.Status == StorefrontStatus.Published && p.StreetPositionIndex != null)
            .OrderBy(p => p.StreetPositionIndex)
            .Select(p => new PublicStreetMapEntryDto
            {
                Slug = p.Slug,
                DisplayName = p.DisplayName,
                Tagline = p.Tagline,
                PublicLogoUrl = p.PublicLogoUrl,
                Category = p.Category,
                StreetPositionIndex = p.StreetPositionIndex!.Value,
                FacadeTheme = p.FacadeTheme,
                BrandPrimaryColorHex = p.BrandPrimaryColorHex,
                BrandSecondaryColorHex = p.BrandSecondaryColorHex
            })
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<PublicStorefrontDetailDto?> GetPublishedDetailBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        return await _db.StorefrontProfiles
            .AsNoTracking()
            .Where(p => p.Status == StorefrontStatus.Published && p.Slug == normalized)
            .Select(p => new PublicStorefrontDetailDto
            {
                Id = p.Id,
                Slug = p.Slug,
                DisplayName = p.DisplayName,
                Tagline = p.Tagline,
                DescriptionMarkdown = p.DescriptionMarkdown,
                BrandPrimaryColorHex = p.BrandPrimaryColorHex,
                BrandSecondaryColorHex = p.BrandSecondaryColorHex,
                PublicLogoUrl = p.PublicLogoUrl,
                PublicCoverImageUrl = p.PublicCoverImageUrl,
                Category = p.Category,
                FacadeTheme = p.FacadeTheme,
                PublicContactEmail = p.PublicContactEmail,
                PublicContactPhone = p.PublicContactPhone,
                PublicContactWhatsApp = p.PublicContactWhatsApp,
                OrderSubmissionEnabled = p.OrderSubmissionEnabled,
                StreetPositionIndex = p.StreetPositionIndex ?? 0
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<PublicStorefrontProductDto>> GetPublishedProductsAsync(
        Guid storefrontProfileId,
        int page,
        int pageSize,
        string? categoryLabel,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.StorefrontProducts
            .AsNoTracking()
            .Where(p => p.StorefrontProfileId == storefrontProfileId && p.IsVisible);

        if (!string.IsNullOrWhiteSpace(categoryLabel))
        {
            var label = categoryLabel.Trim();
            query = query.Where(p => p.CategoryLabel == label);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PublicStorefrontProductDto
            {
                Id = p.Id,
                Slug = p.Slug,
                Name = p.Name,
                DescriptionSanitized = p.DescriptionSanitized,
                PriceAmount = p.PriceAmount,
                PriceCurrency = p.PriceCurrency,
                PublicImageUrl = p.PublicImageUrl,
                CategoryLabel = p.CategoryLabel,
                StockDisplayStatus = p.StockDisplayStatus
            })
            .ToListAsync(cancellationToken);

        return PagedResult<PublicStorefrontProductDto>.Create(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<PublicStorefrontCategoryCountDto>> GetPublishedCategoryCountsAsync(
        Guid storefrontProfileId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.StorefrontProducts
            .AsNoTracking()
            .Where(p => p.StorefrontProfileId == storefrontProfileId && p.IsVisible && p.CategoryLabel != null)
            .GroupBy(p => p.CategoryLabel!)
            .Select(g => new PublicStorefrontCategoryCountDto
            {
                CategoryKey = g.Key,
                Label = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(c => c.Count)
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<IReadOnlyList<StorefrontProductSnapshot>> GetProductSnapshotsAsync(
        IReadOnlyCollection<Guid> storefrontProductIds,
        CancellationToken cancellationToken = default)
    {
        if (storefrontProductIds.Count == 0)
            return Array.Empty<StorefrontProductSnapshot>();

        var ids = storefrontProductIds.ToList();
        var products = await _db.StorefrontProducts
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);

        var profileIds = products.Select(p => p.StorefrontProfileId).Distinct().ToList();
        var publishedProfileIds = await _db.StorefrontProfiles
            .AsNoTracking()
            .Where(s => profileIds.Contains(s.Id) && s.Status == StorefrontStatus.Published)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var published = publishedProfileIds.ToHashSet();
        return products
            .Where(p => published.Contains(p.StorefrontProfileId))
            .Select(p => new StorefrontProductSnapshot(
                p.Id,
                p.StorefrontProfileId,
                p.TenantId,
                p.SourceProductId,
                p.Name,
                p.PriceAmount,
                p.PriceCurrency,
                p.IsVisible))
            .ToList();
    }
}
