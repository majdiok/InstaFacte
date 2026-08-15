using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Tools;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.AI;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Gardes de l'exécuteur des outils cabinet. Toutes les portes fermées doivent renvoyer une
/// <see cref="AiToolResult"/> en échec — jamais lever : une exception dans un outil interrompt
/// le flux SSE et laisse l'utilisateur devant une erreur générique.
/// </summary>
public sealed class FirmAgentToolExecutorTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static MasterDbContext BuildMaster() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ICurrentUser FirmManager()
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.TenantId).Returns(FirmId);
        user.SetupGet(u => u.UserId).Returns(UserId);
        user.SetupGet(u => u.Role).Returns(UserRole.FirmManager);
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.Email).Returns("manager@cabinet.tn");
        return user.Object;
    }

    private static FirmAgentToolExecutor Build(
        MasterDbContext master,
        ICurrentUser currentUser,
        AccountingFirmsOptions options,
        IFirmPortfolioReadService? portfolio = null) =>
        new(
            portfolio ?? Mock.Of<IFirmPortfolioReadService>(),
            Mock.Of<IFirmDossierAccessService>(),
            Mock.Of<ITenantService>(),
            Mock.Of<ITenantDbContextFactory>(),
            master,
            Mock.Of<IEmailService>(),
            currentUser,
            Options.Create(options),
            TimeProvider.System,
            NullLogger<FirmAgentToolExecutor>.Instance);

    private static AccountingFirmsOptions Enabled() =>
        new() { Enabled = true, FirmAgentEnabled = true };

    [Fact]
    public async Task Tools_are_refused_when_agent_flag_is_off()
    {
        await using var master = BuildMaster();
        var executor = Build(master, FirmManager(), new AccountingFirmsOptions { Enabled = true, FirmAgentEnabled = false });

        var result = await executor.ExecuteAsync(FirmAgentTools.PortfolioOverview, new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.Contains("pas activé", result.ErrorMessage);
    }

    [Fact]
    public async Task Tools_are_refused_without_firm_tenant_context()
    {
        await using var master = BuildMaster();
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.TenantId).Returns((Guid?)null);

        var executor = Build(master, user.Object, Enabled());
        var result = await executor.ExecuteAsync(FirmAgentTools.PortfolioOverview, new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.Contains("Contexte cabinet", result.ErrorMessage);
    }

    /// <summary>
    /// Fail-closed : un rôle non cabinet ne doit PAS retomber sur « aucun filtre ». C'est le
    /// pendant, côté outil, de l'invariant que <c>FirmPortfolioReadService</c> impose au job.
    /// </summary>
    [Fact]
    public async Task Tools_are_refused_when_access_scope_cannot_be_resolved()
    {
        await using var master = BuildMaster();
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.TenantId).Returns(FirmId);
        user.SetupGet(u => u.UserId).Returns(UserId);
        user.SetupGet(u => u.Role).Returns(UserRole.Administrator); // ni FirmManager ni FirmAccountant
        user.SetupGet(u => u.IsAuthenticated).Returns(true);

        var executor = Build(master, user.Object, Enabled());
        var result = await executor.ExecuteAsync(FirmAgentTools.PortfolioOverview, new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.Contains("Profil cabinet requis", result.ErrorMessage);
    }

    [Fact]
    public async Task Reminder_is_refused_when_its_own_flag_is_off()
    {
        await using var master = BuildMaster();
        var executor = Build(master, FirmManager(), Enabled()); // FirmAgentReminderToolEnabled = false

        var result = await executor.ExecuteAsync(
            FirmAgentTools.SendReminder,
            new Dictionary<string, object?>
            {
                ["deadline_id"] = Guid.NewGuid().ToString(),
                ["company_tenant_id"] = Guid.NewGuid().ToString()
            });

        Assert.False(result.Success);
        Assert.Contains("désactivé", result.ErrorMessage);
    }

    [Fact]
    public async Task Reminder_rejects_malformed_identifiers()
    {
        await using var master = BuildMaster();
        var options = Enabled();
        options.FirmAgentReminderToolEnabled = true;
        var executor = Build(master, FirmManager(), options);

        var result = await executor.ExecuteAsync(
            FirmAgentTools.SendReminder,
            new Dictionary<string, object?> { ["deadline_id"] = "pas-un-guid", ["company_tenant_id"] = "non-plus" });

        Assert.False(result.Success);
        Assert.Contains("invalide", result.ErrorMessage);
    }

    [Fact]
    public async Task Unknown_firm_tool_returns_error_not_exception()
    {
        await using var master = BuildMaster();
        var executor = Build(master, FirmManager(), Enabled());

        var result = await executor.ExecuteAsync("get_firm_unknown_thing", new Dictionary<string, object?>());

        Assert.False(result.Success);
    }

    /// <summary>
    /// L'outil cabinet routé par <c>AiToolExecutor</c> sans exécuteur enregistré (constructions de
    /// test historiques à 5 arguments) doit dégrader proprement.
    /// </summary>
    [Theory]
    [InlineData(FirmAgentTools.PortfolioOverview)]
    [InlineData(FirmAgentTools.FiscalDeadlines)]
    [InlineData(FirmAgentTools.DossierHealth)]
    [InlineData(FirmAgentTools.CollaboratorWorkload)]
    public async Task Ai_tool_executor_degrades_when_firm_executor_is_absent(string toolName)
    {
        var executor = new AiToolExecutor(
            Mock.Of<IMediator>(),
            NullLogger<AiToolExecutor>.Instance,
            TimeProvider.System,
            Mock.Of<ICurrentUser>(),
            Options.Create(new OllamaSettings()));

        var result = await executor.ExecuteAsync(toolName, new Dictionary<string, object?>(), AiToolExecutionContext.Empty);

        Assert.False(result.Success);
        Assert.Contains("Chef de mission", result.ErrorMessage);
    }

    [Fact]
    public void Firm_tool_names_match_the_registry()
    {
        foreach (var name in FirmAgentTools.All)
            Assert.NotNull(AiToolRegistry.GetToolDefinition(name));
    }
}
