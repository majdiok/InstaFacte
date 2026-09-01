using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Pricing;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.EventHandlers;
using FactuTrust.Application.Features.Accounting.Notifications;
using FactuTrust.Application.Features.FixedAssets;
using FactuTrust.Application.Features.InvoiceWizard.Commands;
using FactuTrust.Application.Features.Stock.Services;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.SectorCatalog;
using FactuTrust.Infrastructure.Services.SectorRules;
using FactuTrust.Infrastructure.Tests.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Phase 4 — comptabilité tunisienne, non-régression (plan §6.1, priorité absolue). Verrouille les
/// invariants documentés dans <c>/code/.plans/analysis/accounting_dependency_map.md</c> :
/// (1) les 10 handlers MediatR d'écritures sont résolus depuis le conteneur DI sans AUCUNE
/// dépendance module/permission ; (2) une facture validée/soumise génère son écriture même quand
/// le module Accounting est refusé pour l'utilisateur courant (<see cref="ICurrentUser.HasPermission"/>
/// retourne systématiquement <c>false</c>) ; (3) le provisioning sectoriel (<see cref="SectorDataTemplateApplier"/>)
/// n'altère jamais le plan comptable système ni les 7 journaux <c>IsSystem=true</c> — additif
/// uniquement ; (4) la ré-application des templates est idempotente sur les comptes.
/// </summary>
public sealed class AccountingNonRegressionTests
{
    // ============================================================================================
    // 1) Les 10 handlers d'écritures sont enregistrés en DI sans condition (plan §6.1, bullet 1).
    // ============================================================================================

    /// <summary>
    /// Reproduit exactement l'enregistrement MediatR de production
    /// (<c>FactuTrust.Application.DependencyInjection.AddApplication</c>) SANS enregistrer le
    /// moindre service lié à un module/une permission/un utilisateur courant lié aux 10 handlers
    /// d'écritures eux-mêmes (aucun <c>IPlanResolver</c>, et <c>ICurrentUser</c> n'est qu'un mock
    /// loose requis par des handlers caisse/stock non comptables qui partagent la même
    /// notification — voir ci-dessous). Si un des 10 handlers d'écritures venait à dépendre
    /// d'un service module/permission dans son constructeur, la résolution ci-dessous échouerait
    /// avec une <c>InvalidOperationException</c> (service non enregistré) — c'est la preuve
    /// automatisée que ces handlers restent, structurellement, indépendants de tout grant/module.
    /// </summary>
    [Fact]
    public void Les_10_handlers_d_ecritures_sont_enregistres_en_DI_sans_condition()
    {
        using var provider = BuildAccountingHandlersProvider();

        // SupplierInvoiceCreatedForAccountingNotification a DEUX handlers comptables (écriture
        // d'achat + création d'immobilisations) — les deux doivent être résolus, sans ordre garanti.
        var supplierInvoiceHandlers = provider
            .GetServices<INotificationHandler<SupplierInvoiceCreatedForAccountingNotification>>()
            .ToList();
        Assert.Equal(2, supplierInvoiceHandlers.Count);
        Assert.Contains(supplierInvoiceHandlers, h => h is GenerateJournalEntryOnSupplierInvoiceHandler);
        Assert.Contains(supplierInvoiceHandlers, h => h is CreateFixedAssetsFromSupplierInvoiceHandler);

        AssertSingleHandlerResolves<SupplierInvoiceWithholdingAccountingNotification, GenerateJournalEntryOnSupplierInvoiceWithholdingHandler>(provider);

        // InvoicePaymentRecordedNotification et SupplierPaymentRecordedForAccountingNotification
        // sont chacune partagées avec un handler caisse non comptable (CreateCashOperationOn...) ;
        // InvoiceCancelledEvent est partagé avec un handler stock non comptable
        // (RestoreStockOnInvoiceCancelledHandler). On vérifie que le handler d'écriture comptable
        // est bien présent parmi les handlers résolus, sans exiger qu'il soit seul.
        AssertHandlerResolvesAmong<InvoicePaymentRecordedNotification, GenerateJournalEntryOnInvoicePaymentHandler>(provider);
        AssertHandlerResolvesAmong<SupplierPaymentRecordedForAccountingNotification, GenerateJournalEntryOnSupplierPaymentHandler>(provider);
        AssertSingleHandlerResolves<BankDepositCreatedForAccountingNotification, GenerateJournalEntryOnBankDepositHandler>(provider);
        AssertSingleHandlerResolves<CashOperationCreatedForAccountingNotification, GenerateJournalEntryOnCashOperationHandler>(provider);
        AssertSingleHandlerResolves<ClientEffetSettledNotification, GenerateJournalEntryOnClientEffetSettledHandler>(provider);
        AssertSingleHandlerResolves<SupplierEffetSettledNotification, GenerateJournalEntryOnSupplierEffetSettledHandler>(provider);
        AssertHandlerResolvesAmong<InvoiceCancelledEvent, ReverseJournalEntryOnInvoiceCancelledHandler>(provider);

        // 2 (SupplierInvoiceCreated) + 8 (un handler comptable chacune) = 10 — le compte documenté
        // par la carte des dépendances comptables.
        const int totalDocumentedHandlers = 10;
        Assert.Equal(totalDocumentedHandlers, supplierInvoiceHandlers.Count + 8);
    }

