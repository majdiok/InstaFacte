using FactuTrust.Application.Configuration;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Forecasting;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.Commands;
using FactuTrust.Application.Features.AI.Tools;
using FactuTrust.Application.Features.WithholdingTax.Services;
using FactuTrust.Domain.Services;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using FactuTrust.Infrastructure.Services;
using FactuTrust.Infrastructure.Services.AI;
using FactuTrust.Infrastructure.Services.Forecasting;
using FactuTrust.Infrastructure.Services.Forecasting.Background;
using FactuTrust.Infrastructure.Services.Forecasting.Calendar;
using FactuTrust.Infrastructure.Services.Storefront;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace FactuTrust.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);

        // Master Database
        services.AddDbContext<MasterDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("MasterConnection"),
                b => b.MigrationsAssembly(typeof(MasterDbContext).Assembly.FullName)));

        // Tenant Database Factory
        // Only register if not in design-time mode (EF Core tools)
        if (!IsDesignTime())
        {
            // Register TenantDbContextFactory as both concrete type and interface
            services.AddScoped<TenantDbContextFactory>();
            services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>(sp => sp.GetRequiredService<TenantDbContextFactory>());
            
            // Register a custom factory adapter that resolves tenant connection string at runtime
            // This allows IDbContextFactory<TenantDbContext> to work correctly with multi-tenancy
            services.AddScoped<IDbContextFactory<TenantDbContext>, TenantDbContextFactoryAdapter>();
        }

        // Data Protection for encrypting connection strings
        services.AddDataProtection()
            .SetApplicationName("FactuTrust")
            .PersistKeysToDbContext<MasterDbContext>();

        // Multi-tenancy
        // Only register if not in design-time mode (EF Core tools)
        if (!IsDesignTime())
        {
            services.AddScoped<ITenantContext, TenantContext>();
            services.AddScoped<ITenantService, TenantService>();
            services.AddScoped<ITenantAuthTokenService, TenantAuthTokenService>();
            services.AddScoped<IFirmAssignmentService, FirmAssignmentService>();
            services.AddScoped<IFirmDashboardService, FirmDashboardService>();
            services.AddScoped<IFirmFiscalScheduleService, FirmFiscalScheduleService>();
            services.AddScoped<IFirmFiscalScheduleWriteService, FirmFiscalScheduleWriteService>();
            services.AddScoped<IFirmContextService, FirmContextService>();
            services.AddSingleton<IAccountingFirmsFeature, AccountingFirmsFeature>();
            services.AddScoped<ITenantMigrationGuard, TenantMigrationGuard>();
            // TenantDbContextFactory is already registered above
        }

        // Repositories
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ITaxRepository, TaxRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductCategoryRepository, ProductCategoryRepository>();
        services.AddScoped<IInvoiceDraftRepository, InvoiceDraftRepository>();

        // Stock Management Repositories
        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<IStockItemRepository, StockItemRepository>();
        services.AddScoped<IStockMovementRepository, StockMovementRepository>();
        services.AddScoped<IPhysicalInventoryRepository, PhysicalInventoryRepository>();
        services.AddScoped<IStockTransferRepository, StockTransferRepository>();

        // Delivery Notes Repository
        services.AddScoped<IDeliveryNoteRepository, DeliveryNoteRepository>();

        // Purchasing Repositories
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<ISupplierInvoiceRepository, SupplierInvoiceRepository>();
        services.AddScoped<ISupplierPaymentRepository, SupplierPaymentRepository>();
        services.AddScoped<ICashOperationRepository, CashOperationRepository>();
        services.AddScoped<IBankDepositRepository, BankDepositRepository>();
        services.AddScoped<IBankAccountRepository, BankAccountRepository>();

        services.AddScoped<IChartOfAccountRepository, ChartOfAccountRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IJournalRepository, JournalRepository>();
        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<IJournalEntryTemplateRepository, JournalEntryTemplateRepository>();
        services.AddScoped<IVatDeclarationRepository, VatDeclarationRepository>();
        services.AddScoped<IFiscalScheduleRepository, FiscalScheduleRepository>();
        services.AddScoped<IFixedAssetRepository, FixedAssetRepository>();
        services.AddScoped<IDepreciationRateCategoryRepository, DepreciationRateCategoryRepository>();

        // Withholding Tax (TEJ)
        services.AddScoped<IWithholdingTaxRepository, WithholdingTaxRepository>();
        services.AddScoped<IWithholdingFiscalYearParameterRepository, WithholdingFiscalYearParameterRepository>();
        services.AddScoped<ITejXmlExportLogRepository, TejXmlExportLogRepository>();

        // Payroll (RH & Paie)
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IPayrollRunRepository, PayrollRunRepository>();
        services.AddScoped<IPayrollParametersRepository, PayrollParametersRepository>();
        services.AddScoped<ILeaveRequestRepository, LeaveRequestRepository>();
        services.AddScoped<IEmployeeAdvanceRepository, EmployeeAdvanceRepository>();
        services.AddScoped<IPayrollOvertimeRepository, PayrollOvertimeRepository>();
        services.AddScoped<ILeaveBalanceAccrualRepository, LeaveBalanceAccrualRepository>();

        services.AddScoped<IOpportunityRepository, OpportunityRepository>();
        services.AddScoped<ISalesActivityRepository, SalesActivityRepository>();
        services.AddScoped<ISalesTargetRepository, SalesTargetRepository>();
        services.AddScoped<IQuoteTemplateRepository, QuoteTemplateRepository>();
        services.AddScoped<ITenantMemberDirectory, TenantMemberDirectory>();
        services.AddScoped<IAssignableTenantUsersSource, AssignableTenantUsersSource>();

        services.AddScoped<IStorefrontProfileRepository, StorefrontProfileRepository>();
        services.AddScoped<IStorefrontPublishingConsentRepository, StorefrontPublishingConsentRepository>();
        services.AddScoped<IStorefrontSlugService, StorefrontSlugService>();
        services.AddScoped<IIpAddressHasher, IpAddressHasher>();
        services.AddSingleton<IStorefrontOutboxPayloadBuilder, StorefrontOutboxPayloadBuilder>();
        services.AddScoped<IStorefrontTenantWriter, StorefrontTenantWriter>();
        services.AddScoped<IPublicStorefrontReadRepository, PublicStorefrontReadRepository>();
        services.AddScoped<IStorefrontOrderRepository, StorefrontOrderRepository>();
        services.AddScoped<IStorefrontCaptchaValidator, StorefrontCaptchaValidator>();

        services.AddScoped<IAccountingPeriodService, AccountingPeriodService>();
        services.AddScoped<IAccountingService, AccountingService>();
        services.AddScoped<IFiscalStampResolver, FiscalStampResolver>();
        services.AddScoped<IAccountingReportingService, AccountingReportingService>();
        services.AddScoped<IThirdPartyDirectoryService, ThirdPartyDirectoryService>();
        services.AddScoped<ITenantCompanySummaryProvider, TenantCompanySummaryProvider>();
        services.AddScoped<IAccountingExportService, AccountingExportService>();
        services.AddScoped<IDepreciationEngine, DepreciationEngine>();
        services.AddScoped<IFixedAssetExportService, FixedAssetExportService>();
        services.AddScoped<ILetteringService, LetteringService>();
        services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
        services.AddScoped<IFecExportService, FecExportService>();
        services.AddScoped<IJournalImportService, JournalImportService>();
        services.AddScoped<IJournalEntryAttachmentService, JournalEntryAttachmentService>();
        services.AddScoped<IFiscalScheduleGenerator, FiscalScheduleGenerator>();
        services.AddScoped<IFiscalScheduleAttachmentService, FiscalScheduleAttachmentService>();
        services.AddScoped<IBankReconciliationService, BankReconciliationService>();
        services.AddScoped<IBankAccountChartProvisioningService, BankAccountChartProvisioningService>();
        services.AddScoped<BankAccountMatcher>();
        services.AddScoped<ImportBankStatementFromFileHandler>();
        services.AddScoped<IBankStatementPdfImportService, BankStatementPdfImportService>();

        // Services
        services.AddScoped<IEnsureDefaultCompanyService, EnsureDefaultCompanyService>();
        services.AddHttpClient("InvoicePdfLogo", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
        });
        services.AddScoped<IInvoicePdfContextLoader, InvoicePdfContextLoader>();
        services.AddScoped<IPdfService, PdfService>();

        // Modèles visuels d'impression (configuration par type de document + surcharge à l'impression).
        services.AddSingleton<IDocumentTemplate, Services.Templates.StandardDocumentTemplate>();
        services.AddSingleton<IDocumentTemplate, Services.Templates.ClassicTvaSyntheseTemplate>();
        services.AddSingleton<IDocumentTemplate, Services.Templates.OrangeTableTemplate>();
        services.AddSingleton<IDocumentTemplate, Services.Templates.OrangeTableCompactTemplate>();
        services.AddSingleton<IDocumentTemplate, Services.Templates.ModernBoxedTemplate>();
        services.AddSingleton<IDocumentTemplate, Services.Templates.ModernBoxedPaymentsTemplate>();
        services.AddSingleton<IDocumentTemplateRegistry, Services.Templates.DocumentTemplateRegistry>();
        services.AddScoped<IDocumentTemplatePreferenceRepository, DocumentTemplatePreferenceRepository>();
        services.AddScoped<IUserDashboardLayoutRepository, UserDashboardLayoutRepository>();
        // Studio (low-code) repositories.
        services.AddScoped<ICustomEntityRepository, Repositories.Studio.CustomEntityRepository>();
        services.AddScoped<ICustomSystemRepository, Repositories.Studio.CustomSystemRepository>();
        services.AddScoped<ICustomFieldRepository, Repositories.Studio.CustomFieldRepository>();
        services.AddScoped<ICustomRecordRepository, Repositories.Studio.CustomRecordRepository>();
        services.AddScoped<ICustomFormRepository, Repositories.Studio.CustomFormRepository>();
        services.AddScoped<ICustomReportRepository, Repositories.Studio.CustomReportRepository>();
        services.AddScoped<ICustomViewRepository, Repositories.Studio.CustomViewRepository>();
        services.AddScoped<FactuTrust.Application.Features.Studio.Common.IExistingDataSourceProvider, Services.Studio.ExistingDataSourceProvider>();
        services.AddScoped<FactuTrust.Application.Features.Studio.Common.ISqlSchemaProvider, Services.Studio.SqlSchemaProvider>();
        services.AddScoped<ISqlColumnValueFormatter, Services.Studio.SqlColumnValueFormatter>();
        services.AddScoped<ISqlViewResultEnricher, Services.Studio.SqlViewResultEnricher>();
        services.AddScoped<IJsonIndexManager, Services.Studio.JsonIndexManager>();
        services.AddSingleton<IStudioQrGenerator, Services.Studio.StudioQrGenerator>();
        services.AddScoped<ICustomSequenceAllocator, Services.Studio.CustomSequenceAllocator>();
        services.AddScoped<IStudioComputedFieldWriter, Services.Studio.StudioComputedFieldWriter>();
        services.AddScoped<IStudioComputedFieldReader, Services.Studio.StudioComputedFieldReader>();
        services.AddSingleton<IStudioFileStorageService, Services.Studio.StudioFileStorageService>();
        // ERP bridge (Studio automations).
        services.AddScoped<ICustomAutomationRepository, Repositories.Studio.CustomAutomationRepository>();
        services.AddScoped<IStudioBridgeExecutor, Services.Studio.StudioBridgeExecutor>();
        services.AddScoped<IDocumentTemplateResolver, DocumentTemplateResolver>();
        services.AddScoped<ISignatureService, SignatureService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditChainRepairService, AuditChainRepairService>();
        services.AddScoped<ISubscriptionResolver, SubscriptionResolver>();
        services.AddScoped<IInvoiceComplianceValidator, InvoiceComplianceValidator>();
        services.AddScoped<IInvoiceNumberGenerator, InvoiceNumberGenerator>();
        services.AddScoped<IQuoteNumberGenerator, QuoteNumberGenerator>();
        services.AddScoped<ICashOperationNumberGenerator, CashOperationNumberGenerator>();
        services.AddScoped<IBankDepositNumberGenerator, BankDepositNumberGenerator>();
        services.AddScoped<IDocumentNumberService, DocumentNumberService>();
        services.AddScoped<INumberingSchemeRepository, NumberingSchemeRepository>();
        services.AddScoped<IQuoteToInvoiceConversionService, QuoteToInvoiceConversionService>();
        services.AddScoped<IStockTransferCompletionService, StockTransferCompletionService>();
        services.AddScoped<IEmailService, EmailService>();
        // Lot C2 — Email options + log query service + Hangfire SendEmailJob
        services.Configure<FactuTrust.Infrastructure.Services.Email.SmtpOptions>(
            configuration.GetSection(FactuTrust.Infrastructure.Services.Email.SmtpOptions.SectionName));
        services.AddScoped<IEmailMessageQueryService, EmailMessageQueryService>();
        services.AddScoped<SendEmailJob>();
        services.AddScoped<IEffectivePermissionService, EffectivePermissionService>();
        services.AddScoped<IPlatformTenantQueryService, PlatformTenantQueryService>();
        services.AddScoped<ISubscriptionAdminService, SubscriptionAdminService>();
        services.AddScoped<IPlatformMfaService, TotpPlatformMfaService>();
        services.AddScoped<IUserSessionService, UserSessionService>();
        services.AddScoped<IFailedLoginAttemptService, FailedLoginAttemptService>();

        // Lot C1 — Plans configurables + module overrides + jobs Hangfire
        services.AddScoped<IPlanResolver, DbPlanResolver>();
        services.AddScoped<IPlanAdminService, PlanAdminService>();
        services.AddScoped<IPlanQuotaService, PlanQuotaService>();
        services.AddScoped<IStudioQuotaService, StudioQuotaService>();
        services.AddScoped<ITenantModuleOverrideService, TenantModuleOverrideService>();
        services.AddScoped<FactuTrust.Infrastructure.Services.Background.ExpireModuleOverridesJob>();

        // Lot C3 — Coupons + crédits tenant
        services.AddScoped<ICouponAdminService, CouponAdminService>();
        services.AddScoped<ITenantCreditAdminService, TenantCreditAdminService>();

        // Lot C4 — Facturation plateforme (PDF QuestPDF + numérotation séquentielle DGI + reçus)
        services.AddScoped<IPlatformDocumentNumberService, FactuTrust.Infrastructure.Services.Billing.PlatformDocumentNumberService>();
        services.AddScoped<IPlatformFiscalSettingsService, FactuTrust.Infrastructure.Services.Billing.PlatformFiscalSettingsService>();
        services.AddSingleton<IPlatformInvoicePdfRenderer, FactuTrust.Infrastructure.Services.Billing.PlatformInvoicePdfRenderer>();
        services.AddScoped<IPlatformInvoiceAdminService, FactuTrust.Infrastructure.Services.Billing.PlatformInvoiceAdminService>();
        services.AddScoped<IPlatformReceiptAdminService, FactuTrust.Infrastructure.Services.Billing.PlatformReceiptAdminService>();

        // Lot C6 — Renouvellement auto + Dunning (campagnes + cycles + jobs Hangfire)
        services.AddScoped<IDunningCampaignService, FactuTrust.Infrastructure.Services.Billing.DunningCampaignService>();
        services.AddScoped<IDunningStateQueryService, FactuTrust.Infrastructure.Services.Billing.DunningStateQueryService>();
        services.AddScoped<ISubscriptionRenewalService, FactuTrust.Infrastructure.Services.Billing.SubscriptionRenewalService>();
        services.AddScoped<FactuTrust.Infrastructure.Services.Background.RenewalScanJob>();
        services.AddScoped<FactuTrust.Infrastructure.Services.Background.DunningExecutorJob>();
        services.AddScoped<RecurringEntryGenerator>();
        services.AddScoped<IRecurringEntryService, RecurringEntryService>();
        services.AddScoped<IPreClosingControlService, PreClosingControlService>();
        services.AddScoped<IAssistedInventoryEntryService, AssistedInventoryEntryService>();
        services.AddScoped<IFiscalYearLockService, FiscalYearLockService>();
        services.AddScoped<FactuTrust.Infrastructure.Services.Background.RecurringEntriesJob>();
        services.AddScoped<FiscalReminderService>();
        services.AddScoped<FactuTrust.Infrastructure.Services.Background.FiscalReminderJob>();

        // Lot C5 — Providers paiement (Konnect / Paymee / Virement) + webhooks signés HMAC
        services.AddScoped<FactuTrust.Infrastructure.Services.Billing.PaymentProviderConfigService>();
        services.AddScoped<IPaymentProviderConfigService>(sp => sp.GetRequiredService<FactuTrust.Infrastructure.Services.Billing.PaymentProviderConfigService>());
        services.AddScoped<IWebhookSignatureValidator, FactuTrust.Infrastructure.Services.Billing.WebhookSignatureValidator>();
        services.AddHttpClient(nameof(FactuTrust.Infrastructure.Services.Billing.KonnectPaymentClient), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddHttpClient(nameof(FactuTrust.Infrastructure.Services.Billing.PaymeePaymentClient), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddScoped<IPaymentProviderClient, FactuTrust.Infrastructure.Services.Billing.KonnectPaymentClient>();
        services.AddScoped<IPaymentProviderClient, FactuTrust.Infrastructure.Services.Billing.PaymeePaymentClient>();
        services.AddScoped<IPaymentCheckoutService, FactuTrust.Infrastructure.Services.Billing.PaymentCheckoutService>();
        services.AddScoped<IPaymentWebhookHandler, FactuTrust.Infrastructure.Services.Billing.PaymentWebhookHandler>();
        services.AddScoped<IPaymentIntentQueryService, FactuTrust.Infrastructure.Services.Billing.PaymentIntentQueryService>();

        // Withholding Tax (TEJ) Services
        services.AddScoped<IWithholdingTaxService, WithholdingTaxCalculationService>();
        services.AddScoped<ITejXmlGeneratorService, TejXmlGeneratorService>();
        services.AddScoped<ITejXmlValidatorService, TejXmlValidatorService>();
        services.AddScoped<ISupplierInvoiceTejDeclarationBuilder, SupplierInvoiceTejDeclarationBuilder>();
        services.AddScoped<IWithholdingComplianceService, WithholdingComplianceService>();

        services.Configure<FactuTrust.Infrastructure.Services.ProductImageSearchOptions>(
            configuration.GetSection(FactuTrust.Infrastructure.Services.ProductImageSearchOptions.SectionName));
        services.AddHttpClient("ProductImageSearch", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });
        services.AddScoped<IProductImageSearchService, ProductImageSearchService>();
        services.AddScoped<IProductImageStorageService, ProductImageStorageService>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // AI Assistant (Ollama + OpenAI-compatible providers)
        services.Configure<OllamaSettings>(configuration.GetSection(OllamaSettings.SectionName));
        services.Configure<ScreenAnalysisOptions>(configuration.GetSection(ScreenAnalysisOptions.SectionName));
        services.Configure<OpenRouterSettings>(configuration.GetSection(OpenRouterSettings.SectionName));
        services.Configure<CashDeskFeaturesOptions>(configuration.GetSection(CashDeskFeaturesOptions.SectionName));
        services.Configure<FixedAssetsOptions>(configuration.GetSection(FixedAssetsOptions.SectionName));
        services.Configure<AccountingAttachmentsOptions>(configuration.GetSection(AccountingAttachmentsOptions.SectionName));
        services.Configure<AccountingFirmsOptions>(configuration.GetSection(AccountingFirmsOptions.SectionName));
        services.Configure<AccountingSettings>(configuration.GetSection(AccountingSettings.SectionName));
        services.Configure<StorefrontOptions>(configuration.GetSection(StorefrontOptions.SectionName));
        services.Configure<ChannelsSettings>(configuration.GetSection(ChannelsSettings.SectionName));
        // Canaux externes (WhatsApp) : liaison d'identité en base tenant/master. Le pont Node et
        // l'envoi sortant (IChannelOutboundSender) sont hébergés côté API (processus enfant piloté).
        services.AddScoped<IChannelLinkService, FactuTrust.Infrastructure.Services.Channels.ChannelLinkService>();
        services.AddSingleton<IQrCodeGenerator, FactuTrust.Infrastructure.Services.QrCodeGenerator>();
        // Porte de concurrence des générations Ollama (singleton partagé par tout le processus) :
        // limite le nombre de générations LLM simultanées pour protéger un moteur mono-instance / CPU.
        services.AddSingleton<FactuTrust.Infrastructure.Services.AI.OllamaGenerationGate>();
        services.AddSingleton<IOllamaGenerationGate>(sp =>
            sp.GetRequiredService<FactuTrust.Infrastructure.Services.AI.OllamaGenerationGate>());
        services.AddHttpClient<IOllamaClient, OllamaHttpClient>((sp, client) =>
        {
            var settings = configuration.GetSection(OllamaSettings.SectionName).Get<OllamaSettings>() ?? new OllamaSettings();
            client.BaseAddress = new Uri(settings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        });
        services.AddHttpClient("OllamaKeepAlive", (sp, client) =>
        {
            var settings = configuration.GetSection(OllamaSettings.SectionName).Get<OllamaSettings>() ?? new OllamaSettings();
            client.BaseAddress = new Uri(settings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 5, 120));
        });
        services.AddHttpClient("OpenAiCompatible", (sp, client) =>
        {
            var settings = configuration.GetSection(OpenRouterSettings.SectionName).Get<OpenRouterSettings>() ?? new OpenRouterSettings();
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 10, 600));
        });
        services.AddScoped<IOpenAiChatCompletionsClient, OpenAiChatCompletionsClient>();
        services.AddSingleton<IAiDeterministicToolCache, AiDeterministicToolCache>();
        services.AddSingleton<IAiReadOnlyToolCache, AiReadOnlyToolCache>();
        services.AddScoped<IAiToolExecutor, AiToolExecutor>();
        services.AddSingleton<IAiToolExecutorScopeFactory, AiToolExecutorScopeFactory>();
        services.AddSingleton<IAiPdfRenderer, PdfToImagePdfRenderer>();
        services.AddSingleton<IAiOcrService, TesseractOcrService>();
        services.AddSingleton<IImagePreprocessingService, ImagePreprocessingService>();
        services.AddScoped<IAiDocumentTextExtractor, AiDocumentTextExtractor>();
        services.AddScoped<IAiContextBuilder, AiContextBuilder>();
        services.AddScoped<AiVolatileContextFormatter>();
        services.AddScoped<AiScreenAnalysisEnricher>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<ITenantAiProviderRepository, TenantAiProviderRepository>();
        services.AddScoped<SendChatMessageHandler>();
        services.AddScoped<ImportInvoiceFromFileHandler>();
        services.AddScoped<IOllamaModelReadinessChecker, OllamaModelReadinessChecker>();
        services.AddScoped<IAiModelRecommender, OllamaModelRecommender>();
        services.AddScoped<IPlatformAiSettingsService, FactuTrust.Infrastructure.Services.AI.PlatformAiSettingsService>();
        services.AddScoped<IOllamaInferenceProfileResolver, OllamaInferenceProfileResolver>();

        // AI Export — PowerPoint generation (Clean Architecture: contracts in Application, implementations here).
        services.Configure<FactuTrust.Infrastructure.Services.AI.Export.Storage.ExportStorageOptions>(
            configuration.GetSection("AiExport:Storage"));
        services.Configure<FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering.PowerPointValidationOptions>(
            configuration.GetSection("AiExport:Validation"));
        services.Configure<FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering.PowerPointRenderingOptions>(
            configuration.GetSection(FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering.PowerPointRenderingOptions.SectionName));
        services.AddScoped<FactuTrust.Application.Common.Interfaces.Repositories.IAiExportAuditRepository,
            FactuTrust.Infrastructure.Repositories.AiExportAuditRepository>();
        services.AddSingleton<FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes.IBrandAssetProvider,
            FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes.FilesystemBrandAssetProvider>();
        services.AddSingleton<FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes.IPowerPointThemeResolver,
            FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes.PowerPointThemeResolver>();
        services.AddSingleton<FactuTrust.Application.Features.AI.Export.PowerPoint.Services.IPowerPointTemplateCatalog,
            FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.PowerPointTemplateCatalog>();
        services.AddSingleton<FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Templates.IPowerPointBaseTemplateRepository,
            FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Templates.PowerPointBaseTemplateRepository>();
        services.AddHostedService<FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Templates.PowerPointThemePreviewBootstrapper>();
        services.AddScoped<FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.HybridTemplateGenerator>();
        services.AddScoped<FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.PowerPointGenerator>();
        services.AddScoped<FactuTrust.Application.Features.AI.Export.PowerPoint.Services.IPowerPointGenerator,
            FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.PowerPointGenerationEngineRouter>();
        services.AddScoped<FactuTrust.Application.Features.AI.Export.PowerPoint.Services.IPowerPointExportPreviewService,
            FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.PowerPointExportPreviewService>();
        services.AddScoped<FactuTrust.Application.Features.AI.Export.PowerPoint.Services.IExportStorageService,
            FactuTrust.Infrastructure.Services.AI.Export.Storage.FilesystemExportStorageService>();
        services.AddScoped<FactuTrust.Infrastructure.Services.Background.AiExportCleanupJob>();
        if (!IsDesignTime())
        {
            services.AddHostedService<OcrStartupCheckHostedService>();
            services.AddHostedService<OllamaKeepAliveService>();
            services.AddHostedService<StorefrontProjectionSyncService>();
        }

        // ────────────────────────────────────────────────────────────────────
        //  AI Forecasting module
        //  Always bind options (the executor / context-builder consult them
        //  to decide whether to expose forecasting hints/tools), but only
        //  register the scoped services and hosted background workers when
        //  the feature flag is enabled. This guarantees zero impact when the
        //  module is off.
        // ────────────────────────────────────────────────────────────────────
        services.Configure<ForecastingOptions>(configuration.GetSection(ForecastingOptions.SectionName));
        var forecastingOptions = configuration.GetSection(ForecastingOptions.SectionName).Get<ForecastingOptions>() ?? new ForecastingOptions();
        if (forecastingOptions.Enabled)
        {
            services.AddScoped<ITunisianCalendarService, TunisianCalendarService>();
            services.AddScoped<IForecastingService, ForecastingService>();
            services.AddScoped<IPromotionRecommendationService, PromotionRecommendationService>();
            services.AddScoped<IAbcXyzClassifier, AbcXyzClassifier>();
            services.AddScoped<IForecastRecomputeOrchestrator, ForecastRecomputeOrchestrator>();

            // Replenishment — single registration since the V1 cutover (2026-05-13).
            services.AddScoped<IReplenishmentService, Services.Forecasting.ReplenishmentService>();
            services.AddScoped<IPurchaseOrderDraftFactory, Services.PurchaseOrders.PurchaseOrderDraftFactory>();

            if (!IsDesignTime() && forecastingOptions.BackgroundRecomputeEnabled)
                services.AddHostedService<ForecastRecomputationService>();
            if (!IsDesignTime() && forecastingOptions.PromotionDetectorEnabled)
                services.AddHostedService<PromotionWindowDetectorService>();
        }

        return services;
    }

    private static bool IsDesignTime()
    {
        // EF Core tools set this environment variable
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EF_DOTNET_DESIGN_TIME")))
        {
            return true;
        }

        // In design-time, there's no entry assembly (e.g., when running EF Core tools)
        // This is the most reliable way to detect design-time mode
        return Assembly.GetEntryAssembly() == null;
    }
}

/// <summary>
/// Unit of Work implementation.
/// </summary>
/// <remarks>
/// <see cref="BeginTransactionAsync"/> calls <c>Database.BeginTransactionAsync</c> directly. With tenant SQL Server retry enabled,
/// callers must not use these methods unless the work is also wrapped in <c>CreateExecutionStrategy().ExecuteAsync</c>.
/// </remarks>
public sealed class UnitOfWork : IUnitOfWork, IDisposable, IAsyncDisposable
{
    private readonly TenantDbContextFactory _contextFactory;
    private TenantDbContext? _context;
    private bool _disposed;

    public UnitOfWork(TenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private TenantDbContext Context => _context ??= _contextFactory.CreateContext();

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await Context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        await Context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        await Context.Database.CommitTransactionAsync(cancellationToken);
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        await Context.Database.RollbackTransactionAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _context?.Dispose();
        _context = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        if (_context is not null)
        {
            await _context.DisposeAsync();
            _context = null;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
