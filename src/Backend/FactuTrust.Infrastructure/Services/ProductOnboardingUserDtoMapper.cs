using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Onboarding;
using FactuTrust.Infrastructure.Persistence;

namespace FactuTrust.Infrastructure.Services;

public static class ProductOnboardingUserDtoMapper
{
    public static ProductOnboardingChecklistDto ChecklistOf(ApplicationUser user) =>
        ProductOnboardingChecklistSerializer.Deserialize(user.ProductOnboardingChecklistJson);
}