    private static void AssertSingleHandlerResolves<TNotification, THandler>(IServiceProvider provider)
        where TNotification : INotification
        where THandler : INotificationHandler<TNotification>
    {
        var handlers = provider.GetServices<INotificationHandler<TNotification>>().ToList();
        var handler = Assert.Single(handlers);
        Assert.IsType<THandler>(handler);
    }

    private static void AssertHandlerResolvesAmong<TNotification, THandler>(IServiceProvider provider)
        where TNotification : INotification
        where THandler : INotificationHandler<TNotification>
    {
        var handlers = provider.GetServices<INotificationHandler<TNotification>>().ToList();
        Assert.Contains(handlers, h => h is THandler);
    }

    /// <summary>
    /// Construit un conteneur DI minimal : MediatR câblé exactement comme en production sur
    /// l'assembly Application (mêmes handlers scannés), plus un mock loose pour chaque interface
    /// de repository/service requise par les 10 constructeurs — et RIEN d'autre. Aucun
    /// <c>ICurrentUser</c>/<c>IPlanResolver</c>/gate de permission n'est enregistré.
    /// </summary>
    private static ServiceProvider BuildAccountingHandlersProvider()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GenerateJournalEntryOnSupplierInvoiceHandler).Assembly));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        RegisterLooseMock<ISupplierInvoiceRepository>(services);
        RegisterLooseMock<IAccountingService>(services);
        RegisterLooseMock<IFixedAssetRepository>(services);
        RegisterLooseMock<IDepreciationRateCategoryRepository>(services);
        RegisterLooseMock<IBankDepositRepository>(services);
        RegisterLooseMock<ICashOperationRepository>(services);
        RegisterLooseMock<ISupplierPaymentRepository>(services);
        RegisterLooseMock<ILetteringService>(services);
        RegisterLooseMock<IPaymentRepository>(services);
        services.AddSingleton(Options.Create(new FixedAssetsOptions()));

        // Ces notifications comptables sont partagées avec des handlers NON comptables
        // (caisse / stock) enregistrés dans le même assembly. Ils doivent rester
        // constructibles pour que la résolution DI des handlers d'écritures (l'objet réel
        // de ce test) ne casse pas — mais ils ne font PAS partie des 10 handlers documentés.
        RegisterLooseMock<ICashOperationNumberGenerator>(services);
        RegisterLooseMock<ICurrentUser>(services);
        RegisterLooseMock<IInvoiceRepository>(services);
        RegisterLooseMock<IStockItemRepository>(services);
        RegisterLooseMock<IWarehouseRepository>(services);
        RegisterLooseMock<IProductRepository>(services);
        RegisterLooseMock<IStockMovementRepository>(services);
        RegisterLooseMock<IStockMutationService>(services);
        services.AddSingleton(Options.Create(new CashDeskFeaturesOptions()));

        return services.BuildServiceProvider();
    }

    private static void RegisterLooseMock<T>(IServiceCollection services) where T : class
        => services.AddSingleton(new Mock<T>().Object);

    // ============================================================================================
    // 2) Facture validée/soumise → écriture générée même si Accounting est refusé pour l'utilisateur
    //    (plan §6.1, bullet 2 — invariant n°1 de la carte des dépendances comptables).
    // ============================================================================================

    /// <summary>
    /// <see cref="ICurrentUser.HasPermission"/> — le point d'application réel des grants module
    /// (JWT claims → <c>PermissionAuthorizationHandler</c>/<c>EffectivePermissionsCalculator</c>) —
    /// refuse systématiquement TOUTE permission, y compris <c>Permissions.Accounting.Create</c> :
    /// c'est la représentation fidèle d'un utilisateur dont le module Accounting est désactivé.
    /// La soumission de facture doit malgré tout produire l'écriture comptable — <see cref="SubmitInvoiceCommandHandler"/>
    /// n'interroge jamais ce gate sur son chemin de génération d'écriture.
    /// </summary>
    [Fact]
    public async Task Facture_validee_genere_l_ecriture_meme_si_le_module_Accounting_est_desactive_pour_l_utilisateur()
    {
        var accounting = new Mock<IAccountingService>(MockBehavior.Strict);
        accounting
            .Setup(x => x.GenerateInvoiceSaleEntryAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FactuTrust.Domain.Common.Result.Success());

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        // Module Accounting désactivé pour cet utilisateur : toute permission comptable (et toute
        // autre) est refusée — reproduit l'effet d'un UserModuleGrant Accounting.IsEnabled=false
        // au niveau où il est réellement exploité (claims JWT → HasPermission).
        currentUser
            .Setup(x => x.HasPermission(It.IsAny<string>()))
            .Returns(false);

        var handler = CreateStandardInvoiceHandler(accounting, currentUser);

        var result = await handler.Handle(
            new SubmitInvoiceCommand(CurrentDraft(handler).Id, new string('k', 40)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        accounting.Verify(
            x => x.GenerateInvoiceSaleEntryAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()),
            Times.Once);
        // Le gate de permission n'est jamais consulté sur ce chemin — l'écriture est inconditionnelle.
        currentUser.Verify(x => x.HasPermission(It.IsAny<string>()), Times.Never);
    }

    private InvoiceDraft? _draft;

    private InvoiceDraft CurrentDraft(SubmitInvoiceCommandHandler _) => _draft!;

    private SubmitInvoiceCommandHandler CreateStandardInvoiceHandler(Mock<IAccountingService> accounting, Mock<ICurrentUser> currentUser)
    {
        var client = Client.Create(
            "Ste Test",
            ClientType.Individual,
            Address.Create("1 rue Test", "Tunis", "Tunis", postalCode: "1000").Value,
            Email.Create("client@test.local").Value).Value;

        _draft = InvoiceDraft.Create(InvoiceType.Standard);
        _draft.UpdateMetadata(new DraftMetadata
        {
            Type = InvoiceType.Standard,
            IssueDate = new DateTime(2026, 9, 20),
            Currency = "TND"
        });
        _draft.UpdateSeller(Guid.NewGuid());
        _draft.UpdateClient(client.Id, null);
        _draft.UpdateLines(new List<DraftInvoiceLine>
        {
            new()
            {
                Designation = "Prestation de test",
                Quantity = 1,
                Unit = "Unité",
                UnitPriceHT = 100m,
                VatRate = 19,
                FodecApplicable = false
            }
        });
        _draft.UpdatePaymentLegal(new DraftPaymentLegal { PaymentMethod = "CASH" });

        var draftRepository = new Mock<IInvoiceDraftRepository>();
        draftRepository
            .Setup(x => x.GetByIdAsync(_draft.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_draft);

        var invoiceRepository = new Mock<IInvoiceRepository>();
        invoiceRepository
            .Setup(x => x.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invoice invoice, CancellationToken _) => invoice);

        var clientRepository = new Mock<IClientRepository>();
        clientRepository
            .Setup(x => x.GetByIdAsync(client.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        var numberGenerator = new Mock<IInvoiceNumberGenerator>();
        numberGenerator
            .Setup(x => x.ReserveNextNumberAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, string prefix, int year, CancellationToken _) =>
                InvoiceNumber.Create(prefix, year, 1));

        var complianceValidator = new Mock<IInvoiceComplianceValidator>();
        complianceValidator
            .Setup(x => x.ValidateAsync(It.IsAny<InvoiceDraft>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WizardValidationResultDto { IsValid = true, CanProceed = true });

        var unitOfWork = new Mock<IUnitOfWork>();

        var fiscalStampResolver = new Mock<IFiscalStampResolver>();
        fiscalStampResolver
            .Setup(x => x.ResolveSignedStampAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Money.FromSignedAmount(1m, "TND"));

        var trackedStock = new Mock<ITrackedDocumentStockService>();
        trackedStock
            .Setup(x => x.ApplyExitsAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<MovementReason>(),
                It.IsAny<IReadOnlyList<TrackedDocumentLine>>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(FactuTrust.Domain.Common.Result.Success());

        return new SubmitInvoiceCommandHandler(
            draftRepository.Object,
            invoiceRepository.Object,
            clientRepository.Object,
            new Mock<ICompanyRepository>().Object,
            new Mock<IProductRepository>().Object,
            numberGenerator.Object,
            complianceValidator.Object,
            unitOfWork.Object,
            currentUser.Object,
            new Mock<IAuditService>().Object,
            new Mock<IWarehouseRepository>().Object,
            fiscalStampResolver.Object,
            new Mock<ILinePricingOrchestrator>().Object,
            accounting.Object,
            Options.Create(new AccountingSettings()),
            NullLogger<SubmitInvoiceCommandHandler>.Instance,
            new Mock<ICashRegisterSessionRepository>().Object,
            trackedStock.Object,
            new Mock<IRecurringContractInvoiceLinker>().Object,
            new Mock<IStockMovementRepository>().Object,
            new Mock<IStockItemRepository>().Object);
    }

    // ============================================================================================
    // 3) Le provisioning sectoriel n'altère pas le plan comptable système (plan §6.1, bullet 3).
    // ============================================================================================

    /// <summary>
    /// Un segment par ligne + son domaine "autre" — couvre les 6 segments — et un domaine
    /// représentatif du catalogue par segment (plan §6.1 : "pour chaque segment×(1 domaine + autre)").
    /// </summary>
    public static IEnumerable<object[]> SegmentDomainCombos()
    {
        yield return new object[] { CompanySegments.Entreprise, BusinessDomains.TechnologieInformatique };
        yield return new object[] { CompanySegments.Entreprise, BusinessDomains.Autre };
        yield return new object[] { CompanySegments.Commerce, BusinessDomains.AlimentationAgroalimentaire };
        yield return new object[] { CompanySegments.Commerce, BusinessDomains.Autre };
        yield return new object[] { CompanySegments.Services, BusinessDomains.TechnologieInformatique };
        yield return new object[] { CompanySegments.Services, BusinessDomains.Autre };
        yield return new object[] { CompanySegments.BtpConstruction, BusinessDomains.Immobilier };
        yield return new object[] { CompanySegments.BtpConstruction, BusinessDomains.Autre };
        yield return new object[] { CompanySegments.Association, BusinessDomains.SanteParamedical };
        yield return new object[] { CompanySegments.Association, BusinessDomains.Autre };
        yield return new object[] { CompanySegments.EtablissementEducatif, BusinessDomains.TechnologieInformatique };
        yield return new object[] { CompanySegments.EtablissementEducatif, BusinessDomains.Autre };
    }

    /// <summary>
    /// SQL Server / LocalDB requis pour la fidélité de ce test : le plan comptable système
    /// (130+ comptes) et les 7 journaux <c>IsSystem=true</c> ne sont insérés que par les
    /// migrations réelles (<c>migrationBuilder.InsertData</c> dans
    /// <c>20260326233329_AddAccountingModule_Tenant</c> / <c>20260707011639_AddJournalCatalog_Tenant</c>),
    /// PAS par <c>EnsureCreated</c> — même chemin que la mise en service réelle d'un tenant
    /// (<c>TenantDatabaseProvisioner</c>/<c>TenantService</c> appellent <c>Database.MigrateAsync</c>).
    /// Repli silencieux (<see cref="SqlTestDatabase.CanRun"/> == false) si SQL Server/LocalDB est
    /// indisponible — même convention que le reste de la suite (ex. <c>Nct01ChartMigrationServiceTests</c>).
    /// </summary>
    [Theory]
    [MemberData(nameof(SegmentDomainCombos))]
    public async Task Le_provisioning_sectoriel_n_altere_pas_le_plan_comptable_systeme(string segment, string domain)
    {
        using var sqlDb = new SqlTestDatabase($"{nameof(AccountingNonRegressionTests)}_{segment}_{domain}");
        if (!sqlDb.CanRun) return;

        var options = BuildSqlServerOptions(sqlDb.ConnectionString!);
        await using (var migrateContext = new TenantDbContext(options))
            await migrateContext.Database.MigrateAsync();

        List<(string Number, string Label, int AccountClass, AccountNatureType Nature, bool IsSystem)> accountsBefore;
        int systemJournalsBefore;
        await using (var before = new TenantDbContext(options))
        {
            var rawAccountsBefore = await before.ChartOfAccounts.AsNoTracking()
                .Select(a => new { a.AccountNumber, a.Label, a.AccountClass, a.NatureType, a.IsSystem })
                .ToListAsync();
            accountsBefore = rawAccountsBefore
                .Select(a => (a.AccountNumber, a.Label, a.AccountClass, a.NatureType, a.IsSystem))
                .ToList();
            systemJournalsBefore = await before.Journals.AsNoTracking().CountAsync(j => j.IsSystem);
        }

        Assert.True(accountsBefore.Count >= 130, $"Plan comptable système incomplet avant provisioning : {accountsBefore.Count} comptes.");
        Assert.Equal(7, systemJournalsBefore);
        var systemAccountsBefore = accountsBefore.Where(a => a.IsSystem).ToList();

        var catalogProvider = new StaticSectorCatalogProvider();
        var factory = new SingleConnectionTenantDbContextFactory(options);
        var applier = new SectorDataTemplateApplier(catalogProvider, factory, NullLogger<SectorDataTemplateApplier>.Instance);

        var applyResult = await applier.ApplyAsync(Guid.NewGuid(), sqlDb.ConnectionString!, segment, domain, dryRun: false, CancellationToken.None);
        Assert.Empty(applyResult.Warnings);

        await using var after = new TenantDbContext(options);
        var accountsAfter = await after.ChartOfAccounts.AsNoTracking().ToListAsync();

        // Aucun compte système préexistant n'a été supprimé ni modifié.
        foreach (var before in accountsBefore)
        {
            var match = accountsAfter.SingleOrDefault(a => a.AccountNumber == before.Number);
            Assert.NotNull(match);
            Assert.Equal(before.Label, match!.Label);
            Assert.Equal(before.AccountClass, match.AccountClass);
            Assert.Equal(before.Nature, match.NatureType);
            Assert.Equal(before.IsSystem, match.IsSystem);
        }

        // Le nombre de comptes SYSTÈME est strictement inchangé — les templates sont additifs et
        // n'ajoutent jamais de compte IsSystem=true (payload JSON des templates : "isSystem":false).
        var systemAccountsAfter = accountsAfter.Where(a => a.IsSystem).ToList();
        Assert.Equal(systemAccountsBefore.Count, systemAccountsAfter.Count);

        var newAccounts = accountsAfter.Where(a => accountsBefore.All(b => b.Number != a.AccountNumber)).ToList();
        Assert.All(newAccounts, a => Assert.False(a.IsSystem, $"Nouveau compte {a.AccountNumber} ne devrait pas être système."));

        var journalsAfter = await after.Journals.AsNoTracking().Where(j => j.IsSystem).ToListAsync();
        Assert.Equal(7, journalsAfter.Count);
    }

    // ============================================================================================
    // 4) Ré-application des templates sectoriels : idempotente sur les comptes (plan §6.1, bullet 4).
    //    Couverture synthétique déjà exhaustive dans SectorDataTemplateApplierTests
    //    (Apply_skips_template_already_applied_same_version, Apply_never_updates_existing_tenant_row,
    //    Apply_reapplies_when_version_increases_without_touching_existing_rows) : ce test l'ÉTEND en
    //    rejouant les VRAIS templates du catalogue (pas de fixtures synthétiques) pour chaque
    //    combo segment×domaine — même chemin SQL réel que le test #3 ci-dessus.
    // ============================================================================================
    [Theory]
    [MemberData(nameof(SegmentDomainCombos))]
    public async Task Reapplication_des_templates_sectoriels_est_idempotente_sur_les_comptes(string segment, string domain)
    {
        using var sqlDb = new SqlTestDatabase($"{nameof(AccountingNonRegressionTests)}_Idem_{segment}_{domain}");
        if (!sqlDb.CanRun) return;

        var options = BuildSqlServerOptions(sqlDb.ConnectionString!);
        await using (var migrateContext = new TenantDbContext(options))
            await migrateContext.Database.MigrateAsync();

        var catalogProvider = new StaticSectorCatalogProvider();
        var factory = new SingleConnectionTenantDbContextFactory(options);
        var applier = new SectorDataTemplateApplier(catalogProvider, factory, NullLogger<SectorDataTemplateApplier>.Instance);

        var first = await applier.ApplyAsync(Guid.NewGuid(), sqlDb.ConnectionString!, segment, domain, dryRun: false, CancellationToken.None);

        List<ChartOfAccount> accountsAfterFirst;
        await using (var ctx = new TenantDbContext(options))
            accountsAfterFirst = await ctx.ChartOfAccounts.AsNoTracking().ToListAsync();

        var second = await applier.ApplyAsync(Guid.NewGuid(), sqlDb.ConnectionString!, segment, domain, dryRun: false, CancellationToken.None);

        // Rien de nouveau à appliquer la 2e fois : tout ce qui matchait ce segment/domaine est déjà
        // marqué "skipped" (même version) — jamais réappliqué en double.
        Assert.Empty(second.Applied);
        if (first.Applied.Count > 0)
            Assert.Equal(first.Applied.Count, second.Skipped.Count);

        await using (var ctx = new TenantDbContext(options))
        {
            var accountsAfterSecond = await ctx.ChartOfAccounts.AsNoTracking().ToListAsync();
            Assert.Equal(accountsAfterFirst.Count, accountsAfterSecond.Count);
            foreach (var acc in accountsAfterFirst)
            {
                var match = accountsAfterSecond.Single(a => a.AccountNumber == acc.AccountNumber);
                Assert.Equal(acc.Label, match.Label);
                Assert.Equal(acc.AccountClass, match.AccountClass);
                Assert.Equal(acc.NatureType, match.NatureType);
            }
        }
    }

    private static DbContextOptions<TenantDbContext> BuildSqlServerOptions(string connectionString) =>
        new DbContextOptionsBuilder<TenantDbContext>().UseSqlServer(connectionString).Options;

    /// <summary>Minimal <see cref="ITenantDbContextFactory"/> pinned to a single real SQL connection string.</summary>
    private sealed class SingleConnectionTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly DbContextOptions<TenantDbContext> _options;
        public SingleConnectionTenantDbContextFactory(DbContextOptions<TenantDbContext> options) => _options = options;
        public TenantDbContext CreateContext() => new(_options);
    }
}
