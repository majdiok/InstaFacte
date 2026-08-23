using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.ProductOnboarding;

/// <summary>
/// Canonical v1 identifiers and seed values for the first-login product tour / checklist.
/// </summary>
public static class ProductOnboardingDefaults
{
    public const int CatalogVersion = 1;

    public const string EmptyChecklistJson = """{"dismissed":false,"doneIds":[]}""";

    public static class CompanyItemIds
    {
        public const string CompanyProfile = "company-profile";
        public const string CreateClient = "create-client";
        public const string CreateProduct = "create-product";
        public const string CreateInvoice = "create-invoice";
        public const string Numbering = "numbering";
        public const string InviteUser = "invite-user";
    }

    public static class FirmItemIds
    {
        public const string FirmSettings = "firm-settings";
        public const string AddCollaborator = "add-collaborator";
        public const string ClientDossier = "client-dossier";
        public const string FiscalSchedule = "fiscal-schedule";
        public const string ChefDeMission = "chef-de-mission";
    }

    public static ProductOnboardingSeed ForNewInteractiveUser() =>
        new(
            ProductOnboardingStatus.NotStarted,
            CatalogVersion,
            EmptyChecklistJson,
            DateTime.UtcNow);
}

public readonly record struct ProductOnboardingSeed(
    ProductOnboardingStatus Status,
    int Version,
    string ChecklistJson,
    DateTime UpdatedAt);
