using DocumentFormat.OpenXml.Packaging;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Templates;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using P = DocumentFormat.OpenXml.Presentation;

namespace FactuTrust.Infrastructure.Tests.AI.Export.PowerPoint;

public sealed class HybridTemplateGeneratorTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-0000000000B1");

    [Theory]
    [InlineData(PowerPointTemplate.TechReport)]
    [InlineData(PowerPointTemplate.MinimalBusiness)]
    [InlineData(PowerPointTemplate.DarkCinematic)]
    public async Task GenerateAsync_hybrid_pilot_themes_produce_valid_pptx(PowerPointTemplate template)
    {
        var conversation = BuildConversation(out var messageId);
        var generator = BuildHybridGenerator(conversation);

        var result = await generator.GenerateAsync(BuildRequest(conversation.Id, messageId, template), UserId);

        Assert.True(result.Content.Length > 4_000);
        using var ms = new MemoryStream(result.Content, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        Assert.NotNull(doc.PresentationPart?.SlideMasterParts.First().ThemePart?.Theme);
        Assert.True(doc.PresentationPart!.SlideParts.Any());
    }

    [Fact]
    public async Task GenerateAsync_hybrid_cover_preserves_master_background()
    {
        var conversation = BuildConversation(out var messageId);
        var generator = BuildHybridGenerator(conversation);

        var result = await generator.GenerateAsync(
            BuildRequest(conversation.Id, messageId, PowerPointTemplate.DarkCinematic, includeCover: true),
            UserId);

        using var ms = new MemoryStream(result.Content, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        var masterShapes = doc.PresentationPart!.SlideMasterParts.First()
            .SlideMaster!.CommonSlideData!.ShapeTree!.Descendants<P.Shape>()
            .Select(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        Assert.Contains("MasterBackground", masterShapes);
        Assert.Contains("MasterAccentBar", masterShapes);
    }

    [Fact]
    public async Task Router_uses_legacy_when_hybrid_flag_disabled()
    {
        var conversation = BuildConversation(out var messageId);
        var router = BuildRouter(conversation, new PowerPointRenderingOptions
        {
            ExtendedThemeLibraryEnabled = true,
            TemplateHybridEnabled = false
        });

        var result = await router.GenerateAsync(
            BuildRequest(conversation.Id, messageId, PowerPointTemplate.TechReport),
            UserId);

        Assert.True(result.SlideCount > 0);
    }

    [Fact]
    public async Task Router_uses_hybrid_when_flag_enabled()
    {
        var conversation = BuildConversation(out var messageId);
        var router = BuildRouter(conversation, new PowerPointRenderingOptions
        {
            ExtendedThemeLibraryEnabled = true,
            TemplateHybridEnabled = true,
            HybridThemeMinId = 3
        });

        var result = await router.GenerateAsync(
            BuildRequest(conversation.Id, messageId, PowerPointTemplate.TechReport),
            UserId);

        Assert.True(result.SlideCount > 0);
        using var ms = new MemoryStream(result.Content, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        var masterNames = doc.PresentationPart!.SlideMasterParts.First()
            .SlideMaster!.CommonSlideData!.ShapeTree!.Descendants<P.Shape>()
            .Select(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value);
        Assert.Contains("MasterBackground", masterNames);
    }

    [Theory]
    [InlineData(PowerPointTemplate.Standard)]
    [InlineData(PowerPointTemplate.Analyse)]
    [InlineData(PowerPointTemplate.Executive)]
    public async Task Router_keeps_themes_0_2_on_legacy_even_when_hybrid_enabled(PowerPointTemplate template)
    {
        var conversation = BuildConversation(out var messageId);
        var legacy = BuildLegacyGenerator(conversation);
        var router = BuildRouter(conversation, new PowerPointRenderingOptions
        {
            ExtendedThemeLibraryEnabled = true,
            TemplateHybridEnabled = true,
            HybridThemeMinId = 3
        });

        var request = BuildRequest(conversation.Id, messageId, template);
        var legacyResult = await legacy.GenerateAsync(request, UserId);
        var routerResult = await router.GenerateAsync(request, UserId);

        Assert.Equal(legacyResult.SlideCount, routerResult.SlideCount);
    }

    public static IEnumerable<object[]> AllHybridTemplates =>
        PowerPointThemeLibrary.AllDefinitions
            .Where(d => d.Engine == PowerPointThemeEngine.Hybrid)
            .Select(d => new object[] { d.Template });

    [Theory]
    [MemberData(nameof(AllHybridTemplates))]
    public async Task GenerateAsync_all_hybrid_themes_valid_openxml(PowerPointTemplate template)
    {
        var conversation = BuildConversation(out var messageId);
        var generator = BuildHybridGenerator(conversation);

        var result = await generator.GenerateAsync(
            BuildRequest(conversation.Id, messageId, template),
            UserId);

        using var ms = new MemoryStream(result.Content, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        Assert.NotNull(doc.PresentationPart);
        Assert.True(doc.PresentationPart!.SlideParts.Any());
    }

    [Fact]
    public async Task GenerateAsync_invoice_list_hybrid_theme_produces_valid_pptx()
    {
        var conversation = BuildInvoiceListConversation(out var messageId);
        var generator = BuildHybridGenerator(conversation, strictValidation: true);

        var result = await generator.GenerateAsync(
            new PowerPointExportRequestDto
            {
                Title = "Synthèse — Liste factures",
                Template = PowerPointTemplate.Vortex,
                Orientation = SlideOrientation.Widescreen16x9,
                IncludeCoverSlide = true,
                IncludeAgenda = true,
                IncludeSpeakerNotes = true,
                IncludeSources = true,
                Responses = new[]
                {
                    new ResponseSelectionDto { ConversationId = conversation.Id, MessageId = messageId }
                }
            },
            UserId);

        Assert.True(result.Content.Length > 4_000);
        using var ms = new MemoryStream(result.Content, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        Assert.True(doc.PresentationPart!.SlideParts.Count() >= 3);
    }

    [Fact]
    public async Task GenerateAsync_hybrid_with_speaker_notes_does_not_throw()
    {
        var conversation = BuildConversation(out var messageId);
        var generator = BuildHybridGenerator(conversation, strictValidation: true);

        var result = await generator.GenerateAsync(
            new PowerPointExportRequestDto
            {
                Title = "Notes",
                Template = PowerPointTemplate.Pearl,
                IncludeCoverSlide = true,
                IncludeSpeakerNotes = true,
                Responses = new[]
                {
                    new ResponseSelectionDto { ConversationId = conversation.Id, MessageId = messageId }
                }
            },
            UserId);

        using var ms = new MemoryStream(result.Content, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        Assert.NotNull(doc.PresentationPart!.NotesMasterPart);
    }

    [Fact]
    public async Task Router_falls_back_to_legacy_when_hybrid_throws()
    {
        var conversation = BuildConversation(out var messageId);
        var repo = MockConversationRepo(conversation);
        var brand = new Mock<IBrandAssetProvider>();
        brand.Setup(b => b.GetLogoPng()).Returns((byte[]?)null);
        var opts = Options.Create(new PowerPointRenderingOptions
        {
            ExtendedThemeLibraryEnabled = true,
            TemplateHybridEnabled = true,
            HybridThemeMinId = 3
        });
        var resolver = new PowerPointThemeResolver(brand.Object, opts);
        var templateRepo = new Mock<IPowerPointBaseTemplateRepository>();
        templateRepo.Setup(t => t.HasTemplate(It.IsAny<IPowerPointTheme>(), It.IsAny<SlideOrientation>())).Returns(true);
        templateRepo.Setup(t => t.GetTemplateBytes(It.IsAny<IPowerPointTheme>(), It.IsAny<SlideOrientation>()))
            .Throws(new InvalidOperationException("broken template"));

        var legacy = new PowerPointGenerator(repo.Object, resolver, NullLogger<PowerPointGenerator>.Instance);
        var hybrid = new HybridTemplateGenerator(
            repo.Object, resolver, templateRepo.Object, NullLogger<HybridTemplateGenerator>.Instance, null, opts);
        var router = new PowerPointGenerationEngineRouter(
            legacy, hybrid, resolver, templateRepo.Object, opts, NullLogger<PowerPointGenerationEngineRouter>.Instance);

        var result = await router.GenerateAsync(
            BuildRequest(conversation.Id, messageId, PowerPointTemplate.Vortex),
            UserId);

        Assert.True(result.SlideCount > 0);
    }

    private static PowerPointExportRequestDto BuildRequest(
        Guid conversationId,
        Guid messageId,
        PowerPointTemplate template,
        bool includeCover = false)
    {
        return new PowerPointExportRequestDto
        {
            Title = $"Hybrid — {template}",
            Template = template,
            Orientation = SlideOrientation.Widescreen16x9,
            IncludeCoverSlide = includeCover,
            IncludeAgenda = false,
            IncludeSources = false,
            Responses = new[]
            {
                new ResponseSelectionDto { ConversationId = conversationId, MessageId = messageId }
            }
        };
    }

    private static HybridTemplateGenerator BuildHybridGenerator(Conversation conversation, bool strictValidation = false)
    {
        var repo = MockConversationRepo(conversation);
        var brand = new Mock<IBrandAssetProvider>();
        brand.Setup(b => b.GetLogoPng()).Returns((byte[]?)null);
        var resolver = new PowerPointThemeResolver(brand.Object);
        var templateRepo = new PowerPointBaseTemplateRepository(MockHostEnvironment(), NullLogger<PowerPointBaseTemplateRepository>.Instance);
        var validationOptions = strictValidation
            ? Options.Create(PowerPointValidationOptions.StrictForTests)
            : null;

        return new HybridTemplateGenerator(
            repo.Object,
            resolver,
            templateRepo,
            NullLogger<HybridTemplateGenerator>.Instance,
            validationOptions);
    }

    private static PowerPointGenerator BuildLegacyGenerator(Conversation conversation)
    {
        var repo = MockConversationRepo(conversation);
        var brand = new Mock<IBrandAssetProvider>();
        brand.Setup(b => b.GetLogoPng()).Returns((byte[]?)null);
        return new PowerPointGenerator(repo.Object, new PowerPointThemeResolver(brand.Object), NullLogger<PowerPointGenerator>.Instance);
    }

    private static PowerPointGenerationEngineRouter BuildRouter(
        Conversation conversation,
        PowerPointRenderingOptions options)
    {
        var repo = MockConversationRepo(conversation);
        var brand = new Mock<IBrandAssetProvider>();
        brand.Setup(b => b.GetLogoPng()).Returns((byte[]?)null);
        var opts = Options.Create(options);
        var resolver = new PowerPointThemeResolver(brand.Object, opts);
        var templateRepo = new PowerPointBaseTemplateRepository(MockHostEnvironment(), NullLogger<PowerPointBaseTemplateRepository>.Instance);
        var legacy = new PowerPointGenerator(repo.Object, resolver, NullLogger<PowerPointGenerator>.Instance);
        var hybrid = new HybridTemplateGenerator(
            repo.Object, resolver, templateRepo, NullLogger<HybridTemplateGenerator>.Instance, null, opts);

        return new PowerPointGenerationEngineRouter(
            legacy, hybrid, resolver, templateRepo, opts, NullLogger<PowerPointGenerationEngineRouter>.Instance);
    }

    private static Mock<IConversationRepository> MockConversationRepo(Conversation conversation)
    {
        var repo = new Mock<IConversationRepository>();
        repo.Setup(r => r.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);
        return repo;
    }

    private static IHostEnvironment MockHostEnvironment()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());
        return env.Object;
    }

    private static Conversation BuildConversation(out Guid assistantMessageId)
    {
        var convo = Conversation.Create(UserId, "Hybrid test", model: "ollama:gpt-oss");
        convo.AddMessage(MessageRole.User, "Synthèse.");
        var assistant = convo.AddMessage(MessageRole.Assistant, """
            # Synthèse

            Point clé : **42 %** de croissance.

            ```json
            {
              "title": "KPI",
              "sections": [
                {"type":"kpi_card","title":"Croissance","data":{"value":42,"unit":"%","trend":3.1}}
              ]
            }
            ```
            """);
        assistantMessageId = assistant.Id;
        return convo;
    }

    private static Conversation BuildInvoiceListConversation(out Guid assistantMessageId)
    {
        var goldenPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "docs", "ai-screen-analysis", "golden", "invoice-list.json");

        var fixturePath = Path.GetFullPath(goldenPath);
        if (!File.Exists(fixturePath))
        {
            fixturePath = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                "..", "..", "..", "..", "..",
                "docs", "ai-screen-analysis", "golden", "invoice-list.json"));
        }

        string assistantContent;
        if (File.Exists(fixturePath))
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(fixturePath));
            assistantContent = doc.RootElement.GetProperty("assistantMessageFixture").GetString()!;
        }
        else
        {
            assistantContent = """
                ## Synthèse exécutive
                Montant total **90 151.910 TND**.

                ## Indicateurs clés
                | Indicateur | Montant (TND) | Détails |
                | :--- | :--- | :--- |
                | Total Créances Clients | 90 151.910 | Somme |
                | Créances en Retard | 85 472.250 | Arriérés |

                ## Analyse détaillée
                FAC-2026-000095 en retard.
                """;
        }

        var convo = Conversation.Create(UserId, "Analyse factures", model: "ollama:gpt-oss");
        convo.AddMessage(MessageRole.User, "Analyse la liste des factures.");
        var assistant = convo.AddMessage(MessageRole.Assistant, assistantContent);
        assistantMessageId = assistant.Id;
        return convo;
    }
}
