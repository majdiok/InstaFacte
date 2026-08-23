using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Onboarding;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ProductOnboarding;
using FactuTrust.Infrastructure.Persistence;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Onboarding;

public sealed class ProductOnboardingChecklistSerializerTests
{
    [Fact]
    public void Deserialize_empty_returns_default()
    {
        var dto = ProductOnboardingChecklistSerializer.Deserialize(null);
        Assert.False(dto.Dismissed);
        Assert.Empty(dto.DoneIds);
    }

    [Fact]
    public void Roundtrip_preserves_done_ids_and_dismissed()
    {
        var original = new ProductOnboardingChecklistDto
        {
            Dismissed = true,
            DoneIds = new[] { ProductOnboardingDefaults.CompanyItemIds.CreateClient, "create-invoice" }
        };

        var json = ProductOnboardingChecklistSerializer.Serialize(original);
        var roundtrip = ProductOnboardingChecklistSerializer.Deserialize(json);

        Assert.True(roundtrip.Dismissed);
        Assert.Equal(original.DoneIds, roundtrip.DoneIds);
    }

    [Fact]
    public void WithDoneId_is_idempotent_and_trims()
    {
        var empty = ProductOnboardingChecklistSerializer.Empty();
        var once = ProductOnboardingChecklistSerializer.WithDoneId(empty, " create-client ");
        var twice = ProductOnboardingChecklistSerializer.WithDoneId(once, "create-client");

        Assert.Single(twice.DoneIds);
        Assert.Equal("create-client", twice.DoneIds[0]);
    }

    [Fact]
    public void Deserialize_invalid_json_does_not_throw()
    {
        var dto = ProductOnboardingChecklistSerializer.Deserialize("{not-json");
        Assert.False(dto.Dismissed);
        Assert.Empty(dto.DoneIds);
    }
}

public sealed class ProductOnboardingDefaultsTests
{
    [Fact]
    public void ForNewInteractiveUser_is_not_started()
    {
        var seed = ProductOnboardingDefaults.ForNewInteractiveUser();
        Assert.Equal(ProductOnboardingStatus.NotStarted, seed.Status);
        Assert.Equal(ProductOnboardingDefaults.CatalogVersion, seed.Version);
        Assert.False(string.IsNullOrWhiteSpace(seed.ChecklistJson));
    }

    [Fact]
    public void ApplicationUser_defaults_to_completed_until_interactive_factory()
    {
        var existing = new ApplicationUser { FirstName = "Old", LastName = "User" };
        Assert.Equal(ProductOnboardingStatus.Completed, existing.ProductOnboardingStatus);

        existing.ApplyNewInteractiveProductOnboarding();
        Assert.Equal(ProductOnboardingStatus.NotStarted, existing.ProductOnboardingStatus);
        Assert.Equal(ProductOnboardingDefaults.CatalogVersion, existing.ProductOnboardingVersion);
    }
}
