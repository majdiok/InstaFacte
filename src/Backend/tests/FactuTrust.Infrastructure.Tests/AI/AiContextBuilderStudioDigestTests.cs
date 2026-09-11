using System.Reflection;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// PR 1.2 — digest de contexte dans le prompt StudioBuilder. Le prompt n'expose le « SCHÉMA EXISTANT »
/// que si <see cref="OllamaSettings.EnableStudioAiSchemaDigest"/> est actif ; le budget de caractères
/// transmis au service dépend du modèle retenu (CPU 1200 / avancé 4000) ; l'intention connue ajoute un
/// préambule, une intention inconnue n'ajoute rien ; la révision de cache passe à « v4 ».
/// </summary>
public sealed class AiContextBuilderStudioDigestTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string UserId = "11111111-2222-3333-4444-555555555555";

    private static OllamaSettings Settings(bool digestEnabled) => new()
    {
        EnableStudioAiSchemaDigest = digestEnabled,
        EnableStudioAiPlanPreview = true,
        EnableStudioAiModifyTools = true,
        StudioSchemaDigestMaxCharsCpu = 1200,
        StudioSchemaDigestMaxCharsAdvanced = 4000,
        StudioLastPlanDigestMaxChars = 600
    };

    private static AiContextBuilder Build(OllamaSettings settings, IStudioContextDigestService? digest)
    {
        var companyRepo = new Mock<ICompanyRepository>();
        companyRepo.Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Company?)null);

        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(t => t.TenantId).Returns(TenantId);

        return new AiContextBuilder(
            companyRepo.Object,
            tenant.Object,
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IOllamaInferenceProfileResolver>(),
            Options.Create(settings),
            TimeProvider.System,
            studioDigest: digest);
    }

    private static Mock<IStudioContextDigestService> DigestMock(string schema, string? lastPlan)
    {
        var digest = new Mock<IStudioContextDigestService>();
        digest.Setup(d => d.BuildSchemaDigestAsync(TenantId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(schema);
        digest.Setup(d => d.BuildLastPlanDigestAsync(TenantId, UserId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lastPlan);
        return digest;
    }

    private static StudioPromptOptions Opts(bool advanced = false, string? intent = null) =>
        new(advanced, intent, TenantId, UserId);

    [Fact]
    public async Task Flag_off_produces_no_schema_section_and_never_calls_the_digest_service()
    {
        var digest = DigestMock("- employes « Employés » : nom:text", "- [Terminé 10:00] Système « Congés »");
        var builder = Build(Settings(digestEnabled: false), digest.Object);

        var prompt = await builder.BuildSystemPromptAsync(
            AssistantMode.StudioBuilder, null, AssistantAgentScope.None, Opts(intent: "system"));

        Assert.DoesNotContain("SCHÉMA EXISTANT", prompt);
        Assert.DoesNotContain("DERNIER PLAN", prompt);
        Assert.DoesNotContain("11. Le SCHÉMA EXISTANT", prompt);
        digest.Verify(d => d.BuildSchemaDigestAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        digest.Verify(d => d.BuildLastPlanDigestAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        // L'intention, elle, ne dépend pas du digest.
        Assert.Contains("INTENTION DE L'UTILISATEUR : créer un système de plusieurs tables liées.", prompt);
    }

    [Fact]
    public async Task Flag_on_adds_schema_section_rules_11_and_12_and_last_plan()
    {
        var digest = DigestMock(
            "- employes « Employés » : nom:text, poste:select\n- conges « Congés » (systeme:gestion_conges) : employe:relation, debut:date",
            "- [En attente 09:41] Système « Gestion des congés » : 2 tables (Employés, Congés)");
        var builder = Build(Settings(digestEnabled: true), digest.Object);

        var prompt = await builder.BuildSystemPromptAsync(
            AssistantMode.StudioBuilder, null, AssistantAgentScope.None, Opts());

        Assert.Contains("11. Le SCHÉMA EXISTANT liste les tables déjà présentes avec leurs VRAIES clés.", prompt);
        Assert.Contains("12. Le DERNIER PLAN décrit ce qui vient d'être préparé ou créé.", prompt);
        Assert.Contains("SCHÉMA EXISTANT (tables Studio de ce client) :\n- employes « Employés » : nom:text, poste:select", prompt);
        Assert.Contains("(systeme:gestion_conges)", prompt);
        Assert.Contains("DERNIER PLAN :\n- [En attente 09:41] Système « Gestion des congés »", prompt);
        // Les sections précèdent l'exemple final : le modèle lit le schéma avant l'exemple.
        Assert.True(prompt.IndexOf("SCHÉMA EXISTANT", StringComparison.Ordinal) < prompt.IndexOf("EXEMPLE système congés", StringComparison.Ordinal));
        // Sans intention transmise : pas de préambule.
        Assert.DoesNotContain("INTENTION DE L'UTILISATEUR", prompt);
    }

    [Fact]
    public async Task Flag_on_without_tables_says_so_and_omits_rule_12_when_no_plan()
    {
        var digest = DigestMock(string.Empty, null);
        var builder = Build(Settings(digestEnabled: true), digest.Object);

        var prompt = await builder.BuildSystemPromptAsync(
            AssistantMode.StudioBuilder, null, AssistantAgentScope.None, Opts());

        Assert.Contains("SCHÉMA EXISTANT (tables Studio de ce client) :\nAucune table Studio pour l'instant.", prompt);
        Assert.Contains("11. Le SCHÉMA EXISTANT", prompt);
        Assert.DoesNotContain("12. Le DERNIER PLAN", prompt);
        Assert.DoesNotContain("DERNIER PLAN :", prompt);
    }

    [Theory]
    [InlineData(false, 1200)]
    [InlineData(true, 4000)]
    public async Task Schema_budget_depends_on_the_model_retained(bool advanced, int expectedBudget)
    {
        var digest = DigestMock("- t « T » : a:text", null);
        var builder = Build(Settings(digestEnabled: true), digest.Object);

        await builder.BuildSystemPromptAsync(
            AssistantMode.StudioBuilder, null, AssistantAgentScope.None, Opts(advanced));

        digest.Verify(d => d.BuildSchemaDigestAsync(TenantId, expectedBudget, It.IsAny<CancellationToken>()), Times.Once);
        digest.Verify(d => d.BuildLastPlanDigestAsync(TenantId, UserId, 600, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Digest_calls_are_sequential_schema_then_last_plan()
    {
        var order = new List<string>();
        var digest = new Mock<IStudioContextDigestService>();
        digest.Setup(d => d.BuildSchemaDigestAsync(TenantId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async () => { order.Add("schema:start"); await Task.Delay(10); order.Add("schema:end"); return string.Empty; });
        digest.Setup(d => d.BuildLastPlanDigestAsync(TenantId, UserId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async () => { order.Add("plan:start"); await Task.Delay(1); order.Add("plan:end"); return (string?)null; });
        var builder = Build(Settings(digestEnabled: true), digest.Object);

        await builder.BuildSystemPromptAsync(AssistantMode.StudioBuilder, null, AssistantAgentScope.None, Opts());

        Assert.Equal(["schema:start", "schema:end", "plan:start", "plan:end"], order);
    }

    [Theory]
    [InlineData("system", "créer un système de plusieurs tables liées")]
    [InlineData("TABLE", "créer une table simple")]
    [InlineData(" report ", "obtenir un état / rapport")]
    [InlineData("workflow", "automatiser un enchaînement d'étapes (bientôt disponible : explique-le sans inventer d'outil)")]
    public async Task Known_intent_adds_a_preamble_after_the_first_line(string intent, string expectedLabel)
    {
        var builder = Build(Settings(digestEnabled: true), DigestMock(string.Empty, null).Object);

        var prompt = await builder.BuildSystemPromptAsync(
            AssistantMode.StudioBuilder, null, AssistantAgentScope.None, Opts(intent: intent));

        var lines = prompt.Split('\n');
        Assert.Equal($"INTENTION DE L'UTILISATEUR : {expectedLabel}.", lines[1].TrimEnd());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("inconnu")]
    [InlineData("drop table")]
    public async Task Unknown_intent_adds_nothing(string? intent)
    {
        var builder = Build(Settings(digestEnabled: true), DigestMock(string.Empty, null).Object);

        var prompt = await builder.BuildSystemPromptAsync(
            AssistantMode.StudioBuilder, null, AssistantAgentScope.None, Opts(intent: intent));

        Assert.DoesNotContain("INTENTION DE L'UTILISATEUR", prompt);
        if (!string.IsNullOrEmpty(intent))
            Assert.DoesNotContain(intent, prompt);
    }

    [Fact]
    public async Task Without_studio_options_or_service_the_prompt_is_unchanged()
    {
        // Constructions historiques (pas de service injecté) ou appel sans options : aucune section.
        var builder = Build(Settings(digestEnabled: true), digest: null);

        var withoutOptions = await builder.BuildSystemPromptAsync(AssistantMode.StudioBuilder);
        var withOptions = await builder.BuildSystemPromptAsync(
            AssistantMode.StudioBuilder, null, AssistantAgentScope.None, Opts(intent: "table"));

        Assert.DoesNotContain("SCHÉMA EXISTANT", withoutOptions);
        Assert.DoesNotContain("SCHÉMA EXISTANT", withOptions);
        Assert.Contains("INTENTION DE L'UTILISATEUR : créer une table simple.", withOptions);
    }

    [Fact]
    public async Task Digest_failure_degrades_to_a_prompt_without_context_sections()
    {
        // Un dépôt en erreur (SQL indisponible…) ne doit jamais faire échouer le tour Studio :
        // le prompt est produit sans « SCHÉMA EXISTANT », l'intention est conservée.
        var digest = new Mock<IStudioContextDigestService>();
        digest.Setup(d => d.BuildSchemaDigestAsync(TenantId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SQL indisponible"));
        var builder = Build(Settings(digestEnabled: true), digest.Object);

        var prompt = await builder.BuildSystemPromptAsync(
            AssistantMode.StudioBuilder, null, AssistantAgentScope.None, Opts(intent: "table"));

        Assert.DoesNotContain("SCHÉMA EXISTANT", prompt);
        Assert.DoesNotContain("11. Le SCHÉMA EXISTANT", prompt);
        Assert.Contains("INTENTION DE L'UTILISATEUR : créer une table simple.", prompt);
        Assert.Contains("EXEMPLE système congés", prompt);
    }

    [Fact]
    public async Task Cancellation_during_digest_is_propagated()
    {
        var digest = new Mock<IStudioContextDigestService>();
        digest.Setup(d => d.BuildSchemaDigestAsync(TenantId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var builder = Build(Settings(digestEnabled: true), digest.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(() => builder.BuildSystemPromptAsync(
            AssistantMode.StudioBuilder, null, AssistantAgentScope.None, Opts()));
    }

    [Fact]
    public async Task Default_mode_ignores_studio_options()
    {
        var digest = DigestMock("- t « T » : a:text", null);
        var builder = Build(new OllamaSettings { EnableStudioAiSchemaDigest = true, UseCompactChatPrompt = false, UseCompactChatPromptOnCpu = false }, digest.Object);

        var prompt = await builder.BuildSystemPromptAsync(
            AssistantMode.Default, null, AssistantAgentScope.None, Opts(intent: "system"));

        Assert.DoesNotContain("SCHÉMA EXISTANT", prompt);
        Assert.DoesNotContain("INTENTION DE L'UTILISATEUR", prompt);
        digest.VerifyNoOtherCalls();
    }

    [Fact]
    public void System_prompt_cache_revision_is_v4()
    {
        var field = typeof(AiContextBuilder).GetField(
            "SystemPromptCacheRevision",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        Assert.Equal("v4", (string)field!.GetRawConstantValue()!);
    }

    [Theory]
    [InlineData("system", "system")]
    [InlineData("  Relations ", "relations")]
    [InlineData("reference_data", "reference_data")]
    [InlineData("unknown", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void StudioPromptOptions_normalizes_intent(string? raw, string? expected)
    {
        var options = new StudioPromptOptions(false, raw, TenantId, UserId);
        Assert.Equal(expected, options.NormalizedIntent);
    }
}
