using FactuTrust.Application;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Studio.Workflows.Engine;
using FactuTrust.Application.Features.Studio.Workflows.Spec;
using FactuTrust.Infrastructure;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// PR 4.1h — garde DI des workflows Studio : les 7 handlers d'étapes sont enregistrés dans le
/// conteneur de production (<c>FactuTrust.Infrastructure.DependencyInjection.AddInfrastructure</c>)
/// avec des <see cref="IStudioWorkflowStepHandler.StepType"/> distincts correspondant exactement à
/// <see cref="StudioWorkflowStepTypes.All"/>. Patron repris de
/// <c>AccountingNonRegressionTests.BuildAccountingHandlersProvider</c> : vraies extensions de
/// production (<c>AddApplication</c> + <c>AddInfrastructure</c>, comme Program.cs), dépendances de
/// persistance/services remplacées par des mocks loose pour ne pas exiger un vrai SQL Server.
/// </summary>
public sealed class StudioWorkflowDependencyInjectionTests
{
    [Fact]
    public void Seven_step_handlers_are_registered_with_distinct_step_types()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MasterConnection"] = "Server=(localdb)\\mssqllocaldb;Database=FactuTrust_DiProbe_Unused;Trusted_Connection=True;TrustServerCertificate=True;"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Câblage de production réel (Program.cs) — voir doc de classe ci-dessus.
        services.AddApplication();
        services.AddInfrastructure(configuration);

        // Dépendances des 7 constructeurs de handlers remplacées (la dernière inscription l'emporte
        // pour une résolution simple) — aucun DbContext n'est construit ici.
        RegisterLooseMock<INotificationService>(services);
        RegisterLooseMock<IStudioWorkflowRepository>(services);
        RegisterLooseMock<ICustomEntityRepository>(services);
        RegisterLooseMock<ICustomFieldRepository>(services);
        RegisterLooseMock<ICustomRecordRepository>(services);
        RegisterLooseMock<IStudioQuotaService>(services);
        RegisterLooseMock<IStudioComputedFieldWriter>(services);
        RegisterLooseMock<ICustomAutomationRepository>(services);
        RegisterLooseMock<IAiToolExecutor>(services);
        RegisterLooseMock<IPublisher>(services);

        using var provider = services.BuildServiceProvider();

        var handlers = provider.GetServices<IStudioWorkflowStepHandler>().ToList();

        Assert.Equal(7, handlers.Count);
        Assert.Equal(
            StudioWorkflowStepTypes.All.OrderBy(t => t, StringComparer.Ordinal),
            handlers.Select(h => h.StepType).OrderBy(t => t, StringComparer.Ordinal));
    }

    private static void RegisterLooseMock<T>(IServiceCollection services) where T : class
        => services.AddSingleton(new Mock<T>().Object);
}
